using System;
using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session
{
    /// <summary>
    /// The client side of SnapshotCodec: collects the parts of one snapshot
    /// id (a newer id discards a half-built older one), reports completion
    /// when the End part lands with every table whole, and rebuilds the
    /// WorldSim from the assembled tables in SnapshotPartKind order.
    /// </summary>
    public sealed class SnapshotReceiver
    {
        sealed class Table
        {
            public byte[][] chunks;
            public int received;
            public bool Complete => chunks != null && received == chunks.Length;
        }

        readonly Table[] _tables = new Table[(int)SnapshotPartKind.End];
        bool _endSeen;
        bool _hasId;

        public byte SnapId { get; private set; }
        public bool InProgress => _hasId && !IsComplete;

        public bool IsComplete
        {
            get
            {
                if (!_endSeen) return false;
                for (var i = 0; i < _tables.Length; i++)
                {
                    if (_tables[i] == null || !_tables[i].Complete) return false;
                }
                return true;
            }
        }

        public SnapshotReceiver()
        {
            for (var i = 0; i < _tables.Length; i++) _tables[i] = new Table();
        }

        /// <summary>Stores one part; true once the whole snapshot is in hand.</summary>
        public bool Receive(in SnapshotPartMsg part)
        {
            if (!_hasId || part.snapId != SnapId) Reset(part.snapId);
            if (part.kind == SnapshotPartKind.End)
            {
                _endSeen = true;
                return IsComplete;
            }
            var index = (int)part.kind;
            if (index < 0 || index >= _tables.Length || part.partCount == 0 || part.partIndex >= part.partCount) return false;
            var table = _tables[index];
            if (table.chunks == null || table.chunks.Length != part.partCount)
            {
                table.chunks = new byte[part.partCount][];
                table.received = 0;
            }
            if (table.chunks[part.partIndex] == null) table.received++;
            table.chunks[part.partIndex] = part.body ?? Array.Empty<byte>();
            return IsComplete;
        }

        /// <summary>Rebuilds the sim from the collected tables; false when a table failed to read. Clears the receiver either way.</summary>
        public bool ApplyTo(WorldSim sim)
        {
            if (!IsComplete || sim == null)
            {
                Clear();
                return false;
            }
            var ok = true;
            var order = SnapshotCodec.TableOrder;
            for (var i = 0; i < order.Count; i++)
            {
                var kind = order[i];
                var body = Join(_tables[(int)kind]);
                if (!SnapshotCodec.ReadTable(sim, kind, body)) ok = false;
            }
            Clear();
            return ok;
        }

        public void Clear()
        {
            _hasId = false;
            _endSeen = false;
            for (var i = 0; i < _tables.Length; i++)
            {
                _tables[i].chunks = null;
                _tables[i].received = 0;
            }
        }

        void Reset(byte snapId)
        {
            Clear();
            _hasId = true;
            SnapId = snapId;
        }

        static byte[] Join(Table table)
        {
            var total = 0;
            for (var i = 0; i < table.chunks.Length; i++) total += table.chunks[i].Length;
            if (table.chunks.Length == 1) return table.chunks[0];
            var joined = new byte[total];
            var pos = 0;
            for (var i = 0; i < table.chunks.Length; i++)
            {
                Array.Copy(table.chunks[i], 0, joined, pos, table.chunks[i].Length);
                pos += table.chunks[i].Length;
            }
            return joined;
        }
    }
}
