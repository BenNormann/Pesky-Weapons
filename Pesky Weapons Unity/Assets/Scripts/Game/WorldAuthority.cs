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
            AwakeNet();
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
            OnDestroyNet();
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                weapons[i].Broken -= OnWeaponBroken;
                weapons[i].Respawned -= OnWeaponRespawned;
            }
        }

        void Start()
        {
            StartNet();
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
            // HIT_CLAIM -> host validation -> ENEMY_HEALTH -> OnEnemyHealth (WorldAuthorityNet.cs).
            return NetHitEnemy(weaponId, enemyId, relativeSpeed, point);
        }

        // ------------------------------------------------------------------ request: damage a weapon

        public bool RequestDamageWeapon(int weaponId, float amount, GoblinBrain source)
        {
            return RequestDamageWeapon(GetWeapon(weaponId), amount, source);
        }

        public bool RequestDamageWeapon(WeaponBody weapon, float amount, GoblinBrain source)
        {
            return NetDamageWeapon(weapon, amount, source, Vector3.zero);
        }

        // ------------------------------------------------------------------ request: possess / release / break

public bool RequestPossess(PlayerSoul soul, WeaponBody weapon)
        {
            // POSSESS_REQ -> WEAPON_OWNER -> ReconcileOwner (WorldAuthorityNet.cs).
            return NetPossess(soul, weapon);
        }

        /// <summary>Q. Out of combat the weapon drops; in combat it breaks (section 3).</summary>
        /// <summary>Q. Out of combat the weapon drops; in combat it breaks (section 3).</summary>
public bool RequestRelease(PlayerSoul soul)
        {
            // RELEASE_REQ: the host drops the weapon (WEAPON_OWNER) or, in combat, breaks it (WEAPON_BROKEN).
            return NetRelease(soul);
        }

        public bool RequestBreak(int weaponId) { return RequestBreak(GetWeapon(weaponId)); }

        public bool RequestBreak(WeaponBody weapon)
        {
            return NetBreak(weapon);
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
            return NetPickup(pickupId, taker);
        }

        // ------------------------------------------------------------------ request: plate mass report

        /// <summary>A plate reports the summed mass of the weapons resting on it. It latches at its threshold.</summary>
public void ReportPlateMass(int plateId, float mass)
        {
            NetPlateMass(plateId, mass);
        }

        // ------------------------------------------------------------------ request: anvil

        /// <summary>Held Possess for the full charge next to an anvil, out of combat: full HP.</summary>
public bool RequestAnvilUse(int anvilId, WeaponBody weapon)
        {
            return NetAnvil(anvilId, weapon);
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
            return NetDoorCheck(door);
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
