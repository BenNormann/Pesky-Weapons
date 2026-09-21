using System;
using System.Collections.Generic;
using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>A stable serialized integer id, unique inside the scene (docs/SLICE-1.md section 7).</summary>
    public interface ISceneId
    {
        int SceneId { get; }
    }

    /// <summary>Marks a serialized reference that is allowed to be empty; the scene validator skips it.</summary>
    public sealed class OptionalRefAttribute : PropertyAttribute { }

    /// <summary>
    /// The netcode seam (docs/SLICE-1.md section 7). Every shared state change is a REQUEST that this
    /// component VALIDATES and then APPLIES, after which it raises a C# EVENT. Offline the apply happens
    /// at once; the ATCK port replaces the apply with a server round trip and keeps the same events.
    /// Views and kit objects only ever react to the events - they never change shared state themselves.
    /// Everything it can touch is wired through the serialized arrays below, so there are no Find calls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class WorldAuthority : MonoBehaviour
    {
        [Header("Registry (wired in the Inspector)")]
        [SerializeField] WeaponBody[] weapons = new WeaponBody[0];
        [SerializeField] GoblinBrain[] enemies = new GoblinBrain[0];
        [SerializeField] Door[] doors = new Door[0];
        [SerializeField] PressurePlate[] plates = new PressurePlate[0];
        [SerializeField] KeyPickup[] keys = new KeyPickup[0];
        [SerializeField] RunePickup[] runes = new RunePickup[0];
        [SerializeField] AnvilStation[] anvils = new AnvilStation[0];
        [SerializeField] RoomVolume[] rooms = new RoomVolume[0];
        [Header("View")]
        [Tooltip("The one camera the world is seen through. World-space UI (goblin health bars, markers) billboards to it.")]
        [SerializeField] Camera viewCamera;


        [Header("Impact damage (section 5)")]
        [Tooltip("Relative speed at which impact damage starts.")]
        [SerializeField] float speedMin = 3f;
        [Tooltip("Relative speed at which impact damage is full.")]
        [SerializeField] float speedMax = 12f;
        [Tooltip("Per weapon, per target hit cooldown in seconds.")]
        [SerializeField] float hitCooldown = 0.35f;

        readonly Dictionary<int, WeaponBody> _weaponById = new Dictionary<int, WeaponBody>();
        readonly Dictionary<int, GoblinBrain> _enemyById = new Dictionary<int, GoblinBrain>();
        readonly Dictionary<int, Door> _doorById = new Dictionary<int, Door>();
        readonly Dictionary<int, PressurePlate> _plateById = new Dictionary<int, PressurePlate>();
        readonly Dictionary<int, KeyPickup> _keyById = new Dictionary<int, KeyPickup>();
        readonly Dictionary<int, RunePickup> _runeById = new Dictionary<int, RunePickup>();
        readonly Dictionary<int, AnvilStation> _anvilById = new Dictionary<int, AnvilStation>();
        readonly Dictionary<int, RoomVolume> _roomById = new Dictionary<int, RoomVolume>();
        readonly Dictionary<long, float> _hitTimes = new Dictionary<long, float>();
        readonly Dictionary<GoblinBrain, PlayerSoul> _targeting = new Dictionary<GoblinBrain, PlayerSoul>();
        readonly HashSet<int> _partyKeys = new HashSet<int>();
        readonly List<WeaponBody> _possessed = new List<WeaponBody>();
        readonly Dictionary<WeaponBody, PlayerSoul> _soulOf = new Dictionary<WeaponBody, PlayerSoul>();

        // ------------------------------------------------------------------ events

        /// <summary>(enemy, weapon, damage, point)</summary>
        public event Action<GoblinBrain, WeaponBody, float, Vector3> EnemyDamaged;
        /// <summary>(enemy, killer or null)</summary>
        public event Action<GoblinBrain, WeaponBody> EnemyDied;
        /// <summary>(weapon, damage)</summary>
        public event Action<WeaponBody, float> WeaponDamaged;
        public event Action<WeaponBody> WeaponBroken;
        public event Action<WeaponBody> WeaponRespawned;
        /// <summary>(soul, weapon)</summary>
        public event Action<PlayerSoul, WeaponBody> Possessed;
        /// <summary>(soul, weapon, broke)</summary>
        public event Action<PlayerSoul, WeaponBody, bool> ReleasedWeapon;
        /// <summary>(pickup scene id, taker or null)</summary>
        public event Action<int, WeaponBody> PickupTaken;
        public event Action<PressurePlate> PlateLatched;
        public event Action<Door> DoorOpened;
        /// <summary>(key id)</summary>
        public event Action<int> KeyGained;
        /// <summary>(weapon, modifier)</summary>
        public event Action<WeaponBody, ModifierDef> ModifierAttached;
        public event Action<WeaponBody> Healed;

        // ------------------------------------------------------------------ read-only state

        public IReadOnlyList<WeaponBody> Weapons { get { return weapons; } }
        public IReadOnlyList<GoblinBrain> Enemies { get { return enemies; } }
        public IReadOnlyList<Door> Doors { get { return doors; } }
        public IReadOnlyList<RoomVolume> Rooms { get { return rooms; } }
        public IReadOnlyList<WeaponBody> PossessedWeapons { get { return _possessed; } }
        public int KeyCount { get { return _partyKeys.Count; } }
        public bool HasAnyKey { get { return _partyKeys.Count > 0; } }
        public bool HasKey(int keyId) { return _partyKeys.Contains(keyId); }
        /// <summary>The anvil the local possessed weapon is standing in, for the HUD prompt.</summary>
        public AnvilStation NearAnvil { get; private set; }        /// <summary>The still-locked door the local possessed weapon is standing at, for the HUD prompt.</summary>
        public Door NearDoor { get; private set; }
        /// <summary>The single scene camera, for world-space UI that has to face the player.</summary>
        public Camera ViewCamera { get { return viewCamera; } }


        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Index(weapons, _weaponById);
            Index(enemies, _enemyById);
            Index(doors, _doorById);
            Index(plates, _plateById);
            Index(keys, _keyById);
            Index(runes, _runeById);
            Index(anvils, _anvilById);
            Index(rooms, _roomById);
            AwakeKit();
            AwakePuzzles();
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                weapons[i].Broken += OnWeaponBroken;
                weapons[i].Respawned += OnWeaponRespawned;
            }
        }

        void OnDestroy()
        {
            OnDestroyKit();
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                weapons[i].Broken -= OnWeaponBroken;
                weapons[i].Respawned -= OnWeaponRespawned;
            }
        }

        void Start()
        {
            EvaluateDoors();
        }

        static void Index<T>(T[] source, Dictionary<int, T> target) where T : class, ISceneId
        {
            for (int i = 0; i < source.Length; i++)
            {
                T item = source[i];
                if (item == null) continue;
                if (!target.ContainsKey(item.SceneId)) target.Add(item.SceneId, item);
            }
        }

        public WeaponBody GetWeapon(int id) { WeaponBody w; return _weaponById.TryGetValue(id, out w) ? w : null; }
        public GoblinBrain GetEnemy(int id) { GoblinBrain e; return _enemyById.TryGetValue(id, out e) ? e : null; }
        public Door GetDoor(int id) { Door d; return _doorById.TryGetValue(id, out d) ? d : null; }
        public RoomVolume GetRoom(int id) { RoomVolume r; return _roomById.TryGetValue(id, out r) ? r : null; }

        // ------------------------------------------------------------------ request: hit an enemy

        /// <summary>Pure: the impact damage curve from section 5.</summary>
        public float ImpactDamage(float weaponDamage, float relativeSpeed)
        {
            return weaponDamage * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(speedMin, speedMax, relativeSpeed));
        }

        /// <summary>A weapon struck an enemy. Validates the per target cooldown, applies damage, raises events.</summary>
        public bool RequestHitEnemy(int weaponId, int enemyId, float relativeSpeed, Vector3 point)
        {
            WeaponBody weapon = GetWeapon(weaponId);
            GoblinBrain enemy = GetEnemy(enemyId);
            if (weapon == null || enemy == null) return false;
            if (weapon.IsBroken || enemy.IsDead) return false;

            long key = ((long)weaponId << 32) ^ (uint)enemyId;
            float last;
            if (_hitTimes.TryGetValue(key, out last) && Time.time - last < hitCooldown) return false;

            float raw = ImpactDamage(weapon.Def != null ? weapon.Def.damage : 0f, relativeSpeed) * weapon.DamageMultiplier;
            if (raw <= 0f) return false;

            // The shield boss filters the hit: while its shield holds, only a heavy weapon gets through to
            // it at all; once the shield is gone a bladed weapon does bonus damage. Ordinary goblins have
            // no shield and a bonus of 1, so this is the same arithmetic as before for them.
            GoblinBrain.HitOutcome outcome = enemy.ResolveDamage(weapon, raw);
            _hitTimes[key] = Time.time;

            Vector3 knockDir = enemy.Centre - point;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.0001f) knockDir = enemy.transform.forward;
            knockDir.Normalize();

            Vector3 knock = knockDir * Mathf.Min(outcome.damage * enemy.KnockbackPerDamage, enemy.MaxKnockbackSpeed);
            enemy.ApplyHit(outcome, knock, weapon);
            weapon.NotifyCombat();
            if (EnemyDamaged != null) EnemyDamaged(enemy, weapon, outcome.damage, point);
            if (outcome.shieldBroke) RaiseShieldBroken(enemy);

            if (!outcome.hitShield && outcome.newHp <= 0f)
            {
                SetTargeting(enemy, null);
                enemy.Kill();
                if (EnemyDied != null) EnemyDied(enemy, weapon);
                EvaluateDoors();
            }
            return true;
        }

        // ------------------------------------------------------------------ request: damage a weapon

        public bool RequestDamageWeapon(int weaponId, float amount, GoblinBrain source)
        {
            return RequestDamageWeapon(GetWeapon(weaponId), amount, source);
        }

        public bool RequestDamageWeapon(WeaponBody weapon, float amount, GoblinBrain source)
        {
            if (weapon == null || weapon.IsBroken || amount <= 0f) return false;
            weapon.ApplyDamage(amount);
            if (WeaponDamaged != null) WeaponDamaged(weapon, amount);
            return true;
        }

        // ------------------------------------------------------------------ request: possess / release / break

        public bool RequestPossess(PlayerSoul soul, WeaponBody weapon)
        {
            if (soul == null || weapon == null) return false;
            if (soul.IsPossessing || !weapon.IsFree) return false;
            if (!soul.ApplyPossess(weapon)) return false;

            if (!_possessed.Contains(weapon)) _possessed.Add(weapon);
            _soulOf[weapon] = soul;
            if (Possessed != null) Possessed(soul, weapon);
            return true;
        }

        /// <summary>Q. Out of combat the weapon drops; in combat it breaks (section 3).</summary>
        /// <summary>Q. Out of combat the weapon drops; in combat it breaks (section 3).</summary>
        public bool RequestRelease(PlayerSoul soul)
        {
            if (soul == null || !soul.IsPossessing) return false;
            WeaponBody weapon = soul.Weapon;
            if (soul.InCombat) return RequestBreak(weapon);

            soul.ApplyRelease();
            _possessed.Remove(weapon);
            _soulOf.Remove(weapon);
            if (ReleasedWeapon != null) ReleasedWeapon(soul, weapon, false);
            return true;
        }

        public bool RequestBreak(int weaponId) { return RequestBreak(GetWeapon(weaponId)); }

        public bool RequestBreak(WeaponBody weapon)
        {
            if (weapon == null || weapon.IsBroken) return false;
            // Break fires WeaponBody.Broken, which OnWeaponBroken turns into WeaponBroken + ReleasedWeapon.
            weapon.Break();
            return true;
        }

        void OnWeaponBroken(WeaponBody weapon)
        {
            _possessed.Remove(weapon);
            PlayerSoul soul;
            if (_soulOf.TryGetValue(weapon, out soul)) _soulOf.Remove(weapon);
            if (WeaponBroken != null) WeaponBroken(weapon);
            if (soul != null && ReleasedWeapon != null) ReleasedWeapon(soul, weapon, true);
        }

        void OnWeaponRespawned(WeaponBody weapon)
        {
            if (WeaponRespawned != null) WeaponRespawned(weapon);
        }

        // ------------------------------------------------------------------ request: pickup

        /// <summary>Touch pickup. Keys go to the party for good; runes attach to the weapon that touched them.</summary>
        public bool RequestPickup(int pickupId, WeaponBody taker)
        {
            KeyPickup key;
            if (_keyById.TryGetValue(pickupId, out key))
            {
                if (key.Taken) return false;
                key.ApplyTaken();
                bool isNew = _partyKeys.Add(key.KeyId);
                if (PickupTaken != null) PickupTaken(pickupId, taker);
                if (isNew && KeyGained != null) KeyGained(key.KeyId);
                EvaluateDoors();
                return true;
            }

            RunePickup rune;
            if (_runeById.TryGetValue(pickupId, out rune))
            {
                if (rune.Taken || !rune.IsAvailable || taker == null || taker.IsBroken) return false;
                if (rune.Modifier == null) return false;
                rune.ApplyTaken();
                taker.AddModifier(rune.Modifier);
                if (PickupTaken != null) PickupTaken(pickupId, taker);
                if (ModifierAttached != null) ModifierAttached(taker, rune.Modifier);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ request: plate mass report

        /// <summary>A plate reports the summed mass of the weapons resting on it. It latches at its threshold.</summary>
        public void ReportPlateMass(int plateId, float mass)
        {
            PressurePlate plate;
            if (!_plateById.TryGetValue(plateId, out plate)) return;
            bool wasLatched = plate.Latched;
            plate.ApplyMass(mass);
            if (!wasLatched && plate.Latched)
            {
                if (PlateLatched != null) PlateLatched(plate);
                EvaluateDoors();
            }
        }

        // ------------------------------------------------------------------ request: anvil

        /// <summary>Held Possess for the full charge next to an anvil, out of combat: full HP.</summary>
        public bool RequestAnvilUse(int anvilId, WeaponBody weapon)
        {
            AnvilStation anvil;
            if (!_anvilById.TryGetValue(anvilId, out anvil)) return false;
            if (weapon == null || weapon.IsBroken) return false;
            PlayerSoul soul = weapon.Possessor as PlayerSoul;
            if (soul == null || soul.InCombat) return false;
            if (weapon.Hp >= weapon.MaxHp) return false;

            weapon.RestoreFullHp();
            if (Healed != null) Healed(weapon);
            return true;
        }

        /// <summary>A DoorPrompt trigger reports that the possessed weapon is standing at a closed door.</summary>
        public void SetNearDoor(Door door, bool inside)
        {
            if (inside) NearDoor = door;
            else if (NearDoor == door) NearDoor = null;
        }

        public void SetNearAnvil(AnvilStation anvil, bool inside)
        {
            if (inside) NearAnvil = anvil;
            else if (NearAnvil == anvil) NearAnvil = null;
        }

        // ------------------------------------------------------------------ request: door condition check

        public bool RequestDoorCheck(int doorId) { return RequestDoorCheck(GetDoor(doorId)); }

        public bool RequestDoorCheck(Door door)
        {
            if (door == null || door.IsOpen) return false;
            if (!door.ConditionSatisfied(this)) return false;
            door.ApplyOpen();
            if (DoorOpened != null) DoorOpened(door);
            return true;
        }

        /// <summary>Re-checks every door. Called on start and whenever an event could have changed a condition.</summary>
        public void EvaluateDoors()
        {
            for (int i = 0; i < doors.Length; i++) RequestDoorCheck(doors[i]);
        }

        // ------------------------------------------------------------------ targeting bookkeeping

        /// <summary>A goblin started or stopped targeting a soul; keeps PlayerSoul.InCombat true meanwhile.</summary>
        public void SetTargeting(GoblinBrain enemy, PlayerSoul soul)
        {
            if (enemy == null) return;
            PlayerSoul current;
            _targeting.TryGetValue(enemy, out current);
            if (current == soul) return;
            if (current != null) current.RemoveTargeter();
            if (soul != null) { _targeting[enemy] = soul; soul.AddTargeter(); }
            else _targeting.Remove(enemy);
        }
    }
}
