using UnityEngine;
using UnityEngine.InputSystem;
using Pesky.Data;

namespace Pesky.Game
{
    /// <summary>
    /// Third-person orbit camera. Yaw and pitch come from the Look action only; the pivot follows the
    /// target's POSITION (SmoothDamp), so the weapon's rotation can never affect the view. A probe sphere
    /// big enough to hold the near clip plane is walked from the target to the camera through free space
    /// only, so the view can never be outside World. Click locks the pointer, Pause (Esc) frees it. No zoom.
    /// Pitch convention: positive looks down at the target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrbitCamera : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [SerializeField] string lookAction = "Look";
        [SerializeField] string pauseAction = "Pause";
        [Tooltip("Degrees per unit of the Look action.")]
        [SerializeField] float lookSensitivity = 2f;
        [Tooltip("The mouse-look spike filter's tunables (Assets/Data/LookTuning.asset).")]
        [SerializeField] LookTuning lookTuning;

        [Header("Orbit")]
        [Tooltip("Set at runtime by the PlayerSoul; empty in the authored scene.")]
        [OptionalRef][SerializeField] Transform target;
        [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] float distance = 5f;
        [SerializeField] float minPitch = -35f;
        [SerializeField] float maxPitch = 75f;
        [SerializeField] float restingPitch = 20f;
        [SerializeField] float followSmoothTime = 0.08f;

        [Header("Collision")]
        [Tooltip("World only. Souls' barriers, weapons and enemies never block the camera.")]
        [SerializeField] LayerMask collisionMask = 256;
        [Tooltip("Radius of the probe that carries the pivot and the camera. Widened by itself if the near clip plane needs more.")]
        [SerializeField] float collisionRadius = 0.25f;
        [Tooltip("Gap kept between the probe and a surface.")]
        [SerializeField] float skin = 0.04f;
        [Tooltip("In a gap too tight for it the probe shrinks down to this, and the near clip plane shrinks with it.")]
        [SerializeField] float minProbeRadius = 0.05f;
        [Tooltip("Pivot held against a floor or roof: the orbit flattens to keep this distance rather than land on the target. 0 = off.")]
        [SerializeField] float comfortDistance = 1f;
        [Tooltip("Most the orbit may flatten away from the view pitch, degrees. Under half the vertical FOV keeps the target on screen.")]
        [SerializeField] float maxPitchEase = 25f;
        [Tooltip("Seconds to ease back OUT once geometry stops blocking. Pulling IN is instant.")]
        [SerializeField] float distanceEaseTime = 0.12f;

        const float NearMargin = 0.01f;

        InputAction _look;
        InputAction _pause;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        float _yaw;
        float _pitch;
        bool _inputEnabled = true;
        readonly LookFilter _filter = new LookFilter();
        float _distance;
        float _distanceVelocity;
        float _lift = 1f;
        float _liftVelocity;
        float _ease;
        float _easeVelocity;
        float _baseNear = 0.1f;
        Camera _camera;
        SphereCollider _probe;
        readonly Collider[] _overlaps = new Collider[16];

        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }
        public float RestingPitch { get { return restingPitch; } }
        public Transform Target { get { return target; } }
        /// <summary>False = ignore the mouse (pause menus, automated probes). SetLook still works.</summary>
        public bool InputEnabled
        {
            get { return _inputEnabled; }
            set
            {
                // Re-enabled after an overlay: the next delta is whatever piled up meanwhile, not a movement.
                if (value && !_inputEnabled) _filter.SkipNext();
                _inputEnabled = value;
            }
        }
        /// <summary>The mouse-look spike filter, for the debug overlay's counters.</summary>
        public LookFilter Filter { get { return _filter; } }
        /// <summary>The real lock, or the ?nolock=1 bypass standing in for it on a page that will not grant one.</summary>
        public bool PointerLocked { get { return DebugGate.PointerLocked; } }
        /// <summary>Full 3D view direction (yaw and pitch).</summary>
        public Vector3 Forward { get { return Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward; } }

        void Awake()
        {
            _pitch = Mathf.Clamp(restingPitch, minPitch, maxPitch);
            _yaw = transform.eulerAngles.y;
            _distance = distance;
            _camera = GetComponent<Camera>();
            if (_camera != null) _baseNear = _camera.nearClipPlane;

            if (target != null) _pivot = target.position + pivotOffset;
        }

        void OnEnable()
        {
            if (controls == null) return;
            InputActionMap map = controls.FindActionMap(actionMap, true);
            _look = map.FindAction(lookAction, true);
            _pause = map.FindAction(pauseAction, true);
            _look.Enable();
            _pause.Enable();
        }

        void OnDisable()
        {
            FreePointer();
        }

        void OnDestroy()
        {
            if (_probe != null) Destroy(_probe.gameObject);
        }

        public void SetTarget(Transform newTarget, bool snap)
        {
            target = newTarget;
            if (snap && target != null)
            {
                _pivot = target.position + pivotOffset;
                _pivotVelocity = Vector3.zero;
                _distance = distance;
                _distanceVelocity = 0f;
                _lift = 1f;
                _liftVelocity = 0f;
                _ease = 0f;
                _easeVelocity = 0f;
            }
        }

        public void SetLook(float yawDeg, float pitchDeg)
        {
            _yaw = Mathf.Repeat(yawDeg, 360f);
            _pitch = Mathf.Clamp(pitchDeg, minPitch, maxPitch);
        }

/// <summary>
        /// The target went through a MagicDoor: turn the yaw by the same rotation and carry the smoothed pivot
        /// across, so "forward into A" is "forward out of B" and the view does not sweep through the level.
        /// </summary>
        public void Warp(Quaternion turn, Vector3 from, Vector3 to)
        {
            Vector3 heading = turn * (Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward);
            if (heading.x * heading.x + heading.z * heading.z > 1e-6f)
                _yaw = Mathf.Repeat(Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg, 360f);
            _pivot = to + turn * (_pivot - from);
            _pivotVelocity = turn * _pivotVelocity;
        }


public void LockPointer()
        {
            // Under the ?nolock=1 harness bypass the page will never grant the lock, so it is never asked for either.
            if (DebugGate.PointerLockBypass) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void FreePointer()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

void Update()
        {
            if (_pause != null && _pause.WasPressedThisFrame()) FreePointer();

            Mouse mouse = Mouse.current;
            bool locked = PointerLocked;
            _filter.Track(locked, Application.isFocused || DebugGate.PointerLockBypass);
            if (_inputEnabled && !locked && Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame)
                LockPointer();

            if (_look != null && _inputEnabled && locked)
            {
                // The Look action is a per-frame pixel delta: it is never scaled by deltaTime (a hitch would
                // become a huge turn), and it goes through the spike filter first.
                Vector2 look = _filter.Filter(_look.ReadValue<Vector2>(), lookTuning) * lookSensitivity;
                if (look.sqrMagnitude > 0f) SetLook(_yaw + look.x, _pitch - look.y);
            }
        }

        void LateUpdate()
        {
            Vector3 targetPosition = target != null ? target.position : _pivot;
            if (target != null)
                _pivot = Vector3.SmoothDamp(_pivot, targetPosition + pivotOffset, ref _pivotVelocity, followSmoothTime);

            // One chain of same-sized probes, target -> anchor -> pivot -> camera, each link starting where the
            // last one was proven free. A cast is blind to whatever it STARTS inside, so no link may start in World.
            float r = FullRadius();
            Vector3 anchor = FreeAnchor(targetPosition, ref r);
            FitNearClip(r);
            Vector3 pivot = LiftPivot(anchor, _pivot + (anchor - targetPosition), r);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = Quaternion.Euler(OrbitPitch(pivot, r), _yaw, 0f) * Vector3.back;
            float wanted = Free(pivot, back, distance, r);
            // A flattened orbit looks past the target, so it stays close.
            wanted = Mathf.Min(wanted, Mathf.Lerp(distance, comfortDistance, Mathf.Abs(_ease) / 5f));

            // In at once - geometry must never cut the view - and out again gently, so the camera does not pop.
            _distance = EaseOut(_distance, wanted, ref _distanceVelocity);

            transform.SetPositionAndRotation(Settle(pivot, pivot + back * _distance, r), rotation);
        }

        /// <summary>Down to the limit at once, back up to it over distanceEaseTime.</summary>
        float EaseOut(float current, float limit, ref float velocity)
        {
            if (limit <= current || distanceEaseTime <= 0f)
            {
                velocity = 0f;
                return limit;
            }
            return Mathf.Min(limit, Mathf.SmoothDamp(current, limit, ref velocity, distanceEaseTime));
        }

        /// <summary>
        /// A free point for the probe at the target. A weapon lying on a floor or against a wall has its
        /// origin INSIDE the probe's reach of that surface, so the probe is pushed clear first. In a gap too
        /// tight for it (under a rail) the probe shrinks until it fits; 'r' returns the size that did.
        /// </summary>
        Vector3 FreeAnchor(Vector3 at, ref float r)
        {
            Vector3 best;
            if (Fits(at, r, out best)) return best;

            float lo = Mathf.Min(minProbeRadius, r);
            float hi = r;
            r = lo;
            // Buried in geometry (spawn, a warp): nothing fits, stay on the target.
            if (!Fits(at, lo, out best)) return at;
            for (int i = 0; i < 4; i++)
            {
                float mid = 0.5f * (lo + hi);
                Vector3 p;
                if (Fits(at, mid, out p)) { lo = mid; best = p; }
                else hi = mid;
            }
            r = lo;
            return best;
        }

        bool Fits(Vector3 at, float r, out Vector3 p)
        {
            p = at;
            if (!Depenetrate(ref p, r)) return false;
            // Pushed out on the far side of a thin piece: not the target's space.
            return !Blocked(at, p);
        }

        /// <summary>
        /// The pivot rides pivotOffset above the target, reached by a sweep FROM the anchor so it can never
        /// cross a surface the target has not crossed (a roof, a rail, a ledge). Identical for weapons and
        /// souls. The lift eases like the distance: down at once, up again gently.
        /// </summary>
        Vector3 LiftPivot(Vector3 anchor, Vector3 wanted, float r)
        {
            Vector3 delta = wanted - anchor;
            float len = delta.magnitude;
            if (len < 1e-4f) return anchor;
            Vector3 dir = delta / len;
            _lift = EaseOut(_lift, Free(anchor, dir, len, r) / len, ref _liftVelocity);
            return Settle(anchor, anchor + dir * (len * _lift), r);
        }

        /// <summary>
        /// Where the camera sits on its orbit: the view pitch, unless the pivot is held against a floor or a
        /// roof (under a rail, a soul at the ceiling). There the orbit flattens to keep comfortDistance
        /// instead of landing on the target. The VIEW always keeps the full pitch.
        /// </summary>
        float OrbitPitch(Vector3 pivot, float r)
        {
            float ease = 0f;
            if (comfortDistance > 0f)
            {
                float up = Mathf.Asin(Mathf.Clamp01(Free(pivot, Vector3.up, comfortDistance, r) / comfortDistance)) * Mathf.Rad2Deg;
                float down = Mathf.Asin(Mathf.Clamp01(Free(pivot, Vector3.down, comfortDistance, r) / comfortDistance)) * Mathf.Rad2Deg;
                ease = Mathf.Clamp(Mathf.Clamp(_pitch, -down, up) - _pitch, -maxPitchEase, maxPitchEase);
            }
            _ease = Mathf.SmoothDamp(_ease, ease, ref _easeVelocity, distanceEaseTime);
            return _pitch + _ease;
        }

        /// <summary>How far the probe can travel from a FREE point before it would touch World.</summary>
        float Free(Vector3 from, Vector3 dir, float max, float r)
        {
            float d = max;
            RaycastHit hit;
            if (Physics.SphereCast(from, r, dir, out hit, max, collisionMask, QueryTriggerInteraction.Ignore))
                d = hit.distance - skin;
            // The hard limit. A plain ray has no size to start inside anything, and it also catches a thin
            // edge the sphere slipped around.
            if (d > 0f && Physics.Raycast(from, dir, out hit, d + r, collisionMask, QueryTriggerInteraction.Ignore))
                d = Mathf.Min(d, hit.distance - r - skin);
            return Mathf.Max(0f, d);
        }

        /// <summary>The last word on a probe position: pushed clear of World, and never across a surface from 'from'.</summary>
        Vector3 Settle(Vector3 from, Vector3 p, float r)
        {
            Vector3 q = p;
            // No room for the skin as well: the swept position itself is still free.
            if (!Depenetrate(ref q, r)) q = p;
            return Blocked(from, q) ? from : q;
        }

        /// <summary>World between two points. A plain line: it has no size to start inside anything.</summary>
        bool Blocked(Vector3 a, Vector3 b)
        {
            return (b - a).sqrMagnitude > 1e-8f && Physics.Linecast(a, b, collisionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Pushes the probe (plus skin) out of the World it overlaps, a few rounds for corners.
        /// False = the probe does not fit here.
        /// </summary>
        bool Depenetrate(ref Vector3 p, float r)
        {
            float fat = r + skin;
            SphereCollider probe = Probe(fat);
            for (int round = 0; round < 4; round++)
            {
                int n = Physics.OverlapSphereNonAlloc(p, fat, _overlaps, collisionMask, QueryTriggerInteraction.Ignore);
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    Collider c = _overlaps[i];
                    Vector3 dir;
                    float depth;
                    if (Physics.ComputePenetration(probe, p, Quaternion.identity, c, c.transform.position, c.transform.rotation, out dir, out depth) && depth > 1e-4f)
                    {
                        p += dir * depth;
                        moved = true;
                        continue;
                    }
                    // Only touching, or a pair the solver will not take: the nearest point does the same job.
                    MeshCollider mesh = c as MeshCollider;
                    if (mesh != null && !mesh.convex) continue;
                    Vector3 away = p - c.ClosestPoint(p);
                    float gap = away.magnitude;
                    if (gap > 1e-4f && gap < fat - 1e-4f)
                    {
                        p += away * ((fat - gap) / gap);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            return !Physics.CheckSphere(p, r, collisionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// ComputePenetration wants a live collider for the probe's shape. It is only ever given poses, so the
        /// real one is parked far below the level where it touches nothing.
        /// </summary>
        SphereCollider Probe(float radius)
        {
            if (_probe == null)
            {
                GameObject go = new GameObject("OrbitCamera Probe");
                go.hideFlags = HideFlags.HideInHierarchy;
                go.layer = 2; // Ignore Raycast
                go.transform.position = new Vector3(0f, -10000f, 0f);
                _probe = go.AddComponent<SphereCollider>();
                _probe.isTrigger = true;
            }
            _probe.radius = radius;
            return _probe;
        }

        /// <summary>The probe at full size: never smaller than the near clip plane's far corner needs.</summary>
        float FullRadius()
        {
            float r = Mathf.Max(0.01f, collisionRadius);
            if (_camera == null) return r;
            return Mathf.Max(r, _baseNear * NearReach() + NearMargin);
        }

        /// <summary>A shrunken probe takes the near clip plane in with it, so the plane never pokes out of the probe.</summary>
        void FitNearClip(float r)
        {
            if (_camera == null) return;
            float near = Mathf.Min(_baseNear, Mathf.Max(0.01f, (r - NearMargin) / NearReach()));
            if (!Mathf.Approximately(near, _camera.nearClipPlane)) _camera.nearClipPlane = near;
        }

        /// <summary>Distance from the camera to a corner of the near clip plane, per metre of near clip.</summary>
        float NearReach()
        {
            float h = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * _camera.aspect;
            return Mathf.Sqrt(1f + h * h + w * w);
        }
    }
}
