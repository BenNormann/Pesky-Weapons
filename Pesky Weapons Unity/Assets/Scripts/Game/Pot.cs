using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A big pot. Only a BLUNT weapon hitting it at minSmashSpeed or more breaks it; a bladed weapon and
    /// the Banana bounce off (they are neither blunt nor heavy enough to matter). Smashing LATCHES.
    /// A pot may hide something - a lever, a key, or nothing - wired as `contents`, which starts
    /// INACTIVE in the scene and is switched on when the pot breaks.
    /// The component lives on the object carrying the pot's collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Pot : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;

        [Header("Rule")]
        [Tooltip("Only a weapon whose WeaponDef.blunt is set can smash it.")]
        [SerializeField] bool requiresBlunt = true;
        [Tooltip("Impact speed in m/s at or above which a blunt weapon smashes it.")]
        [SerializeField] float minSmashSpeed = 5f;

        [Header("Wiring")]
        [Tooltip("The pot's own collider. Disabled when it breaks.")]
        [SerializeField] Collider shell;
        [Tooltip("The whole pot mesh. Hidden when it breaks.")]
        [SerializeField] GameObject intact;
        [Tooltip("The shards left behind. Optional.")]
        [OptionalRef][SerializeField] GameObject shards;
        [Tooltip("What was hiding inside: an ImpactLever, a Key, or nothing. Starts inactive.")]
        [OptionalRef][SerializeField] GameObject contents;

        bool _smashed;

        public int SceneId { get { return id; } }
        public bool IsSmashed { get { return _smashed; } }
        public float MinSmashSpeed { get { return minSmashSpeed; } }
        public bool RequiresBlunt { get { return requiresBlunt; } }
        public GameObject Contents { get { return contents; } }

        void Awake()
        {
            if (shards != null) shards.SetActive(false);
            if (contents != null) contents.SetActive(false);
        }

        /// <summary>True when this weapon, at this speed, is allowed to smash the pot. Pure.</summary>
        public bool CanSmash(WeaponBody weapon, float speed)
        {
            if (_smashed || weapon == null || weapon.IsBroken || weapon.Def == null) return false;
            if (requiresBlunt && !weapon.Def.blunt) return false;
            return speed >= minSmashSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_smashed || authority == null) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null) return;
            authority.RequestSmashPot(id, weapon, collision.relativeVelocity.magnitude);
        }

        /// <summary>Authority only. Latching: a smashed pot never comes back.</summary>
        public void ApplySmashed()
        {
            if (_smashed) return;
            _smashed = true;
            if (shell != null) shell.enabled = false;
            if (intact != null) intact.SetActive(false);
            if (shards != null) shards.SetActive(true);
            if (contents != null) contents.SetActive(true);
        }
    }
}
