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
        bool _carried;
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
            body.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>The motor calls this on every launch.</summary>
        public void MarkLaunched()
        {
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
                Stick(wood, collision, c.normal);
                return true;
            }
            return false;
        }

        void Stick(WoodSurface wood, Collision collision, Vector3 normal)
        {
            Transform host = collision.rigidbody != null ? collision.rigidbody.transform : collision.collider.transform;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            Vector3 p = body.position + TipDirection * tuning.stickSinkDepth;
            body.position = p;
            transform.position = p;

            _stuck = true;
            _stuckTo = host;
            _stuckFollows = collision.rigidbody != null;
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
