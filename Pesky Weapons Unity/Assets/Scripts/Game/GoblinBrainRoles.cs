using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The goblin variants, kept out of the base FSM file: patrol routes, sleeping, the Porter that
    /// carries still weapons through its own gate, and the Shield boss.
    ///
    /// * PATROL   - wire a PatrolRoute and the goblin walks it instead of wandering. With the 110 degree
    ///              view cone that is the Patrol Room: move only while nobody is looking at you.
    /// * SLEEP    - startAsleep. It perceives nothing at all until an ANIMATE weapon comes within
    ///              EnemyDef.wakeAnimateRange or something bangs within wakeImpactRange, and then it
    ///              wakes the sleepers around it (once - there is no chain across the whole room).
    /// * PORTER   - role = Porter. A weapon that has been INANIMATE for porterInanimateSeconds inside its
    ///              reach gets picked up, carried along its route (its PorterGate opens for it) and set
    ///              down on its dropPoint. Play dead and it carries you through. Go animate in its hands
    ///              and it drops you and turns hostile.
    /// * BOSS     - role = ShieldBoss with EnemyDef.shieldMaxHp &gt; 0. Only weapons of at least
    ///              shieldMinMass touch the shield; once it is gone the body takes normal damage with
    ///              bladedDamageBonus on top for blades. Heavy weapons break the shield, blades finish it.
    /// </summary>
    public sealed partial class GoblinBrain
    {
        public enum Role { Grunt = 0, Porter = 1, ShieldBoss = 2 }

        [Header("Role")]
        [SerializeField] Role role = Role.Grunt;
        [Tooltip("Waypoints it walks while it has nothing better to do. Empty = wander around its anchor.")]
        [OptionalRef][SerializeField] PatrolRoute patrol;
        [Tooltip("It starts asleep: no perception at all until something wakes it.")]
        [SerializeField] bool startAsleep;
        [SerializeField] Color sleepColor = new Color(0.35f, 0.72f, 0.94f, 1f);

        [Header("Porter")]
        [Tooltip("Where a carried weapon is held. Keep it clear of the goblin's own capsule.")]
        [OptionalRef][SerializeField] Transform carrySocket;
        [Tooltip("The stand it carries weapons to. Scene transform, beyond its gate.")]
        [OptionalRef][SerializeField] Transform dropPoint;

        [Header("Shield boss")]
        [OptionalRef][SerializeField] Renderer shieldRenderer;
        [OptionalRef][SerializeField] Transform shieldBar;
        [Tooltip("Left-anchored pivot: its local X scale is the shield fraction.")]
        [OptionalRef][SerializeField] Transform shieldBarFill;

        WeaponBody _carried;
        WeaponBody _fetch;
        float _porterCooldownUntil = -999f;
        float _shieldHp;
        int _patrolIndex;
        int _patrolDir = 1;

        public Role GoblinRole { get { return role; } }
        public bool IsPorter { get { return role == Role.Porter; } }
        public PatrolRoute Patrol { get { return patrol; } }
        public Transform CarrySocket { get { return carrySocket; } }
        public Transform DropPoint { get { return dropPoint; } }
        public bool StartsAsleep { get { return startAsleep; } }
        public bool IsAsleep { get { return _state == State.Asleep; } }
        /// <summary>The weapon in the porter's hands, or null.</summary>
        public WeaponBody Carried { get { return _carried; } }
        public bool IsCarrying { get { return role == Role.Porter && _carried != null && !_carried.IsBroken; } }
        public float ShieldHp { get { return _shieldHp; } }
        public float MaxShieldHp { get { return def != null ? def.shieldMaxHp : 0f; } }
        public bool HasShield { get { return _shieldHp > 0f; } }

        /// <summary>Called at the end of Awake.</summary>
        void AwakeRoles()
        {
            _shieldHp = def != null ? def.shieldMaxHp : 0f;
            ShowShield(_shieldHp > 0f);
            if (!startAsleep) return;
            _state = State.Asleep;
            _awake = false;
            ShowMarker("z", sleepColor);
        }

        // ---------------------------------------------------------------- sleep

        /// <summary>Asleep: no sight, no cone, no room aggro. It only listens.</summary>
        void TickAsleep()
        {
            Stop();
            if (def == null || room == null) return;
            IReadOnlyList<WeaponBody> inside = room.WeaponsInside;
            for (int i = 0; i < inside.Count; i++)
            {
                WeaponBody w = inside[i];
                if (w == null || w.IsBroken || w.Body == null) continue;
                float d = Vector3.Distance(w.Body.worldCenterOfMass, transform.position);
                if (w.IsAnimate && d <= def.wakeAnimateRange) { Rouse(w, true); return; }
                if (d <= def.wakeImpactRange && Time.time - w.LastImpactTime < 0.5f
                    && w.LastImpactSpeed >= def.wakeImpactSpeed) { Rouse(w, true); return; }
            }
        }

        void Rouse(WeaponBody source, bool spread)
        {
            if (_state != State.Asleep) return;
            _awake = true;
            HideMarker();
            SetState(State.Idle);
            _hasWanderPoint = false;
            _wanderPauseUntil = 0f;
            if (spread) RouseNeighbours();
            if (source != null && source.IsAnimate) BecomeHostile(source, true);
        }

        void RouseNeighbours()
        {
            if (room == null || def == null) return;
            float r2 = def.wakeNeighbourRadius * def.wakeNeighbourRadius;
            IReadOnlyList<GoblinBrain> mates = room.Enemies;
            for (int i = 0; i < mates.Count; i++)
            {
                GoblinBrain g = mates[i];
                if (g == null || g == this || g.IsDead || !g.IsAsleep) continue;
                if ((g.transform.position - transform.position).sqrMagnitude > r2) continue;
                g.RouseFromNeighbour();
            }
        }

        /// <summary>A sleeper next to it woke up. It does NOT wake further neighbours, so there is no chain reaction.</summary>
        public void RouseFromNeighbour()
        {
            Rouse(null, false);
        }

        // ---------------------------------------------------------------- patrol

        void TickPatrol()
        {
            if (!_awake || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) { Stop(); return; }
            if (Time.time < _wanderPauseUntil) { Stop(); return; }
            agent.speed = def != null ? def.patrolSpeed : 1.6f;
            FollowRoute();
        }

        /// <summary>One step along the wired PatrolRoute. Shared by the patrolling grunt and the carrying porter.</summary>
        void FollowRoute()
        {
            if (patrol == null || patrol.Count == 0) { Stop(); return; }
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) { Stop(); return; }

            Vector3 p = patrol.Point(_patrolIndex);
            _wanderPoint = p;
            if (!_hasWanderPoint)
            {
                _hasWanderPoint = true;
                agent.SetDestination(p);
                return;
            }

            Vector3 flat = p - transform.position;
            flat.y = 0f;
            float arrive = def != null ? def.patrolArrive : 0.6f;
            if (flat.magnitude <= arrive || (!agent.pathPending && agent.remainingDistance <= arrive))
            {
                _patrolIndex = patrol.Next(_patrolIndex, ref _patrolDir);
                _hasWanderPoint = false;
                Stop();
                _wanderPauseUntil = Time.time + patrol.PauseSeconds;
            }
        }

        // ---------------------------------------------------------------- porter

        /// <summary>
        /// Called from Perceive when nothing animate is in sight. Returns true when the porter has taken
        /// charge of the state machine, so the ordinary Curious behaviour is skipped.
        /// </summary>
        bool PorterPerceive()
        {
            if (_carried != null || _state == State.Place) return true;
            if (def == null || room == null) return false;
            if (Time.time < _porterCooldownUntil)
            {
                if (_state == State.Fetch) { _fetch = null; HideMarker(); SetState(State.Idle); }
                return false;
            }

            WeaponBody best = null;
            float bestDistance = def.porterNoticeRange;
            IReadOnlyList<WeaponBody> inside = room.WeaponsInside;
            for (int i = 0; i < inside.Count; i++)
            {
                WeaponBody w = inside[i];
                if (w == null || w.IsBroken || w.Body == null || w.IsCarried) continue;
                if (w.IsAnimate || w.SecondsSinceAnimate < def.porterInanimateSeconds) continue;
                // Anything already sitting on the stand has been delivered; do not fetch it again.
                if (dropPoint != null &&
                    Vector3.Distance(w.Body.worldCenterOfMass, dropPoint.position) < def.porterReach) continue;
                float d = Vector3.Distance(w.Body.worldCenterOfMass, transform.position);
                if (d > bestDistance) continue;
                if (!CanSee(w)) continue;
                bestDistance = d;
                best = w;
            }

            if (best == null)
            {
                if (_state == State.Fetch) { _fetch = null; HideMarker(); SetState(State.Idle); }
                return false;
            }

            _fetch = best;
            _target = best;
            if (_state != State.Fetch) SetState(State.Fetch);
            ShowMarker("?", curiousColor);
            return true;
        }

        /// <summary>Authority validation: may this porter pick this weapon up right now? Pure.</summary>
        public bool CanPickUp(WeaponBody weapon)
        {
            if (role != Role.Porter || _state == State.Dead || _carried != null || def == null) return false;
            if (weapon == null || weapon.IsBroken || weapon.Body == null || weapon.IsCarried) return false;
            if (weapon.IsAnimate || weapon.SecondsSinceAnimate < def.porterInanimateSeconds) return false;
            Vector3 to = weapon.Body.worldCenterOfMass - transform.position;
            to.y = 0f;
            return to.magnitude <= def.porterReach + 0.5f;
        }

        /// <summary>Authority only.</summary>
        public void ApplyCarry(WeaponBody weapon)
        {
            if (weapon == null) return;
            _carried = weapon;
            _fetch = null;
            _target = null;
            weapon.SetCarried(true);
            HideMarker();
            SetState(State.Carry);
        }

        /// <summary>Authority only. placed = set down on the stand; otherwise it came alive in his hands.</summary>
        public void ApplyDrop(bool placed)
        {
            WeaponBody weapon = _carried;
            _carried = null;
            if (weapon != null)
            {
                weapon.SetCarried(false);
                if (placed && dropPoint != null) weapon.Teleport(dropPoint.position, dropPoint.rotation);
            }
            if (def != null) _porterCooldownUntil = Time.time + def.porterCooldownSeconds;
            if (_state == State.Dead) return;
            if (placed)
            {
                _hasWanderPoint = false;
                SetState(State.Idle);
            }
            else
            {
                BecomeHostile(weapon, true);
            }
        }

        void TickFetch()
        {
            if (def == null || _fetch == null || _fetch.Body == null || _fetch.IsBroken
                || _fetch.IsAnimate || _fetch.IsCarried)
            {
                _fetch = null;
                HideMarker();
                SetState(State.Idle);
                return;
            }

            Vector3 to = _fetch.Body.worldCenterOfMass - transform.position;
            to.y = 0f;
            if (to.magnitude <= def.porterReach)
            {
                Stop();
                if (authority != null) authority.RequestPorterPickUp(this, _fetch);
                return;
            }
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.speed = def.curiousSpeed;
                agent.SetDestination(_fetch.Body.worldCenterOfMass);
            }
        }

        void TickCarry()
        {
            if (_carried == null) { SetState(State.Idle); return; }
            float reach = def != null ? def.porterReach : 2f;
            if (dropPoint != null)
            {
                Vector3 to = dropPoint.position - transform.position;
                to.y = 0f;
                if (to.magnitude <= reach) { Stop(); SetState(State.Place); return; }
            }
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            agent.speed = def != null ? def.porterCarrySpeed : 2.2f;

            if (patrol != null && patrol.Count > 0)
            {
                if (Time.time < _wanderPauseUntil) { Stop(); return; }
                FollowRoute();
            }
            else if (dropPoint != null)
            {
                agent.SetDestination(dropPoint.position);
            }
        }

        void TickPlace()
        {
            Stop();
            float wait = def != null ? def.porterPlaceSeconds : 0.6f;
            if (_stateTime < wait) return;
            if (authority != null) authority.RequestPorterDrop(this, true);
            else ApplyDrop(true);
        }

        /// <summary>The carry pose is written every physics step; velocity is not, so a launch still takes.</summary>
        void FixedUpdate()
        {
            if (_carried == null) return;
            if (_state == State.Dead || _carried.IsBroken || _carried.IsAnimate)
            {
                if (authority != null) authority.RequestPorterDrop(this, false);
                else ApplyDrop(false);
                return;
            }
            if (carrySocket != null) _carried.CarryTo(carrySocket.position, carrySocket.rotation);
        }

        // ---------------------------------------------------------------- shield boss

        /// <summary>What one impact does, after the shield has had its say.</summary>
        public struct HitOutcome
        {
            /// <summary>Damage that actually landed (0 when the shield turned it away).</summary>
            public float damage;
            public float newHp;
            public float newShieldHp;
            /// <summary>The hit went into the shield, not the body.</summary>
            public bool hitShield;
            public bool shieldBroke;
            /// <summary>Too light for the shield: it did nothing at all.</summary>
            public bool blocked;
        }

        /// <summary>
        /// Authority only, pure: how much of a raw impact gets through. An ordinary goblin has no shield
        /// and a bladed bonus of 1, so this is exactly the old arithmetic for it.
        /// </summary>
        public HitOutcome ResolveDamage(WeaponBody weapon, float rawDamage)
        {
            HitOutcome outcome = new HitOutcome();
            outcome.damage = rawDamage;
            outcome.newHp = _hp;
            outcome.newShieldHp = _shieldHp;

            if (_shieldHp > 0f)
            {
                outcome.hitShield = true;
                float mass = 0f;
                if (weapon != null)
                    mass = weapon.Def != null ? weapon.Def.mass : (weapon.Body != null ? weapon.Body.mass : 0f);
                float need = def != null ? def.shieldMinMass : 8f;
                if (mass < need)
                {
                    outcome.damage = 0f;
                    outcome.blocked = true;
                    return outcome;
                }
                outcome.newShieldHp = Mathf.Max(0f, _shieldHp - rawDamage);
                outcome.shieldBroke = outcome.newShieldHp <= 0f;
                return outcome;
            }

            if (weapon != null && weapon.Def != null && weapon.Def.bladed && def != null && def.bladedDamageBonus > 0f)
                outcome.damage = rawDamage * def.bladedDamageBonus;
            outcome.newHp = Mathf.Max(0f, _hp - outcome.damage);
            return outcome;
        }

        /// <summary>Authority only: the validated result of an impact.</summary>
        public void ApplyHit(HitOutcome outcome, Vector3 knockVelocity, WeaponBody source)
        {
            if (_state == State.Dead) return;
            _shieldHp = outcome.newShieldHp;
            if (!outcome.hitShield) _hp = Mathf.Max(0f, outcome.newHp);
            _knock += knockVelocity;
            _awake = true;
            _lastHurtTime = Time.time;
            _flashUntil = Time.time + (def != null ? def.hitFlashSeconds : 0.12f);
            if (outcome.shieldBroke) ShowShield(false);
            if (_state == State.Asleep) Rouse(source, true);
            BecomeHostile(source != null ? source : _target, true);
        }

        void ShowShield(bool on)
        {
            if (shieldRenderer != null && shieldRenderer.enabled != on) shieldRenderer.enabled = on;
            if (shieldBar != null && shieldBar.gameObject.activeSelf != on) shieldBar.gameObject.SetActive(on);
        }

        /// <summary>The shield bar is a second world-space bar above the body bar.</summary>
        void LateUpdate()
        {
            if (shieldBar == null || !shieldBar.gameObject.activeSelf) return;
            float max = Mathf.Max(1f, MaxShieldHp);
            if (shieldBarFill != null)
            {
                Vector3 s = shieldBarFill.localScale;
                shieldBarFill.localScale = new Vector3(Mathf.Clamp01(_shieldHp / max), s.y, s.z);
            }
            Camera view = authority != null ? authority.ViewCamera : null;
            if (view != null) shieldBar.rotation = view.transform.rotation;
        }

        // ---------------------------------------------------------------- gizmos

        void OnDrawGizmosSelected()
        {
            if (def == null) return;
            Vector3 eye = transform.position + Vector3.up * def.eyeHeight;
            float half = Mathf.Max(5f, def.viewConeDeg) * 0.5f;
            Gizmos.color = new Color(0.98f, 0.92f, 0.40f, 0.9f);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, -half, 0f) * transform.forward * def.curiousRange);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, half, 0f) * transform.forward * def.curiousRange);
            Gizmos.color = new Color(0.96f, 0.35f, 0.20f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, def.aggroRange);
            if (startAsleep)
            {
                Gizmos.color = new Color(0.35f, 0.72f, 0.94f, 0.6f);
                Gizmos.DrawWireSphere(transform.position, def.wakeAnimateRange);
                Gizmos.DrawWireSphere(transform.position, def.wakeImpactRange);
            }
            if (role == Role.Porter && dropPoint != null)
            {
                Gizmos.color = new Color(0.24f, 0.86f, 0.94f, 0.9f);
                Gizmos.DrawLine(transform.position, dropPoint.position);
                Gizmos.DrawWireCube(dropPoint.position, Vector3.one * 0.5f);
            }
        }
    }
}
