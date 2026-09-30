using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The scene side of the simplified RUN (docs/RUN.md): the start room, the POOL of rooms a run may
    /// string together, and the Exit room. It maps the sim's run (RunState: a sequence of room ids) onto
    /// the authored rooms and answers the two questions everything else asks:
    ///   - where does this doorway lead RIGHT NOW (a room's exit leads to the next room's entry, and back;
    ///     the start room's exit leads to room 0; the last room's exit leads to the Exit room)
    ///   - which room, or which part of the run, is this player in.
    /// Every room has exactly two doorways: ENTRY = north (Heading 0), EXIT = south (Heading 2); the other
    /// two sides are sealed (LabyrinthRoom.sealedSides).
    ///
    /// It plugs into the scene's LabyrinthDirector (its `run` field), which the hundred grid doorways
    /// already reference, so MagicDoor's grid mode, ExitZone and the culling all keep working unchanged.
    /// It changes nothing shared: it reads the sim and answers the host through WorldAuthority's IHostWorld.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunDirector : MonoBehaviour
    {
        public const int Entry = (int)Heading.North;
        public const int Exit = (int)Heading.South;

        [Header("Session")]
        [Tooltip("The scene's SessionRunner. The run sequence lives in its NetSession's WorldSim.")]
        [SerializeField] SessionRunner sessionRunner;

        [Tooltip("The numbers when the session has no GameData (opening the scene straight from the Editor).")]
        [OptionalRef][SerializeField] RunDef fallbackDef;

        [Header("Rooms")]
        [Tooltip("The start room: the rack, the soul spawns, the run's first door on its south side and a sealed placeholder on its north.")]
        [SerializeField] LabyrinthRoom startRoom;

        [Tooltip("The Exit room: its north doorway is the entry from the last room, its south doorway is the lit EXIT.")]
        [SerializeField] LabyrinthRoom exitRoom;

        [Tooltip("The Exit room's RoomVolume box: the crew wins when every non-Mage player is inside it at once.")]
        [SerializeField] BoxCollider exitVolume;

        [Tooltip("Every room a run may pick, in any order. Never the start or Exit room. Each carries its own room id.")]
        [SerializeField] LabyrinthRoom[] pool = new LabyrinthRoom[0];

        NetSession _session;
        readonly Dictionary<int, LabyrinthRoom> _byRoomId = new Dictionary<int, LabyrinthRoom>();

        /// <summary>The run half of the sim, or null before a session exists.</summary>
        public RunState Run
        {
            get { return _session != null && _session.Sim != null ? _session.Sim.Run : null; }
        }

        public RunDef Def
        {
            get
            {
                GameData data = _session != null ? _session.Data : null;
                if (data != null && data.run != null) return data.run;
                return fallbackDef;
            }
        }

        public LabyrinthRoom StartRoom { get { return startRoom; } }
        public LabyrinthRoom ExitRoom { get { return exitRoom; } }
        public IReadOnlyList<LabyrinthRoom> Pool { get { return pool; } }

        void Awake()
        {
            _session = sessionRunner != null ? sessionRunner.Session : null;
            if (_session == null) Debug.LogError("RunDirector has no SessionRunner: no run, no doorways.", this);
            _byRoomId.Clear();
            for (int i = 0; i < pool.Length; i++)
            {
                LabyrinthRoom room = pool[i];
                if (room == null) continue;
                if (_byRoomId.ContainsKey(room.RoomId))
                {
                    Debug.LogError("Two pool rooms share room id " + room.RoomId + ".", room);
                    continue;
                }
                _byRoomId.Add(room.RoomId, room);
            }
            if (startRoom == null || exitRoom == null) Debug.LogError("RunDirector needs a start room and an Exit room.", this);
        }

        // ---------------------------------------------------------------- the chain

        /// <summary>The pool room at position k of the run, or null.</summary>
        public LabyrinthRoom RoomAtIndex(int index)
        {
            RunState run = Run;
            if (run == null) return null;
            int id = run.RoomAt(index);
            LabyrinthRoom room;
            return id >= 0 && _byRoomId.TryGetValue(id, out room) ? room : null;
        }

        static MagicDoor EntryOf(LabyrinthRoom room) { return room != null ? room.Doorway(Entry) : null; }
        static MagicDoor ExitOf(LabyrinthRoom room) { return room != null ? room.Doorway(Exit) : null; }

        /// <summary>
        /// Where a doorway leads right now, resolved from the run sequence at the moment it is asked. Null
        /// for the start room's sealed north side, for the Exit room's south side (the Exit itself), for a
        /// pool room that is not in this run, and before RUN_LAYOUT has arrived.
        /// </summary>
        public MagicDoor ResolveTwin(MagicDoor door)
        {
            if (door == null || door.Room == null) return null;
            RunState run = Run;
            if (run == null || run.Count == 0) return null;
            LabyrinthRoom room = door.Room;
            int dir = door.DoorwayDir;

            if (room == startRoom) return dir == Exit ? EntryOf(RoomAtIndex(0)) : null;
            if (room == exitRoom) return dir == Entry ? ExitOf(RoomAtIndex(run.Count - 1)) : null;

            int k = run.IndexOf(room.RoomId);
            if (k < 0) return null;
            if (dir == Entry) return k == 0 ? ExitOf(startRoom) : ExitOf(RoomAtIndex(k - 1));
            if (dir == Exit) return k == run.Count - 1 ? EntryOf(exitRoom) : EntryOf(RoomAtIndex(k + 1));
            return null;
        }

        /// <summary>The one doorway that leads out: the Exit room's south side. Lit by its ExitZone.</summary>
        public bool IsExit(MagicDoor door)
        {
            return door != null && exitRoom != null && door.Room == exitRoom && door.DoorwayDir == Exit;
        }

        /// <summary>What a doorway leads to, for a label: the next room's name, "START", "EXIT", or "" for nowhere.</summary>
        public string DestinationLabel(MagicDoor door)
        {
            if (IsExit(door)) return "EXIT";
            MagicDoor twin = ResolveTwin(door);
            if (twin == null || twin.Room == null) return "";
            if (twin.Room == startRoom) return "START";
            LabyrinthDirector lab = door.Labyrinth;
            LabyrinthDef def = lab != null ? lab.Def : null;
            return def != null ? def.LabelOf(twin.Room.RoomId) : "Room " + twin.Room.RoomId;
        }

        // ---------------------------------------------------------------- places

        /// <summary>Which room of the scene a world point is in, or null.</summary>
        public LabyrinthRoom RoomAt(Vector3 world)
        {
            if (startRoom != null && startRoom.Contains(world)) return startRoom;
            if (exitRoom != null && exitRoom.Contains(world)) return exitRoom;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null && pool[i].Contains(world)) return pool[i];
            return null;
        }

        /// <summary>Inside the Exit room's volume box (not merely its footprint): what the crew's win is measured by.</summary>
        public bool InExitVolume(Vector3 world)
        {
            if (exitVolume == null) return exitRoom != null && exitRoom.Contains(world);
            Vector3 local = exitVolume.transform.InverseTransformPoint(world) - exitVolume.center;
            Vector3 half = exitVolume.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>Where a world point is in the run: the start room, one of this run's rooms, the Exit volume, or nowhere.</summary>
        public RunPlace PlaceAt(Vector3 world)
        {
            if (InExitVolume(world)) return RunPlace.Exit;
            LabyrinthRoom room = RoomAt(world);
            if (room == null) return RunPlace.Unknown;
            if (room == startRoom) return RunPlace.Start;
            if (room == exitRoom) return RunPlace.Rooms;
            RunState run = Run;
            return run != null && run.IndexOf(room.RoomId) >= 0 ? RunPlace.Rooms : RunPlace.Unknown;
        }

        // ---------------------------------------------------------------- the host's questions

        /// <summary>Host only, through WorldAuthority's IHostWorld: the pool's room ids.</summary>
        public bool CollectRunPool(List<ushort> into)
        {
            if (into == null) return false;
            into.Clear();
            for (int i = 0; i < pool.Length; i++)
            {
                LabyrinthRoom room = pool[i];
                if (room == null || room == startRoom || room == exitRoom) continue;
                int id = room.RoomId;
                if (id < 0 || id > ushort.MaxValue) continue;
                into.Add((ushort)id);
            }
            return into.Count > 0;
        }

        /// <summary>
        /// Host only: where every player is, from the sim's player rows, which every peer streams, so one
        /// implementation answers for local and remote players alike.
        /// </summary>
        public bool CollectRunPlaces(RunPlace[] placeBySlot)
        {
            if (placeBySlot == null || _session == null || _session.Sim == null) return false;
            PlayerTable players = _session.Sim.Players;
            int count = placeBySlot.Length < players.Count ? placeBySlot.Length : players.Count;
            for (int i = 0; i < count; i++)
            {
                placeBySlot[i] = RunPlace.Unknown;
                PlayerState p = players[i];
                if (p == null || !p.present || p.poseCount == 0) continue;
                placeBySlot[i] = PlaceAt(p.pos);
            }
            return true;
        }
    }
}
