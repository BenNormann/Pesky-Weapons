using Pesky.Data;
using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>
    /// Whether a doorway of a cell may be walked through. Null means every doorway is open, which is the
    /// case today: every room has four working doorways. A later stage that bars a doorway hangs its rule
    /// here and the connectivity check and the compass both start respecting it at once.
    /// </summary>
    public delegate bool DoorwayFilter(int cell, int dir);

    /// <summary>What a compass should show. Direction only: there is never a distance.</summary>
    public struct CompassHint
    {
        /// <summary>False when there is no path at all, so the needle should read as lost.</summary>
        public bool known;

        /// <summary>The player is standing in the target room: the needle spins.</summary>
        public bool spin;

        /// <summary>Which doorway of the CURRENT room to take (a Heading), when known and not spinning.</summary>
        public byte doorway;

        public static CompassHint None { get { return default(CompassHint); } }

        public static CompassHint Spinning()
        {
            CompassHint h = default(CompassHint);
            h.known = true;
            h.spin = true;
            return h;
        }

        public static CompassHint Take(int dir)
        {
            CompassHint h = default(CompassHint);
            h.known = true;
            h.doorway = (byte)dir;
            return h;
        }
    }

    /// <summary>
    /// The labyrinth as the host holds it: a WIDTH x HEIGHT table of cell -> room id, and nothing else.
    /// Plain C#, no Unity, no clock, no randomness beyond the seed it is generated from.
    ///
    /// THE GRID IS NOT PHYSICAL. Rooms sit far apart in world space; a doorway is a teleport. The grid only
    /// says which room a doorway currently leads to, and it is resolved at the moment of traversal, so a
    /// room that moved while you were in the air changes where you come out.
    ///
    /// Opposite edges WRAP: leaving east from the last column arrives in the first column of the same row,
    /// and leaving north from row 0 arrives in the last row of the same column. There is exactly ONE
    /// doorway in the whole grid that does not wrap: <see cref="ExitDir"/> of <see cref="GoodEndCell"/>.
    /// Going through it is the Exit.
    ///
    /// North is row - 1 (row 0 is the north edge), east is column + 1 (column 0 is the west edge).
    /// </summary>
    public sealed class LabyrinthGrid
    {
        /// <summary>No such cell: off the one non-wrapping edge, or an index outside the table.</summary>
        public const int NoCell = -1;

        readonly ushort[] _cells;
        readonly int[] _dist;
        readonly int[] _queue;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int CellCount { get { return Width * Height; } }

        /// <summary>The cell holding the start room (the weapon rack). Never swapped.</summary>
        public int StartCell { get; private set; }

        /// <summary>The cell holding the Resurrection Room. Never swapped.</summary>
        public int BadEndCell { get; private set; }

        /// <summary>The cell holding the good end. Never swapped.</summary>
        public int GoodEndCell { get; private set; }

        /// <summary>Which doorway of the good-end room is the Exit. It points off the grid and does not wrap.</summary>
        public Heading ExitDir { get; private set; }

        /// <summary>Optional: a doorway that cannot be used. Null (the default) means all four always work.</summary>
        public DoorwayFilter DoorOpen { get; set; }

        public LabyrinthGrid(int width, int height)
        {
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            int n = Width * Height;
            _cells = new ushort[n];
            _dist = new int[n];
            _queue = new int[n];
            for (int i = 0; i < n; i++) _cells[i] = (ushort)i;
            StartCell = 0;
            BadEndCell = n > 1 ? 1 : 0;
            GoodEndCell = n > 2 ? n - 1 : 0;
            ExitDir = Heading.North;
        }

        // ---------------------------------------------------------------- geometry

        public static int Opposite(int dir) { return (dir + 2) & 3; }

        public int Index(int column, int row) { return row * Width + column; }
        public int ColumnOf(int cell) { return cell % Width; }
        public int RowOf(int cell) { return cell / Width; }
        public bool InRange(int cell) { return cell >= 0 && cell < CellCount; }

        /// <summary>True for the one doorway in the grid that leads out instead of wrapping.</summary>
        public bool IsExitDoorway(int cell, int dir) { return cell == GoodEndCell && dir == (int)ExitDir; }

        /// <summary>True when stepping this way leaves one edge of the map and arrives at the other.</summary>
        public bool Wraps(int cell, int dir)
        {
            if (!InRange(cell)) return false;
            switch (dir)
            {
                case (int)Heading.North: return RowOf(cell) == 0;
                case (int)Heading.East: return ColumnOf(cell) == Width - 1;
                case (int)Heading.South: return RowOf(cell) == Height - 1;
                case (int)Heading.West: return ColumnOf(cell) == 0;
            }
            return false;
        }

        /// <summary>The cell a doorway leads to, wrapping at the edges. NoCell for the Exit doorway alone.</summary>
        public int Neighbour(int cell, int dir)
        {
            if (IsExitDoorway(cell, dir)) return NoCell;
            return Step(cell, dir);
        }

        /// <summary>The cell a step lands in, ignoring the Exit hole. This is the MAP's neighbour, which is what a swap drag uses.</summary>
        public int Step(int cell, int dir)
        {
            if (!InRange(cell) || dir < 0 || dir > 3) return NoCell;
            int x = ColumnOf(cell);
            int y = RowOf(cell);
            switch (dir)
            {
                case (int)Heading.North: y = (y - 1 + Height) % Height; break;
                case (int)Heading.East: x = (x + 1) % Width; break;
                case (int)Heading.South: y = (y + 1) % Height; break;
                default: x = (x - 1 + Width) % Width; break;
            }
            return Index(x, y);
        }

        bool Passable(int cell, int dir, int into)
        {
            if (into == NoCell) return false;
            if (DoorOpen == null) return true;
            return DoorOpen(cell, dir) && DoorOpen(into, Opposite(dir));
        }

        // ---------------------------------------------------------------- the table

        public ushort RoomAt(int cell) { return InRange(cell) ? _cells[cell] : (ushort)Wire.NoId; }

        /// <summary>Where a room currently is, or NoCell.</summary>
        public int CellOfRoom(int roomId)
        {
            for (int i = 0; i < _cells.Length; i++) if (_cells[i] == roomId) return i;
            return NoCell;
        }

        /// <summary>The start room and the two end rooms never move, so their cells are the fixed ones.</summary>
        public bool IsFixedCell(int cell)
        {
            return cell == StartCell || cell == BadEndCell || cell == GoodEndCell;
        }

        public void CopyTable(ushort[] into)
        {
            if (into == null) return;
            int n = into.Length < _cells.Length ? into.Length : _cells.Length;
            for (int i = 0; i < n; i++) into[i] = _cells[i];
        }

        public ushort[] Table()
        {
            ushort[] copy = new ushort[_cells.Length];
            CopyTable(copy);
            return copy;
        }

        void Exchange(int a, int b)
        {
            ushort t = _cells[a];
            _cells[a] = _cells[b];
            _cells[b] = t;
        }

        // ---------------------------------------------------------------- swapping

        /// <summary>
        /// Orthogonally adjacent on the map, never diagonal. <paramref name="allowWrap"/> decides whether a
        /// drag may leave one edge and arrive at the other; the door graph always wraps, a drag usually
        /// should not.
        /// </summary>
        public bool AreAdjacent(int a, int b, bool allowWrap, out int dir)
        {
            dir = -1;
            if (!InRange(a) || !InRange(b) || a == b) return false;
            for (int d = 0; d < 4; d++)
            {
                if (Step(a, d) != b) continue;
                if (!allowWrap && Wraps(a, d)) continue;
                dir = d;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The whole rule set for a Mage's drag, cooldown aside: in range, not the same cell, orthogonally
        /// adjacent, neither cell holding the start room or an end room, and every room still able to reach
        /// the Exit afterwards. Leaves the table exactly as it found it.
        /// </summary>
        public bool CanSwap(int a, int b, bool allowWrap)
        {
            int dir;
            if (!AreAdjacent(a, b, allowWrap, out dir)) return false;
            if (IsFixedCell(a) || IsFixedCell(b)) return false;
            Exchange(a, b);
            bool ok = EveryCellReachesExit();
            Exchange(a, b);
            return ok;
        }

        /// <summary>
        /// Applies a swap the host already decided. Deliberately checks nothing but the indices: the sim
        /// applies the host's fact, it never re-judges it, so no peer can disagree about the table.
        /// </summary>
        public bool ForceSwap(int a, int b)
        {
            if (!InRange(a) || !InRange(b) || a == b) return false;
            Exchange(a, b);
            return true;
        }

        // ---------------------------------------------------------------- paths

        void Flood(int source)
        {
            for (int i = 0; i < _dist.Length; i++) _dist[i] = int.MaxValue;
            if (!InRange(source)) return;
            _dist[source] = 0;
            int head = 0;
            int tail = 0;
            _queue[tail++] = source;
            while (head < tail)
            {
                int c = _queue[head++];
                int d = _dist[c] + 1;
                for (int dir = 0; dir < 4; dir++)
                {
                    int n = Neighbour(c, dir);
                    if (!Passable(c, dir, n)) continue;
                    if (_dist[n] <= d) continue;
                    _dist[n] = d;
                    if (tail < _queue.Length) _queue[tail++] = n;
                }
            }
        }

        /// <summary>The connectivity hook a swap must satisfy: every cell can still reach the good-end room, and so the Exit.</summary>
        public bool EveryCellReachesExit()
        {
            Flood(GoodEndCell);
            for (int i = 0; i < _dist.Length; i++) if (_dist[i] == int.MaxValue) return false;
            return true;
        }

        /// <summary>
        /// The doorway of <paramref name="from"/> that starts the shortest path to the Exit, wrapped edges
        /// included. Standing in the good-end room it names the Exit doorway itself, which is still "a
        /// doorway of the room you are in", so the needle never has to spin on the truthful setting.
        /// </summary>
        public CompassHint ToExit(int from)
        {
            if (!InRange(from)) return CompassHint.None;
            if (from == GoodEndCell) return CompassHint.Take((int)ExitDir);
            Flood(GoodEndCell);
            return FirstStep(from);
        }

        /// <summary>The doorway that starts the shortest path to a cell; spins when you are already in it.</summary>
        public CompassHint ToCell(int from, int target)
        {
            if (!InRange(from) || !InRange(target)) return CompassHint.None;
            if (from == target) return CompassHint.Spinning();
            Flood(target);
            return FirstStep(from);
        }

        CompassHint FirstStep(int from)
        {
            int best = -1;
            int bestDist = int.MaxValue;
            for (int dir = 0; dir < 4; dir++)
            {
                int n = Neighbour(from, dir);
                if (!Passable(from, dir, n)) continue;
                if (_dist[n] >= bestDist) continue;
                bestDist = _dist[n];
                best = dir;
            }
            if (best < 0 || bestDist == int.MaxValue) return CompassHint.None;
            return CompassHint.Take(best);
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// The one deterministic layout: the start room in the centre cell, the two end rooms in two
        /// DIFFERENT corners chosen by the seed, the Exit on an outward edge of the good-end corner, and
        /// every other room shuffled into what is left. Same seed, same labyrinth on every peer.
        /// </summary>
        public static LabyrinthGrid Generate(LabyrinthDef def, uint seed)
        {
            int w = def != null ? def.Width : 5;
            int h = def != null ? def.Height : 5;
            LabyrinthGrid g = new LabyrinthGrid(w, h);
            int cells = g.CellCount;
            Rng rng = new Rng(seed);

            int start = RoleRoom(def, LabyrinthRoomRole.Start, 0, cells);
            int bad = RoleRoom(def, LabyrinthRoomRole.BadEnd, 1, cells);
            int good = RoleRoom(def, LabyrinthRoomRole.GoodEnd, 2, cells);
            if (bad == start) bad = start == 0 ? 1 : 0;
            if (good == start || good == bad)
            {
                good = 0;
                while (good == start || good == bad) good++;
            }

            g.StartCell = g.Index(w / 2, h / 2);

            int[] corners = { g.Index(0, 0), g.Index(w - 1, 0), g.Index(0, h - 1), g.Index(w - 1, h - 1) };
            int ia = rng.Range(0, 4);
            int ib = (ia + 1 + rng.Range(0, 3)) % 4;
            g.BadEndCell = corners[ia];
            g.GoodEndCell = corners[ib];

            // The Exit points OUT of the map: at a corner that is one of two edges, and the seed picks which.
            int gx = g.ColumnOf(g.GoodEndCell);
            int gy = g.RowOf(g.GoodEndCell);
            Heading vertical = gy == 0 ? Heading.North : Heading.South;
            Heading horizontal = gx == 0 ? Heading.West : Heading.East;
            g.ExitDir = rng.Range(0, 2) == 0 ? vertical : horizontal;

            for (int i = 0; i < cells; i++) g._cells[i] = ushort.MaxValue;
            g._cells[g.StartCell] = (ushort)start;
            g._cells[g.BadEndCell] = (ushort)bad;
            g._cells[g.GoodEndCell] = (ushort)good;

            // Everything else: the remaining room ids, shuffled, into the remaining cells in order.
            int[] rest = new int[cells];
            int count = 0;
            for (int id = 0; id < cells; id++)
            {
                if (id == start || id == bad || id == good) continue;
                rest[count++] = id;
            }
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int t = rest[i];
                rest[i] = rest[j];
                rest[j] = t;
            }
            int next = 0;
            for (int cell = 0; cell < cells; cell++)
            {
                if (g._cells[cell] != ushort.MaxValue) continue;
                g._cells[cell] = (ushort)rest[next++];
            }
            return g;
        }

        static int RoleRoom(LabyrinthDef def, LabyrinthRoomRole role, int fallback, int cells)
        {
            int id = def != null ? def.RoomWithRole(role) : -1;
            if (id < 0 || id >= cells) id = fallback < cells ? fallback : 0;
            return id;
        }

        // ---------------------------------------------------------------- the wire

        /// <summary>The whole table as a LAB_LAYOUT.</summary>
        public LabLayoutMsg ToLayout()
        {
            LabLayoutMsg msg = new LabLayoutMsg();
            msg.width = (byte)Width;
            msg.height = (byte)Height;
            msg.startCell = (ushort)StartCell;
            msg.badEndCell = (ushort)BadEndCell;
            msg.goodEndCell = (ushort)GoodEndCell;
            msg.exitDir = ExitDir;
            msg.cells = Table();
            return msg;
        }

        /// <summary>A grid built from a LAB_LAYOUT, or null when the message does not describe a whole table.</summary>
        public static LabyrinthGrid FromLayout(in LabLayoutMsg msg)
        {
            if (msg.width < 1 || msg.height < 1) return null;
            int cells = msg.width * msg.height;
            if (msg.cells == null || msg.cells.Length != cells) return null;
            LabyrinthGrid g = new LabyrinthGrid(msg.width, msg.height);
            for (int i = 0; i < cells; i++) g._cells[i] = msg.cells[i];
            g.StartCell = msg.startCell < cells ? msg.startCell : 0;
            g.BadEndCell = msg.badEndCell < cells ? msg.badEndCell : 0;
            g.GoodEndCell = msg.goodEndCell < cells ? msg.goodEndCell : 0;
            g.ExitDir = (Heading)((byte)msg.exitDir & 3);
            return g;
        }

        public void Write(NetWriter w)
        {
            w.U8((byte)Width).U8((byte)Height);
            w.U16((ushort)StartCell).U16((ushort)BadEndCell).U16((ushort)GoodEndCell).U8((byte)ExitDir);
            w.U16((ushort)_cells.Length);
            for (int i = 0; i < _cells.Length; i++) w.U16(_cells[i]);
        }

        /// <summary>Reads a table written by <see cref="Write"/>; null when the reader ran out or the size does not add up.</summary>
        public static LabyrinthGrid Read(NetReader r)
        {
            int w = r.U8();
            int h = r.U8();
            int start = r.U16();
            int bad = r.U16();
            int good = r.U16();
            byte exit = r.U8();
            int count = r.U16();
            if (r.Failed || w < 1 || h < 1 || count != w * h) return null;
            LabyrinthGrid g = new LabyrinthGrid(w, h);
            for (int i = 0; i < count; i++) g._cells[i] = r.U16();
            if (r.Failed) return null;
            g.StartCell = start < count ? start : 0;
            g.BadEndCell = bad < count ? bad : 0;
            g.GoodEndCell = good < count ? good : 0;
            g.ExitDir = (Heading)(exit & 3);
            return g;
        }
    }
}
