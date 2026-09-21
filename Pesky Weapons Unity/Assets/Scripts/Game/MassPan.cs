using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One pan of a scales: a kinematic platform that knows how much weapon mass is RESTING on it, and
    /// that can be told to move to a height. It has no id and no host-owned state of its own - the
    /// CounterweightPair or the ScalesLock that owns it does, and calls <see cref="MoveTo"/>.
    /// Riders use the ClockMover / Lift fix (RiderCarry): the walking surface is zero-friction P_Slick and
    /// the pan adds its own delta to every rider once per physics step, then brakes grounded riders so a
    /// weapon settles instead of sliding off. A weapon that has just launched is never braked.
    /// The component sits on the pan ROOT: Trigger layer, kinematic Rigidbody, a trigger BoxCollider that
    /// defines the sensing volume, and a child `Surface` on the World layer that is the thing you stand on.
    /// It must NOT be marked static.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MassPan : MonoBehaviour
    {
        [SerializeField] Rigidbody body;
        [Tooltip("A weapon only counts while it is RESTING: slower than this. A fly-through never weighs anything.")]
        [SerializeField] float restSpeed = 1.5f;
        [Tooltip("The pan surface, tinted to show whether this pan is happy (ScalesLock only).")]
        [SerializeField] Renderer surfaceRenderer;

        [Header("Riders")]
        [Tooltip("Local-space centre of the box that collects riders (just above the surface).")]
        [SerializeField] Vector3 riderBoxCenter = new Vector3(0f, 0.6f, 0f);
        [SerializeField] Vector3 riderBoxSize = new Vector3(2.4f, 1.2f, 2.4f);
        [Tooltip("Weapon | Enemy.")]
        [SerializeField] LayerMask riderMask = (1 << 9) | (1 << 11);
        [Tooltip("Horizontal braking of grounded riders in m/s^2 (stands in for the friction P_Slick does not have). 0 = off.")]
        [SerializeField] float riderBrake = 14f;
        [Tooltip("A rider that launched less than this many seconds ago is not braked.")]
        [SerializeField] float brakeLaunchGrace = 0.75f;

        readonly List<WeaponBody> _on = new List<WeaponBody>();
        readonly Collider[] _hits = new Collider[32];
        readonly List<Rigidbody> _riders = new List<Rigidbody>();
        MaterialPropertyBlock _mpb;
        Vector3 _last;
        float _mass;

        /// <summary>Summed mass of the weapons resting on the pan, refreshed every physics step.</summary>
        public float Mass { get { return _mass; } }
        public float BaseY { get; private set; }
        public Rigidbody Body { get { return body; } }
        public IReadOnlyList<WeaponBody> WeaponsOn { get { return _on; } }

        void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            _mpb = new MaterialPropertyBlock();
            _last = transform.position;
            BaseY = _last.y;
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

        /// <summary>Re-sums the resting mass. The owning puzzle calls this at the top of its FixedUpdate.</summary>
        public float Measure()
        {
            float sum = 0f;
            for (int i = _on.Count - 1; i >= 0; i--)
            {
                WeaponBody w = _on[i];
                if (w == null || w.IsBroken) { _on.RemoveAt(i); continue; }
                if (w.Body == null || w.IsCarried) continue;
                if (w.Body.linearVelocity.magnitude > restSpeed) continue;
                sum += w.Body.mass;
            }
            _mass = sum;
            return sum;
        }

        /// <summary>Move the pan to a world height, carrying whatever is standing on it.</summary>
        public void MoveTo(float worldY)
        {
            if (body == null) return;
            Vector3 target = new Vector3(_last.x, worldY, _last.z);
            Vector3 delta = target - _last;
            _last = target;

            RiderCarry.Collect(transform, riderBoxCenter, riderBoxSize, riderMask, body, _hits, _riders);
            // A step of more than a metre is a snap, not travel: riders are not dragged along it.
            if (delta.sqrMagnitude > 1e-10f && delta.sqrMagnitude < 1f) RiderCarry.Move(_riders, delta);
            if (riderBrake > 0f) BrakeRiders();
            body.MovePosition(target);
        }

        void BrakeRiders()
        {
            float drop = riderBrake * Time.fixedDeltaTime;
            for (int i = 0; i < _riders.Count; i++)
            {
                Rigidbody rb = _riders[i];
                WeaponBody weapon = rb.GetComponent<WeaponBody>();
                if (weapon == null || !weapon.IsGrounded || weapon.SecondsSinceLaunch < brakeLaunchGrace) continue;
                Vector3 v = rb.linearVelocity;
                Vector3 flat = new Vector3(v.x, 0f, v.z);
                float speed = flat.magnitude;
                if (speed < 0.001f) continue;
                flat = speed <= drop ? Vector3.zero : flat * (1f - drop / speed);
                rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
            }
        }

        /// <summary>The owning puzzle tints the pan so you can read it from across the room.</summary>
        public void Tint(Color c)
        {
            if (surfaceRenderer == null || _mpb == null) return;
            surfaceRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            surfaceRenderer.SetPropertyBlock(_mpb);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.47f, 0.59f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(riderBoxCenter, riderBoxSize);
        }
    }
}
