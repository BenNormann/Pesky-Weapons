using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// A trigger volume in front of a door. While the possessed weapon is standing in it and the door is
    /// still shut, it reports the door to the WorldAuthority so the HUD can say what the door is waiting
    /// for ("KEY NEEDED"). It is a pure sensor: it never opens anything and never changes shared state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DoorPrompt : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;
        [Tooltip("The door this volume belongs to.")]
        [SerializeField] Door door;

        readonly List<WeaponBody> _inside = new List<WeaponBody>();

        public int SceneId { get { return id; } }
        public Door Door { get { return door; } }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody w = Weapon(other);
            if (w == null || _inside.Contains(w)) return;
            _inside.Add(w);
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

        bool HasPossessedInside()
        {
            for (int i = _inside.Count - 1; i >= 0; i--)
            {
                WeaponBody w = _inside[i];
                if (w == null) { _inside.RemoveAt(i); continue; }
                if (w.IsPossessed && !w.IsBroken) return true;
            }
            return false;
        }

        void Update()
        {
            if (authority == null || door == null) return;
            bool show = !door.IsOpen && HasPossessedInside();
            authority.SetNearDoor(door, show);
        }

        void OnDisable()
        {
            if (authority != null && door != null) authority.SetNearDoor(door, false);
        }

        void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.95f, 0.78f, 0.25f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
