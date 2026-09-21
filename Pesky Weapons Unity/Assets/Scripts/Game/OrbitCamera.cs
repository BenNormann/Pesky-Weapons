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

        InputAction _look;
        InputAction _pause;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        float _yaw;
        float _pitch;
        bool _inputEnabled = true;

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
            _yaw = transform.eulerAngles.y;
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
                _pivotVelocity = Vector3.zero;
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

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float d = distance;
            RaycastHit hit;
            if (Physics.SphereCast(_pivot, collisionRadius, back, out hit, d, collisionMask, QueryTriggerInteraction.Ignore))
                d = hit.distance;
            // A spherecast skips whatever it starts inside (pivot hugging a wall), so a plain ray backs it up.
            if (Physics.Raycast(_pivot, back, out hit, d, collisionMask, QueryTriggerInteraction.Ignore))
                d = hit.distance - 0.1f;
            d = Mathf.Max(minDistance, d);
            transform.SetPositionAndRotation(_pivot + back * d, rotation);
        }
    }
}
