using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A kinematic platform whose position is a PURE FUNCTION of LevelClock.Ms and a period - never a sum of
    /// deltaTime - so a late joiner or a rewind lands on the same spot (SLICE-1 section 6).
    /// Riders (weapons, enemies) are carried explicitly: PhysX friction alone does not hold a Rigidbody on a
    /// kinematic platform, so every physics step the platform's own delta is added to each body standing in
    /// the rider box. Grounded weapons are gently braked so they settle with the platform instead of
    /// sliding off; recently launched weapons keep their full velocity, so jumping never feels glued.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ClockMover : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] LevelClock clock;
        [SerializeField] Rigidbody body;

        [Header("Path (world space, authored)")]
        [SerializeField] Transform pointA;
        [SerializeField] Transform pointB;
        [Tooltip("Seconds for a full there-and-back cycle.")]
        [SerializeField] float periodSeconds = 8f;
        [Tooltip("0..1 offset into the cycle.")]
        [SerializeField] float phase;
        [Tooltip("Ease in and out at the ends instead of a constant speed.")]
        [SerializeField] bool smooth = true;

        [Header("Riders")]
        [Tooltip("Local-space centre of the box that collects riders (just above the surface).")]
        [SerializeField] Vector3 riderBoxCenter = new Vector3(0f, 0.75f, 0f);
        [SerializeField] Vector3 riderBoxSize = new Vector3(3.2f, 1.4f, 3.2f);
        [Tooltip("Weapon | Enemy.")]
        [SerializeField] LayerMask riderMask = (1 << 9) | (1 << 11);
        [Tooltip("Horizontal braking of grounded weapons in m/s^2. 0 = off.")]
        [SerializeField] float riderBrake = 18f;
        [Tooltip("A weapon that launched less than this many seconds ago is not braked.")]
        [SerializeField] float brakeLaunchGrace = 0.75f;

        readonly Collider[] _hits = new Collider[32];
        readonly List<Rigidbody> _riders = new List<Rigidbody>();
        Vector3 _last;

        public int SceneId { get { return id; } }
        public float PeriodSeconds { get { return periodSeconds; } }
        public Transform PointA { get { return pointA; } }
        public Transform PointB { get { return pointB; } }
        public Vector3 RiderBoxSize { get { return riderBoxSize; } }
        public float RiderBrake { get { return riderBrake; } }
        public float BrakeLaunchGrace { get { return brakeLaunchGrace; } }

        void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            _last = Evaluate(clock != null ? clock.Ms : 0L);
            body.position = _last;
            transform.position = _last;
        }

        /// <summary>Pure: where the platform is at a given clock time.</summary>
        public Vector3 Evaluate(long ms)
        {
            if (pointA == null || pointB == null) return transform.position;
            if (periodSeconds < 0.01f) return pointA.position;
            float t = Mathf.Repeat((float)(ms * 0.001) / periodSeconds + phase, 1f);
            float tri = 1f - Mathf.Abs(2f * t - 1f);          // 0 -> 1 -> 0
            float s = smooth ? Mathf.SmoothStep(0f, 1f, tri) : tri;
            return Vector3.Lerp(pointA.position, pointB.position, s);
        }

        void FixedUpdate()
        {
            if (clock == null || pointA == null || pointB == null) return;

            Vector3 target = Evaluate(clock.Ms);
            Vector3 delta = target - _last;
            _last = target;

            CarryRiders(delta);
            body.MovePosition(target);
        }

        void CarryRiders(Vector3 delta)
        {
            // The rider fix is shared with the Lift: see RiderCarry (P_Slick surface plus one explicit delta).
            RiderCarry.Collect(transform, riderBoxCenter, riderBoxSize, riderMask, body, _hits, _riders);
            if (delta.sqrMagnitude > 1e-10f) RiderCarry.Move(_riders, delta);
            if (riderBrake > 0f) BrakeRiders();
        }

        void BrakeRiders()
        {
            float drop = riderBrake * Time.fixedDeltaTime;
            for (int i = 0; i < _riders.Count; i++)
            {
                Rigidbody rb = _riders[i];
                WeaponBody weapon = rb.GetComponent<WeaponBody>();
                if (weapon == null || !weapon.IsGrounded || weapon.SecondsSinceLaunch < brakeLaunchGrace) continue;

                Vector3 velocity = rb.linearVelocity;
                Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
                float speed = horizontal.magnitude;
                if (speed < 0.001f) continue;

                horizontal = speed <= drop ? Vector3.zero : horizontal * (1f - drop / speed);
                rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.47f, 0.59f, 0.9f);
            if (pointA != null && pointB != null) Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(riderBoxCenter, riderBoxSize);
        }
    }
}
