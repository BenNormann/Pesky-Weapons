using UnityEngine;
using UnityEngine.InputSystem;

namespace Pesky.Game
{
    /// <summary>
    /// Third-person orbit camera. Yaw and pitch come from the Look action only; the pivot follows the
    /// target's POSITION (SmoothDamp), so the weapon's rotation can never affect the view. A spherecast
    /// against World pulls the camera in. Click locks the pointer, Pause (Esc) frees it. No zoom.
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
        [SerializeField] LayerMask collisionMask = 256;
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] float minDistance = 0.1f;
        [Tooltip("How far short of a surface the pivot and the camera stop, so neither ever rests inside it.")]
        [SerializeField] float skin = 0.08f;
        [Tooltip("Radius of the cast that lifts the pivot off the target. Narrower than the camera probe so the pivot can still rise in a tight room.")]
        [SerializeField] float pivotProbeRadius = 0.2f;
        [Tooltip("On: the pull-in probe is widened to cover the near clip plane, so the plane cannot poke through a wall.")]
        [SerializeField] bool fitRadiusToNearClip = true;
        [Tooltip("Seconds to ease back OUT once geometry stops blocking. Pulling IN is instant.")]
        [SerializeField] float distanceEaseTime = 0.12f;

        InputAction _look;
        InputAction _pause;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        float _yaw;
        float _pitch;
        bool _inputEnabled = true;
        float _distance;
        float _distanceVelocity;
        Camera _camera;

        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }
        public float RestingPitch { get { return restingPitch; } }
        public Transform Target { get { return target; } }
        /// <summary>False = ignore the mouse (pause menus, automated probes). SetLook still works.</summary>
        public bool InputEnabled { get { return _inputEnabled; } set { _inputEnabled = value; } }
        public bool PointerLocked { get { return Cursor.lockState == CursorLockMode.Locked; } }
        /// <summary>Full 3D view direction (yaw and pitch).</summary>
        public Vector3 Forward { get { return Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward; } }

        void Awake()
        {
            _pitch = Mathf.Clamp(restingPitch, minPitch, maxPitch);
            _yaw = transform.eulerAngles.y;            _distance = distance;

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

        public void SetTarget(Transform newTarget, bool snap)
        {
            target = newTarget;
            if (snap && target != null)
            {
                _pivot = target.position + pivotOffset;
                _pivotVelocity = Vector3.zero;                _distance = distance;
                _distanceVelocity = 0f;

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
            if (_inputEnabled && !PointerLocked && Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame)
                LockPointer();

            if (_look != null && _inputEnabled && PointerLocked)
            {
                Vector2 look = _look.ReadValue<Vector2>() * lookSensitivity;
                SetLook(_yaw + look.x, _pitch - look.y);
            }
        }

        void LateUpdate()
        {
            if (target != null)
                _pivot = Vector3.SmoothDamp(_pivot, target.position + pivotOffset, ref _pivotVelocity, followSmoothTime);

            Vector3 anchor = target != null ? target.position : _pivot;
            Vector3 pivot = ClampPivot(anchor, _pivot);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float wanted = FreeDistance(pivot, back);

            // In at once - geometry must never cut the view - and out again gently, so the camera does not pop.
            if (wanted <= _distance || distanceEaseTime <= 0f) _distance = wanted;
            else _distance = Mathf.SmoothDamp(_distance, wanted, ref _distanceVelocity, distanceEaseTime);
            _distance = Mathf.Clamp(_distance, minDistance, distance);

            transform.SetPositionAndRotation(pivot + back * _distance, rotation);
        }

        /// <summary>
        /// The pivot rides pivotOffset above the target. Against a ceiling (or any wall) that raised point
        /// ends up on the FAR side of the slab, and then everything cast from it starts outside the room -
        /// which is how the camera used to get out of the map. So the pivot is reached by a cast FROM THE
        /// TARGET, and stops a skin short of whatever is in the way: it can never cross a surface the target
        /// has not crossed. Identical for weapons and souls - it only knows the target's position.
        /// </summary>
        Vector3 ClampPivot(Vector3 anchor, Vector3 wanted)
        {
            Vector3 delta = wanted - anchor;
            float len = delta.magnitude;
            if (len < 1e-4f) return anchor;
            Vector3 dir = delta / len;
            float r = Mathf.Max(0.01f, pivotProbeRadius);
            // The target itself is buried in geometry (spawn, a warp): leave the pivot on it.
            if (Physics.CheckSphere(anchor, r, collisionMask, QueryTriggerInteraction.Ignore)) return anchor;
            RaycastHit hit;
            if (Physics.SphereCast(anchor, r, dir, out hit, len, collisionMask, QueryTriggerInteraction.Ignore))
                len = Mathf.Max(0f, hit.distance - skin);
            return anchor + dir * len;
        }

        /// <summary>How far back the camera may sit before it would cut into the world.</summary>
        float FreeDistance(Vector3 pivot, Vector3 back)
        {
            float r = CastRadius();
            // A cast reports nothing for what it ALREADY overlaps. Rather than snap the camera onto the
            // pivot (a pop), narrow the probe until it fits in the space the pivot is actually in.
            int guard = 0;
            while (r > 0.03f && guard++ < 6 && Physics.CheckSphere(pivot, r, collisionMask, QueryTriggerInteraction.Ignore))
                r *= 0.5f;

            float d = distance;
            RaycastHit hit;
            if (Physics.SphereCast(pivot, r, back, out hit, d, collisionMask, QueryTriggerInteraction.Ignore))
                d = hit.distance - skin;
            // A thin edge the sphere slipped around still stops a plain ray.
            if (d > 0f && Physics.Raycast(pivot, back, out hit, d, collisionMask, QueryTriggerInteraction.Ignore))
                d = Mathf.Min(d, hit.distance - skin);
            return Mathf.Clamp(d, minDistance, distance);
        }

        /// <summary>
        /// The pull-in probe's radius. Kept at least as wide as the near clip plane's far corner, so the
        /// plane itself cannot poke through the surface the sphere stopped against.
        /// </summary>
        float CastRadius()
        {
            float r = Mathf.Max(0.01f, collisionRadius);
            if (!fitRadiusToNearClip) return r;
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return r;
            float near = _camera.nearClipPlane;
            float h = near * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * _camera.aspect;
            return Mathf.Max(r, Mathf.Sqrt(near * near + h * h + w * w) + 0.01f);
        }
    }
}
