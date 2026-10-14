using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A magic rune in the floor that throws any weapon touching it along a fixed arc: up by upSpeed, along the
    /// pad's forward by forwardSpeed. Every weapon is welcome - bladed, blunt, metal, wooden - which makes it the
    /// way up to a ledge that a launch cannot reach, or across a gap in one go.
    /// No shared state: like the MagnetZone it acts on every body THIS peer simulates (a non-kinematic
    /// Rigidbody), so the flight is the same on every peer without a message. The throw is a WeaponBody.Fling:
    /// it frees a braced, stuck or racked body and counts as a launch (a magnet lets it go, the blade aligns).
    /// The BoxCollider (trigger, Trigger layer) is the sensing volume; weapons are found with an overlap query
    /// every physics step. `rune` (optional, an M_MagicFlow renderer) flares for a moment on every throw.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LaunchPad : MonoBehaviour
    {
        [SerializeField] BoxCollider volume;
        [Header("Throw")]
        [Tooltip("Vertical speed in m/s. Gravity is 20: 14 m/s climbs about 4.9 m, 18 m/s about 8 m.")]
        [SerializeField] float upSpeed = 14f;
        [Tooltip("Speed along the pad's forward (its blue arrow) in m/s. 0 = straight up.")]
        [SerializeField] float forwardSpeed = 0f;
        [Tooltip("Seconds before the same weapon can be thrown again, so one landing is one throw.")]
        [SerializeField] float rearmSeconds = 0.8f;
        [Tooltip("A weapon launched by its player less than this long ago is left alone: a launch across the pad flies its own arc.")]
        [SerializeField] float ignoreAfterLaunchSeconds = 0.25f;
        [Tooltip("Weapon layer.")]
        [SerializeField] LayerMask weaponMask = 1 << 9;

        [Header("Look")]
        [Tooltip("The glowing rune (M_MagicFlow). It flares on every throw.")]
        [OptionalRef][SerializeField] Renderer rune;
        [SerializeField] float idleLive = 1f;
        [SerializeField] float flareLive = 2.6f;
        [SerializeField] float flareSeconds = 0.35f;

        static readonly int LiveId = Shader.PropertyToID("_Live");
        readonly Collider[] _hits = new Collider[32];
        readonly List<WeaponBody> _seen = new List<WeaponBody>();
        readonly Dictionary<WeaponBody, float> _rearmAt = new Dictionary<WeaponBody, float>();
        MaterialPropertyBlock _mpb;
        float _flareUntil = -1f;
        float _shownLive = -1f;

        /// <summary>The velocity a weapon leaves the pad with. Pure.</summary>
        public Vector3 LaunchVelocity { get { return Vector3.up * upSpeed + Flat(transform.forward) * forwardSpeed; } }

        void Reset()
        {
            volume = GetComponent<BoxCollider>();
            if (volume != null) volume.isTrigger = true;
        }

        void Awake()
        {
            if (volume == null) volume = GetComponent<BoxCollider>();
            _mpb = new MaterialPropertyBlock();
            ShowLive(idleLive);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        void FixedUpdate()
        {
            if (volume == null) return;
            Vector3 centre = transform.TransformPoint(volume.center);
            Vector3 half = Vector3.Scale(volume.size, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(centre, half, _hits, transform.rotation, weaponMask, QueryTriggerInteraction.Ignore);

            _seen.Clear();
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = _hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;                    // another peer simulates it
                WeaponBody w = rb.GetComponent<WeaponBody>();
                if (w == null || w.IsBroken || w.IsCarried || _seen.Contains(w)) continue;
                _seen.Add(w);
                float rearm;
                if (_rearmAt.TryGetValue(w, out rearm) && Time.time < rearm) continue;
                if (w.SecondsSinceLaunch < ignoreAfterLaunchSeconds) continue;
                w.Fling(LaunchVelocity);
                _rearmAt[w] = Time.time + rearmSeconds;
                _flareUntil = Time.time + flareSeconds;
            }
        }

        void Update()
        {
            if (rune == null) return;
            float k = _flareUntil > Time.time ? Mathf.Clamp01((_flareUntil - Time.time) / Mathf.Max(0.01f, flareSeconds)) : 0f;
            ShowLive(Mathf.Lerp(idleLive, flareLive, k));
        }

        void ShowLive(float live)
        {
            if (rune == null || _mpb == null || Mathf.Approximately(live, _shownLive)) return;
            _shownLive = live;
            rune.GetPropertyBlock(_mpb);
            _mpb.SetFloat(LiveId, live);
            rune.SetPropertyBlock(_mpb);
        }

        void OnDrawGizmosSelected()
        {
            // The flight from the pad's centre, drawn for one second.
            Gizmos.color = new Color(0.7f, 0.5f, 1f, 0.9f);
            Vector3 p = transform.position + Vector3.up * 0.2f;
            Vector3 v = LaunchVelocity;
            Vector3 g = Physics.gravity.sqrMagnitude > 0.01f ? Physics.gravity : new Vector3(0f, -20f, 0f);
            const float dt = 0.05f;
            for (int i = 0; i < 40; i++)
            {
                Vector3 next = p + v * dt + 0.5f * g * dt * dt;
                Gizmos.DrawLine(p, next);
                v += g * dt;
                p = next;
            }
        }
    }
}
