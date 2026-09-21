using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A box volume that pulls METAL weapons (WeaponDef.metal; possessed or loose) toward a target point
    /// or a target plane. Everything else - the wooden Staff, the Banana, the Orb, souls, goblins - is
    /// left alone. Far from the target the pull is an acceleration with a distance falloff; inside
    /// holdDistance a damped spring takes over, cancels gravity and HOLDS the weapon on the target.
    /// A held weapon counts as grounded, so it can launch off the magnet (and the wall-jump counter
    /// resets): nothing can be stranded on a magnet. A launch switches the pull off for that weapon for
    /// launchReleaseSeconds so the launch flies its previewed arc.
    /// On / off is host-owned state: WorldAuthority.RequestSetMagnet -> ApplyOn -> MagnetChanged.
    /// The BoxCollider (trigger, Trigger layer) only defines the volume; weapons are found with an
    /// overlap query every physics step, so respawns and teleports cannot leave stale riders behind.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MagnetZone : MonoBehaviour, ISceneId
    {
        public enum PullMode
        {
            /// <summary>Pull to the target's position (a magnet block: weapons gather under it).</summary>
            TowardPoint = 0,
            /// <summary>Pull to the plane through the target whose normal is the target's up axis (a magnet strip: weapons rise to a height and keep their place along it).</summary>
            TowardPlane = 1
        }

        [SerializeField] int id;
        [SerializeField] BoxCollider volume;
        [Tooltip("Where metal is pulled to. Keep it clear of geometry (about 1.2 m under a ceiling magnet) so a held weapon has room to launch.")]
        [SerializeField] Transform target;
        [SerializeField] PullMode mode = PullMode.TowardPoint;

        [Header("Pull")]
        [Tooltip("Acceleration toward the target in m/s^2 at zero distance (mass independent, like the launch). Gravity is 20, so more than 20 is needed to lift.")]
        [SerializeField] float pullAcceleration = 45f;
        [Tooltip("Distance at which the pull has fallen to edgeStrength.")]
        [SerializeField] float range = 8f;
        [Tooltip("Fraction of the pull left at the range distance (1 = no falloff).")]
        [Range(0f, 1f)] [SerializeField] float edgeStrength = 0.6f;
        [Tooltip("Shape of the falloff: 1 = linear, 2 = stays strong then drops late, 0.5 = drops early.")]
        [SerializeField] float falloffPower = 1f;
        [Tooltip("The pull stops adding speed toward the target above this, so nothing slams into the magnet.")]
        [SerializeField] float maxPullSpeed = 8f;

        [Header("Hold")]
        [Tooltip("Within this distance of the target the weapon is held: damped spring, gravity cancelled, counts as grounded.")]
        [SerializeField] float holdDistance = 0.9f;
        [Tooltip("Spring stiffness of the hold in 1/s^2.")]
        [SerializeField] float holdSpring = 40f;
        [Tooltip("Damping of the hold in 1/s. About 2 * sqrt(holdSpring) is critically damped.")]
        [SerializeField] float holdDamping = 12.6f;
        [Tooltip("TowardPlane only: damping along the plane, so a held weapon drifts to rest but can still be launched along it.")]
        [SerializeField] float holdPlanarDamping = 3f;
        [Tooltip("Seconds after a launch during which the magnet leaves that weapon alone.")]
        [SerializeField] float launchReleaseSeconds = 0.6f;

        [Header("Power (host-owned)")]
        [SerializeField] bool startsOn = true;
        [Tooltip("Shown while the magnet is on (a glow, a label). Optional.")]
        [OptionalRef][SerializeField] GameObject onVisual;
        [Tooltip("Weapon layer.")]
        [SerializeField] LayerMask weaponMask = 1 << 9;

        readonly Collider[] _hits = new Collider[48];
        readonly System.Collections.Generic.List<WeaponBody> _seen = new System.Collections.Generic.List<WeaponBody>();
        bool _on;

        public int SceneId { get { return id; } }
        public bool IsOn { get { return _on; } }
        public Transform Target { get { return target; } }
        public PullMode Mode { get { return mode; } }

        void Reset()
        {
            volume = GetComponent<BoxCollider>();
            if (volume != null) volume.isTrigger = true;
        }

        void Awake()
        {
            if (volume == null) volume = GetComponent<BoxCollider>();
            _on = startsOn;
            if (onVisual != null) onVisual.SetActive(_on);
        }

        /// <summary>Authority only.</summary>
        public void ApplyOn(bool on)
        {
            _on = on;
            if (onVisual != null) onVisual.SetActive(on);
        }

        /// <summary>Pure: the vector from a point to where the magnet wants it.</summary>
        public Vector3 PullVector(Vector3 from)
        {
            if (target == null) return Vector3.zero;
            Vector3 to = target.position - from;
            if (mode == PullMode.TowardPlane)
            {
                Vector3 n = target.up;
                return n * Vector3.Dot(to, n);
            }
            return to;
        }

        /// <summary>Pure: pull acceleration in m/s^2 at a distance from the target.</summary>
        public float StrengthAt(float distance)
        {
            float k = range > 0.001f ? Mathf.Clamp01(distance / range) : 1f;
            return pullAcceleration * Mathf.Lerp(1f, edgeStrength, Mathf.Pow(k, Mathf.Max(0.01f, falloffPower)));
        }

        void FixedUpdate()
        {
            if (!_on || target == null || volume == null) return;

            Vector3 centre = transform.TransformPoint(volume.center);
            Vector3 half = Vector3.Scale(volume.size, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(centre, half, _hits, transform.rotation, weaponMask, QueryTriggerInteraction.Ignore);

            _seen.Clear();
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = _hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                WeaponBody w = rb.GetComponent<WeaponBody>();
                if (w == null || w.IsBroken || w.Def == null || !w.Def.metal) continue;   // non-metal is unaffected
                if (_seen.Contains(w)) continue;
                _seen.Add(w);
                if (w.SecondsSinceLaunch < launchReleaseSeconds) continue;
                Pull(w, rb);
            }
        }

        void Pull(WeaponBody weapon, Rigidbody rb)
        {
            Vector3 to = PullVector(rb.worldCenterOfMass);
            float d = to.magnitude;
            Vector3 v = rb.linearVelocity;

            if (d <= holdDistance)
            {
                Vector3 accel;
                if (mode == PullMode.TowardPlane)
                {
                    Vector3 n = target.up;
                    Vector3 vn = n * Vector3.Dot(v, n);
                    accel = to * holdSpring - vn * holdDamping - (v - vn) * holdPlanarDamping;
                }
                else
                {
                    accel = to * holdSpring - v * holdDamping;
                }
                rb.AddForce(accel - Physics.gravity, ForceMode.Acceleration);
                weapon.MarkSupported(d > 0.001f ? -to / d : Vector3.up);
                return;
            }

            Vector3 dir = to / d;
            if (Vector3.Dot(v, dir) < maxPullSpeed) rb.AddForce(dir * StrengthAt(d), ForceMode.Acceleration);
        }

        void OnDrawGizmosSelected()
        {
            BoxCollider box = volume != null ? volume : GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.color = new Color(0.85f, 0.2f, 0.2f, 0.5f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            if (target != null)
            {
                Gizmos.color = new Color(0.85f, 0.2f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(target.position, holdDistance);
            }
        }
    }
}
