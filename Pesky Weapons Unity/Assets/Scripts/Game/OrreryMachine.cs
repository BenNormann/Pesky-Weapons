using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The Orrery's machine reads the star levers (docs/level-design/orrery-room.md). Each planet's arm is linked to one
    /// lever: while the lever is OFF the planet hangs where it likes, and when the lever turns ON the arm swings round
    /// to the shared bearing, so three lit levers line the three worlds up in one row and the machine, aligned, draws
    /// in the power that opens the exit (the exit line itself is a PowerLine). The sun brightens with every world that
    /// has come into line. A pure view: it changes nothing shared, it follows ImpactLever.IsOn, which already travels
    /// as KIT_STATE, so every peer shows the same machine with no message of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrreryMachine : MonoBehaviour
    {
        [System.Serializable]
        public struct PlanetLink
        {
            [Tooltip("The planet's arm (a child of the turning rotor); it is yawed about the axle.")]
            public Transform arm;
            [Tooltip("Aligned while this lever is on.")]
            public ImpactLever lever;
            [Tooltip("Local yaw in degrees while the lever is off.")]
            public float offYaw;
            [Tooltip("Local yaw in degrees while the lever is on: the same for every planet, so they come into line.")]
            public float onYaw;
        }

        [SerializeField] PlanetLink[] planets = new PlanetLink[0];
        [Tooltip("Seconds a planet takes to swing from one bearing to the other.")]
        [SerializeField] float alignSeconds = 2.5f;

        [Header("The sun")]
        [OptionalRef][SerializeField] Light sunLight;
        [SerializeField] float lightIdle = 2.2f;
        [SerializeField] float lightFull = 6.5f;
        [Tooltip("M_MagicFlow renderers of the sun; their _Live goes from idle to full (above 1 brightens).")]
        [OptionalRef][SerializeField] Renderer[] sunRenderers = new Renderer[0];
        [SerializeField] float liveIdle = 1f;
        [SerializeField] float liveFull = 2.4f;

        static readonly int LiveId = Shader.PropertyToID("_Live");
        float[] _aligned;
        MaterialPropertyBlock _mpb;
        float _shownPower = -1f;

        /// <summary>0 with no world in line, 1 with every one in line (smoothed, so it matches what is drawn).</summary>
        public float Power
        {
            get
            {
                if (_aligned == null || _aligned.Length == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < _aligned.Length; i++) sum += _aligned[i];
                return sum / _aligned.Length;
            }
        }

        void Awake()
        {
            _aligned = new float[planets.Length];
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            float step = alignSeconds > 0.01f ? Time.deltaTime / alignSeconds : 1f;
            for (int i = 0; i < planets.Length; i++)
            {
                PlanetLink p = planets[i];
                float target = p.lever != null && p.lever.IsOn ? 1f : 0f;
                _aligned[i] = Mathf.MoveTowards(_aligned[i], target, step);
                if (p.arm == null) continue;
                float eased = Mathf.SmoothStep(0f, 1f, _aligned[i]);
                p.arm.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(p.offYaw, p.onYaw, eased), 0f);
            }

            float power = Power;
            if (Mathf.Approximately(power, _shownPower)) return;
            _shownPower = power;
            if (sunLight != null) sunLight.intensity = Mathf.Lerp(lightIdle, lightFull, power);
            float live = Mathf.Lerp(liveIdle, liveFull, power);
            for (int i = 0; i < sunRenderers.Length; i++)
            {
                Renderer r = sunRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(LiveId, live);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
