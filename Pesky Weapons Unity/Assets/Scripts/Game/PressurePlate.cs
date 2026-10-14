using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Sums the mass of the weapons resting on it and reports that to the WorldAuthority every physics step.
    /// The authority decides when it latches; once latched it stays latched (it holds its door open).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PressurePlate : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [Tooltip("Summed weapon mass needed to latch (SLICE-1 section 6: 10).")]
        [SerializeField] float massThreshold = 10f;
        [Tooltip("On = the usual plate: reaching the threshold LATCHES for good. Off = a HOLD plate: Held is true only while enough mass rests on it (a parked body keeps a portcullis up).")]
        [SerializeField] bool latching = true;
        [Tooltip("Only METAL weapons count (a circuit gap that a wooden Staff or the Banana cannot bridge).")]
        [SerializeField] bool metalOnly;
        bool _held;
        [Tooltip("A weapon only counts while it is RESTING: slower than this. A fly-through never latches it.")]
        [SerializeField] float restSpeed = 1.5f;
        [Tooltip("The pad that sinks while the plate is pressed.")]
        [SerializeField] Transform pad;
        [SerializeField] float pressDepth = 0.14f;   // the pad's height less a centimetre: a held plate sits a hair above the floor, never flush (coplanar faces flicker)
        [SerializeField] Renderer padRenderer;
        [SerializeField] Color idleColor = new Color(0.63f, 0.63f, 0.67f, 1f);
        [SerializeField] Color latchedColor = new Color(0.24f, 0.63f, 0.27f, 1f);
        [Tooltip("Glow pieces: floor halo, rising sheen (both additive: the pad itself keeps its own material). The labyrinth's purple with drifting specks while idle; gone once held (a done plate asks for nothing).")]
        [OptionalRef][SerializeField] Renderer[] glowRenderers;
        [ColorUsage(false, true)][SerializeField] Color glowIdle = new Color(0.7f, 0.42f, 1.8f, 1f);
        [ColorUsage(false, true)][SerializeField] Color glowHeld = Color.black;
        [Tooltip("A plate that can no longer do anything once this lever is on (the Gatehouse's hold plate after the winch is pinned): from then on it sits PRESSED with its glow off, whatever rests on it, and asks for nothing (owner, round 29b).")]
        [OptionalRef][SerializeField] ImpactLever spentWhenLeverOn;

        [Header("Sound")]
        [Tooltip("The pad starting down under a weight: the wooden slide (any press, permanent or not).")]
        [OptionalRef][SerializeField] AudioClip pressSound;
        [Tooltip("The latch: the metal flick, layered on the slide when the press becomes permanent.")]
        [OptionalRef][SerializeField] AudioClip latchSound;
        [Tooltip("The labyrinth's magic, quietly behind every circuit piece.")]
        [OptionalRef][SerializeField] AudioClip magicSound;
        [Range(0f, 1f)][SerializeField] float pressVolume = 0.8f;
        [Range(0f, 1f)][SerializeField] float latchVolume = 0.9f;
        [Range(0f, 1f)][SerializeField] float magicVolume = 0.2f;
        bool _soundPressed;

        readonly List<WeaponBody> _on = new List<WeaponBody>();
        MaterialPropertyBlock _mpb;
        Vector3 _padUp;
        float _mass;
        bool _latched;
        bool _spentShown;

        public int SceneId { get { return id; } }
        public float MassThreshold { get { return massThreshold; } }
        public float Mass { get { return _mass; } }
        public bool Latched { get { return _latched; } }
        public bool IsLatching { get { return latching; } }
        /// <summary>Host-decided: a latching plate once latched, a hold plate while enough mass rests on it.</summary>
        public bool Held { get { return latching ? _latched : _held; } }
        /// <summary>The plate no longer matters (its lever has taken over): pressed for good, glow off. A view fact, not a host one.</summary>
        public bool IsSpent { get { return spentWhenLeverOn != null && spentWhenLeverOn.IsOn; } }

        void Awake()
        {
            if (pad != null) _padUp = pad.localPosition;
            _mpb = new MaterialPropertyBlock();
            Tint(idleColor);
        }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w != null && !_on.Contains(w)) _on.Add(w);
        }

        void OnTriggerExit(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w != null) _on.Remove(w);
        }

        static WeaponBody Weapon(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<WeaponBody>() : null;
        }

        void FixedUpdate()
        {
            float sum = 0f;
            for (int i = _on.Count - 1; i >= 0; i--)
            {
                WeaponBody w = _on[i];
                if (w == null || w.IsBroken) { _on.RemoveAt(i); continue; }
                if (w.Body == null) continue;
                if (metalOnly && (w.Def == null || !w.Def.metal)) continue;
                if (w.Body.linearVelocity.magnitude > restSpeed) continue;
                sum += w.Body.mass;
            }
            _mass = sum;
            if (authority != null) authority.ReportPlateMass(id, sum);
        }

        /// <summary>Authority only: the validated mass report, which may latch the plate.</summary>
/// <summary>Authority only: the validated mass report. Latching plates latch; hold plates track Held.</summary>
        public void ApplyMass(float mass)
        {
            _mass = mass;
            if (latching)
            {
                if (!_latched && mass >= massThreshold)
                {
                    _latched = true;
                    Tint(latchedColor);
                    // Permanent: the flick that locks it, over the slide the pad's own descent plays.
                    Sfx.Play(latchSound, transform.position, latchVolume);
                }
                return;
            }
            bool held = mass >= massThreshold;
            if (held == _held) return;
            _held = held;
            Tint(held || IsSpent ? latchedColor : idleColor);
        }

        void Update()
        {
            // Spent (its lever took over): the pad goes down and stays, the glow goes out, like a plate that has latched.
            bool spent = IsSpent;
            if (spent != _spentShown)
            {
                _spentShown = spent;
                Tint(spent || Held ? latchedColor : idleColor);
            }
            if (pad == null) return;
            // The pad only goes down under ENOUGH weight: the press is the activation (a latching plate stays down; a hold
            // plate comes back up when the weight leaves). A single Dagger on a 10 kg plate does nothing visible.
            bool pressed = _latched || spent || _mass >= massThreshold;
            if (pressed != _soundPressed)
            {
                _soundPressed = pressed;
                // The pad starting down: the wooden slide, the magic quietly behind it. Coming back up is silent.
                if (pressed)
                {
                    Sfx.Play(pressSound, transform.position, pressVolume);
                    Sfx.Play(magicSound, transform.position, magicVolume);
                }
            }
            float target = pressed ? pressDepth : 0f;
            Vector3 want = _padUp - Vector3.up * target;
            pad.localPosition = Vector3.MoveTowards(pad.localPosition, want, Time.deltaTime * 0.4f);
        }

        void Tint(Color c)
        {
            if (_mpb == null) return;
            bool held = c == latchedColor;
            if (glowRenderers != null)
                for (int i = 0; i < glowRenderers.Length; i++)
                    GlowLook.Set(glowRenderers[i], _mpb, held ? glowHeld : glowIdle, held ? GlowLook.ActiveLive : 1f);
            if (padRenderer == null) return;
            padRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            padRenderer.SetPropertyBlock(_mpb);
        }
    }
}
