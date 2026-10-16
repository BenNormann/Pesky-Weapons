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

        [OptionalRef][SerializeField] Rope rope;
        [Tooltip("Optional. A lever instead of (or as well as) a rope: the ramp falls when the lever is ON, timed from the lever's OnMs (a drawbridge).")]
        [OptionalRef][SerializeField] ImpactLever lever;
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
        [Header("Under the deck")]
        [Tooltip("The deck's collider. Once the ramp has started to fall, a weapon caught under the deck within reach of its underside is shoved toward the hinge (Swing: where the gap stays tall) or out of the side (Drop), instead of being squeezed through the floor or left in a pocket.")]
        [OptionalRef][SerializeField] Collider deck;
        [Tooltip("How far under the deck's underside the shove reaches, metres.")]
        [SerializeField] float shoveReach = 2.5f;
        [SerializeField] float shoveSpeed = 10f;
        readonly Collider[] _hits = new Collider[32];

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
/// <summary>Pure: how far through the fall the ramp is at a given clock time (rope cut or lever on).</summary>
        public float ProgressAt(long ms)
        {
            long startMs;
            if (rope != null && rope.IsCut) startMs = rope.CutMs;
            else if (lever != null && lever.IsOn) startMs = lever.OnMs;
            else return 0f;
            if (fallSeconds <= 0.001f || startMs <= 0L) return 1f;
            double t = (ms - startMs) * 0.001;
            float k = Mathf.Clamp01((float)(t / fallSeconds));
            return smooth ? Mathf.SmoothStep(0f, 1f, k) : k;
        }

        void FixedUpdate()
        {
            float k = ProgressAt(clock != null ? clock.Ms : 0L);
            ApplyPose(k);
            if (k > 0f) ShoveFromUnderDeck();
        }

        /// <summary>Every weapon this peer simulates that sits under the deck, within shoveReach of its underside, is sent
        /// toward the hinge (Swing) or out of the nearer side (Drop). Runs from the first moment of the fall on: a body in
        /// the sweep is pushed clear instead of crushed, and one that wanders under the landed deck's low end is pushed
        /// back out to where it can launch.</summary>
        void ShoveFromUnderDeck()
        {
            if (deck == null) return;
            Bounds b = deck.bounds;
            Vector3 centre = new Vector3(b.center.x, b.center.y - shoveReach * 0.5f, b.center.z);
            Vector3 half = new Vector3(b.extents.x, b.extents.y + shoveReach * 0.5f, b.extents.z);
            int n = Physics.OverlapBoxNonAlloc(centre, half, _hits, Quaternion.identity, 1 << 9, QueryTriggerInteraction.Ignore);
            if (n == 0) return;
            // The deck in the ramp's own (unscaled) space: a slab of deckSize centred at deckCentre.
            Transform dt = deck.transform;
            BoxCollider box = deck as BoxCollider;
            Vector3 size = box != null ? Vector3.Scale(box.size, dt.localScale) : dt.localScale;
            Vector3 deckCentre = dt.localPosition + (box != null ? Vector3.Scale(box.center, dt.localScale) : Vector3.zero);
            float underside = deckCentre.y - size.y * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = _hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                WeaponBody w = rb.GetComponent<WeaponBody>();
                if (w == null || w.IsBroken) continue;
                Vector3 lp = transform.InverseTransformPoint(rb.worldCenterOfMass);
                if (lp.y >= underside || lp.y < underside - shoveReach) continue;
                if (Mathf.Abs(lp.x - deckCentre.x) > size.x * 0.5f || Mathf.Abs(lp.z - deckCentre.z) > size.z * 0.5f) continue;
                Vector3 dir = mode == Mode.Swing ? -transform.forward : (lp.x >= deckCentre.x ? transform.right : -transform.right);
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                rb.linearVelocity = dir.normalized * shoveSpeed + Vector3.up * 1f;
            }
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
