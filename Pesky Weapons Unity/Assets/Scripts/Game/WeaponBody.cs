using System;
using System.Collections.Generic;
using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The physical weapon: Rigidbody setup from its WeaponDef, grounded / wall contact tracking, last
    /// safe position and kill-Y recovery, HP, the modifier list, possession state, Break and the timed
    /// respawn on its home slot. It never reads input; a WeaponMotor drives it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed partial class WeaponBody : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [Tooltip("Stable id of this weapon instance inside its scene.")]
        [SerializeField] int id;

        [Header("Data")]
        [SerializeField] WeaponDef def;
        [SerializeField] MovementTuning tuning;

        [Header("Wiring")]
        [SerializeField] Rigidbody body;
        [Tooltip("Scene slot this weapon respawns on. Without one it respawns where it was authored.")]
        [SerializeField] WeaponHomeSlot homeSlot;

        [Header("Break")]
        [SerializeField] float respawnDelay = 10f;
        [SerializeField] float maxAngularVelocity = 25f;        [Tooltip("While free and never yet possessed, the weapon is held kinematic in its home slot pose so nothing topples.")]
        [SerializeField] bool restAtHome = true;


        [Header("State (modifiers live on the weapon)")]
        [SerializeField] List<ModifierDef> modifiers = new List<ModifierDef>();

        Collider[] _colliders;
        Renderer[] _renderers;
        IWeaponPossessor _possessor;
        Vector3 _spawnPosition;
        Quaternion _spawnRotation;
        Vector3 _lastSafePosition;
        Vector3 _groundNormal = Vector3.up;
        Vector3 _wallNormal = Vector3.zero;
        float _lastGroundTime = -999f;
        float _lastWallTime = -999f;
        bool _stepGrounded, _stepWall, _restGrounded, _restWall;
        float _hp;
        float _respawnAt;
        bool _broken;        bool _held;
        float _lastAnimateTime = -999f;
        float _lastLaunchTime = -999f;
        bool _stuck;
        bool _stuckFollows;
        Transform _stuckTo;
        Vector3 _stuckLocalPos;
        Quaternion _stuckLocalRot = Quaternion.identity;
        Vector3 _stuckNormal = Vector3.up;
        float _stickRearmAt;
        // CLIMB (round 29): after a climb hop the blade probes for the timber it left and stabs back in where it stops rising.
        bool _climbing;
        Vector3 _climbNormal;
        bool _carried;
        [Tooltip("A loose weapon that stands up by itself and takes a bat: the Bat Room test scene's bot Dagger. Nobody's, so the host bats it directly.")]
        [SerializeField] bool autoBrace;
        bool _braceWanted, _braced;
        float _braceHoldOff = -999f;
        const float BraceTurnDegreesPerSecond = 270f;   // slow: a weapon has weight
        const float BraceTurnGain = 6f;                  // angular speed per radian of error, up to the cap above
        float _nextOverlapCheck;
        float _zMin, _zMax;                              // the solid colliders' reach along local Z (the grip end, the point / head end)
        static readonly Collider[] _overlapBuf = new Collider[24];
        const float BraceWobbleDegrees = 12f;            // it overshoots upright and settles back
        float _braceStandStart = -1f;
        float _lastImpactTime = -999f;
        float _lastImpactSpeed;
        public event Action<WeaponBody> Broken;
        public event Action<WeaponBody> Respawned;
        public event Action<WeaponBody> HpChanged;
        public event Action<WeaponBody> ModifiersChanged;
        public event Action<WeaponBody> Recovered;
        /// <summary>(weapon, wood) a bladed weapon stuck point-first in a WoodSurface.</summary>
        public event Action<WeaponBody, WoodSurface> Stuck;
        /// <summary>The weapon came free of the wood (launch, knockback, release, break, teleport).</summary>
        public event Action<WeaponBody> Unstuck;


        public int Id { get { return id; } }
        public int SceneId { get { return id; } }
        public WeaponDef Def { get { return def; } }
        public MovementTuning Tuning { get { return tuning; } }
        public Rigidbody Body { get { return body; } }

        /// <summary>The union of this body's solid collider bounds in world space: the shape as it lies right now.</summary>
        public Bounds WorldBounds
        {
            get
            {
                Bounds b = new Bounds(body != null ? body.position : transform.position, Vector3.zero);
                bool first = true;
                if (_colliders != null)
                {
                    for (int i = 0; i < _colliders.Length; i++)
                    {
                        Collider c = _colliders[i];
                        if (c == null || !c.enabled || c.isTrigger) continue;
                        if (first) { b = c.bounds; first = false; }
                        else b.Encapsulate(c.bounds);
                    }
                }
                return b;
            }
        }
        public WeaponHomeSlot HomeSlot { get { return homeSlot; } }
        public float Hp { get { return _hp; } }
        public float MaxHp { get { return def != null ? def.maxHp : 0f; } }
        public bool IsBroken { get { return _broken; } }
        public IWeaponPossessor Possessor { get { return _possessor; } }
        public bool IsPossessed { get { return _possessor != null; } }
        public bool IsFree { get { return !_broken && _possessor == null; } }
        public IReadOnlyList<ModifierDef> Modifiers { get { return modifiers; } }
        public Vector3 LastSafePosition { get { return _lastSafePosition; } }
        public Vector3 GroundNormal { get { return _groundNormal; } }
        public Vector3 WallNormal { get { return _wallNormal; } }
        /// <summary>Fixed time of the latest ground contact (kept fresh while the body sleeps on the ground).</summary>
        public float LastGroundContactTime { get { return _lastGroundTime; } }
        public float LastWallContactTime { get { return _lastWallTime; } }
        public float SecondsUntilRespawn { get { return _broken ? Mathf.Max(0f, _respawnAt - Time.time) : 0f; } }        /// <summary>True while the weapon is frozen in its home slot pose (on the rack, untouched).</summary>
        public bool IsHeldAtHome { get { return _held; } }
        /// <summary>True while a bladed weapon is held in a WoodSurface. It counts as grounded meanwhile.</summary>
        public bool IsStuck { get { return _stuck; } }
        /// <summary>True between a climb hop's launch and its re-stick (or its landing).</summary>
        public bool IsClimbing { get { return _climbing; } }
        /// <summary>The wood's surface normal at the point the weapon stuck (points out of the wood).</summary>
        public Vector3 StuckNormal { get { return _stuckNormal; } }
        /// <summary>World direction from the grip to the point (WeaponDef.tipAxis).</summary>
        public Vector3 TipDirection
        {
            get
            {
                Vector3 axis = def != null && def.tipAxis.sqrMagnitude > 1e-6f ? def.tipAxis : Vector3.forward;
                return transform.TransformDirection(axis).normalized;
            }
        }
        public float SecondsSinceLaunch { get { return Time.time - _lastLaunchTime; } }


        /// <summary>
        /// ANIMATE = moving under its own power: for animateSeconds after a launch or roll input, or while
        /// faster than animateSpeed. This is the only thing a goblin can perceive as alive; a still weapon
        /// is just an object, which is why playing dead works.
        /// </summary>
        public bool IsAnimate
        {
            get
            {
                if (_broken || tuning == null || body == null) return false;
                if (_remoteDriven) return _remoteAnimate;
                if (Time.time - _lastAnimateTime < tuning.animateSeconds) return true;
                return body.linearVelocity.magnitude > tuning.animateSpeed;
            }
        }

        /// <summary>Seconds since the weapon last looked animate (big while it has been still).</summary>
        public float SecondsSinceAnimate
        {
            get { return IsAnimate ? 0f : Time.time - _lastAnimateTime; }
        }

        /// <summary>The motor calls this on every launch and while roll input is held.</summary>
        public void MarkAnimated()
        {
            _lastAnimateTime = Time.time;
        }

        /// <summary>Freeze the weapon in its home slot pose (the rack). Called on spawn and after a respawn.</summary>
public void HoldAtHome()
        {
            if (!restAtHome || homeSlot == null || _broken || body == null) return;
            Unstick();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            transform.SetPositionAndRotation(homeSlot.Position, homeSlot.Rotation);
            body.position = homeSlot.Position;
            body.rotation = homeSlot.Rotation;
            _held = true;
        }

        /// <summary>Let physics have the weapon again (it was possessed, or something shoved it).</summary>
        public void ReleaseHold()
        {
            if (!_held) return;
            _held = false;
            if (body != null && !_broken)
            {
                body.isKinematic = false;
                body.WakeUp();
            }
        }


        public bool IsGrounded
        {
            get { return !_broken && (_stuck || Time.fixedTime - _lastGroundTime <= tuning.groundedGrace); }
        }

        public bool IsTouchingWall
        {
            get { return !_broken && Time.fixedTime - _lastWallTime <= tuning.wallGrace; }
        }

        public float DamageMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < modifiers.Count; i++)
                    if (modifiers[i] != null) m *= modifiers[i].damageMultiplier;
                return m;
            }
        }

        void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        void OnValidate()
        {
            if (body == null) body = GetComponent<Rigidbody>();
        }

        void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            MeasureLength();
            ApplyDef();
            _hp = MaxHp;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _lastSafePosition = _spawnPosition;            HoldAtHome();

        }

        /// <summary>Copies the WeaponDef numbers onto the Rigidbody and the child colliders.</summary>
        [ContextMenu("Apply Def")]
        public void ApplyDef()
        {
            if (def == null || body == null) return;
            body.mass = def.mass;
            body.linearDamping = def.linearDrag;
            body.angularDamping = def.angularDrag;
            body.useGravity = true;
            // A kinematic mover (a falling platform, a closing gate) that squeezes the body against a floor may not fling it
            // out at any speed PhysX likes: capped, the body is overlapped for a moment and pushed clear, not shot through the floor.
            body.maxDepenetrationVelocity = 12f;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = maxAngularVelocity;
            if (def.physicsMaterial != null)
            {
                Collider[] cols = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++) cols[i].sharedMaterial = def.physicsMaterial;
            }
        }

        // ---------------------------------------------------------------- contacts
        void OnCollisionEnter(Collision collision)
        {
            NoteImpact(collision);
            NoteBat(collision);
            if (!TryStick(collision)) ReadContacts(collision);
        }

        /// <summary>Records how hard the last collision was, so a sleeping goblin has something to listen for.</summary>
        void NoteImpact(Collision collision)
        {
            if (_broken) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 1f) return;
            if (Time.time - _lastImpactTime > 0.2f || speed > _lastImpactSpeed)
            {
                _lastImpactTime = Time.time;
                _lastImpactSpeed = speed;
            }
        }
        void OnCollisionStay(Collision collision) { ReadContacts(collision); }

        void ReadContacts(Collision collision)
        {
            if (_broken) return;
            int bit = 1 << collision.collider.gameObject.layer;
            bool groundLayer = (tuning.groundMask.value & bit) != 0;
            bool wallLayer = (tuning.wallMask.value & bit) != 0;
            if (!groundLayer && !wallLayer) return;

            float now = Time.fixedTime;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                Vector3 n = collision.GetContact(i).normal;
                if (groundLayer && n.y > tuning.groundedNormalY)
                {
                    _lastGroundTime = now;
                    _groundNormal = n;
                    _stepGrounded = true;
                    // Safe = slow, on static World geometry (never on a moving platform or another weapon).
                    if (wallLayer && collision.rigidbody == null
                        && body.linearVelocity.sqrMagnitude < tuning.safeSpeed * tuning.safeSpeed)
                        _lastSafePosition = body.position;
                }
                else if (wallLayer && Mathf.Abs(n.y) < tuning.wallNormalY)
                {
                    _lastWallTime = now;
                    _wallNormal = n;
                    _stepWall = true;
                }
            }
        }

void FixedUpdate()
        {
            if (_broken) return;
            if (NetFixedUpdate()) return;
            TickBrace();
            // A body lying still may be wedged (a blade in the seam between a floor and a wall): now and then push it out.
            if (Time.fixedTime >= _nextOverlapCheck && !_held && !_carried && !_stuck && !body.isKinematic && body.linearVelocity.sqrMagnitude < 0.04f)
            {
                _nextOverlapCheck = Time.fixedTime + 0.5f;
                ResolveOverlaps();
            }

            if (_stuck)
            {
                // Stuck in wood counts as standing: a launch is allowed and the wall-jump counter resets.
                _lastGroundTime = Time.fixedTime;
                _groundNormal = Vector3.up;
                _stepGrounded = false;
                _stepWall = false;
                if (_stuckTo == null || !_stuckTo.gameObject.activeInHierarchy) Unstick();
                else if (_stuckFollows)
                {
                    body.MovePosition(_stuckTo.TransformPoint(_stuckLocalPos));
                    body.MoveRotation(_stuckTo.rotation * _stuckLocalRot);
                }
                return;
            }

            if (_climbing && TickClimb()) return;

            // A sleeping body gets no OnCollisionStay, so a settled weapon keeps the contact state it fell asleep with.
            if (body.IsSleeping())
            {
                float now = Time.fixedTime;
                if (_restGrounded) _lastGroundTime = now;
                if (_restWall) _lastWallTime = now;
            }
            else
            {
                _restGrounded = _stepGrounded;
                _restWall = _stepWall;
            }
            _stepGrounded = false;
            _stepWall = false;

            AlignBlade();
            if (body.position.y < tuning.killY) Recover();
        }

        void Update()
        {
            if (_broken && !_netDriven && Time.time >= _respawnAt) Respawn();
        }

        // ---------------------------------------------------------------- movement helpers

        /// <summary>Hard move: zero velocity, forget contacts.</summary>
public void Teleport(Vector3 position, Quaternion rotation)
        {
            Unstick();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            ForgetContacts();
            body.WakeUp();
        }

        /// <summary>
        /// Hard move that KEEPS motion (a MagicDoor): pose and both velocities are set as given, contacts are
        /// forgotten. Setting the Transform as well resets Rigidbody interpolation, so there is no streak.
        /// </summary>
        public void Warp(Vector3 position, Quaternion rotation, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            Unstick();
            ReleaseHold();
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
            ForgetContacts();
            body.WakeUp();
        }

        void ForgetContacts()
        {
            _climbing = false;
            _lastGroundTime = -999f;
            _lastWallTime = -999f;
            _stepGrounded = _stepWall = _restGrounded = _restWall = false;
        }

        /// <summary>A shove from outside (a goblin's hit). Frees a racked or stuck weapon first.</summary>
        public void Knockback(Vector3 velocityChange)
        {
            if (_broken || body == null) return;
            ReleaseHold();
            Unstick();
            // A braced body takes the hit: the stance lets go for a moment so the next step does not zero the shove.
            _braceHoldOff = Time.fixedTime + 1f;
            if (_braced) { _braced = false; body.constraints = RigidbodyConstraints.None; }
            body.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>A bat on a BRACED body: the stance lets go and the body leaves at EXACTLY this velocity, whatever the
        /// contact's own impulse did, so the flight is the one the batter aimed. It counts as a launch: a magnet lets it go
        /// for its release window and the blade aligns to the flight.</summary>
        public void Fling(Vector3 velocity)
        {
            if (_broken || body == null) return;
            ReleaseHold();
            Unstick();
            _braceHoldOff = Time.fixedTime + 1f;
            if (_braced) { _braced = false; body.constraints = RigidbodyConstraints.None; }
            body.WakeUp();
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            MarkLaunched();
        }

        /// <summary>For a moment this body passes through another weapon's solid colliders: the batter, stopped dead where it
        /// struck, must not stand in the flyer's way (and the flyer must not kick the batter over the edge).</summary>
        public void IgnoreCollisionsWith(WeaponBody other, float seconds)
        {
            if (other == null || other == this || _colliders == null || other._colliders == null) return;
            SetCollisionsIgnored(other._colliders, true);
            StartCoroutine(RestoreCollisions(other._colliders, seconds));
        }

        void SetCollisionsIgnored(Collider[] theirs, bool ignored)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider a = _colliders[i];
                if (a == null || a.isTrigger) continue;
                for (int j = 0; j < theirs.Length; j++)
                {
                    Collider b = theirs[j];
                    if (b == null || b.isTrigger) continue;
                    Physics.IgnoreCollision(a, b, ignored);
                }
            }
        }

        System.Collections.IEnumerator RestoreCollisions(Collider[] theirs, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            SetCollisionsIgnored(theirs, false);
        }

        /// <summary>The possessing player holds the brace key (Left Shift while possessing): stand up and take a bat.</summary>
        public void SetBrace(bool on)
        {
            _braceWanted = on;
        }

        /// <summary>
        /// BRACE (docs/level-design/rooms-v3.md, Bat Room). While the possessing player holds Shift and the weapon is on
        /// the ground it stands up (a blade on its point, anything else on its handle), stops rolling and stays put: a
        /// tall, still target. A heavier friend launching into it bats it along THEIR aim (WorldAuthority.RequestBat);
        /// the shove arrives as a Knockback, which drops the stance for a second so the body flies.
        /// </summary>
        void TickBrace()
        {
            bool want = ((_braceWanted && IsLocallyPossessed) || (autoBrace && _possessor == null && !_remoteDriven)) && !_held && !_carried && !_stuck && IsGrounded
                && body != null && !body.isKinematic && Time.fixedTime >= _braceHoldOff;
            if (want != _braced)
            {
                _braced = want;
                // Never frozen: the solver must stay free to resolve contacts while the body stands and while it stands there.
                body.constraints = RigidbodyConstraints.None;
                if (want) _braceStandStart = -1f;
                else body.angularVelocity = Vector3.zero;
            }
            if (!_braced) return;
            // Stand: local +Z (the point / the head) down for a blade, up for the rest; keep the facing.
            Vector3 flat = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            // A blade that already points up (hanging in its rack, leaning on a wall) stands on its pommel instead: it never
            // has to flip end over end through the rack's rails or the floor to get up, so Shift works from the rack too.
            bool pointUp = def != null && def.bladed && Vector3.Dot(transform.forward, Vector3.up) > 0.3f;
            Vector3 along = def != null && def.bladed && !pointUp ? Vector3.down : Vector3.up;
            Quaternion target = Quaternion.LookRotation(along, flat.normalized);
            // Up, then a wobble: once nearly upright it tips past vertical and settles back, a thing with weight finding its feet.
            float off = Quaternion.Angle(body.rotation, target);
            if (_braceStandStart < 0f && off < 6f) _braceStandStart = Time.fixedTime;
            if (_braceStandStart >= 0f)
            {
                float s = Time.fixedTime - _braceStandStart;
                float tilt = BraceWobbleDegrees * Mathf.Exp(-2.5f * s) * Mathf.Sin(8f * s);
                Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
                target = Quaternion.AngleAxis(tilt, side) * target;
            }
            // PHYSICALLY: an angular velocity toward the stance, capped, never a kinematic MoveRotation. The floor and the
            // walls push back as the body rights itself, so a blade is never rotated INTO the seam between them and pinned
            // there. The horizontal velocity is held at zero (a braced body does not slide); the vertical is left to gravity
            // and to the contacts, so a tip that has sunk in is pushed back out.
            Quaternion delta = target * Quaternion.Inverse(body.rotation);
            float angle; Vector3 axis;
            delta.ToAngleAxis(out angle, out axis);
            if (angle > 180f) angle -= 360f;
            Vector3 spin = Vector3.zero;
            if (Mathf.Abs(angle) > 0.01f && axis.sqrMagnitude > 1e-6f && !float.IsNaN(axis.x))
                spin = Vector3.ClampMagnitude(axis.normalized * (angle * Mathf.Deg2Rad * BraceTurnGain), BraceTurnDegreesPerSecond * Mathf.Deg2Rad);
            body.angularVelocity = spin;
            // Pivot about the end that stands on the floor: the centre RISES as the body turns, the way a thing is righted by
            // its foot. Turning about the centre drove that end into the floor and the solver cancelled the turn to keep it
            // out: a flat Sword felt too heavy to get up at all (round 28).
            float endZ = along == Vector3.down ? _zMax : _zMin;
            Vector3 pivot = transform.TransformPoint(new Vector3(0f, 0f, endZ));
            Vector3 rise = Vector3.Cross(spin, body.worldCenterOfMass - pivot);
            Vector3 v = body.linearVelocity;
            body.linearVelocity = new Vector3(0f, Mathf.Max(v.y, rise.y), 0f);
        }

        /// <summary>How far the solid colliders reach along local Z, both ways: the ends a braced body stands on.</summary>
        void MeasureLength()
        {
            _zMin = 0f; _zMax = 0f;
            if (_colliders == null) return;
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider c = _colliders[i];
                if (c == null || c.isTrigger) continue;
                BoxCollider box = c as BoxCollider;
                if (box != null)
                {
                    Vector3 h = box.size * 0.5f;
                    int k = 0;
                    for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                        corners[k++] = box.transform.TransformPoint(box.center + new Vector3(sx * h.x, sy * h.y, sz * h.z));
                }
                else
                {
                    Bounds b = c.bounds;
                    int k = 0;
                    for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                        corners[k++] = b.center + new Vector3(sx * b.extents.x, sy * b.extents.y, sz * b.extents.z);
                }
                for (int k = 0; k < 8; k++)
                {
                    float z = transform.InverseTransformPoint(corners[k]).z;
                    if (z < _zMin) _zMin = z;
                    if (z > _zMax) _zMax = z;
                }
            }
        }

        /// <summary>Pushes the body out of any geometry it overlaps (a blade wedged in the seam between a floor and a wall,
        /// a corner it was rotated into, a door that closed on it). Called before every launch, so a wedged weapon's launch
        /// is never absorbed, and now and then while a body lies still. Static and kinematic colliders on the ground and
        /// wall layers count; other weapons do not (the solver settles those). Returns true when it moved the body.</summary>
        public bool ResolveOverlaps()
        {
            if (_broken || body == null || body.isKinematic || _colliders == null || tuning == null) return false;
            Bounds b = WorldBounds;
            b.Expand(0.1f);
            int mask = tuning.groundMask.value | tuning.wallMask.value;
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, _overlapBuf, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                Collider other = _overlapBuf[i];
                if (other == null || other.isTrigger) continue;
                Rigidbody orb = other.attachedRigidbody;
                if (orb == body) continue;
                if (orb != null && !orb.isKinematic) continue;   // a dynamic body (another weapon): the solver's business
                for (int j = 0; j < _colliders.Length; j++)
                {
                    Collider mine = _colliders[j];
                    if (mine == null || mine.isTrigger || !mine.enabled) continue;
                    Vector3 dir; float dist;
                    if (Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation, other, other.transform.position, other.transform.rotation, out dir, out dist) && dist > 0.04f)   // a resting contact sits a centimetre in; only a real overlap is pushed
                    {
                        push += dir * dist;
                        hits++;
                    }
                }
            }
            if (hits == 0) return false;
            push = Vector3.ClampMagnitude(push, 0.6f);
            body.position += push + push.normalized * 0.01f;
            body.WakeUp();
            return true;
        }

        /// <summary>The motor calls this on every launch.</summary>
        public void MarkLaunched()
        {
            _climbing = false;
            _lastLaunchTime = Time.time;
            MarkAnimated();
        }

        /// <summary>Something other than the floor is holding the weapon up (a MagnetZone): it counts as grounded this step.</summary>
        public void MarkSupported(Vector3 supportNormal)
        {
            if (_broken) return;
            _lastGroundTime = Time.fixedTime;
            _groundNormal = supportNormal;
        }

        /// <summary>True while a goblin porter is carrying the weapon.</summary>
        public bool IsCarried { get { return _carried; } }

        /// <summary>Time.time of the last collision hard enough to be worth hearing.</summary>
        public float LastImpactTime { get { return _lastImpactTime; } }

        /// <summary>Relative speed of that collision in m/s. A sleeping goblin listens for these.</summary>
        public float LastImpactSpeed { get { return _lastImpactSpeed; } }

        /// <summary>Authority only: a porter picked the weapon up, or set it down. Gravity is off while carried.</summary>
        public void SetCarried(bool on)
        {
            if (_carried == on) return;
            _carried = on;
            if (body == null || _broken) return;
            if (on)
            {
                ReleaseHold();
                Unstick();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = !on;
            body.WakeUp();
        }

        /// <summary>
        /// The porter writes the carry pose every physics step. Velocity is never written back after the
        /// porter has seen the weapon go ANIMATE, so a launch out of the porter's hands still takes.
        /// Being carried counts as standing, so a launch is allowed at all.
        /// </summary>
        public void CarryTo(Vector3 position, Quaternion rotation)
        {
            if (body == null || _broken || !_carried || _remoteDriven) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            MarkSupported(Vector3.up);
        }


        // ---------------------------------------------------------------- stick in wood

        bool TryStick(Collision collision)
        {
            if (_broken || _stuck || _held || def == null || !def.bladed || body.isKinematic) return false;
            if (Time.time < _stickRearmAt) return false;
            Vector3 relative = collision.relativeVelocity;
            if (relative.magnitude < tuning.stickMinSpeed) return false;
            WoodSurface wood = collision.collider.GetComponentInParent<WoodSurface>();
            if (wood == null || !wood.Stickable) return false;

            Vector3 tip = TipDirection;
            Vector3 com = body.worldCenterOfMass;
            float cosMax = Mathf.Cos(tuning.stickMaxAngleDeg * Mathf.Deg2Rad);
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                ContactPoint c = collision.GetContact(i);
                if (Vector3.Dot(tip, -c.normal) < cosMax) continue;                 // not point-first
                if (Vector3.Dot(c.point - com, tip) <= 0f) continue;                // the grip end hit, not the point
                if (Mathf.Abs(Vector3.Dot(relative, c.normal)) < tuning.stickMinSpeed * 0.5f) continue; // a glancing slide
                StickTo(wood, collision.collider, c.normal);
                return true;
            }
            return false;
        }

        /// <summary>The motor launched a climb hop: straight up the wooden wall the blade was stuck in. Until the body sticks
        /// again or lands, every physics step past the top of the hop probes sideways for that timber and stabs back in.</summary>
        public void BeginClimb(Vector3 wallNormal)
        {
            Vector3 flat = new Vector3(wallNormal.x, 0f, wallNormal.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            _climbing = true;
            _climbNormal = flat.normalized;
        }

        /// <summary>A climb hop in flight (round 29): once the rise is spent, look along the wall's inward direction for the timber
        /// and stab back in, point into the face, grip out. Keeps probing while it falls, so a hop that overshoots the top of a
        /// post sticks at the top on the way down. Ends when the body sticks, lands on anything, or finds stone instead of wood.
        /// Returns true when it stuck this step.</summary>
        bool TickClimb()
        {
            if (_broken || _held || _carried || body == null || body.isKinematic || def == null || !def.bladed) { _climbing = false; return false; }
            if (Time.time - _lastLaunchTime < 0.1f) return false;
            if (Time.fixedTime - _lastGroundTime <= tuning.groundedGrace) { _climbing = false; return false; }   // landed: the hop is over
            if (body.linearVelocity.y > tuning.climbRestickBelowSpeed) return false;                             // still rising
            RaycastHit hit;
            int mask = tuning.groundMask.value | tuning.wallMask.value;
            if (!Physics.Raycast(body.worldCenterOfMass, -_climbNormal, out hit, tuning.climbReach, mask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.normal.y) >= tuning.wallNormalY) return false;                                      // not a face: keep looking
            WoodSurface wood = hit.collider.GetComponentInParent<WoodSurface>();
            if (wood == null || !wood.Stickable) { _climbing = false; return false; }                              // stone: a plain fall from here
            // The stab: the point into the face at the hit, the grip straight out along its normal; StickTo sinks the tip.
            Vector3 n = hit.normal;
            Quaternion rot = Quaternion.LookRotation(-n, Vector3.up);
            Vector3 p = hit.point + n * _zMax;
            body.rotation = rot;
            body.position = p;
            transform.SetPositionAndRotation(p, rot);
            StickTo(wood, hit.collider, n);
            return true;
        }

        /// <summary>Holds the weapon in the wood it just hit: kinematic at the contact pose, the tip sunk stickSinkDepth in.</summary>
        void StickTo(WoodSurface wood, Collider on, Vector3 normal)
        {
            Transform host = on.attachedRigidbody != null ? on.attachedRigidbody.transform : on.transform;
            _climbing = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            Vector3 p = body.position + TipDirection * tuning.stickSinkDepth;
            body.position = p;
            transform.position = p;

            _stuck = true;
            _stuckTo = host;
            _stuckFollows = on.attachedRigidbody != null;
            _stuckLocalPos = host.InverseTransformPoint(p);
            _stuckLocalRot = Quaternion.Inverse(host.rotation) * body.rotation;
            _stuckNormal = normal;
            _lastGroundTime = Time.fixedTime;
            if (Stuck != null) Stuck(this, wood);
        }

        /// <summary>Frees a weapon stuck in wood: a launch, a knockback, Release, a break or a teleport all call this.</summary>
        public void Unstick()
        {
            if (!_stuck) return;
            _stuck = false;
            _stuckTo = null;
            _stickRearmAt = Time.time + tuning.stickRearmSeconds;
            if (body != null && !_broken)
            {
                // Back out by the sink depth first, so the body is not overlapping the wood when it goes dynamic.
                Vector3 p = body.position - TipDirection * tuning.stickSinkDepth;
                body.position = p;
                transform.position = p;
                body.isKinematic = false;
                body.WakeUp();
            }
            if (Unstuck != null) Unstuck(this);
        }

        /// <summary>An airborne bladed weapon turns its point into its velocity, like an arrow. Linear motion is untouched.</summary>
        void AlignBlade()
        {
            if (def == null || !def.bladed || tuning.bladeAlignRate <= 0f || body.isKinematic) return;
            float now = Time.fixedTime;
            if (now - _lastGroundTime <= tuning.groundedGrace || now - _lastWallTime <= tuning.wallGrace) return;
            if (Time.time - _lastLaunchTime < tuning.bladeAlignDelay) return;
            Vector3 v = body.linearVelocity;
            float speed = v.magnitude;
            if (speed < tuning.bladeAlignMinSpeed) return;

            Vector3 dir = v / speed;
            Vector3 tip = TipDirection;
            Vector3 axis = Vector3.Cross(tip, dir);
            if (axis.sqrMagnitude < 1e-8f)
            {
                if (Vector3.Dot(tip, dir) > 0f) return;                 // already point-first
                axis = Vector3.Cross(tip, Vector3.up);                   // flying backwards: turn about a side axis
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.right;
            }
            float angle = Vector3.Angle(tip, dir) * Mathf.Deg2Rad;
            Vector3 roll = Vector3.Dot(body.angularVelocity, tip) * tip;    // keep the spin about the blade
            body.angularVelocity = axis.normalized * (angle * tuning.bladeAlignRate) + roll;
        }

        /// <summary>Fell out of the world: back to the last safe position. No damage.</summary>
        public void Recover()
        {
            Teleport(_lastSafePosition + Vector3.up * tuning.recoverLift, body.rotation);
            if (Recovered != null) Recovered(this);
        }

        // ---------------------------------------------------------------- possession

        public bool SetPossessor(IWeaponPossessor possessor)
        {
            if (possessor == null || !IsFree) return false;
            _possessor = possessor;
            ReleaseHold();
            body.WakeUp();
            return true;
        }

        public void ClearPossessor(IWeaponPossessor possessor)
        {
            if (_possessor == possessor) _possessor = null;
        }

        /// <summary>Damage was dealt or taken: tell whoever is driving.</summary>
        public void NotifyCombat()
        {
            if (_possessor != null) _possessor.NotifyCombat();
        }

        // ---------------------------------------------------------------- HP and modifiers

        public void ApplyDamage(float amount)
        {
            if (_broken || amount <= 0f) return;
            // In a session hit points are a host fact: route through the authority (WEAPON_DAMAGED) instead of changing them here.
            if (_netDriven && _net != null) { _net.RequestDamageWeapon(this, amount, null); return; }
            _hp = Mathf.Max(0f, _hp - amount);
            NotifyCombat();
            if (HpChanged != null) HpChanged(this);
            if (_hp <= 0f) Break();
        }

        public void RestoreFullHp()
        {
            if (_broken) return;
            _hp = MaxHp;
            if (HpChanged != null) HpChanged(this);
        }

        public void AddModifier(ModifierDef modifier)
        {
            if (_broken || modifier == null) return;
            modifiers.Add(modifier);
            if (ModifiersChanged != null) ModifiersChanged(this);
        }

        public void ClearModifiers()
        {
            if (modifiers.Count == 0) return;
            modifiers.Clear();
            if (ModifiersChanged != null) ModifiersChanged(this);
        }

        // ---------------------------------------------------------------- break and respawn

        /// <summary>The weapon vanishes, loses its modifiers and returns on its home slot after respawnDelay.</summary>
        public void Break()
        {
            if (_broken) return;
            SetCarried(false);
            ReleaseHold();
            Unstick();
            _broken = true;
            _possessor = null;
            ClearModifiers();
            SetPresent(false);
            _respawnAt = Time.time + respawnDelay;
            if (Broken != null) Broken(this);
        }

        void Respawn()
        {
            _broken = false;
            SetPresent(true);
            Vector3 p = homeSlot != null ? homeSlot.Position : _spawnPosition;
            Quaternion r = homeSlot != null ? homeSlot.Rotation : _spawnRotation;
            Teleport(p, r);
            _lastSafePosition = p;
            _hp = MaxHp;
            HoldAtHome();
            if (HpChanged != null) HpChanged(this);
            if (Respawned != null) Respawned(this);
        }

        void SetPresent(bool present)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = present;
            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = present;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = present;
            body.constraints = present ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeAll;
        }
    }
}
