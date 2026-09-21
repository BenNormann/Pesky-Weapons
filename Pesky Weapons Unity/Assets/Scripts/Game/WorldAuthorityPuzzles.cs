using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The puzzle-kit half of the netcode seam: ropes, pots, levers, counterweights, scales, lightning,
    /// cracked walls and porter gates, plus the porter's carry. Same contract as the rest of
    /// WorldAuthority - request -> validate -> apply -> event - and views only react to the events.
    /// Every piece here LATCHES where the design says it latches, so a puzzle can never be un-solved.
    /// </summary>
    public sealed partial class WorldAuthority
    {
        [Header("Registry: puzzle kit (wired in the Inspector)")]
        [SerializeField] Rope[] ropes = new Rope[0];
        [SerializeField] Pot[] pots = new Pot[0];
        [SerializeField] ImpactLever[] levers = new ImpactLever[0];
        [SerializeField] CounterweightPair[] counterweights = new CounterweightPair[0];
        [SerializeField] ScalesLock[] scales = new ScalesLock[0];
        [SerializeField] LightningField[] lightningFields = new LightningField[0];
        [SerializeField] CrackedWall[] crackedWalls = new CrackedWall[0];
        [SerializeField] PorterGate[] porterGates = new PorterGate[0];

        readonly Dictionary<int, Rope> _ropeById = new Dictionary<int, Rope>();
        readonly Dictionary<int, Pot> _potById = new Dictionary<int, Pot>();
        readonly Dictionary<int, ImpactLever> _leverById = new Dictionary<int, ImpactLever>();
        readonly Dictionary<int, CounterweightPair> _counterweightById = new Dictionary<int, CounterweightPair>();
        readonly Dictionary<int, ScalesLock> _scalesById = new Dictionary<int, ScalesLock>();
        readonly Dictionary<int, LightningField> _lightningById = new Dictionary<int, LightningField>();
        readonly Dictionary<int, CrackedWall> _crackedWallById = new Dictionary<int, CrackedWall>();
        readonly Dictionary<int, PorterGate> _porterGateById = new Dictionary<int, PorterGate>();

        /// <summary>(rope, the weapon that cut it)</summary>
        public event Action<Rope, WeaponBody> RopeCut;
        /// <summary>(pot, the weapon that smashed it)</summary>
        public event Action<Pot, WeaponBody> PotSmashed;
        /// <summary>(lever, on)</summary>
        public event Action<ImpactLever, bool> LeverChanged;
        /// <summary>(pair, the new target offset)</summary>
        public event Action<CounterweightPair, float> CounterweightChanged;
        public event Action<ScalesLock> ScalesLatched;
        /// <summary>(field, victim, strike point)</summary>
        public event Action<LightningField, WeaponBody, Vector3> LightningStruck;
        /// <summary>(wall, the weapon that broke it)</summary>
        public event Action<CrackedWall, WeaponBody> WallBroken;
        /// <summary>(gate, open)</summary>
        public event Action<PorterGate, bool> PorterGateChanged;
        /// <summary>(porter, weapon)</summary>
        public event Action<GoblinBrain, WeaponBody> PorterPickedUp;
        /// <summary>(porter, weapon, placed on its stand rather than dropped in alarm)</summary>
        public event Action<GoblinBrain, WeaponBody, bool> PorterDropped;
        /// <summary>The shield boss lost its shield: from here it takes normal damage, and more from blades.</summary>
        public event Action<GoblinBrain> EnemyShieldBroken;

        public IReadOnlyList<Rope> Ropes { get { return ropes; } }
        public IReadOnlyList<Pot> Pots { get { return pots; } }
        public IReadOnlyList<ImpactLever> Levers { get { return levers; } }
        public IReadOnlyList<CounterweightPair> Counterweights { get { return counterweights; } }
        public IReadOnlyList<ScalesLock> Scales { get { return scales; } }
        public IReadOnlyList<LightningField> LightningFields { get { return lightningFields; } }
        public IReadOnlyList<CrackedWall> CrackedWalls { get { return crackedWalls; } }
        public IReadOnlyList<PorterGate> PorterGates { get { return porterGates; } }

        public Rope GetRope(int id) { Rope r; return _ropeById.TryGetValue(id, out r) ? r : null; }
        public Pot GetPot(int id) { Pot p; return _potById.TryGetValue(id, out p) ? p : null; }
        public ImpactLever GetLever(int id) { ImpactLever l; return _leverById.TryGetValue(id, out l) ? l : null; }
        public CounterweightPair GetCounterweight(int id) { CounterweightPair c; return _counterweightById.TryGetValue(id, out c) ? c : null; }
        public ScalesLock GetScales(int id) { ScalesLock s; return _scalesById.TryGetValue(id, out s) ? s : null; }
        public LightningField GetLightningField(int id) { LightningField f; return _lightningById.TryGetValue(id, out f) ? f : null; }
        public CrackedWall GetCrackedWall(int id) { CrackedWall w; return _crackedWallById.TryGetValue(id, out w) ? w : null; }
        public PorterGate GetPorterGate(int id) { PorterGate g; return _porterGateById.TryGetValue(id, out g) ? g : null; }

        /// <summary>Called from Awake, after AwakeKit.</summary>
        void AwakePuzzles()
        {
            Index(ropes, _ropeById);
            Index(pots, _potById);
            Index(levers, _leverById);
            Index(counterweights, _counterweightById);
            Index(scales, _scalesById);
            Index(lightningFields, _lightningById);
            Index(crackedWalls, _crackedWallById);
            Index(porterGates, _porterGateById);
        }

        // ------------------------------------------------------------------ request: cut a rope

        /// <summary>A weapon struck a rope. Only a bladed weapon, fast enough, cuts it - and only once.</summary>
        public bool RequestCutRope(int ropeId, WeaponBody weapon, float speed)
        {
            Rope rope = GetRope(ropeId);
            if (rope == null || !rope.CanCut(weapon, speed)) return false;
            rope.ApplyCut(rope.Clock != null ? rope.Clock.Ms : 0L);
            if (RopeCut != null) RopeCut(rope, weapon);
            return true;
        }

        // ------------------------------------------------------------------ request: smash a pot

        /// <summary>A weapon struck a pot. Only a blunt weapon, fast enough, smashes it - and only once.</summary>
        public bool RequestSmashPot(int potId, WeaponBody weapon, float speed)
        {
            Pot pot = GetPot(potId);
            if (pot == null || !pot.CanSmash(weapon, speed)) return false;
            pot.ApplySmashed();
            if (PotSmashed != null) PotSmashed(pot, weapon);
            EvaluateDoors();
            return true;
        }

        // ------------------------------------------------------------------ request: flip a lever

        /// <summary>A weapon struck a lever hard enough to flip it. A latching lever only ever goes on.</summary>
        public bool RequestLeverImpact(int leverId, WeaponBody weapon, float speed)
        {
            ImpactLever lever = GetLever(leverId);
            if (lever == null || !lever.CanFlip(weapon, speed)) return false;
            return ApplyLever(lever, !lever.IsOn);
        }

        /// <summary>Set a lever directly (a scripted opening, or a test).</summary>
        public bool RequestSetLever(int leverId, bool on)
        {
            ImpactLever lever = GetLever(leverId);
            if (lever == null || lever.IsOn == on) return false;
            if (lever.IsLatching && !on) return false;
            return ApplyLever(lever, on);
        }

        bool ApplyLever(ImpactLever lever, bool on)
        {
            lever.ApplySet(on);
            if (LeverChanged != null) LeverChanged(lever, on);
            // The Winch variant: turning the lever on switches its Lift on for good.
            if (on && lever.Lift != null) RequestSetLift(lever.Lift.SceneId, true);
            EvaluateDoors();
            return true;
        }

        // ------------------------------------------------------------------ report: counterweight masses

        /// <summary>A counterweight pair reports what is standing on its two pans. The host owns the target.</summary>
        public void ReportCounterweight(int pairId, float massA, float massB, long ms)
        {
            CounterweightPair pair = GetCounterweight(pairId);
            if (pair == null) return;
            float want = pair.WantedOffset(massA, massB);
            if (Mathf.Approximately(want, pair.TargetOffset)) return;
            pair.ApplyTarget(want, ms);
            if (CounterweightChanged != null) CounterweightChanged(pair, want);
        }

        // ------------------------------------------------------------------ report: scales

        /// <summary>A scales lock reports its pans. Once all three are right at the same moment it latches.</summary>
        public void ReportScales(int scalesId)
        {
            ScalesLock lockPiece = GetScales(scalesId);
            if (lockPiece == null || lockPiece.Latched) return;
            if (!lockPiece.AllSatisfied) return;
            lockPiece.ApplyLatched();
            if (ScalesLatched != null) ScalesLatched(lockPiece);
            EvaluateDoors();
        }

        // ------------------------------------------------------------------ request: lightning

        /// <summary>The field's schedule came round and it picked the highest metal weapon inside it.</summary>
        public bool RequestLightningStrike(int fieldId, WeaponBody weapon)
        {
            LightningField field = GetLightningField(fieldId);
            if (field == null || weapon == null || weapon.IsBroken) return false;
            if (weapon.Def == null || !weapon.Def.metal) return false;
            Vector3 point = weapon.Body != null ? weapon.Body.worldCenterOfMass : weapon.transform.position;
            RequestDamageWeapon(weapon, field.StrikeDamage, null);
            if (LightningStruck != null) LightningStruck(field, weapon, point);
            return true;
        }

        // ------------------------------------------------------------------ request: break a cracked wall

        /// <summary>A weapon struck a cracked wall. Heavy and fast enough breaks it; anything else puffs.</summary>
        public bool RequestBreakWall(int wallId, WeaponBody weapon, float speed)
        {
            CrackedWall wall = GetCrackedWall(wallId);
            if (wall == null || wall.IsBroken) return false;
            if (!wall.CanBreak(weapon, speed))
            {
                if (speed >= 2f) wall.ApplyPuff();
                return false;
            }
            wall.ApplyBroken();
            if (WallBroken != null) WallBroken(wall, weapon);
            EvaluateDoors();
            return true;
        }

        // ------------------------------------------------------------------ request: porter gate

        public bool RequestSetPorterGate(int gateId, bool open)
        {
            PorterGate gate = GetPorterGate(gateId);
            if (gate == null || gate.IsOpen == open) return false;
            // Validate against the gate's own rule, so a client cannot simply ask for it to be open.
            if (open != gate.WantsOpen()) return false;
            gate.ApplyOpen(open);
            if (PorterGateChanged != null) PorterGateChanged(gate, open);
            return true;
        }

        // ------------------------------------------------------------------ request: porter carry

        /// <summary>A porter reached a weapon that has been still long enough and wants to pick it up.</summary>
        public bool RequestPorterPickUp(GoblinBrain porter, WeaponBody weapon)
        {
            if (porter == null || weapon == null) return false;
            if (!porter.CanPickUp(weapon)) return false;
            porter.ApplyCarry(weapon);
            if (PorterPickedUp != null) PorterPickedUp(porter, weapon);
            return true;
        }

        /// <summary>
        /// The porter lets go: placed = true when it set the weapon down on its stand, false when the
        /// weapon came alive in its hands (and then it turns hostile).
        /// </summary>
        public bool RequestPorterDrop(GoblinBrain porter, bool placed)
        {
            if (porter == null || !porter.IsCarrying) return false;
            WeaponBody weapon = porter.Carried;
            porter.ApplyDrop(placed);
            if (PorterDropped != null) PorterDropped(porter, weapon, placed);
            return true;
        }

        /// <summary>Raised by RequestHitEnemy when a shield boss loses its shield.</summary>
        void RaiseShieldBroken(GoblinBrain enemy)
        {
            if (EnemyShieldBroken != null) EnemyShieldBroken(enemy);
        }
    }
}
