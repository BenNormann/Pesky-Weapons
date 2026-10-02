using Pesky.Data;
using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The curse ON THIS PLAYER (docs/RUN.md), applied by the victim's own machine and nobody else's. One
    /// CurseState with an expiry on the LevelClock, and one small effect per curse:
    ///   1 MAGNETIC  the victim's launches bend toward the CLOSEST other player; everybody else's launches bend
    ///              toward the victim (the magnet), within magneticRange. Never toward a loose weapon, and the
    ///              Mage is only ever pulled at as 'the closest player', never named
    ///   2 NAUSEA    the camera rolls and yaws on a sine, and the launch heading wanders on a slower one
    ///   3 SLIPPERY  the weapon's colliders wear the zero-friction material, so it slides and cannot settle
    ///   4 BLINDNESS the RunHud darkens the screen to a small clear circle around the centre
    ///   5 HEAVY     launch speed times heavySpeedScale
    /// The launch effects reach the weapon through WeaponMotor.Curse (ILaunchCurse), re-attached whenever
    /// the player possesses something else. Every effect is undone when the curse expires or the
    /// component is disabled. Nothing here knows or asks who cast it: the wire never said.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurseEffects : MonoBehaviour, ILaunchCurse
    {
        /// <summary>A curse and when it ends, on the level clock.</summary>
        public struct CurseState
        {
            public CurseKind kind;
            public long untilMs;

            public bool Active(long nowMs) { return kind != CurseKind.None && nowMs < untilMs; }
            public float SecondsLeft(long nowMs) { return kind == CurseKind.None ? 0f : Mathf.Max(0f, (untilMs - nowMs) / 1000f); }
        }

        [Header("Wiring")]
        [SerializeField] WorldAuthority authority;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("The level clock the expiry reads: the same number on every peer.")]
        [SerializeField] LevelClock clock;
        [Tooltip("Shows the curse line and the blindness overlay. Empty in a scene without a run HUD.")]
        [OptionalRef][SerializeField] RunHud runHud;
        [Tooltip("The numbers when the session has no GameData (opening the scene straight from the Editor).")]
        [OptionalRef][SerializeField] RunDef fallbackDef;

        CurseState _state;
        WeaponMotor _attached;
        WeaponBody _slickWeapon;
        Collider[] _slickColliders;
        PhysicsMaterial[] _savedMaterials;
        bool _swaying;
        bool _blinding;
        // Other players' MAGNETIC curses by slot (level clock ms they end): the magnets my launches bend toward.
        readonly long[] _magnetUntilMs = new long[Wire.MaxPlayers];

        /// <summary>The curse on this player right now (None when there is none).</summary>
        public CurseState State { get { return _state; } }

        public CurseKind Current { get { return _state.Active(NowMs) ? _state.kind : CurseKind.None; } }

        RunDef Def
        {
            get
            {
                GameData data = authority != null && authority.Session != null ? authority.Session.Data : null;
                if (data != null && data.run != null) return data.run;
                return fallbackDef;
            }
        }

        long NowMs { get { return clock != null ? clock.Ms : (long)(Time.time * 1000f); } }

        void OnEnable()
        {
            if (authority != null) { authority.Cursed += OnCursed; authority.CurseSeen += OnCurseSeen; }
        }

        void OnDisable()
        {
            if (authority != null) { authority.Cursed -= OnCursed; authority.CurseSeen -= OnCurseSeen; }
            Clear();
        }

        /// <summary>CURSE_EVENT named this peer's slot. A new curse replaces whatever was on.</summary>
        void OnCursed(CurseKind kind, float seconds)
        {
            Clear();
            _state.kind = kind;
            _state.untilMs = NowMs + (long)(seconds * 1000f);
            DebugGate.Log("curse: " + kind + " on me for " + seconds.ToString("0.0") + " s");
            Apply();
        }

        void Update()
        {
            long now = NowMs;
            if (_state.kind != CurseKind.None && !_state.Active(now))
            {
                Clear();
                return;
            }
            CurseKind kind = _state.Active(now) ? _state.kind : CurseKind.None;

            // The launch hook follows whatever the player possesses right now.
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            WeaponMotor motor = soul != null && soul.IsPossessing ? soul.Motor : null;
            if (kind == CurseKind.None) motor = null;
            if (motor != _attached)
            {
                if (_attached != null) _attached.Curse = null;
                _attached = motor;
                if (_attached != null) _attached.Curse = this;
            }

            // Slippery follows the possessed weapon too: a swap mid-curse moves the material with the player.
            WeaponBody weapon = soul != null && soul.IsPossessing ? soul.Weapon : null;
            if (kind == CurseKind.Slippery && weapon != _slickWeapon)
            {
                RestoreMaterials();
                SwapMaterials(weapon);
            }

            if (kind == CurseKind.Nausea) Sway(now);
            if (runHud != null) runHud.SetCurse(kind, _state.SecondsLeft(now));
        }

        void Apply()
        {
            RunDef def = Def;
            if (_state.kind == CurseKind.Blindness && runHud != null)
            {
                runHud.SetBlindness(true, def != null ? def.blindnessClearRadius : 0.12f, def != null ? def.blindnessOpacity : 0.96f);
                _blinding = true;
            }
        }

        /// <summary>Undo every effect: the launch hook, the material, the sway, the overlay, the line.</summary>
        void Clear()
        {
            _state.kind = CurseKind.None;
            _state.untilMs = 0;
            if (_attached != null) { _attached.Curse = null; _attached = null; }
            RestoreMaterials();
            if (_swaying && orbitCamera != null) orbitCamera.SetSway(0f, 0f);
            _swaying = false;
            if (_blinding && runHud != null) runHud.SetBlindness(false, 0.12f, 0.96f);
            _blinding = false;
            if (runHud != null) runHud.SetCurse(CurseKind.None, 0f);
        }

        // ---------------------------------------------------------------- 2 nausea

        void Sway(long nowMs)
        {
            if (orbitCamera == null) return;
            RunDef def = Def;
            float period = def != null ? def.nauseaPeriod : 3f;
            float t = nowMs * 0.001f;
            float phase = 2f * Mathf.PI * t / Mathf.Max(0.1f, period);
            float roll = (def != null ? def.nauseaRoll : 6f) * Mathf.Sin(phase);
            float yaw = (def != null ? def.nauseaYaw : 4f) * Mathf.Sin(phase * 0.5f + 1.3f);
            orbitCamera.SetSway(yaw, roll);
            _swaying = true;
        }

        // ---------------------------------------------------------------- 3 slippery

        void SwapMaterials(WeaponBody weapon)
        {
            RunDef def = Def;
            PhysicsMaterial slick = def != null ? def.slipperyMaterial : null;
            if (weapon == null || slick == null) return;
            _slickWeapon = weapon;
            _slickColliders = weapon.GetComponentsInChildren<Collider>(true);
            _savedMaterials = new PhysicsMaterial[_slickColliders.Length];
            for (int i = 0; i < _slickColliders.Length; i++)
            {
                if (_slickColliders[i] == null || _slickColliders[i].isTrigger) continue;
                _savedMaterials[i] = _slickColliders[i].sharedMaterial;
                _slickColliders[i].sharedMaterial = slick;
            }
        }

        void RestoreMaterials()
        {
            if (_slickColliders != null)
            {
                for (int i = 0; i < _slickColliders.Length; i++)
                {
                    if (_slickColliders[i] == null || _slickColliders[i].isTrigger) continue;
                    _slickColliders[i].sharedMaterial = _savedMaterials != null && i < _savedMaterials.Length ? _savedMaterials[i] : null;
                }
            }
            _slickColliders = null;
            _savedMaterials = null;
            _slickWeapon = null;
        }

        /// <summary>Every curse on anybody: a MAGNETIC one on another player makes MY launches bend toward them.</summary>
        void OnCurseSeen(byte slot, CurseKind kind, float seconds)
        {
            if (slot >= Wire.MaxPlayers || kind != CurseKind.Magnetic) return;
            byte local = authority != null ? authority.LocalSlot : Wire.NoSlot;
            if (slot == local) return;
            _magnetUntilMs[slot] = NowMs + (long)(seconds * 1000f);
        }

        // ---------------------------------------------------------------- the launch hook (1 magnetic, 2 nausea, 5 heavy)

        public float SpeedScale
        {
            get
            {
                if (Current != CurseKind.Heavy) return 1f;
                RunDef def = Def;
                return def != null ? def.heavySpeedScale : 0.5f;
            }
        }

        public Vector3 BendDirection(Vector3 direction, Vector3 from)
        {
            CurseKind kind = Current;
            RunDef def = Def;
            float range = def != null ? def.magneticRange : 20f;
            float bend = def != null ? def.magneticBend : 0.5f;
            Vector3 toTarget;
            if (kind == CurseKind.Magnetic)
            {
                // The magnet myself: pulled toward the CLOSEST other player, whoever that happens to be.
                if (NearestPlayerWeapon(from, range, Wire.NoSlot, out toTarget))
                    return Vector3.Slerp(direction.normalized, toTarget.normalized, Mathf.Clamp01(bend));
            }
            else if (NearestMagnet(from, range, out toTarget))
            {
                // Somebody else is the magnet: everybody's launches bend toward them.
                return Vector3.Slerp(direction.normalized, toTarget.normalized, Mathf.Clamp01(bend));
            }
            if (kind == CurseKind.Nausea)
            {
                float period = def != null ? def.nauseaPeriod : 3f;
                float t = NowMs * 0.001f;
                float drift = (def != null ? def.nauseaHeadingDrift : 12f) * Mathf.Sin(Mathf.PI * t / Mathf.Max(0.1f, period));
                return Quaternion.AngleAxis(drift, Vector3.up) * direction;
            }
            return direction;
        }

        /// <summary>The nearest magnet: another player whose MAGNETIC curse is still running, within range; false when none.</summary>
        bool NearestMagnet(Vector3 from, float range, out Vector3 toMagnet)
        {
            toMagnet = Vector3.zero;
            long now = NowMs;
            float bestSq = range * range;
            bool found = false;
            for (int slot = 0; slot < Wire.MaxPlayers; slot++)
            {
                if (_magnetUntilMs[slot] <= now) continue;
                Vector3 to;
                if (!NearestPlayerWeapon(from, range, (byte)slot, out to)) continue;
                float sq = to.sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                toMagnet = to;
                found = true;
            }
            return found;
        }

        /// <summary>The nearest weapon POSSESSED by another player within range of a point (onlySlot = Wire.NoSlot for any
        /// player, else that player's); loose weapons never count. False when none.</summary>
        bool NearestPlayerWeapon(Vector3 from, float range, byte onlySlot, out Vector3 toWeapon)
        {
            toWeapon = Vector3.zero;
            if (authority == null) return false;
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            WeaponBody mine = soul != null ? soul.Weapon : null;
            float bestSq = range * range;
            bool found = false;
            System.Collections.Generic.IReadOnlyList<WeaponBody> weapons = authority.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponBody w = weapons[i];
                if (w == null || w == mine || w.Body == null || w.IsBroken) continue;
                RemotePossessor holder = w.Possessor as RemotePossessor;
                if (holder == null && !(w.Possessor is PlayerSoul)) continue;              // loose: not a player
                if (onlySlot != Wire.NoSlot && (holder == null || holder.slot != onlySlot)) continue;
                Vector3 to = w.Body.position - from;
                float sq = to.sqrMagnitude;
                if (sq < 0.01f || sq > bestSq) continue;
                bestSq = sq;
                toWeapon = to;
                found = true;
            }
            return found;
        }
    }
}
