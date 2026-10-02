using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One more straight run of a Rope: its own object with its own solid collider. Unity delivers a collision
    /// to the object that OWNS the collider that was hit, never to a parent, so this forwards the hit to the
    /// Rope it belongs to. No state and no scene id of its own: the Rope lists it in `segments`, hides it on
    /// the cut and measures the host's range check against its collider. Must not be static.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RopeSegment : MonoBehaviour
    {
        [Tooltip("The rope this is a piece of.")]
        [SerializeField] Rope rope;

        Collider _collider;

        public Rope Rope { get { return rope; } }
        /// <summary>This run's solid collider (null before Awake).</summary>
        public Collider Collider { get { return _collider; } }

        void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (rope != null) rope.ReportHit(collision);
        }
    }
}
