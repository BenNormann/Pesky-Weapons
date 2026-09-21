using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pesky.Game
{
    /// <summary>
    /// The player (docs/SLICE-1.md section 3). Free: a glowing sphere with no gravity that collides with
    /// World only; holding Jump thrusts along the full 3D camera forward, releasing drags it to a stop.
    /// Possessing: the visual hides and the same input drives the weapon's WeaponMotor instead.
    /// E possesses the best free weapon within range (smallest angle to the view), Q leaves it; leaving
    /// while in combat breaks the weapon. If the weapon breaks the soul pops out where it died.
    /// Every input has a public method (SetThrust, TryPossess, Release, TryLaunch, SetRoll) so the same
    /// calls can come from a network layer or a test.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerSoul : MonoBehaviour, IWeaponPossessor
    {
        [Header("Input")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [SerializeField] string jumpAction = "Jump";
        [SerializeField] string possessAction = "Possess";
        [SerializeField] string releaseAction = "Release";
        [Tooltip("WASD Vector2. Free soul: planar flight relative to the camera yaw. Possessing: Orb roll only.")]
        [SerializeField] string moveAction = "Move";
        [Tooltip("Left Shift: the free soul descends.")]
        [SerializeField] string descendAction = "Descend";

        [Header("Wiring")]
        [SerializeField] Rigidbody body;
        [SerializeField] Collider soulCollider;
        [SerializeField] GameObject visual;
        [SerializeField] TrajectoryPreview preview;

        [Header("Flight")]
        [SerializeField] float thrustAccel = 18f;
        [SerializeField] float maxSpeed = 7f;
        [Tooltip("Linear damping while Jump is released.")]
        [SerializeField] float releaseDrag = 4f;

        [Header("Possession")]
        [SerializeField] float possessRange = 2f;
        [SerializeField] LayerMask weaponMask = 512;
        [Tooltip("Seconds after damage dealt or taken during which leaving the weapon breaks it.")]
        [SerializeField] float combatSeconds = 5f;
        [Tooltip("How far above the weapon the soul appears when it leaves.")]
        [SerializeField] float popOutLift = 0.5f;

        readonly Collider[] _hits = new Collider[32];
        InputAction _jump, _possess, _release, _move, _descend;
        OrbitCamera _camera;
        WorldAuthority _authority;
        WeaponBody _weapon;
        WeaponMotor _motor;
        WeaponBody _candidate;
        Vector2 _moveInput;
        bool _ascend;
        bool _descendHeld;
        bool _inputEnabled = true;
        float _lastCombatTime = -999f;
        int _targeters;

        /// <summary>(weapon) the soul entered a weapon.</summary>
        public event Action<WeaponBody> Possessed;
        /// <summary>(weapon, broke) the soul left a weapon, by choice or because it broke.</summary>
        public event Action<WeaponBody, bool> Released;

        public WeaponBody Weapon { get { return _weapon; } }
        public WeaponMotor Motor { get { return _motor; } }
        public bool IsPossessing { get { return _weapon != null; } }
        /// <summary>The weapon E would take right now (for the HUD prompt). Null while possessing.</summary>
        public WeaponBody Candidate { get { return _candidate; } }
        /// <summary>Any flight input is held (WASD, Space or Left Shift).</summary>
        public bool IsThrusting { get { return FlightDirection().sqrMagnitude > 1e-6f; } }
        public Vector2 MoveInput { get { return _moveInput; } }
        public bool IsAscending { get { return _ascend; } }
        public bool IsDescending { get { return _descendHeld; } }
        /// <summary>Possess (E) held down - the Anvil charges off this.</summary>
        public bool PossessHeld { get { return _inputEnabled && _possess != null && _possess.IsPressed(); } }
        public WorldAuthority Authority { get { return _authority; } }
        public Rigidbody Body { get { return body; } }
        public bool VisualVisible { get { return visual != null && visual.activeSelf; } }
        /// <summary>False = ignore the device input (pause menus, automated probes). The public methods still work.</summary>
        public bool InputEnabled { get { return _inputEnabled; } set { _inputEnabled = value; if (!value) ClearFlight(); } }

        public bool InCombat
        {
            get { return _targeters > 0 || Time.time - _lastCombatTime < combatSeconds; }
        }

        public float CombatSecondsLeft
        {
            get { return Mathf.Max(0f, combatSeconds - (Time.time - _lastCombatTime)); }
        }

        void Reset()
        {
            body = GetComponent<Rigidbody>();
            soulCollider = GetComponent<Collider>();
        }

        void Awake()
        {
            body.useGravity = false;
            body.freezeRotation = true;
        }

        void OnEnable()
        {
            if (controls == null) return;
            InputActionMap map = controls.FindActionMap(actionMap, true);
            _jump = map.FindAction(jumpAction, true);
            _possess = map.FindAction(possessAction, true);
            _release = map.FindAction(releaseAction, true);
            _move = map.FindAction(moveAction, true);
            _descend = map.FindAction(descendAction, true);
            map.Enable();
        }

        void OnDisable()
        {
            if (_weapon != null) Detach(_weapon.Body.worldCenterOfMass, false);
        }

        /// <summary>Called by the PlayerSpawner: the camera this soul aims with.</summary>
        public void Init(OrbitCamera orbitCamera)
        {
            Init(orbitCamera, _authority);
        }

        /// <summary>Called by the PlayerSpawner: the camera this soul aims with and the world authority.</summary>
        public void Init(OrbitCamera orbitCamera, WorldAuthority worldAuthority)
        {
            _camera = orbitCamera;
            Bind(worldAuthority);
            if (_camera != null) _camera.SetTarget(transform, true);
            if (preview != null) preview.SetCamera(_camera != null ? _camera.GetComponent<Camera>() : null);
        }

public void SetAuthority(WorldAuthority worldAuthority)
        {
            Bind(worldAuthority);
        }

        /// <summary>Registers with the authority (so magic doors can see the free soul) and listens for door trips.</summary>
        void Bind(WorldAuthority worldAuthority)
        {
            if (_authority == worldAuthority) return;
            if (_authority != null)
            {
                _authority.MagicDoorTraversed -= OnMagicDoorTraversed;
                _authority.UnregisterSoul(this);
            }
            _authority = worldAuthority;
            if (_authority != null)
            {
                _authority.RegisterSoul(this);
                _authority.MagicDoorTraversed += OnMagicDoorTraversed;
            }
            if (preview != null) preview.SetAuthority(_authority);
        }

        void OnDestroy()
        {
            Bind(null);
        }

        /// <summary>This soul (free, or driving a weapon) went through a magic door: turn the view with it so forward stays forward.</summary>
        void OnMagicDoorTraversed(MagicDoorTraversal trip)
        {
            if (trip.soul != this || _camera == null) return;
            _camera.Warp(trip.turn, trip.fromPosition, trip.toPosition);
            if (_motor != null) _motor.SetAim(_camera.Yaw, _camera.Pitch);
        }

        /// <summary>Authority only: hard move of the free soul that KEEPS its motion (a MagicDoor).</summary>
        public void Warp(Vector3 position, Vector3 velocity)
        {
            if (_weapon != null) return;
            body.position = position;
            transform.position = position;
            body.linearVelocity = velocity;
        }

        // ---------------------------------------------------------------- IWeaponPossessor

        public void NotifyCombat()
        {
            _lastCombatTime = Time.time;
        }

        /// <summary>A goblin started / stopped targeting this player (keeps InCombat true meanwhile).</summary>
        public void AddTargeter() { _targeters++; }
        public void RemoveTargeter() { _targeters = Mathf.Max(0, _targeters - 1); }

        // ---------------------------------------------------------------- the actions input maps to

        /// <summary>Free soul only: Jump held = thrust.</summary>
        public void SetThrust(bool on)
        {
            SetAscend(on);
        }

        /// <summary>Free soul only: Space held = rise.</summary>
        public void SetAscend(bool on)
        {
            _ascend = on && _weapon == null;
        }

        /// <summary>Free soul only: Left Shift held = sink.</summary>
        public void SetDescend(bool on)
        {
            _descendHeld = on && _weapon == null;
        }

        /// <summary>WASD. Free soul: planar flight relative to the camera yaw. Possessing: passed on as roll.</summary>
        public void SetMove(Vector2 input)
        {
            _moveInput = Vector2.ClampMagnitude(input, 1f);
            if (_weapon != null) SetRoll(_moveInput);
        }

        void ClearFlight()
        {
            _moveInput = Vector2.zero;
            _ascend = false;
            _descendHeld = false;
        }

        /// <summary>
        /// The unit flight direction the free soul is asking for: WASD planar and camera-yaw relative,
        /// Space straight up, Left Shift straight down (docs/SLICE-1.md section 3).
        /// </summary>
        public Vector3 FlightDirection()
        {
            if (_weapon != null) return Vector3.zero;
            float yaw = _camera != null ? _camera.Yaw : transform.eulerAngles.y;
            Vector3 dir = LaunchAim.Heading(yaw) * _moveInput.y + LaunchAim.Right(yaw) * _moveInput.x;
            if (_ascend) dir += Vector3.up;
            if (_descendHeld) dir += Vector3.down;
            return Vector3.ClampMagnitude(dir, 1f);
        }

        /// <summary>Possessing only: Jump pressed = launch the weapon.</summary>
        public LaunchResult TryLaunch()
        {
            return _motor != null ? _motor.TryLaunch() : LaunchResult.RefusedBroken;
        }

        /// <summary>Possessing only: WASD roll. The motor ignores it unless the weapon can roll (Orb).</summary>
        public void SetRoll(Vector2 input)
        {
            if (_motor != null) _motor.SetRoll(input);
        }

        /// <summary>E: possess the best free weapon in range.</summary>
        /// <summary>E: possess the best free weapon in range. Routed through the WorldAuthority.</summary>
        public bool TryPossess()
        {
            if (_weapon != null) return false;
            WeaponBody best = FindBestWeapon();
            if (best == null) return false;
            return _authority != null ? _authority.RequestPossess(this, best) : ApplyPossess(best);
        }

        /// <summary>Request to possess a specific weapon; the authority validates it.</summary>
        public bool Possess(WeaponBody target)
        {
            if (target == null) return false;
            return _authority != null ? _authority.RequestPossess(this, target) : ApplyPossess(target);
        }

        /// <summary>Authority only: the validated attach.</summary>
        public bool ApplyPossess(WeaponBody target)
        {
            if (_weapon != null || target == null || !target.SetPossessor(this)) return false;

            _weapon = target;
            _motor = target.GetComponent<WeaponMotor>();
            _candidate = null;
            ClearFlight();
            target.Broken += OnWeaponBroken;

            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            if (soulCollider != null) soulCollider.enabled = false;
            if (visual != null) visual.SetActive(false);
            if (_camera != null)
            {
                _camera.SetTarget(target.transform, false);
                if (_motor != null) _motor.SetAim(_camera.Yaw, _camera.Pitch);
            }
            if (preview != null) preview.SetMotor(_motor);
            if (Possessed != null) Possessed(target);
            return true;
        }

        /// <summary>Q: leave the weapon. In combat the weapon breaks; otherwise it just drops there.</summary>
        /// <summary>Q: leave the weapon. In combat the weapon breaks; otherwise it just drops there.</summary>
        public bool Release()
        {
            if (_weapon == null) return false;
            if (_authority != null) return _authority.RequestRelease(this);

            if (InCombat)
            {
                _weapon.Break(); // OnWeaponBroken pops the soul out.
                return true;
            }
            ApplyRelease();
            return true;
        }

        /// <summary>Authority only: the validated out-of-combat detach.</summary>
        public void ApplyRelease()
        {
            if (_weapon == null) return;
            Detach(_weapon.Body.worldCenterOfMass, false);
        }

        void OnWeaponBroken(WeaponBody w)
        {
            if (w == _weapon) Detach(w.Body.worldCenterOfMass, true);
        }

        void Detach(Vector3 weaponPosition, bool broke)
        {
            WeaponBody w = _weapon;
            w.Broken -= OnWeaponBroken;
            if (_motor != null) _motor.SetRoll(Vector2.zero);
            w.ClearPossessor(this);
            w.Unstick(); // leaving a weapon that is stuck in wood lets it drop
            _weapon = null;
            _motor = null;

            Vector3 p = weaponPosition + Vector3.up * popOutLift;
            transform.position = p;
            body.position = p;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            if (soulCollider != null) soulCollider.enabled = true;
            if (visual != null) visual.SetActive(true);
            if (preview != null) preview.SetMotor(null);
            if (_camera != null) _camera.SetTarget(transform, false);
            if (Released != null) Released(w, broke);
        }

        /// <summary>Hard move of the free soul (spawning, checkpoints).</summary>
        public void Teleport(Vector3 position)
        {
            if (_weapon != null) return;
            body.linearVelocity = Vector3.zero;
            body.position = position;
            transform.position = position;
        }

        // ---------------------------------------------------------------- loops

        void Update()
        {
            if (_inputEnabled && _jump != null) ReadInput();

            if (_weapon != null)
            {
                if (_camera != null && _motor != null) _motor.SetAim(_camera.Yaw, _camera.Pitch);
            }
            else
            {
                _candidate = FindBestWeapon();
            }
        }

        void ReadInput()
        {
            if (_weapon != null)
            {
                if (_jump.WasPressedThisFrame()) TryLaunch();
                SetRoll(_move.ReadValue<Vector2>());
                if (_release.WasPressedThisFrame()) Release();
            }
            else
            {
                SetMove(_move.ReadValue<Vector2>());
                SetAscend(_jump.IsPressed());
                SetDescend(_descend != null && _descend.IsPressed());
                if (_possess.WasPressedThisFrame()) TryPossess();
            }
        }

        void FixedUpdate()
        {
            if (_weapon != null) return;

            Vector3 dir = FlightDirection();
            if (dir.sqrMagnitude > 1e-6f)
            {
                body.linearDamping = 0f;
                Vector3 v = body.linearVelocity + dir * (thrustAccel * Time.fixedDeltaTime);
                body.linearVelocity = Vector3.ClampMagnitude(v, maxSpeed);
            }
            else
            {
                body.linearDamping = releaseDrag;
                if (body.linearVelocity.sqrMagnitude < 0.0025f) body.linearVelocity = Vector3.zero;
            }
        }

        void LateUpdate()
        {
            // While possessing, the hidden soul rides the weapon so it pops out in the right place.
            if (_weapon != null) transform.position = _weapon.Body.worldCenterOfMass;
        }

        Vector3 ViewForward()
        {
            return _camera != null ? _camera.Forward : transform.forward;
        }

        WeaponBody FindBestWeapon()
        {
            Vector3 viewPos = _camera != null ? _camera.transform.position : transform.position;
            Vector3 viewDir = ViewForward();
            int count = Physics.OverlapSphereNonAlloc(transform.position, possessRange, _hits, weaponMask, QueryTriggerInteraction.Ignore);
            WeaponBody best = null;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = _hits[i].attachedRigidbody;
                if (rb == null) continue;
                WeaponBody w = rb.GetComponent<WeaponBody>();
                if (w == null || !w.IsFree) continue;
                float angle = Vector3.Angle(viewDir, rb.worldCenterOfMass - viewPos);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = w;
                }
            }
            return best;
        }
    }
}
