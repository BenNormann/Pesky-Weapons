using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>
    /// One mark on the shared scratch pad: a polyline in normalised canvas coordinates (u16 over the whole
    /// width and height), the slot that drew it (which is where its colour comes from) and whether it is an
    /// eraser. An eraser is drawn, not deleted: it is a stroke painted in the canvas colour on top, so every
    /// peer replaying the same list in the same order sees the same picture.
    /// </summary>
    public sealed class PadStroke
    {
        public ushort seq;
        public byte slot;
        public PadFlags flags;
        public byte width;

        /// <summary>x, y pairs: two entries per point.</summary>
        public ushort[] points;

        public int PointCount { get { return points == null ? 0 : points.Length / 2; } }

        public bool Erase { get { return (flags & PadFlags.Erase) != 0; } }
    }

    /// <summary>
    /// The team's shared scratch pad: an ordered list of strokes, the same on every peer.
    ///
    /// NOTHING HERE IS A SECRET. Unlike the labyrinth's compass targets and roles, the pad is public by
    /// design: it is where the crew draws the map they worked out between them, and a Mage drawing a lie on
    /// it is part of the game. It rides the snapshot so a late joiner sees what the crew already drew, and
    /// it is wiped at the start of every round.
    ///
    /// The list is capped both ways (strokes and total points). When a cap is passed the OLDEST strokes are
    /// dropped, which keeps the snapshot small and bounded: at the defaults the whole table is under 16 KB.
    /// </summary>
    public sealed class ScratchPadState
    {
        /// <summary>The most strokes the pad keeps before the oldest start falling off.</summary>
        public const int DefaultMaxStrokes = 300;

        /// <summary>The most points across every stroke. 4,000 points is about 16 KB on the wire.</summary>
        public const int DefaultMaxPoints = 4000;

        /// <summary>A hard ceiling, so a bad def can never make the snapshot enormous.</summary>
        public const int HardMaxStrokes = 2000;
        public const int HardMaxPoints = 20000;

        readonly List<PadStroke> _strokes = new List<PadStroke>(64);
        int _points;

        /// <summary>The strokes in the order they are painted. Oldest first.</summary>
        public IReadOnlyList<PadStroke> Strokes { get { return _strokes; } }

        public int MaxStrokes { get; private set; }
        public int MaxPoints { get; private set; }

        /// <summary>The sequence number of the last stroke taken.</summary>
        public ushort LastSeq { get; private set; }

        /// <summary>Bumped on every change, so a view can notice cheaply. Local only, never snapshotted.</summary>
        public uint Rev { get; private set; }

        public int PointTotal { get { return _points; } }

        public ScratchPadState()
        {
            MaxStrokes = DefaultMaxStrokes;
            MaxPoints = DefaultMaxPoints;
        }

        /// <summary>Takes the caps from the data asset, so every peer in one build trims identically.</summary>
        public void Configure(LabyrinthDef def)
        {
            int strokes = def != null ? def.padMaxStrokes : DefaultMaxStrokes;
            int points = def != null ? def.padMaxPoints : DefaultMaxPoints;
            MaxStrokes = strokes < 1 ? 1 : (strokes > HardMaxStrokes ? HardMaxStrokes : strokes);
            MaxPoints = points < 16 ? 16 : (points > HardMaxPoints ? HardMaxPoints : points);
            Trim();
        }

        public void Clear()
        {
            _strokes.Clear();
            _points = 0;
            LastSeq = 0;
            Rev++;
        }

        // ---------------------------------------------------------------- messages

        public void Apply(in PadStrokeMsg msg)
        {
            Add(msg.seq, msg.slot, msg.flags, msg.width, msg.points);
        }

        public void Apply(in PadClearMsg msg)
        {
            Clear();
        }

        /// <summary>
        /// Appends a stroke. Events reach every peer in tick order and, inside a tick, in the order the host
        /// pushed them, so appending IS the shared order; the sequence number is carried for diagnostics and
        /// so a drawer can recognise its own stroke coming back.
        /// </summary>
        public void Add(ushort seq, byte slot, PadFlags flags, byte width, ushort[] points)
        {
            int count = points == null ? 0 : points.Length / 2;
            if (count <= 0) return;
            if (count > PadStrokeReqMsg.MaxPoints) count = PadStrokeReqMsg.MaxPoints;

            PadStroke stroke = new PadStroke();
            stroke.seq = seq;
            stroke.slot = slot;
            stroke.flags = flags;
            stroke.width = width;
            stroke.points = new ushort[count * 2];
            for (int i = 0; i < count * 2; i++) stroke.points[i] = points[i];

            _strokes.Add(stroke);
            _points += count;
            LastSeq = seq;
            Trim();
            Rev++;
        }

        void Trim()
        {
            while (_strokes.Count > 0 && (_strokes.Count > MaxStrokes || _points > MaxPoints))
            {
                _points -= _strokes[0].PointCount;
                _strokes.RemoveAt(0);
            }
            if (_points < 0) _points = 0;
        }

        // ---------------------------------------------------------------- snapshot

        public void Write(NetWriter w)
        {
            w.U16((ushort)_strokes.Count);
            for (int i = 0; i < _strokes.Count; i++)
            {
                PadStroke s = _strokes[i];
                int n = s.PointCount;
                w.U16(s.seq).U8(s.slot).U8((byte)s.flags).U8(s.width).U8((byte)n);
                for (int p = 0; p < n * 2; p++) w.U16(s.points[p]);
            }
            w.U16(LastSeq);
        }

        public void Read(NetReader r)
        {
            _strokes.Clear();
            _points = 0;
            int count = r.U16();
            if (r.Failed) { Rev++; return; }
            if (count > HardMaxStrokes) count = HardMaxStrokes;
            for (int i = 0; i < count; i++)
            {
                PadStroke s = new PadStroke();
                s.seq = r.U16();
                s.slot = r.U8();
                s.flags = (PadFlags)r.U8();
                s.width = r.U8();
                int n = r.U8();
                if (r.Failed || n > PadStrokeReqMsg.MaxPoints || n * 4 > r.Remaining) { Rev++; return; }
                s.points = new ushort[n * 2];
                for (int p = 0; p < n * 2; p++) s.points[p] = r.U16();
                if (r.Failed) { Rev++; return; }
                _strokes.Add(s);
                _points += n;
            }
            LastSeq = r.U16();
            Trim();
            Rev++;
        }
    }
}
