using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A break in a magical conduit: two stubs with a cradle between them. A METAL weapon lying still across
    /// BOTH stubs closes it: a Sword spans a 1.05 m break, a Dagger does not; a Dagger spans a 0.5 m one.
    /// The trigger box is the cradle. Every physics step the weapons resting in it are measured against the
    /// two stub points and the host hears whether the break is bridged (WorldAuthority.ReportCircuitBridge);
    /// the host decides and everybody applies the answer (KIT_STATE Circuit, 1 = closed), exactly like a
    /// hold plate. While the stub on the fed side is live and the break is open, it throws sparks. The
    /// Circuit reads Closed on its bridges in order from the source. Once the whole circuit has been closed it SEALS:
    /// the Fill piece bridges the gap for good, the weapon may leave, and the host keeps reporting the break closed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CircuitBridge : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;

        [Header("Rule")]
        [Tooltip("Only METAL weapons bridge it (WeaponDef.metal): the wooden Staff and the Banana never do.")]
        [SerializeField] bool metalOnly = true;
        [Tooltip("A weapon only counts while it is RESTING: slower than this. A fly-through never closes it.")]
        [SerializeField] float restSpeed = 1.5f;
        [Tooltip("The two stub ends. A weapon's collider bounds must reach both, within tolerance.")]
        [SerializeField] Transform stubA;
        [SerializeField] Transform stubB;
        [SerializeField] float tolerance = 0.12f;

        [Header("Wiring")]
        [Tooltip("Where the fed conduit ends: the outer face of stub A. The builder runs the conduit exactly to this point.")]
        [OptionalRef][SerializeField] Transform attachA;
        [Tooltip("Where the onward conduit starts: the outer face of stub B.")]
        [OptionalRef][SerializeField] Transform attachB;

        [Header("Look")]
        [Tooltip("Sparks played while this break is fed and still open.")]
        [OptionalRef][SerializeField] ParticleSystem sparks;
        [Tooltip("The fed stub (M_MagicFlow): live while power reaches this break.")]
        [OptionalRef][SerializeField] Renderer stubRendererA;
        [Tooltip("The far stub (M_MagicFlow): live only once the break is closed.")]
        [OptionalRef][SerializeField] Renderer stubRendererB;
        [Tooltip("The piece that fills the gap once the whole circuit has sealed (M_MagicFlow): off until then.")]
        [OptionalRef][SerializeField] Renderer fill;

        [Header("Snap (a small magnet)")]
        [Tooltip("A small pull on nearby metal weapons toward the cradle's centre, so a close landing settles across the stubs. m/s^2 at the centre, fading to nothing at snapRange. Never lifts (gravity is 20). Off once sealed.")]
        [SerializeField] float snapPull = 16f;
        [SerializeField] float snapRange = 2.5f;
        [Tooltip("The pull stops adding speed toward the cradle above this.")]
        [SerializeField] float snapMaxSpeed = 2.5f;
        [Tooltip("Turns a nearby weapon's long axis along the cradle, either way round (rad/s^2 at the centre).")]
        [SerializeField] float snapTurn = 8f;
        [Tooltip("Weapon layer.")]
        [SerializeField] LayerMask weaponMask = 1 << 9;

        static readonly int LiveId = Shader.PropertyToID("_Live");
        readonly List<WeaponBody> _in = new List<WeaponBody>();
        readonly Collider[] _near = new Collider[32];
        readonly List<WeaponBody> _pulled = new List<WeaponBody>();
        MaterialPropertyBlock _mpb;
        bool _closed;
        bool _powered;
        bool _sparking;
        bool _sealed;
        float _glow = 1f;

        public int SceneId { get { return id; } }
        /// <summary>World point where the fed conduit ends: the outer face of stub A (the stub itself if the prefab has no attach point).</summary>
        public Vector3 AttachA { get { return attachA != null ? attachA.position : (stubA != null ? stubA.position : transform.position); } }
        /// <summary>World point where the onward conduit starts: the outer face of stub B.</summary>
        public Vector3 AttachB { get { return attachB != null ? attachB.position : (stubB != null ? stubB.position : transform.position); } }
        /// <summary>Host-decided: a metal body lies across both stubs.</summary>
        public bool Closed { get { return _closed; } }
        /// <summary>Set by the Circuit: the conduit on this break's near side is live.</summary>
        public bool Powered
        {
            get { return _powered; }
            set { if (_powered == value) return; _powered = value; Refresh(); }
        }
        /// <summary>Set by the Circuit: the whole circuit was closed once; this break has filled itself in for good.</summary>
        public bool Sealed
        {
            get { return _sealed; }
            set { if (_sealed == value) return; _sealed = value; Refresh(); }
        }
        /// <summary>Set by the Circuit: the _Live value its live pieces wear (above 1 while the sealed run pulses).</summary>
        public float Glow
        {
            set { if (Mathf.Abs(_glow - value) < 0.001f) return; _glow = value; Refresh(); }
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            Refresh();
        }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w != null && !_in.Contains(w)) _in.Add(w);
        }

        void OnTriggerExit(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w != null) _in.Remove(w);
        }

        static WeaponBody Weapon(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<WeaponBody>() : null;
        }

        /// <summary>Pure: does this weapon, lying as it does now, span both stubs?</summary>
        public bool Spans(WeaponBody w)
        {
            if (w == null || w.IsBroken || w.Body == null || stubA == null || stubB == null) return false;
            if (metalOnly && (w.Def == null || !w.Def.metal)) return false;
            if (w.Body.linearVelocity.magnitude > restSpeed) return false;
            Bounds b = w.WorldBounds;
            b.Expand(tolerance * 2f);
            return b.Contains(stubA.position) && b.Contains(stubB.position);
        }

        void FixedUpdate()
        {
            Snap();
            bool bridged = false;
            for (int i = _in.Count - 1; i >= 0; i--)
            {
                WeaponBody w = _in[i];
                if (w == null || w.IsBroken) { _in.RemoveAt(i); continue; }
                if (Spans(w)) { bridged = true; break; }
            }
            if (authority != null) authority.ReportCircuitBridge(id, bridged || _sealed);
        }

        /// <summary>The small magnet: every metal weapon this peer simulates within snapRange of the cradle is drawn toward
        /// its centre and turned along it, so a landing that is nearly right becomes right. A body in flight is barely
        /// touched (the pull fades with distance and never lifts); a body that comes to rest a hand off slides in.</summary>
        void Snap()
        {
            if (_sealed || snapPull <= 0f || stubA == null || stubB == null) return;
            Vector3 centre = (stubA.position + stubB.position) * 0.5f;
            Vector3 axis = stubB.position - stubA.position;
            if (axis.sqrMagnitude < 1e-6f) return;
            axis.Normalize();
            int n = Physics.OverlapSphereNonAlloc(centre, snapRange, _near, weaponMask, QueryTriggerInteraction.Ignore);
            _pulled.Clear();
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = _near[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                WeaponBody w = rb.GetComponent<WeaponBody>();
                if (w == null || w.IsBroken || w.Def == null || (metalOnly && !w.Def.metal)) continue;
                if (w.IsHeldAtHome || w.IsCarried || w.IsStuck || w.IsBraced || _pulled.Contains(w)) continue;
                _pulled.Add(w);
                Vector3 to = centre - rb.worldCenterOfMass;
                float d = to.magnitude;
                if (d < 0.02f) continue;
                Vector3 dir = to / d;
                float k = 1f - Mathf.Clamp01(d / snapRange);
                if (Vector3.Dot(rb.linearVelocity, dir) < snapMaxSpeed) rb.AddForce(dir * (snapPull * k), ForceMode.Acceleration);
                // The long axis (local +Z, the blade or the head) along the cradle, whichever end is nearer.
                Vector3 longAxis = rb.transform.forward;
                Vector3 want = Vector3.Dot(longAxis, axis) >= 0f ? axis : -axis;
                Vector3 turn = Vector3.Cross(longAxis, want);
                rb.AddTorque((turn * snapTurn - rb.angularVelocity * 1.5f) * k, ForceMode.Acceleration);
            }
        }

        /// <summary>Authority only.</summary>
        public void ApplyClosed(bool closed)
        {
            if (_closed == closed) return;
            _closed = closed;
            Refresh();
        }

        void Refresh()
        {
            Set(stubRendererA, _powered || _sealed ? _glow : 0f);
            Set(stubRendererB, (_powered && _closed) || _sealed ? _glow : 0f);
            if (fill != null) { fill.enabled = _sealed; if (_sealed) Set(fill, _glow); }
            bool spark = _powered && !_closed && !_sealed;
            if (sparks != null && spark != _sparking)
            {
                _sparking = spark;
                if (spark) sparks.Play(); else sparks.Stop();
            }
        }

        void Set(Renderer r, float live)
        {
            if (r == null || _mpb == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(LiveId, live);
            r.SetPropertyBlock(_mpb);
        }

        void OnDrawGizmosSelected()
        {
            if (stubA == null || stubB == null) return;
            Gizmos.color = new Color(0.66f, 0.27f, 0.95f, 0.9f);
            Gizmos.DrawLine(stubA.position, stubB.position);
            Gizmos.DrawWireSphere(stubA.position, tolerance);
            Gizmos.DrawWireSphere(stubB.position, tolerance);
        }
    }
}
