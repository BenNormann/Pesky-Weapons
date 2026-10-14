using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A kinematic sweeper that turns about its own up axis. Its angle is a PURE FUNCTION of LevelClock.Ms
    /// (like the ClockMover), so every peer and a late joiner see the arm in the same place with no message.
    /// The arm is solid: it shoves any weapon in its way (PhysX pushes rigidbodies out of a kinematic body
    /// driven by MoveRotation). Crossing it is a timing puzzle; standing on its hub is a ride.
    /// Put the script on the Rigidbody that carries the arm colliders (World layer, NOT static).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ClockSpinner : MonoBehaviour
    {
        [SerializeField] LevelClock clock;
        [SerializeField] Rigidbody body;
        [Tooltip("Seconds for one full turn.")]
        [SerializeField] float periodSeconds = 6f;
        [Tooltip("0..1 offset into the turn, so two spinners in one room are out of step.")]
        [Range(0f, 1f)] [SerializeField] float phase;
        [Tooltip("Turn clockwise seen from above. Off = anticlockwise.")]
        [SerializeField] bool clockwise = true;

        Quaternion _base;

        public float PeriodSeconds { get { return periodSeconds; } }

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
            _base = transform.rotation;
            Quaternion now = Evaluate(clock != null ? clock.Ms : 0L);
            body.rotation = now;
            transform.rotation = now;
        }

        /// <summary>Pure: the yaw in degrees at a clock time.</summary>
        public float AngleAt(long ms)
        {
            if (periodSeconds < 0.01f) return 0f;
            float t = Mathf.Repeat((float)(ms * 0.001) / periodSeconds + phase, 1f);
            return (clockwise ? 360f : -360f) * t;
        }

        /// <summary>Pure: the rotation at a clock time.</summary>
        public Quaternion Evaluate(long ms)
        {
            return Quaternion.AngleAxis(AngleAt(ms), _base * Vector3.up) * _base;
        }

        void FixedUpdate()
        {
            if (clock == null) return;
            body.MoveRotation(Evaluate(clock.Ms));
        }
    }
}
