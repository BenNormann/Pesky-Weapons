using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The trigger that IS a room: it knows its room id, the enemies that belong to it, and which weapons
    /// are inside right now. A possessed weapon entering wakes the room's enemies. Souls are on a layer that
    /// does not touch Trigger, so a free soul can never wake a room.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoomVolume : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;
        [SerializeField] string roomName = "Room";
        [Tooltip("The enemies that belong to this room. They only aggro on weapons inside it.")]
        [SerializeField] GoblinBrain[] enemies = new GoblinBrain[0];

        readonly List<WeaponBody> _inside = new List<WeaponBody>();
        bool _woken;

        public int SceneId { get { return id; } }
        public string RoomName { get { return roomName; } }
        public IReadOnlyList<GoblinBrain> Enemies { get { return enemies; } }
        public IReadOnlyList<WeaponBody> WeaponsInside { get { return _inside; } }
        public bool Woken { get { return _woken; } }

        /// <summary>True when every enemy that belongs to this room is dead (or there are none).</summary>
        public bool Cleared
        {
            get
            {
                for (int i = 0; i < enemies.Length; i++)
                    if (enemies[i] != null && !enemies[i].IsDead) return false;
                return true;
            }
        }

        /// <summary>Is this world point inside the room box? Used to keep a wandering goblin at home.</summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return true;
            Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }

        public bool Contains(WeaponBody weapon)
        {
            return weapon != null && _inside.Contains(weapon);
        }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody w = FindWeapon(other);
            if (w == null || _inside.Contains(w)) return;
            _inside.Add(w);
            if (w.IsPossessed) Wake();
        }

        void OnTriggerStay(Collider other)
        {
            WeaponBody w = FindWeapon(other);
            if (w == null) return;
            if (!_inside.Contains(w)) _inside.Add(w);
            if (!_woken && w.IsPossessed) Wake();
        }

        void OnTriggerExit(Collider other)
        {
            WeaponBody w = FindWeapon(other);
            if (w != null) _inside.Remove(w);
        }

        static WeaponBody FindWeapon(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<WeaponBody>() : null;
        }

        /// <summary>Wakes every enemy in the room. Idempotent.</summary>
        public void Wake()
        {
            if (_woken) return;
            _woken = true;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null) enemies[i].Wake();
        }

        void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.24f, 0.86f, 0.94f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
