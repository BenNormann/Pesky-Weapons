using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The one thing in the scene that knows the mapping between the SIM's labyrinth (a table of cell to
    /// room id, which the host owns) and the SCENE's labyrinth (authored rooms with four doorways each).
    /// Everything that wants to ask "where does this doorway lead now" or "which cell is this player in"
    /// asks here, and this asks the sim, so there is one answer on every peer.
    ///
    /// Lives under _Managers. It changes nothing shared: it only reads the grid and reports positions back
    /// to the host through WorldAuthority's IHostWorld.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabyrinthDirector : MonoBehaviour
    {
        [Header("Session")]
        [Tooltip("The scene's SessionRunner. The grid lives in its NetSession's WorldSim.")]
        [SerializeField] SessionRunner sessionRunner;

        [Tooltip("Labels and thresholds when the session has no GameData (opening the scene straight from the Editor).")]
        [OptionalRef][SerializeField] LabyrinthDef fallbackDef;

        [Header("Rooms")]
        [Tooltip("Every authored room. Order does not matter: each one carries its own room id.")]
        [SerializeField] LabyrinthRoom[] rooms = new LabyrinthRoom[0];

        [Header("Culling")]
        [Tooltip("A room whose anchor is farther than this from the local player has its renderers and lights switched off. Rooms sit 200 m apart on the lattice (120 in the tutorial), so this keeps exactly the room the player is in drawn. Nothing else in a room is touched: doorways, colliders, physics and loose weapons keep working everywhere, and a weapon parked in a room is never culled with it.")]
        [SerializeField] float cullDistance = 150f;
        [Tooltip("Off = every room draws all the time, the way it did before feedback round 5 (a hundred lights and fourteen hundred renderers in the frustum).")]
        [SerializeField] bool cullFarRooms = true;
        [Tooltip("The local player's body, read live for the culling so the room a doorway just put the player in is drawn on the same frame. Without it the culling follows the sim's pose row, which trails the body by up to a POSE interval.")]
        [OptionalRef][SerializeField] PlayerSpawner spawner;

        Renderer[][] _roomRenderers;
        Light[][] _roomLights;
        bool[] _roomDrawn;

        readonly Dictionary<int, LabyrinthRoom> _byRoomId = new Dictionary<int, LabyrinthRoom>();
        NetSession _session;
        int _localCell = LabyrinthGrid.NoCell;

        /// <summary>The grid table, or null before a session and its layout exist.</summary>
        public LabyrinthGrid Grid
        {
            get { return _session != null && _session.Sim != null ? _session.Sim.Labyrinth.Grid : null; }
        }

        /// <summary>The labyrinth half of the sim: legend, downed players, the round's outcome, this peer's own compass.</summary>
        public LabyrinthState Lab
        {
            get { return _session != null && _session.Sim != null ? _session.Sim.Labyrinth : null; }
        }

        /// <summary>The data the labels and thresholds come from.</summary>
        public LabyrinthDef Def
        {
            get
            {
                GameData data = _session != null ? _session.Data : null;
                if (data != null && data.labyrinth != null) return data.labyrinth;
                return fallbackDef;
            }
        }

        /// <summary>Which cell the local player is standing in, or NoCell. Refreshed once a frame.</summary>
        public int LocalCell { get { return _localCell; } }

        public IReadOnlyList<LabyrinthRoom> Rooms { get { return rooms; } }

        void Awake()
        {
            _session = sessionRunner != null ? sessionRunner.Session : null;
            _byRoomId.Clear();
            for (int i = 0; i < rooms.Length; i++)
            {
                LabyrinthRoom room = rooms[i];
                if (room == null) continue;
                if (_byRoomId.ContainsKey(room.RoomId))
                {
                    Debug.LogError("Two labyrinth rooms share room id " + room.RoomId + ".", room);
                    continue;
                }
                _byRoomId.Add(room.RoomId, room);
            }
            if (_session == null)
                Debug.LogError("LabyrinthDirector has no SessionRunner: no grid, no doorways, no compass.", this);
            BuildCullTables();
        }

        /// <summary>What each room owns that can be switched off when the player is far away: its renderers (not a traveller's) and its lights.</summary>
        void BuildCullTables()
        {
            _roomRenderers = new Renderer[rooms.Length][];
            _roomLights = new Light[rooms.Length][];
            _roomDrawn = new bool[rooms.Length];
            List<Renderer> keep = new List<Renderer>(128);
            for (int i = 0; i < rooms.Length; i++)
            {
                _roomDrawn[i] = true;
                LabyrinthRoom room = rooms[i];
                if (room == null)
                {
                    _roomRenderers[i] = new Renderer[0];
                    _roomLights[i] = new Light[0];
                    continue;
                }
                keep.Clear();
                Renderer[] all = room.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < all.Length; r++)
                {
                    // A weapon parked in a room is a traveller, not furniture: it leaves with a player and is never culled with the room.
                    if (all[r].GetComponentInParent<Rigidbody>() != null) continue;
                    keep.Add(all[r]);
                }
                _roomRenderers[i] = keep.ToArray();
                _roomLights[i] = room.GetComponentsInChildren<Light>(true);
            }
        }

void Update()
        {
            Vector3 pos;
            bool has = TryLocalPosition(out pos);
            _localCell = has ? CellAt(pos) : LabyrinthGrid.NoCell;
            if (!cullFarRooms) return;
            // The live body, when there is one: a MagicDoor moves it in FixedUpdate and the sim's row only
            // learns of it with the next POSE, so the culling must not wait for the row.
            Vector3 livePos;
            if (TryLiveLocalPosition(out livePos)) RefreshCulling(true, livePos);
            else RefreshCulling(has, pos);
        }

        /// <summary>The local player's body as the scene has it right now: the possessed weapon, else the soul. False without a spawner or a soul.</summary>
        bool TryLiveLocalPosition(out Vector3 pos)
        {
            pos = Vector3.zero;
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            if (soul == null) return false;
            WeaponBody weapon = soul.Weapon;
            if (weapon != null && weapon.Body != null) pos = weapon.Body.position;
            else if (soul.Body != null) pos = soul.Body.position;
            else pos = soul.transform.position;
            return true;
        }

        /// <summary>
        /// Draws the rooms near the local player and nothing else. Twenty-five distance checks a frame,
        /// and a room's renderers and lights are only touched when its state flips. With no local pose yet
        /// (before the first POSE) everything is drawn.
        /// </summary>
        void RefreshCulling(bool hasPosition, Vector3 pos)
        {
            if (_roomDrawn == null || _roomDrawn.Length != rooms.Length) return;
            float limitSq = cullDistance * cullDistance;
            for (int i = 0; i < rooms.Length; i++)
            {
                LabyrinthRoom room = rooms[i];
                if (room == null) continue;
                bool drawn = !hasPosition || (room.Anchor.position - pos).sqrMagnitude <= limitSq;
                if (drawn == _roomDrawn[i]) continue;
                _roomDrawn[i] = drawn;
                Renderer[] rs = _roomRenderers[i];
                for (int r = 0; r < rs.Length; r++) if (rs[r] != null) rs[r].enabled = drawn;
                Light[] ls = _roomLights[i];
                for (int l = 0; l < ls.Length; l++) if (ls[l] != null) ls[l].enabled = drawn;
            }
        }

        // ---------------------------------------------------------------- rooms and cells

        public LabyrinthRoom RoomOfId(int roomId)
        {
            LabyrinthRoom room;
            return _byRoomId.TryGetValue(roomId, out room) ? room : null;
        }

        /// <summary>The room that currently sits in a cell.</summary>
        public LabyrinthRoom RoomInCell(int cell)
        {
            LabyrinthGrid g = Grid;
            if (g == null || !g.InRange(cell)) return null;
            return RoomOfId(g.RoomAt(cell));
        }

        /// <summary>The cell a room currently sits in, or NoCell.</summary>
        public int CellOfRoom(LabyrinthRoom room)
        {
            LabyrinthGrid g = Grid;
            if (g == null || room == null) return LabyrinthGrid.NoCell;
            return g.CellOfRoom(room.RoomId);
        }

        /// <summary>Which cell a world point is in, or NoCell when it is in no room's footprint.</summary>
        public int CellAt(Vector3 world)
        {
            LabyrinthGrid g = Grid;
            if (g == null) return LabyrinthGrid.NoCell;
            for (int i = 0; i < rooms.Length; i++)
            {
                LabyrinthRoom room = rooms[i];
                if (room == null || !room.Contains(world)) continue;
                return g.CellOfRoom(room.RoomId);
            }
            return LabyrinthGrid.NoCell;
        }

        /// <summary>One of the four doorways of whatever room is in a cell.</summary>
        public MagicDoor DoorwayOf(int cell, int dir)
        {
            LabyrinthRoom room = RoomInCell(cell);
            return room != null ? room.Doorway(dir) : null;
        }

        /// <summary>The Exit: the one doorway of the good-end room that leads out of the labyrinth.</summary>
        public MagicDoor ExitDoorway
        {
            get
            {
                LabyrinthGrid g = Grid;
                if (g == null) return null;
                return DoorwayOf(g.GoodEndCell, (int)g.ExitDir);
            }
        }

        /// <summary>The local player's body, as the sim has it. False when this peer has no slot or no pose yet.</summary>
        public bool TryLocalPosition(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (_session == null || _session.Sim == null || !_session.Slots.HasLocalSlot) return false;
            PlayerState p = _session.Sim.Players[_session.LocalSlot];
            if (p == null || !p.present || p.poseCount == 0) return false;
            pos = p.pos;
            return true;
        }

        // ---------------------------------------------------------------- doorways

        /// <summary>
        /// The doorway a grid doorway currently leads to, resolved NOW: the neighbouring cell in the
        /// doorway's direction, the room the table puts there, and that room's opposite doorway. Null for
        /// the Exit, and null while the labyrinth is not built.
        /// </summary>
        public MagicDoor ResolveTwin(MagicDoor door)
        {
            int cell = DestinationCell(door);
            if (cell == LabyrinthGrid.NoCell) return null;
            LabyrinthRoom room = RoomInCell(cell);
            return room != null ? room.Doorway(LabyrinthGrid.Opposite(door.DoorwayDir)) : null;
        }

        /// <summary>Which cell a grid doorway leads to right now, or NoCell for the Exit (and for anything unresolvable).</summary>
        public int DestinationCell(MagicDoor door)
        {
            LabyrinthGrid g = Grid;
            if (g == null || door == null || door.Room == null) return LabyrinthGrid.NoCell;
            int cell = g.CellOfRoom(door.Room.RoomId);
            if (cell == LabyrinthGrid.NoCell) return LabyrinthGrid.NoCell;
            return g.Neighbour(cell, door.DoorwayDir);
        }

        /// <summary>True for the one doorway in the grid that leads out instead of next door.</summary>
        public bool IsExit(MagicDoor door)
        {
            LabyrinthGrid g = Grid;
            if (g == null || door == null || door.Room == null) return false;
            int cell = g.CellOfRoom(door.Room.RoomId);
            return cell != LabyrinthGrid.NoCell && g.IsExitDoorway(cell, door.DoorwayDir);
        }

        /// <summary>The truthful name of whatever a doorway leads to right now. Glyphs never lie; compasses do.</summary>
        public string DestinationLabel(MagicDoor door)
        {
            if (IsExit(door)) return "EXIT";
            LabyrinthGrid g = Grid;
            int cell = DestinationCell(door);
            if (g == null || cell == LabyrinthGrid.NoCell) return "";
            LabyrinthDef def = Def;
            int roomId = g.RoomAt(cell);
            return def != null ? def.LabelOf(roomId) : "Room " + roomId;
        }

        public string DestinationGlyph(MagicDoor door)
        {
            if (IsExit(door)) return "!";
            LabyrinthGrid g = Grid;
            int cell = DestinationCell(door);
            if (g == null || cell == LabyrinthGrid.NoCell) return "";
            LabyrinthDef def = Def;
            return def != null ? def.GlyphOf(g.RoomAt(cell)) : "?";
        }

        // ---------------------------------------------------------------- the host's questions

        /// <summary>
        /// Host only, through WorldAuthority's IHostWorld: where every player is. Positions come from the
        /// sim's player rows, which every peer streams, so one implementation answers for local and remote
        /// players alike.
        /// </summary>
        public bool CollectPlayerCells(int[] cellBySlot, bool[] atExitBySlot)
        {
            if (cellBySlot == null || atExitBySlot == null) return false;
            LabyrinthGrid g = Grid;
            if (_session == null || _session.Sim == null || g == null) return false;

            MagicDoor exit = ExitDoorway;
            LabyrinthDef def = Def;
            float radius = def != null ? def.exitGatherRadius : 6f;
            float radiusSq = radius * radius;

            PlayerTable players = _session.Sim.Players;
            int count = cellBySlot.Length < players.Count ? cellBySlot.Length : players.Count;
            for (int i = 0; i < count; i++)
            {
                cellBySlot[i] = LabyrinthGrid.NoCell;
                if (i < atExitBySlot.Length) atExitBySlot[i] = false;
                PlayerState p = players[i];
                if (p == null || !p.present || p.poseCount == 0) continue;
                cellBySlot[i] = CellAt(p.pos);
                if (exit == null || i >= atExitBySlot.Length) continue;
                atExitBySlot[i] = (p.pos - exit.transform.position).sqrMagnitude <= radiusSq;
            }
            return true;
        }
    }
}
