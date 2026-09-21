using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The ramp a Rope holds up. It has NO host-owned state of its own: its pose is a pure function of
    /// (rope.IsCut, rope.CutMs, LevelClock.Ms), so every client draws the same fall without a message.
    /// Kinematic Rigidbody moved with MovePosition / MoveRotation, so it shoves anything in its way
    /// instead of tunnelling through it. It must NOT be marked static.
    /// Swing = hinge about the root's local X (put the hinge at the root and the deck in front of it).
    /// Drop  = slide along a local offset (a portcullis-style ramp that falls straight down).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DropRamp : MonoBehaviour
    {
        public enum Mode { Swing = 0, Drop = 1 }

        [SerializeField] Rope rope;
        [SerializeField] LevelClock clock;
        [SerializeField] Rigidbody body;

        [Header("Fall")]
        [SerializeField] Mode mode = Mode.Swing;
        [Tooltip("Swing: degrees about the root's local X axis, from the held pose to the landed pose.")]
        [SerializeField] float swingDegrees = -70f;
        [Tooltip("Drop: local-space offset from the held pose to the landed pose.")]
        [SerializeField] Vector3 dropOffset = new Vector3(0f, -4f, 0f);
        [Tooltip("Seconds the fall takes.")]
        [SerializeField] float fallSeconds = 1.2f;
        [Tooltip("Ease in and out instead of a constant rate, so it settles instead of snapping.")]
        [SerializeField] bool smooth = true;

        Vector3 _homePosition;
        Quaternion _homeRotation;

        public Rope Rope { get { return rope; } }
        /// <summary>0 = held up, 1 = landed.</summary>
        public float Progress { get { return ProgressAt(clock != null ? clock.Ms : 0L); } }
        public bool Landed { get { return Progress >= 1f; } }

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
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            ApplyPose(ProgressAt(clock != null ? clock.Ms : 0L));
        }

        /// <summary>Pure: how far through the fall the ramp is at a given clock time.</summary>
        public float ProgressAt(long ms)
        {
            if (rope == null || !rope.IsCut) return 0f;
            if (fallSeconds <= 0.001f) return 1f;
            double t = (ms - rope.CutMs) * 0.001;
            float k = Mathf.Clamp01((float)(t / fallSeconds));
            return smooth ? Mathf.SmoothStep(0f, 1f, k) : k;
        }

        void FixedUpdate()
        {
            ApplyPose(ProgressAt(clock != null ? clock.Ms : 0L));
        }

        void ApplyPose(float k)
        {
            if (body == null) return;
            if (mode == Mode.Swing)
            {
                Quaternion rotation = _homeRotation * Quaternion.Euler(swingDegrees * k, 0f, 0f);
                body.MovePosition(_homePosition);
                body.MoveRotation(rotation);
            }
            else
            {
                body.MovePosition(_homePosition + _homeRotation * (dropOffset * k));
                body.MoveRotation(_homeRotation);
            }
        }

        void OnDrawGizmosSelected()
        {
            Vector3 home = Application.isPlaying ? _homePosition : transform.position;
            Quaternion rot = Application.isPlaying ? _homeRotation : transform.rotation;
            Gizmos.color = new Color(0.95f, 0.72f, 0.25f, 0.9f);
            if (mode == Mode.Swing)
            {
                Gizmos.DrawLine(home, home + rot * Quaternion.Euler(swingDegrees, 0f, 0f) * Vector3.forward * 4f);
                Gizmos.DrawLine(home, home + rot * Vector3.forward * 4f);
            }
            else
            {
                Gizmos.DrawLine(home, home + rot * dropOffset);
            }
        }
    }
}
