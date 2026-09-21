using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Out of combat, holding Possess (E) next to the anvil for chargeSeconds asks the WorldAuthority to
    /// restore the possessed weapon to full HP. Taking damage (or being targeted) cancels it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnvilStation : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [Tooltip("Seconds the Possess button must be held (SLICE-1 section 6: 2 s).")]
        [SerializeField] float chargeSeconds = 2f;

        readonly List<WeaponBody> _inside = new List<WeaponBody>();
        float _charge;

        public int SceneId { get { return id; } }
        public float ChargeSeconds { get { return chargeSeconds; } }
        /// <summary>0..1 for the HUD.</summary>
        public float Progress { get { return chargeSeconds > 0.001f ? Mathf.Clamp01(_charge / chargeSeconds) : 0f; } }
        public WeaponBody CurrentWeapon { get { return FindPossessed(); } }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w == null || _inside.Contains(w)) return;
            _inside.Add(w);
            if (w.IsPossessed && authority != null) authority.SetNearAnvil(this, true);
        }

        void OnTriggerExit(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w == null) return;
            _inside.Remove(w);
            if (FindPossessed() == null)
            {
                _charge = 0f;
                if (authority != null) authority.SetNearAnvil(this, false);
            }
        }

        static WeaponBody Weapon(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<WeaponBody>() : null;
        }

        WeaponBody FindPossessed()
        {
            for (int i = _inside.Count - 1; i >= 0; i--)
            {
                WeaponBody w = _inside[i];
                if (w == null) { _inside.RemoveAt(i); continue; }
                if (w.IsPossessed && !w.IsBroken) return w;
            }
            return null;
        }

        void Update()
        {
            WeaponBody weapon = FindPossessed();
            if (weapon == null)
            {
                _charge = 0f;
                if (authority != null && authority.NearAnvil == this) authority.SetNearAnvil(this, false);
                return;
            }
            if (authority != null) authority.SetNearAnvil(this, true);

            PlayerSoul soul = weapon.Possessor as PlayerSoul;
            bool can = soul != null && !soul.InCombat && soul.PossessHeld && weapon.Hp < weapon.MaxHp;
            if (!can)
            {
                _charge = 0f;
                return;
            }

            _charge += Time.deltaTime;
            if (_charge >= chargeSeconds)
            {
                _charge = 0f;
                if (authority != null) authority.RequestAnvilUse(id, weapon);
            }
        }
    }
}
