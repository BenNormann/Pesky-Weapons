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
        [Tooltip("A weapon only counts while it is RESTING: slower than this. A fly-through never latches it.")]
        [SerializeField] float restSpeed = 1.5f;
        [Tooltip("The pad that sinks while the plate is pressed.")]
        [SerializeField] Transform pad;
        [SerializeField] float pressDepth = 0.06f;
        [SerializeField] Renderer padRenderer;
        [SerializeField] Color idleColor = new Color(0.63f, 0.63f, 0.67f, 1f);
        [SerializeField] Color latchedColor = new Color(0.24f, 0.63f, 0.27f, 1f);

        readonly List<WeaponBody> _on = new List<WeaponBody>();
        MaterialPropertyBlock _mpb;
        Vector3 _padUp;
        float _mass;
        bool _latched;

        public int SceneId { get { return id; } }
        public float MassThreshold { get { return massThreshold; } }
        public float Mass { get { return _mass; } }
        public bool Latched { get { return _latched; } }

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
                if (w.Body.linearVelocity.magnitude > restSpeed) continue;
                sum += w.Body.mass;
            }
            _mass = sum;
            if (authority != null) authority.ReportPlateMass(id, sum);
        }

        /// <summary>Authority only: the validated mass report, which may latch the plate.</summary>
        public void ApplyMass(float mass)
        {
            _mass = mass;
            if (!_latched && mass >= massThreshold)
            {
                _latched = true;
                Tint(latchedColor);
            }
        }

        void Update()
        {
            if (pad == null) return;
            float target = (_latched || _mass > 0.01f) ? pressDepth : 0f;
            Vector3 want = _padUp - Vector3.up * target;
            pad.localPosition = Vector3.MoveTowards(pad.localPosition, want, Time.deltaTime * 0.4f);
        }

        void Tint(Color c)
        {
            if (padRenderer == null || _mpb == null) return;
            padRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            padRenderer.SetPropertyBlock(_mpb);
        }
    }
}
