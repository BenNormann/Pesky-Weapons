using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A wall with a crack in it. It breaks - once, LATCHED - when a weapon of at least minMass hits it
    /// at minSpeed or more, which with the slice-1 masses means the Mace (8) or the Hammer (14). Anything
    /// lighter, or too slow, only puffs: a short flash, and the wall stays put.
    /// Put it under &lt;Room&gt;/Gameplay, on the World layer, NOT marked static (it has to disappear).
    /// The component lives on the object carrying the wall's collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrackedWall : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;

        [Header("Rule")]
        [Tooltip("Only a weapon at least this heavy can break it (Mace 8, Hammer 14).")]
        [SerializeField] float minMass = 8f;
        [Tooltip("Impact speed in m/s at or above which a heavy weapon breaks it.")]
        [SerializeField] float minSpeed = 6f;

        [Header("Wiring")]
        [Tooltip("The wall's own collider. Disabled when it breaks.")]
        [SerializeField] Collider shell;
        [Tooltip("The whole wall. Hidden when it breaks.")]
        [SerializeField] GameObject intact;
        [Tooltip("The rubble left in the hole. Optional.")]
        [OptionalRef][SerializeField] GameObject rubble;

        [Header("Puff (a hit that was not enough)")]
        [SerializeField] Renderer wallRenderer;
        [SerializeField] Color idleColor = new Color(0.52f, 0.50f, 0.47f, 1f);
        [SerializeField] Color puffColor = new Color(0.86f, 0.82f, 0.72f, 1f);
        [SerializeField] float puffSeconds = 0.18f;

        MaterialPropertyBlock _mpb;
        bool _broken;
        float _puffUntil = -999f;

        public int SceneId { get { return id; } }
        public bool IsBroken { get { return _broken; } }
        public float MinMass { get { return minMass; } }
        public float MinSpeed { get { return minSpeed; } }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (rubble != null) rubble.SetActive(false);
            Tint(idleColor);
        }

        /// <summary>True when this weapon, at this speed, is allowed to break the wall. Pure.</summary>
        public bool CanBreak(WeaponBody weapon, float speed)
        {
            if (_broken || weapon == null || weapon.IsBroken || weapon.Body == null) return false;
            return weapon.Body.mass >= minMass && speed >= minSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_broken || authority == null) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null) return;
            authority.RequestBreakWall(id, weapon, collision.relativeVelocity.magnitude);
        }

        /// <summary>Authority only. Latching: a broken wall never comes back.</summary>
        public void ApplyBroken()
        {
            if (_broken) return;
            _broken = true;
            if (shell != null) shell.enabled = false;
            if (intact != null) intact.SetActive(false);
            if (rubble != null) rubble.SetActive(true);
        }

        /// <summary>Authority only: the hit was not enough. Pure view.</summary>
        public void ApplyPuff()
        {
            if (_broken) return;
            _puffUntil = Time.time + puffSeconds;
            Tint(puffColor);
        }

        void Update()
        {
            if (_broken || _puffUntil < 0f) return;
            if (Time.time > _puffUntil && _puffUntil > -998f)
            {
                _puffUntil = -999f;
                Tint(idleColor);
            }
        }

        void Tint(Color c)
        {
            if (wallRenderer == null || _mpb == null) return;
            wallRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            wallRenderer.SetPropertyBlock(_mpb);
        }
    }
}
