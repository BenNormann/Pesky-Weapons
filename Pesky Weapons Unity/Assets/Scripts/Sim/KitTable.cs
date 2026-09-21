using System.Collections.Generic;
using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>The last KIT_STATE a kit piece took. A piece with no entry is still in its authored state.</summary>
    public sealed class KitEntry
    {
        public KitKind kind;
        public ushort pieceId;
        public byte state;
        public ushort actor = Wire.NoId;
        public int value;
        public uint clockMs;
    }

    /// <summary>Every kit piece that has changed since the level loaded, in the order it changed.</summary>
    public sealed class KitTable
    {
        readonly List<KitEntry> _rows = new List<KitEntry>(64);
        readonly Dictionary<int, KitEntry> _byKey = new Dictionary<int, KitEntry>(64);

        public IReadOnlyList<KitEntry> Rows => _rows;
        public int Count => _rows.Count;

        static int Key(KitKind kind, ushort pieceId) => ((int)kind << 16) | pieceId;

        public KitEntry Get(KitKind kind, ushort pieceId) =>
            _byKey.TryGetValue(Key(kind, pieceId), out var row) ? row : null;

        public KitEntry Set(KitKind kind, ushort pieceId, byte state, ushort actor, int value, uint clockMs)
        {
            var key = Key(kind, pieceId);
            if (!_byKey.TryGetValue(key, out var row))
            {
                row = new KitEntry();
                row.kind = kind;
                row.pieceId = pieceId;
                _rows.Add(row);
                _byKey[key] = row;
            }
            row.state = state;
            row.actor = actor;
            row.value = value;
            row.clockMs = clockMs;
            return row;
        }

        public void Clear()
        {
            _rows.Clear();
            _byKey.Clear();
        }

        public void Write(NetWriter w)
        {
            w.U16((ushort)_rows.Count);
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                w.U8((byte)row.kind).U16(row.pieceId).U8(row.state).U16(row.actor).I32(row.value).U32(row.clockMs);
            }
        }

        public void Read(NetReader r)
        {
            Clear();
            var n = r.U16();
            for (var i = 0; i < n && !r.Failed; i++)
            {
                var kind = (KitKind)r.U8();
                var pieceId = r.U16();
                var state = r.U8();
                var actor = r.U16();
                var value = r.I32();
                var clockMs = r.U32();
                if (!r.Failed) Set(kind, pieceId, state, actor, value, clockMs);
            }
        }
    }
}
