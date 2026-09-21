using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A taut rope holding something up. Only a BLADED weapon travelling at minCutSpeed or more cuts it;
    /// blunt weapons just bounce off, which is the whole puzzle (a Mace has to swap for a Sword).
    /// Cutting LATCHES: the cut and the clock time it happened are the host-owned state, and a DropRamp
    /// is a pure function of those two plus LevelClock.Ms.
    /// The component lives on the object that carries the rope's collider, because a static collider is
    /// the one that receives the collision message for the body that hit it.
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

        void OnCollisionEnter(Collision collision)
        {
            if (_cut || authority == null) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null) return;
            authority.RequestCutRope(id, weapon, collision.relativeVelocity.magnitude);
        }

        /// <summary>Authority only. Latching: a cut rope never comes back.</summary>
        public void ApplyCut(long ms)
        {
            if (_cut) return;
            _cut = true;
            _cutMs = ms;
            if (cord != null) cord.enabled = false;
            if (taut != null) taut.SetActive(false);
            if (cutEnds != null) cutEnds.SetActive(true);
        }
    }
}
