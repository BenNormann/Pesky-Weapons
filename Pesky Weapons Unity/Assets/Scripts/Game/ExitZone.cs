using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The lit patch of floor in front of ONE doorway of the good-end room. Which corner the good end
    /// lands in and which way its Exit faces are chosen by the seed, so all four doorways of that room
    /// carry one of these and exactly one of them is live in any round.
    ///
    /// The round is won by the HOST, which measures <c>exitGatherRadius</c> from the exit doorway itself.
    /// This is the visible form of that spot: it tells the crew where to stand and shows them arriving.
    /// It decides nothing, and it never touches shared state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ExitZone : MonoBehaviour, ISceneId
    {
        [SerializeField] int id;

        [Tooltip("The doorway this zone belongs to. The zone is live only while that doorway is the Exit.")]
        [SerializeField] MagicDoor doorway;

        [Tooltip("The bright arch and floor ring. Hidden unless this doorway is the Exit.")]
        [SerializeField] GameObject liveVisual;

        [Tooltip("Shown on top of that while at least one weapon stands in the zone.")]
        [SerializeField] GameObject occupiedVisual;

        [Tooltip("Seconds between asking the doorway whether it is the Exit.")]
        [SerializeField] float refreshInterval = 0.25f;

        readonly List<WeaponBody> _inside = new List<WeaponBody>();
        BoxCollider _box;
        float _next;
        bool _live;

        public int SceneId { get { return id; } }

        /// <summary>True while this doorway is the one doorway in the grid that leads out.</summary>
        public bool IsLive { get { return _live; } }

        /// <summary>How many weapons are standing in the zone right now. Souls are on a layer that never touches Trigger.</summary>
        public int Count
        {
            get
            {
                Prune();
                return _inside.Count;
            }
        }

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
            if (_box != null) _box.isTrigger = true;
        }

        void OnEnable()
        {
            _next = 0f;
            _inside.Clear();
        }

        void Update()
        {
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
                _live = doorway != null && doorway.IsExitDoorway;
                if (_box != null && _box.enabled != _live) _box.enabled = _live;
                if (!_live) _inside.Clear();
                if (liveVisual != null && liveVisual.activeSelf != _live) liveVisual.SetActive(_live);
            }

            bool busy = _live && Count > 0;
            if (occupiedVisual != null && occupiedVisual.activeSelf != busy) occupiedVisual.SetActive(busy);
        }

        void Prune()
        {
            for (int i = _inside.Count - 1; i >= 0; i--)
                if (_inside[i] == null) _inside.RemoveAt(i);
        }

        void OnTriggerEnter(Collider other)
        {
            WeaponBody weapon = Find(other);
            if (weapon == null || _inside.Contains(weapon)) return;
            _inside.Add(weapon);
        }

        void OnTriggerExit(Collider other)
        {
            WeaponBody weapon = Find(other);
            if (weapon != null) _inside.Remove(weapon);
        }

        static WeaponBody Find(Collider other)
        {
            if (other == null) return null;
            Rigidbody rb = other.attachedRigidbody;
            return rb != null ? rb.GetComponent<WeaponBody>() : other.GetComponentInParent<WeaponBody>();
        }
    }
}
