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
        [Tooltip("Optional. When set, the LevelClock time the lever turned on is recorded (OnMs) so a DropRamp or other clock-driven piece can animate from it.")]
        [OptionalRef][SerializeField] LevelClock clock;
        long _onMs;

        [Header("Look")]
        [Tooltip("Rotated about its local X between offAngle and onAngle.")]
        [OptionalRef][SerializeField] Transform handle;
        [SerializeField] float offAngle = -35f;
        [SerializeField] float onAngle = 35f;
        [Tooltip("Seconds the handle takes to swing across.")]
        [SerializeField] float swingSeconds = 0.25f;
        [OptionalRef][SerializeField] Renderer handleRenderer;
        [SerializeField] Color offColor = new Color(0.63f, 0.63f, 0.67f, 1f);
        [SerializeField] Color onColor = new Color(0.24f, 0.63f, 0.27f, 1f);
        [Tooltip("Glow pieces: floor halo, edge shells. The labyrinth's purple with drifting specks while off; gone while on (a done lever asks for nothing).")]
        [OptionalRef][SerializeField] Renderer[] glowRenderers;
        [Tooltip("The floor halo: shown only when the lever stands upright on a floor (its up is the world's up AND a floor is right under its base). A lever on a wall, upright or sideways, shows none.")]
        [OptionalRef][SerializeField] Renderer floorHalo;
        [ColorUsage(false, true)][SerializeField] Color glowOff = new Color(0.7f, 0.42f, 1.8f, 1f);
        [ColorUsage(false, true)][SerializeField] Color glowOn = Color.black;

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
        /// <summary>LevelClock ms when the lever last turned on; 0 when unknown (treat as long ago).</summary>
        public long OnMs { get { return _onMs; } }
        /// <summary>True while an impact would be ignored because the lever has only just flipped.</summary>
        public bool Rearming { get { return Time.time < _rearmAt; } }

        void Start()
        {
            // The floor halo spills light across a floor. A lever on a wall, whether it stands upright on a bracket or
            // sticks out sideways, has no floor under its base, so it shows none (the owner's review, twice).
            if (floorHalo == null) return;
            bool upright = Vector3.Dot(transform.up, Vector3.up) > 0.9f;
            bool floorBelow = upright && Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.35f, 1 << 8, QueryTriggerInteraction.Ignore);
            floorHalo.enabled = floorBelow;
        }

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
/// <summary>Authority only.</summary>
        public void ApplySet(bool on)
        {
            if (on && !_on && clock != null) _onMs = clock.Ms;
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
            if (_mpb == null) return;
            if (!force && !Mathf.Approximately(_shown, 0f) && !Mathf.Approximately(_shown, 1f)) return;
            if (glowRenderers != null)
                for (int i = 0; i < glowRenderers.Length; i++)
                    GlowLook.Set(glowRenderers[i], _mpb, Color.Lerp(glowOff, glowOn, _shown), Mathf.Lerp(1f, GlowLook.ActiveLive, _shown));
            if (handleRenderer == null) return;
            Color c = Color.Lerp(offColor, onColor, _shown);
            handleRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            handleRenderer.SetPropertyBlock(_mpb);
        }
    }
}
