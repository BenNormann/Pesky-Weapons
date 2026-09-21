using Pesky.Data;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Pesky.Game
{
    /// <summary>
    /// The Goblin (docs/SLICE-1.md section 5, revised by the owner's first play-test feedback).
    ///
    /// A goblin does NOT know a weapon is possessed. It only reacts to what it can see:
    /// * Idle      - wanders slowly between points inside its own room.
    /// * Curious   - noticed a weapon within curiousRange with line of sight, walks over, stops about
    ///               curiousStopDistance away, shows "?" and looks at it. It NEVER attacks from here.
    /// * Alarmed   - a short "!" reaction after it perceives an ANIMATE weapon or is damaged.
    /// * Chase / Windup / Strike / Recover - the fight. The strike is a silver arc swept across the
    ///               front; damage only lands on a target inside the swept region.
    /// * Dead.
    ///
    /// A weapon counts as ANIMATE only while it moves under its own power (WeaponBody.IsAnimate).
    /// Turning hostile alerts other goblins within alertRadius in the same room. If the target then stays
    /// inanimate for calmSeconds and the goblin has not been hurt, it finishes the attack in progress,
    /// shows "?" and drops back to Curious - playing dead works. Free souls are ignored entirely.
    /// Only a HOSTILE goblin is reported to the WorldAuthority as targeting the player, so a curious
    /// goblin never sets the player's IN COMBAT flag.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed partial class GoblinBrain : MonoBehaviour, ISceneId
    {
        public enum State
        {
            Idle = 0, Chase = 1, Windup = 2, Recover = 3, Dead = 4, Curious = 5, Alarmed = 6, Strike = 7,
            /// <summary>Sleeping: it perceives nothing until something wakes it (Barracks).</summary>
            Asleep = 8,
            /// <summary>Porter: walking over to a weapon that has lain still long enough.</summary>
            Fetch = 9,
            /// <summary>Porter: carrying a weapon along its route.</summary>
            Carry = 10,
            /// <summary>Porter: setting the weapon down on its stand.</summary>
            Place = 11
        }
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] EnemyDef def;

        [Header("Wiring")]
        [SerializeField] WorldAuthority authority;
        [Tooltip("The room this goblin belongs to. It only notices weapons inside it.")]
        [SerializeField] RoomVolume room;
        [SerializeField] NavMeshAgent agent;
        [SerializeField] Transform bodyVisual;
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Collider bodyCollider;

        [Header("Attack visual")]
        [Tooltip("Pivot at chest height; rotating it about local Y sweeps the blade across the front.")]
        [SerializeField] Transform arcPivot;
        [SerializeField] Renderer bladeRenderer;
        [Tooltip("Degrees the blade is tilted up while it is held raised during the windup.")]
        [SerializeField] float bladeRaiseDeg = 45f;

        [Header("World-space UI")]
        [SerializeField] Transform healthBar;
        [Tooltip("Left-anchored pivot: its local X scale is the HP fraction.")]
        [SerializeField] Transform healthBarFill;
        [Tooltip("Shows \"?\" while curious and \"!\" while alarmed.")]
        [SerializeField] TextMeshPro marker;

        [Header("Colours")]
        [SerializeField] Color idleColor = new Color(0.24f, 0.63f, 0.27f, 1f);
        [SerializeField] Color windupColor = new Color(0.86f, 0.20f, 0.20f, 1f);
        [SerializeField] Color hitColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] Color deadColor = new Color(0.12f, 0.22f, 0.13f, 1f);
        [SerializeField] Color curiousColor = new Color(0.98f, 0.92f, 0.40f, 1f);
        [SerializeField] Color alarmColor = new Color(0.96f, 0.35f, 0.20f, 1f);

        [Header("Start")]
        [Tooltip("Wander from the first frame instead of waiting for its room to be entered.")]
        [SerializeField] bool startAwake;

        MaterialPropertyBlock _mpb;
        Vector3 _visualScale;
        Vector3 _knock;
        Vector3 _anchor;
        Vector3 _wanderPoint;
        WeaponBody _target;
        State _state = State.Idle;
        float _hp;
        float _stateTime;
        float _flashUntil;
        float _wanderPauseUntil;
        float _lookSince = -1f;
        float _ignoreCuriousUntil = -999f;
        float _lastHurtTime = -999f;
        float _lastAnimateSeen = -999f;
        float _sweepAngle;
        float _prevSweep;
        float _strikeSide = 1f;
        bool _hasWanderPoint;
        bool _struck;
        bool _pendingCalm;
        bool _hostile;
        bool _awake;

        public int SceneId { get { return id; } }
        public EnemyDef Def { get { return def; } }
        public RoomVolume Room { get { return room; } }
        public State Current { get { return _state; } }
        public bool IsDead { get { return _state == State.Dead; } }
        public bool IsAwake { get { return _awake; } }
        /// <summary>True only while it is actually fighting. The player's IN COMBAT tag counts these.</summary>
        public bool IsHostile { get { return _hostile && _state != State.Dead; } }
        public bool IsCurious { get { return _state == State.Curious; } }
        public float Hp { get { return _hp; } }
        public float MaxHp { get { return def != null ? def.maxHp : 0f; } }
        public WeaponBody Target { get { return _target; } }
        public float KnockbackPerDamage { get { return def != null ? def.knockbackPerDamage : 0.25f; } }
        public float MaxKnockbackSpeed { get { return def != null ? def.maxKnockbackSpeed : 9f; } }
        public Vector3 Centre { get { return transform.position + Vector3.up * (def != null ? def.height * 0.5f : 0.7f); } }
        /// <summary>Where the blade is right now, in degrees about the goblin's forward axis.</summary>
        public float SweepAngle { get { return _sweepAngle; } }

        Camera View { get { return authority != null ? authority.ViewCamera : null; } }

        void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            _mpb = new MaterialPropertyBlock();
            _visualScale = bodyVisual != null ? bodyVisual.localScale : Vector3.one;
            _anchor = transform.position;
            _hp = MaxHp;
            ApplyDef();
            Tint(idleColor);
            _awake = startAwake;
            ShowBlade(false);
            HideMarker();
            if (healthBar != null) healthBar.gameObject.SetActive(false);
            AwakeRoles();
        }

        [ContextMenu("Apply Def")]
        public void ApplyDef()
        {
            if (def == null || agent == null) return;
            agent.radius = def.radius;
            agent.height = def.height;
            agent.speed = def.moveSpeed;
            agent.angularSpeed = def.angularSpeed;
            agent.acceleration = def.acceleration;
            agent.stoppingDistance = def.attackRange * 0.7f;
            agent.autoBraking = true;
        }

        /// <summary>Its RoomVolume was entered. This only starts it WANDERING - it never causes aggro.</summary>
        /// <summary>Its RoomVolume was entered. This only starts it WANDERING - it never causes aggro, and it never wakes a sleeper.</summary>
        public void Wake()
        {
            if (_state == State.Dead || _state == State.Asleep) return;
            _awake = true;
        }

        // ---------------------------------------------------------------- damage in

        /// <summary>Authority only: the validated result of an impact.</summary>
        public void ApplyHit(float newHp, Vector3 knockVelocity)
        {
            ApplyHit(newHp, knockVelocity, null);
        }

        /// <summary>Authority only: the validated result of an impact. Being damaged always makes it hostile.</summary>
        public void ApplyHit(float newHp, Vector3 knockVelocity, WeaponBody source)
        {
            if (_state == State.Dead) return;
            _hp = Mathf.Max(0f, newHp);
            _knock += knockVelocity;
            _awake = true;
            _lastHurtTime = Time.time;
            _flashUntil = Time.time + (def != null ? def.hitFlashSeconds : 0.12f);
            BecomeHostile(source != null ? source : _target, true);
        }

        /// <summary>A neighbour turned hostile and shouted. Does not shout again.</summary>
        public void AlertedBy(WeaponBody threat)
        {
            if (_state == State.Dead || _hostile) return;
            BecomeHostile(threat, false);
        }

        void BecomeHostile(WeaponBody threat, bool spread)
        {
            if (_state == State.Dead || _puppet) return;
            _awake = true;
            if (threat != null) _target = threat;
            if (_target == null) return;
            _lastAnimateSeen = Time.time;
            _pendingCalm = false;
            if (_hostile) return;

            _hostile = true;
            SetState(State.Alarmed);
            ShowMarker("!", alarmColor);
            if (spread) AlertNeighbours();
        }

        void AlertNeighbours()
        {
            if (room == null || def == null) return;
            float r2 = def.alertRadius * def.alertRadius;
            System.Collections.Generic.IReadOnlyList<GoblinBrain> mates = room.Enemies;
            for (int i = 0; i < mates.Count; i++)
            {
                GoblinBrain g = mates[i];
                if (g == null || g == this || g.IsDead) continue;
                if ((g.transform.position - transform.position).sqrMagnitude > r2) continue;
                g.AlertedBy(_target);
            }
        }

        /// <summary>Authority only.</summary>
        public void Kill()
        {
            if (_state == State.Dead) return;
            _hp = 0f;
            _state = State.Dead;
            _knock = Vector3.zero;
            _target = null;
            _hostile = false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            if (agent != null) agent.enabled = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (bodyVisual != null)
            {
                bodyVisual.localScale = new Vector3(_visualScale.x * 1.15f, _visualScale.y * 0.18f, _visualScale.z * 1.15f);
                Vector3 p = bodyVisual.localPosition;
                bodyVisual.localPosition = new Vector3(p.x, _visualScale.y * 0.18f, p.z);
            }
            ShowBlade(false);
            HideMarker();
            if (healthBar != null) healthBar.gameObject.SetActive(false);
            Tint(deadColor);
        }

        void OnCollisionEnter(Collision collision) { ReportImpact(collision); }

        void ReportImpact(Collision collision)
        {
            if (_state == State.Dead || authority == null) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null || weapon.IsBroken) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : Centre;
            authority.RequestHitEnemy(weapon.Id, id, collision.relativeVelocity.magnitude, point);
        }

        // ---------------------------------------------------------------- FSM

        void Update()
        {
            if (_puppet) { PuppetUpdate(); return; }
            if (_state == State.Dead) return;

            float dt = Time.deltaTime;
            _stateTime += dt;

            if (_knock.sqrMagnitude > 0.0001f)
            {
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.Move(_knock * dt);
                float damp = def != null ? def.knockbackDamping : 6f;
                _knock = Vector3.MoveTowards(_knock, Vector3.zero, damp * dt);
            }

            // A sleeping goblin perceives nothing. It only listens for a close animate weapon or a bang.
            if (_state == State.Asleep)
            {
                TickAsleep();
                UpdateLook();
                return;
            }

            Perceive();

            if (authority != null)
                authority.SetTargeting(this, _hostile && _target != null ? _target.Possessor as PlayerSoul : null);

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Curious: TickCurious(); break;
                case State.Alarmed: TickAlarmed(); break;
                case State.Chase: TickChase(); break;
                case State.Windup: TickWindup(); break;
                case State.Strike: TickStrike(); break;
                case State.Recover: TickRecover(); break;
                case State.Fetch: TickFetch(); break;
                case State.Carry: TickCarry(); break;
                case State.Place: TickPlace(); break;
            }

            UpdateLook();
        }

        void SetState(State next)
        {
            if (_state == next) return;
            _state = next;
            _stateTime = 0f;
            if (next == State.Curious) _lookSince = -1f;
            if (next == State.Strike) _struck = false;
        }

        // ---------------------------------------------------------------- perception

        void Perceive()
        {
            if (def == null || room == null || !_awake)
            {
                if (!_hostile) _target = null;
                return;
            }

            if (_hostile)
            {
                if (_target == null || _target.IsBroken || !room.Contains(_target))
                {
                    LoseHostility(false);
                    return;
                }
                if (_target.IsAnimate) _lastAnimateSeen = Time.time;
                bool hurtRecently = Time.time - _lastHurtTime < def.calmSeconds;
                if (!hurtRecently && Time.time - _lastAnimateSeen >= def.calmSeconds) _pendingCalm = true;
                if (_pendingCalm && (_state == State.Chase || _state == State.Alarmed || _state == State.Idle))
                    LoseHostility(true);
                return;
            }

            WeaponBody animate = null;
            WeaponBody curious = null;
            float bestAnimate = def.aggroRange;
            float bestCurious = def.curiousRange;
            System.Collections.Generic.IReadOnlyList<WeaponBody> inside = room.WeaponsInside;
            for (int i = 0; i < inside.Count; i++)
            {
                WeaponBody w = inside[i];
                if (w == null || w.IsBroken || w.Body == null) continue;
                float d = Vector3.Distance(w.Body.worldCenterOfMass, transform.position);
                if (d > bestCurious && d > bestAnimate) continue;
                if (!CanSee(w)) continue;
                if (w.IsAnimate && d <= bestAnimate) { bestAnimate = d; animate = w; }
                if (d <= bestCurious) { bestCurious = d; curious = w; }
            }

            if (animate != null)
            {
                BecomeHostile(animate, true);
                return;
            }

            // A porter would rather carry a still weapon away than stand and stare at it.
            if (role == Role.Porter && PorterPerceive()) return;

            if (curious != null && Time.time >= _ignoreCuriousUntil)
            {
                if (_state != State.Curious) SetState(State.Curious);
                if (_target != curious) { _target = curious; _lookSince = -1f; }
                ShowMarker("?", curiousColor);
            }
            else if (_state == State.Curious)
            {
                _target = null;
                HideMarker();
                SetState(State.Idle);
            }
        }

        /// <summary>
        /// Line of sight AND the view cone. A goblin does not see what is behind it, which is what makes
        /// "move only when nobody looks" a puzzle instead of a chase. Anything closer than
        /// EnemyDef.closeSightRange is noticed whatever the cone says.
        /// </summary>
        bool CanSee(WeaponBody w)
        {
            if (w == null || w.Body == null || def == null) return false;
            Vector3 eye = transform.position + Vector3.up * def.eyeHeight;
            Vector3 to = w.Body.worldCenterOfMass - eye;
            float d = to.magnitude;
            if (d < 0.05f) return true;
            if (d > def.closeSightRange)
            {
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 1e-6f &&
                    Vector3.Angle(transform.forward, flat.normalized) > Mathf.Max(5f, def.viewConeDeg) * 0.5f)
                    return false;
            }
            return !Physics.Raycast(eye, to / d, d - 0.1f, def.sightBlockers, QueryTriggerInteraction.Ignore);
        }

        void LoseHostility(bool stayCurious)
        {
            _hostile = false;
            _pendingCalm = false;
            _lastAnimateSeen = Time.time;
            if (authority != null) authority.SetTargeting(this, null);
            ShowBlade(false);
            if (stayCurious && _target != null)
            {
                SetState(State.Curious);
                ShowMarker("?", curiousColor);
            }
            else
            {
                _target = null;
                HideMarker();
                SetState(State.Idle);
            }
        }

        // ---------------------------------------------------------------- states

        void Stop()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
        }

        void TickIdle()
        {
            if (patrol != null && patrol.Count > 0) { TickPatrol(); return; }
            if (!_awake || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) { Stop(); return; }
            if (Time.time < _wanderPauseUntil) { Stop(); return; }

            if (!_hasWanderPoint)
            {
                if (!PickWanderPoint()) { _wanderPauseUntil = Time.time + 1f; return; }
                agent.speed = def != null ? def.wanderSpeed : 1.1f;
                agent.SetDestination(_wanderPoint);
                return;
            }

            Vector3 flat = _wanderPoint - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= 0.6f || (!agent.pathPending && agent.remainingDistance <= 0.6f))
            {
                _hasWanderPoint = false;
                Stop();
                float lo = def != null ? def.wanderPauseMin : 1.5f;
                float hi = def != null ? def.wanderPauseMax : 3.5f;
                _wanderPauseUntil = Time.time + Random.Range(lo, hi);
            }
        }

        bool PickWanderPoint()
        {
            float r = def != null ? def.wanderRadius : 4f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 disc = Random.insideUnitCircle * r;
                Vector3 candidate = _anchor + new Vector3(disc.x, 0f, disc.y);
                if (room != null && !room.ContainsPoint(candidate)) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, 1.5f, NavMesh.AllAreas)) continue;
                if (room != null && !room.ContainsPoint(hit.position)) continue;
                _wanderPoint = hit.position;
                _hasWanderPoint = true;
                return true;
            }
            return false;
        }

        void TickCurious()
        {
            if (_target == null || _target.Body == null) { HideMarker(); SetState(State.Idle); return; }
            float stop = def != null ? def.curiousStopDistance : 1.5f;
            Vector3 to = _target.Body.worldCenterOfMass - transform.position;
            to.y = 0f;
            float d = to.magnitude;

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.speed = def != null ? def.curiousSpeed : 1.8f;
                if (d > stop + 0.2f) agent.SetDestination(_target.Body.worldCenterOfMass);
                else Stop();
            }

            if (d <= stop + 0.5f)
            {
                if (_lookSince < 0f) _lookSince = Time.time;
                float look = def != null ? def.curiousSeconds : 4f;
                if (Time.time - _lookSince >= look)
                {
                    _ignoreCuriousUntil = Time.time + look;
                    _target = null;
                    _hasWanderPoint = false;
                    _wanderPauseUntil = 0f;
                    HideMarker();
                    SetState(State.Idle);
                }
            }
        }

        void TickAlarmed()
        {
            Stop();
            float wait = def != null ? def.alarmedSeconds : 0.4f;
            if (_stateTime < wait) return;
            HideMarker();
            SetState(State.Chase);
        }

        void TickChase()
        {
            if (_target == null || _target.Body == null) { LoseHostility(false); return; }
            float range = def != null ? def.attackRange : 1.4f;
            Vector3 to = _target.Body.worldCenterOfMass - transform.position;
            to.y = 0f;
            if (to.magnitude <= range)
            {
                Stop();
                SetState(State.Windup);
                return;
            }
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.speed = def != null ? def.moveSpeed : 3f;
                agent.SetDestination(_target.Body.worldCenterOfMass);
            }
        }

        void TickWindup()
        {
            float windup = def != null ? def.windupSeconds : 0.5f;
            if (_stateTime < windup) return;
            float half = def != null ? def.strikeHalfAngleDeg : 80f;
            _prevSweep = -half * _strikeSide;
            SetState(State.Strike);
        }

        void TickStrike()
        {
            float half = def != null ? def.strikeHalfAngleDeg : 80f;
            float span = def != null ? def.strikeSeconds : 0.25f;
            float k = span > 0.001f ? Mathf.Clamp01(_stateTime / span) : 1f;
            float angle = Mathf.Lerp(-half, half, k) * _strikeSide;

            if (!_struck && _target != null && _target.Body != null && authority != null)
            {
                float reach = def != null ? def.strikeReach : 1.7f;
                Vector3 to = _target.Body.worldCenterOfMass - transform.position;
                to.y = 0f;
                if (to.magnitude <= reach && to.sqrMagnitude > 1e-6f)
                {
                    float bearing = Vector3.SignedAngle(transform.forward, to.normalized, Vector3.up);
                    float lo = Mathf.Min(_prevSweep, angle);
                    float hi = Mathf.Max(_prevSweep, angle);
                    if (bearing >= lo && bearing <= hi)
                    {
                        LandHit(to);
                        _struck = true;
                    }
                }
            }

            _prevSweep = angle;
            _sweepAngle = angle;

            if (k >= 1f)
            {
                _strikeSide = -_strikeSide;
                SetState(State.Recover);
            }
        }

        void LandHit(Vector3 toTarget)
        {
            float dmg = def != null ? def.attackDamage : 15f;
            if (_target.Body == null) return;
            Vector3 push = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            push = (push + Vector3.up * 0.5f).normalized;
            float kb = def != null ? def.attackKnockback : 8f;
            // The damage and the shove travel together (WEAPON_DAMAGED); whoever simulates the weapon applies the shove.
            authority.RequestEnemyAttack(this, _target, dmg, push * kb);
        }

        void TickRecover()
        {
            float cooldown = def != null ? def.attackCooldown : 1.2f;
            if (_stateTime < cooldown) return;
            if (_pendingCalm) { LoseHostility(true); return; }
            SetState(_target != null ? State.Chase : State.Idle);
        }

        // ---------------------------------------------------------------- look

        void UpdateLook()
        {
            float swell = 1f;
            Color colour = idleColor;

            if (_state == State.Windup)
            {
                float windup = def != null ? def.windupSeconds : 0.5f;
                float k = windup > 0.001f ? Mathf.Clamp01(_stateTime / windup) : 1f;
                float peak = def != null ? def.windupSwell : 1.25f;
                swell = Mathf.Lerp(1f, peak, k);
                colour = Color.Lerp(idleColor, windupColor, k);
            }
            else if (_state == State.Strike)
            {
                swell = def != null ? def.windupSwell : 1.25f;
                colour = windupColor;
            }
            else if (_state == State.Recover)
            {
                colour = Color.Lerp(windupColor, idleColor, Mathf.Clamp01(_stateTime * 4f));
            }

            if (Time.time < _flashUntil) colour = hitColor;

            if (bodyVisual != null)
                bodyVisual.localScale = Vector3.Lerp(bodyVisual.localScale, _visualScale * swell, Time.deltaTime * 14f);
            Tint(colour);

            UpdateBlade();
            UpdateWorldUi();

            // Facing: it turns to whatever it is interested in, but never mid-sweep - the arc must mean
            // something, so the swept region is fixed for the whole strike.
            if (_state == State.Strike) return;
            Vector3 lookAt = Vector3.zero;
            if (_target != null && _target.Body != null) lookAt = _target.Body.worldCenterOfMass - transform.position;
            else if (_state == State.Idle && _hasWanderPoint) lookAt = _wanderPoint - transform.position;
            lookAt.y = 0f;
            if (lookAt.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(lookAt), 540f * Time.deltaTime);
        }

        void UpdateBlade()
        {
            bool showing = _state == State.Windup || _state == State.Strike;
            ShowBlade(showing);
            if (!showing || arcPivot == null) return;

            float half = def != null ? def.strikeHalfAngleDeg : 80f;
            if (_state == State.Windup)
            {
                _sweepAngle = -half * _strikeSide;
                arcPivot.localRotation = Quaternion.Euler(-bladeRaiseDeg, _sweepAngle, 0f);
            }
            else
            {
                float span = def != null ? def.strikeSeconds : 0.25f;
                float k = span > 0.001f ? Mathf.Clamp01(_stateTime / span) : 1f;
                arcPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-bladeRaiseDeg, 0f, k), _sweepAngle, 0f);
            }
        }

        void ShowBlade(bool on)
        {
            if (bladeRenderer != null && bladeRenderer.enabled != on) bladeRenderer.enabled = on;
        }

        void UpdateWorldUi()
        {
            Camera view = View;
            float max = Mathf.Max(1f, MaxHp);
            float frac = Mathf.Clamp01(_hp / max);
            float showBelow = def != null ? def.healthBarShowBelow : 0.999f;
            bool showBar = _hostile || frac < showBelow;

            if (healthBar != null)
            {
                if (healthBar.gameObject.activeSelf != showBar) healthBar.gameObject.SetActive(showBar);
                if (showBar)
                {
                    if (healthBarFill != null)
                    {
                        Vector3 s = healthBarFill.localScale;
                        healthBarFill.localScale = new Vector3(frac, s.y, s.z);
                    }
                    if (view != null) healthBar.rotation = view.transform.rotation;
                }
            }

            if (marker != null && marker.gameObject.activeSelf && view != null)
                marker.transform.rotation = view.transform.rotation;
        }

        void ShowMarker(string text, Color colour)
        {
            if (marker == null) return;
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            if (marker.text != text) marker.text = text;
            marker.color = colour;
        }

        void HideMarker()
        {
            if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
        }

        void Tint(Color c)
        {
            if (bodyRenderer == null || _mpb == null) return;
            bodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            bodyRenderer.SetPropertyBlock(_mpb);
        }
    }
}
