using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The tower stacks many rooms on one axis, so only a few may be visible at a time.
    /// This enables the Geometry and Lighting groups of the room the LOCAL player is standing in
    /// and of its door-linked neighbours, and disables the rest. Gameplay and Spawns groups are
    /// never touched: weapons, goblins, doors, keys and spawn points stay live everywhere.
    ///
    /// It is driven by the serialized list below: each entry names a room's RoomVolume (the test
    /// for "the player is here"), its two groups, and the scene ids of the rooms it is door-linked
    /// to. A magic-door traversal re-evaluates at once; otherwise the local player's point is
    /// tested against the volumes every <see cref="checkInterval"/> seconds.
    /// Purely a view: it changes no shared state, so it can run per client unchanged.
    /// </summary>
    public sealed class FloorActivator : MonoBehaviour
    {
        [Serializable]
        public sealed class Floor
        {
            [Tooltip("The room's RoomVolume; its trigger box is the 'player is in this room' test.")]
            [SerializeField] RoomVolume room;
            [Tooltip("The room's Geometry group.")]
            [SerializeField] GameObject geometry;
            [Tooltip("The room's Lighting group.")]
            [SerializeField] GameObject lighting;
            [Tooltip("Scene ids of the rooms this one is door-linked to (magic doors and plain doorways).")]
            [SerializeField] int[] neighbours = new int[0];

            public RoomVolume Room { get { return room; } }
            public GameObject Geometry { get { return geometry; } }
            public GameObject Lighting { get { return lighting; } }
            public int[] Neighbours { get { return neighbours; } }
        }

        [Tooltip("Where the local soul comes from.")]
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("Listened to for magic-door traversals.")]
        [SerializeField] WorldAuthority authority;
        [SerializeField] Floor[] floors = new Floor[0];
        [Tooltip("Seconds between room tests. A magic-door traversal always re-tests at once.")]
        [SerializeField] float checkInterval = 0.2f;
        [Tooltip("Off = every room stays enabled (useful while editing).")]
        [SerializeField] bool cullFloors = true;

        readonly Dictionary<int, int> _indexById = new Dictionary<int, int>();
        readonly HashSet<int> _wanted = new HashSet<int>();
        int _current = -1;
        float _nextCheck;
        bool _appliedOnce;

        public int CurrentFloor { get { return _current; } }
        public int FloorCount { get { return floors != null ? floors.Length : 0; } }

        void Awake()
        {
            for (int i = 0; i < floors.Length; i++)
            {
                Floor f = floors[i];
                if (f == null || f.Room == null) continue;
                int id = f.Room.SceneId;
                if (!_indexById.ContainsKey(id)) _indexById.Add(id, i);
            }
        }

        void OnEnable()
        {
            if (authority != null) authority.MagicDoorTraversed += OnTraversed;
        }

        void OnDisable()
        {
            if (authority != null) authority.MagicDoorTraversed -= OnTraversed;
        }

        void Start()
        {
            Refresh();
        }

        void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);
            Refresh();
        }

        void OnTraversed(MagicDoorTraversal t)
        {
            // The traveller has already been moved, so the new position is readable straight away.
            Refresh();
        }

        /// <summary>Re-test the local player and apply. Safe to call by hand.</summary>
        public void Refresh()
        {
            if (!cullFloors)
            {
                if (!_appliedOnce) return;
                ShowEverything();
                return;
            }

            int found = FindFloor(LocalPoint());
            if (found < 0) found = _current;      // between rooms (a hallway, a shaft): keep what is on
            if (found < 0) return;                // nothing known yet: leave the scene as authored
            if (found == _current && _appliedOnce && !HasRemotePlayers()) return;
            _current = found;
            Apply();
        }

        Vector3 LocalPoint()
        {
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            if (soul == null) return new Vector3(float.NaN, float.NaN, float.NaN);
            WeaponBody weapon = soul.Weapon;
            return weapon != null ? weapon.transform.position : soul.transform.position;
        }

        int FindFloor(Vector3 point)
        {
            if (float.IsNaN(point.x)) return -1;
            for (int i = 0; i < floors.Length; i++)
            {
                Floor f = floors[i];
                if (f == null || f.Room == null) continue;
                if (f.Room.ContainsPoint(point)) return i;
            }
            return -1;
        }

        void Apply()
        {
            _wanted.Clear();
            _wanted.Add(_current);
            AddRemoteFloors();
            Floor here = floors[_current];
            int[] neighbours = here != null ? here.Neighbours : null;
            if (neighbours != null)
            {
                for (int n = 0; n < neighbours.Length; n++)
                {
                    int index;
                    if (_indexById.TryGetValue(neighbours[n], out index)) _wanted.Add(index);
                }
            }

            for (int i = 0; i < floors.Length; i++)
            {
                Floor f = floors[i];
                if (f == null) continue;
                bool on = _wanted.Contains(i);
                if (f.Geometry != null && f.Geometry.activeSelf != on) f.Geometry.SetActive(on);
                if (f.Lighting != null && f.Lighting.activeSelf != on) f.Lighting.SetActive(on);
            }
            _appliedOnce = true;
        }

        readonly List<Vector3> _remotePoints = new List<Vector3>(8);

        bool HasRemotePlayers()
        {
            return authority != null && authority.CopyRemotePoints(_remotePoints) > 0;
        }

        /// <summary>Rooms other players stand in stay live too: on the host their floors carry goblins, sight lines and loose weapons.</summary>
        void AddRemoteFloors()
        {
            if (authority == null) return;
            int n = authority.CopyRemotePoints(_remotePoints);
            for (int i = 0; i < n; i++)
            {
                int floor = FindFloor(_remotePoints[i]);
                if (floor >= 0) _wanted.Add(floor);
            }
        }

        void ShowEverything()
        {
            for (int i = 0; i < floors.Length; i++)
            {
                Floor f = floors[i];
                if (f == null) continue;
                if (f.Geometry != null && !f.Geometry.activeSelf) f.Geometry.SetActive(true);
                if (f.Lighting != null && !f.Lighting.activeSelf) f.Lighting.SetActive(true);
            }
            _appliedOnce = false;
            _current = -1;
        }
    }
}
