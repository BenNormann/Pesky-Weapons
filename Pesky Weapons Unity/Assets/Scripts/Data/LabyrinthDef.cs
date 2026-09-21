using UnityEngine;

namespace Pesky.Data
{
    /// <summary>What a room is for. Exactly one Start, one BadEnd and one GoodEnd are expected; the rest are Blank.</summary>
    public enum LabyrinthRoomRole : byte { Blank = 0, Start = 1, BadEnd = 2, GoodEnd = 3 }

    /// <summary>
    /// One room of the labyrinth, by ROOM ID (its index in <see cref="LabyrinthDef.rooms"/>). A room id is
    /// stable for a build: it names a room, never a place. Where a room currently sits is the grid table,
    /// which the host owns and the Mage can shuffle.
    /// </summary>
    [System.Serializable]
    public sealed class LabyrinthRoomDef
    {
        [Tooltip("The truthful name a doorway glyph shows for this room.")]
        public string label = "Room";

        [Tooltip("One or two characters for the small glyph over a doorway.")]
        public string glyph = "?";

        public LabyrinthRoomRole role = LabyrinthRoomRole.Blank;
    }

    /// <summary>
    /// Everything about the labyrinth that is data rather than code: the grid size, the room list, the
    /// Mage's limits and the round's thresholds. One asset, referenced from <see cref="GameData"/>, so the
    /// sim on every peer is built from the same numbers in the same build.
    ///
    /// The grid is NOT physical. Rooms sit apart in world space; only the teleport doorways join them. The
    /// grid is a table of cell -> room id that the host owns, and opposite edges of it WRAP, except for the
    /// single outward doorway on the good-end room, which is the Exit.
    ///
    /// To grow the labyrinth: raise <see cref="gridWidth"/> / <see cref="gridHeight"/> and append rooms to
    /// <see cref="rooms"/> until there are at least width * height of them. Never reorder the list: a room
    /// id travels on the wire.
    /// </summary>
    [CreateAssetMenu(fileName = "Labyrinth", menuName = "Pesky/Labyrinth")]
    public sealed class LabyrinthDef : ScriptableObject
    {
        /// <summary>Below three a side the centre cell would also be a corner, which the layout rules forbid.</summary>
        public const int MinSide = 3;

        /// <summary>A ceiling that keeps the layout message small and the room ids inside a ushort.</summary>
        public const int MaxSide = 64;

        [Header("Grid")]
        [Tooltip("Columns, west to east. Wraps: leaving east from the last column arrives in the first.")]
        public int gridWidth = 5;

        [Tooltip("Rows, north to south. Wraps: leaving north from the first row arrives in the last.")]
        public int gridHeight = 5;

        [Header("Rooms (index = room id; append only, never reorder)")]
        public LabyrinthRoomDef[] rooms;
        [Header("Doorways")]
        [Tooltip("Show the destination glyph and name above every grid doorway. OFF by design: a room says its OWN number on its floor and a doorway says nothing, so the crew has to remember or draw the map themselves. Turning it on brings the labels back with no other change.")]
        public bool showDoorLabels = false;

        [Tooltip("Lay each room's own number flat on the middle of its floor, top towards the north doorway.")]
        public bool showFloorNumbers = true;

        [Header("Scratch pad (shared, wiped every round)")]
        [Tooltip("How many strokes the shared pad keeps. Past this the OLDEST fall off, which is what keeps the snapshot bounded.")]
        public int padMaxStrokes = 300;

        [Tooltip("How many points across every stroke together. 4,000 points is about 16 KB in a snapshot.")]
        public int padMaxPoints = 4000;

        [Tooltip("How many strokes one player may have accepted per second. The host drops the rest in silence.")]
        public float padStrokesPerSecond = 20f;

        [Tooltip("How many strokes a player may fire off at once before the per-second rate starts to bite.")]
        public int padStrokeBurst = 40;


        [Header("Mage: swapping rooms")]
        [Tooltip("Seconds between one Mage's room swaps.")]
        public float swapCooldownSeconds = 60f;

        [Tooltip("Allow a drag from one edge of the map to the opposite edge (the grid wraps, but the drag is odd). Off by default.")]
        public bool swapAcrossWrap = false;

        [Header("Mage: bending compasses")]
        [Tooltip("Seconds between one Mage's compass bends.")]
        public float bendCooldownSeconds = 15f;

        [Tooltip("Fraction of the non-Mage players that may be bent at once, exclusive. 0.5 is the minority rule: strictly fewer than half.")]
        [Range(0f, 1f)] public float bendFractionLimit = 0.5f;
        [Tooltip("However small the room, a Mage may always bend at least this many compasses. Without it the minority rule is ZERO for 0, 1 or 2 non-Mage players, so the host silently refuses every bend in the tutorial and in any small test.")]
        [Min(0)] public int minBendTargets = 1;


        /// <summary>
        /// How many non-Mage compasses may be bent at once, counting both fragments together. The minority
        /// rule (strictly fewer than bendFractionLimit of the non-Mages) is the ceiling once the room is big
        /// enough; below that minBendTargets keeps the power usable instead of silently dead. Never more
        /// than the number of non-Mages there actually are.
        /// </summary>
        public int MaxBentFor(int nonMages)
        {
            if (nonMages <= 0) return 0;
            int strict = Mathf.CeilToInt(bendFractionLimit * nonMages - 0.0001f) - 1;
            if (strict < 0) strict = 0;
            int allowed = strict > minBendTargets ? strict : minBendTargets;
            return allowed > nonMages ? nonMages : allowed;
        }

        [Header("Roles")]
        [Tooltip("How many Mage fragments a small room gets.")]
        public int mageBaseCount = 1;

        [Tooltip("From this many players up there is a second Mage.")]
        public int mageSecondFromPlayers = 6;

        [Tooltip("Never more Mages than this.")]
        public int mageMaxCount = 2;

        [Header("Round")]
        [Tooltip("Fraction of the connected non-Mage players that must be gathered at the exit doorway, inclusive. 1 = all of them.")]
        [Range(0f, 1f)] public float escapeFraction = 1f;

        [Tooltip("Fraction of the connected players that must be inside the Resurrection Room at once, exclusive. 0.5 is a majority.")]
        [Range(0f, 1f)] public float resurrectionFraction = 0.5f;

        [Tooltip("How close to the exit doorway counts as gathered there, metres.")]
        public float exitGatherRadius = 6f;

        [Tooltip("Seconds a downed player waits before respawning with the group.")]
        public float respawnDelaySeconds = 10f;

        [Tooltip("The legend a downed player is left with. Death costs all of it.")]
        public int legendOnDeath = 0;

        public int Width { get { return Mathf.Clamp(gridWidth, MinSide, MaxSide); } }
        public int Height { get { return Mathf.Clamp(gridHeight, MinSide, MaxSide); } }
        public int CellCount { get { return Width * Height; } }

        public LabyrinthRoomDef Room(int roomId)
        {
            return rooms != null && roomId >= 0 && roomId < rooms.Length ? rooms[roomId] : null;
        }

        /// <summary>The truthful label of a room id; a generated one for an id the list does not cover yet.</summary>
        public string LabelOf(int roomId)
        {
            LabyrinthRoomDef r = Room(roomId);
            if (r != null && !string.IsNullOrEmpty(r.label)) return r.label;
            return "Room " + roomId;
        }

        public string GlyphOf(int roomId)
        {
            LabyrinthRoomDef r = Room(roomId);
            if (r != null && !string.IsNullOrEmpty(r.glyph)) return r.glyph;
            return "?";
        }

        public LabyrinthRoomRole RoleOf(int roomId)
        {
            LabyrinthRoomDef r = Room(roomId);
            return r != null ? r.role : LabyrinthRoomRole.Blank;
        }

        /// <summary>The first room id carrying a role, or -1 when the list does not declare it.</summary>
        public int RoomWithRole(LabyrinthRoomRole role)
        {
            if (rooms == null) return -1;
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i] != null && rooms[i].role == role) return i;
            return -1;
        }

        /// <summary>How many Mage fragments a room of this many players gets.</summary>
        public int MageCountFor(int playerCount)
        {
            int n = Mathf.Max(1, mageBaseCount);
            if (mageSecondFromPlayers > 0 && playerCount >= mageSecondFromPlayers) n++;
            n = Mathf.Min(n, Mathf.Max(1, mageMaxCount));
            // Never so many Mages that nobody is left to fool.
            return Mathf.Clamp(n, 1, Mathf.Max(1, playerCount - 1));
        }

        /// <summary>A sentence per problem, for the scene validator and the Inspector. Empty means the asset is usable.</summary>
        public string Problem()
        {
            if (gridWidth < MinSide || gridHeight < MinSide)
                return "the grid must be at least " + MinSide + " by " + MinSide + " (the centre cell may not be a corner)";
            if (gridWidth > MaxSide || gridHeight > MaxSide)
                return "the grid may be at most " + MaxSide + " by " + MaxSide;
            if (rooms == null || rooms.Length < CellCount)
                return "there are " + CellCount + " cells but only " + (rooms == null ? 0 : rooms.Length) + " rooms";
            if (RoomWithRole(LabyrinthRoomRole.Start) < 0) return "no room is tagged Start";
            if (RoomWithRole(LabyrinthRoomRole.BadEnd) < 0) return "no room is tagged BadEnd";
            if (RoomWithRole(LabyrinthRoomRole.GoodEnd) < 0) return "no room is tagged GoodEnd";
            return "";
        }
    }
}
