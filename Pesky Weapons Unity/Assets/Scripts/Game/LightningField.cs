using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The open roof of the Storm Room. Lightning falls on a schedule that is a pure function of
    /// LevelClock.Ms (period + phase), so every client sees the same strike at the same moment without a
    /// message. One second before each strike the ground under the victim glows: that is the telegraph.
    ///
    /// The bolt only ever hits the HIGHEST METAL weapon inside the field - possessed or loose, it makes
    /// no difference, which is what makes "leave a metal weapon standing as the lightning rod" work.
    /// The wooden Staff, the Banana and the Orb are never hit, so crossing as the Staff is the other
    /// answer. With no metal weapon in the field nothing happens at all.
    /// Damage goes through WorldAuthority.RequestLightningStrike.
    /// Sits on a Trigger-layer object with a trigger BoxCollider that is the field.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LightningField : MonoBehaviour, ISceneId
    {
        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [SerializeField] LevelClock clock;

        [Header("Schedule (pure function of the clock)")]
        [Tooltip("Seconds between strikes.")]
        [SerializeField] float periodSeconds = 6f;
        [Tooltip("Seconds the whole schedule is shifted by, so two fields can be out of step.")]
        [SerializeField] float phaseSeconds;
        [Tooltip("Seconds of ground glow before the strike lands.")]
        [SerializeField] float telegraphSeconds = 1f;

        [Header("Strike")]
        [SerializeField] float strikeDamage = 40f;
        [Tooltip("Seconds the bolt is drawn for.")]
        [SerializeField] float boltSeconds = 0.25f;
        [Tooltip("Height above the field the bolt comes down from, in metres above the field's top face.")]
        [SerializeField] float boltHeight = 6f;

        [Header("Look")]
        [Tooltip("Flat disc laid on the ground under the next victim.")]
        [SerializeField] Transform glow;
        [Tooltip("Thin vertical bar scaled between the sky and the victim.")]
        [SerializeField] Transform bolt;

        readonly List<WeaponBody> _inside = new List<WeaponBody>();
        BoxCollider _box;
        long _lastCycle = long.MinValue;
        float _boltUntil = -999f;

        public int SceneId { get { return id; } }
        public float PeriodSeconds { get { return periodSeconds; } }
        public float TelegraphSeconds { get { return telegraphSeconds; } }
        public float StrikeDamage { get { return strikeDamage; } }
        public IReadOnlyList<WeaponBody> WeaponsInside { get { return _inside; } }

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
            if (glow != null) glow.gameObject.SetActive(false);
            if (bolt != null) bolt.gameObject.SetActive(false);
        }

        void Start()
        {
            _lastCycle = CycleAt(clock != null ? clock.Seconds : 0.0);
        }

        long CycleAt(double seconds)
        {
            double period = Mathf.Max(0.05f, periodSeconds);
            return (long)Math.Floor((seconds - phaseSeconds) / period);
        }

        double IntoCycle(double seconds)
        {
            double period = Mathf.Max(0.05f, periodSeconds);
            double t = (seconds - phaseSeconds) - CycleAt(seconds) * period;
            return t;
        }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w != null && !_inside.Contains(w)) _inside.Add(w);
        }

        void OnTriggerExit(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w != null) _inside.Remove(w);
        }

        static WeaponBody Weapon(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<WeaponBody>() : null;
        }

        /// <summary>The highest METAL weapon in the field right now, or null. Possessed or loose, it makes no difference.</summary>
        public WeaponBody HighestMetal()
        {
            WeaponBody best = null;
            float bestY = float.NegativeInfinity;
            for (int i = _inside.Count - 1; i >= 0; i--)
            {
                WeaponBody w = _inside[i];
                if (w == null) { _inside.RemoveAt(i); continue; }
                if (w.IsBroken || w.Body == null || w.Def == null || !w.Def.metal) continue;
                float y = w.Body.worldCenterOfMass.y;
                if (y > bestY) { bestY = y; best = w; }
            }
            return best;
        }

        void Update()
        {
            if (clock == null) return;
            double seconds = clock.Seconds;
            long cycle = CycleAt(seconds);
            double into = IntoCycle(seconds);
            double untilStrike = Mathf.Max(0.05f, periodSeconds) - into;

            WeaponBody target = HighestMetal();
            bool telegraphing = target != null && untilStrike <= telegraphSeconds;
            ShowGlow(telegraphing, target);

            if (_lastCycle == long.MinValue) { _lastCycle = cycle; return; }
            if (cycle > _lastCycle)
            {
                _lastCycle = cycle;
                Strike(HighestMetal());
            }

            if (bolt != null && bolt.gameObject.activeSelf && Time.time > _boltUntil) bolt.gameObject.SetActive(false);
        }

        void Strike(WeaponBody target)
        {
            if (target == null || authority == null) return;
            Vector3 point = target.Body != null ? target.Body.worldCenterOfMass : target.transform.position;
            if (!authority.RequestLightningStrike(id, target)) return;
            ShowBolt(point);
        }

        void ShowGlow(bool on, WeaponBody target)
        {
            if (glow == null) return;
            if (glow.gameObject.activeSelf != on) glow.gameObject.SetActive(on);
            if (!on || target == null || target.Body == null) return;
            Vector3 p = target.Body.worldCenterOfMass;
            float floorY = _box != null ? _box.bounds.min.y : transform.position.y;
            glow.position = new Vector3(p.x, floorY + 0.03f, p.z);
        }

        /// <summary>View only: the bolt is drawn on the authority's event, so remote clients draw it too.</summary>
        public void ShowBolt(Vector3 point)
        {
            if (bolt == null) return;
            float top = point.y + boltHeight;
            float half = (top - point.y) * 0.5f;
            bolt.position = new Vector3(point.x, point.y + half, point.z);
            Vector3 s = bolt.localScale;
            bolt.localScale = new Vector3(s.x, Mathf.Max(0.05f, half), s.z);
            bolt.gameObject.SetActive(true);
            _boltUntil = Time.time + boltSeconds;
        }

        void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.98f, 0.92f, 0.40f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
