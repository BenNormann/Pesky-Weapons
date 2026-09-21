using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A lever flipped by ANY weapon impact above minSpeed - bladed, blunt, metal, wooden, it does not
    /// care. Two variants live in one component:
    ///   latching = false : every hit toggles it (doors that mode LeverOn follow it both ways).
    ///   latching = true  : the first hit turns it on for good. This is the WINCH: wire `lift` as well
    ///                      and the authority switches that Lift on when the lever turns on.
    /// The handle angle is a view, driven from the host-owned on/off flag.
    /// The component lives on the object carrying the lever's collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactLever : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;

        [Header("Rule")]
        [Tooltip("Any weapon impact at or above this speed in m/s flips the lever.")]
        [SerializeField] float minSpeed = 4f;
        [Tooltip("Once on it can never go off again. The Winch is a latching lever.")]
        [SerializeField] bool latching = true;
        [SerializeField] bool startOn;
        [Tooltip("Seconds after a flip during which further impacts are ignored, so one landing is one flip.")]
        [SerializeField] float rearmSeconds = 0.5f;

        [Header("Winch variant")]
        [Tooltip("Switched on for good when this lever turns on. Leave empty for a plain lever.")]
        [OptionalRef][SerializeField] Lift lift;

        [Header("Look")]
        [Tooltip("Rotated about its local X between offAngle and onAngle.")]
        [SerializeField] Transform handle;
        [SerializeField] float offAngle = -35f;
        [SerializeField] float onAngle = 35f;
        [Tooltip("Seconds the handle takes to swing across.")]
        [SerializeField] float swingSeconds = 0.25f;
        [SerializeField] Renderer handleRenderer;
        [SerializeField] Color offColor = new Color(0.63f, 0.63f, 0.67f, 1f);
        [SerializeField] Color onColor = new Color(0.24f, 0.63f, 0.27f, 1f);

        MaterialPropertyBlock _mpb;
        bool _on;
        float _shown;
        float _rearmAt;

        public int SceneId { get { return id; } }
        public bool IsOn { get { return _on; } }
        public bool IsLatching { get { return latching; } }
        public float MinSpeed { get { return minSpeed; } }
        /// <summary>The lift this lever is the winch for, or null.</summary>
        public Lift Lift { get { return lift; } }
        /// <summary>True while an impact would be ignored because the lever has only just flipped.</summary>
        public bool Rearming { get { return Time.time < _rearmAt; } }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _on = startOn;
            _shown = _on ? 1f : 0f;
            ApplyLook(true);
        }

        /// <summary>True when this impact is allowed to flip the lever. Pure.</summary>
        public bool CanFlip(WeaponBody weapon, float speed)
        {
            if (weapon == null || weapon.IsBroken) return false;
            if (latching && _on) return false;
            if (Time.time < _rearmAt) return false;
            return speed >= minSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (authority == null) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            WeaponBody weapon = rb.GetComponent<WeaponBody>();
            if (weapon == null) return;
            authority.RequestLeverImpact(id, weapon, collision.relativeVelocity.magnitude);
        }

        /// <summary>Authority only.</summary>
        public void ApplySet(bool on)
        {
            _on = on;
            _rearmAt = Time.time + rearmSeconds;
        }

        void Update()
        {
            float target = _on ? 1f : 0f;
            if (Mathf.Approximately(_shown, target)) return;
            float step = swingSeconds > 0.001f ? Time.deltaTime / swingSeconds : 1f;
            _shown = Mathf.MoveTowards(_shown, target, step);
            ApplyLook(false);
        }

        void ApplyLook(bool force)
        {
            if (handle != null)
                handle.localRotation = Quaternion.Euler(Mathf.Lerp(offAngle, onAngle, _shown), 0f, 0f);
            if (handleRenderer == null || _mpb == null) return;
            if (!force && !Mathf.Approximately(_shown, 0f) && !Mathf.Approximately(_shown, 1f)) return;
            Color c = Color.Lerp(offColor, onColor, _shown);
            handleRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            handleRenderer.SetPropertyBlock(_mpb);
        }
    }
}
