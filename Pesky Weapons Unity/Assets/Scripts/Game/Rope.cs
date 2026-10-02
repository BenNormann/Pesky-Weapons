using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A taut rope holding something up. Only a BLADED weapon travelling at minCutSpeed or more cuts it;
    /// blunt weapons just bounce off, which is the whole puzzle (a Mace has to swap for a Sword).
    /// Cutting LATCHES: the cut and the clock time it happened are the host-owned state, and a DropRamp
    /// is a pure function of those two plus LevelClock.Ms.
    /// The component lives on the object that carries the rope's collider, because a static collider is
    /// the one that receives the collision message for the body that hit it. A rope with more than one
    /// straight run adds RopeSegments (each its own object and collider) that forward their hits here:
    /// still one rope, one id, one cut.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rope : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [SerializeField] LevelClock clock;

        [Header("Rule")]
        [Tooltip("Only a weapon whose WeaponDef.bladed is set can cut it.")]
        [SerializeField] bool requiresBladed = true;
        [Tooltip("Impact speed in m/s at or above which a bladed weapon cuts it.")]
        [SerializeField] float minCutSpeed = 5f;

        [Header("Wiring")]
        [Tooltip("The rope's own collider. Disabled when it is cut.")]
        [SerializeField] Collider cord;
        [Tooltip("The taut rope mesh. Hidden when it is cut.")]
        [SerializeField] GameObject taut;
        [Tooltip("The loose ends, shown when it is cut. Optional.")]
        [OptionalRef][SerializeField] GameObject cutEnds;
        [Tooltip("The ramp this rope holds up. Optional: a rope can also just be a latch for a door.")]
        [OptionalRef][SerializeField] DropRamp ramp;
        [Tooltip("Further runs of the same rope: RopeSegments with their own solid collider pointing back here. A hit on any of them is this rope's cut, and they vanish with it. Optional.")]
        [OptionalRef][SerializeField] RopeSegment[] segments = new RopeSegment[0];

        bool _cut;
        long _cutMs;

        public int SceneId { get { return id; } }
        public bool IsCut { get { return _cut; } }
        /// <summary>LevelClock time the rope was cut. The ramp animates from this.</summary>
        public long CutMs { get { return _cutMs; } }
        public DropRamp Ramp { get { return ramp; } }
        public float MinCutSpeed { get { return minCutSpeed; } }
        public bool RequiresBladed { get { return requiresBladed; } }
        public LevelClock Clock { get { return clock; } }

        void Awake()
        {
            if (cutEnds != null) cutEnds.SetActive(false);
        }

        /// <summary>True when this weapon, at this speed, is allowed to cut the rope. Pure.</summary>
        public bool CanCut(WeaponBody weapon, float speed)
        {
            if (_cut || weapon == null || weapon.IsBroken || weapon.Def == null) return false;
            if (requiresBladed && !weapon.Def.bladed) return false;
            return speed >= minCutSpeed;
        }

/// <summary>Host sanity check: is the point within range of ANY part of the rope? A long rope's far run
        /// can be well over the kit claim range from its root, which is only the bottom of its first run.</summary>
        public bool IsNear(Vector3 point, float range)
        {
            float r2 = range * range;
            if (Within(cord, point, r2)) return true;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null && Within(segments[i].Collider, point, r2)) return true;
            return (transform.position - point).sqrMagnitude <= r2;
        }

        static bool Within(Collider c, Vector3 point, float r2)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return false;
            return (c.ClosestPoint(point) - point).sqrMagnitude <= r2;
        }

        void OnCollisionEnter(Collision collision)
        {
            ReportHit(collision);
        }

        /// <summary>A body hit this rope's own cord or one of its segments: ask the authority for the cut.</summary>
        public void ReportHit(Collision collision)
        {
            if (_cut || authority == null) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null) return;
            authority.RequestCutRope(id, weapon, collision.relativeVelocity.magnitude);
        }

        /// <summary>Authority only. Latching: a cut rope never comes back.</summary>
/// <summary>Authority only. Latching: a cut rope never comes back.</summary>
        public void ApplyCut(long ms)
        {
            if (_cut) return;
            _cut = true;
            _cutMs = ms;
            if (cord != null) cord.enabled = false;
            if (taut != null) taut.SetActive(false);
            if (cutEnds != null) cutEnds.SetActive(true);
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null) segments[i].gameObject.SetActive(false);
        }
    }
}
