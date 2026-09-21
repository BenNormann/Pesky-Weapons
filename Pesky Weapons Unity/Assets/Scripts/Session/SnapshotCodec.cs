using System;
using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session
{
    /// <summary>
    /// Turns a WorldSim into SNAPSHOT parts and back. Each table is written
    /// with WorldSim.WritePart, split into bodies no larger than
    /// SnapshotPartMsg.MaxBody (so every part travels in its own FRAME), and
    /// followed by an End part that marks the snapshot complete. partIndex
    /// and partCount count the chunks of one table; SnapshotReceiver
    /// reassembles them in SnapshotPartKind order.
    /// </summary>
    public static class SnapshotCodec
    {
        // Send order. Every kind but End belongs here, and End must stay out
        // of it: SnapshotReceiver treats it as the terminator.
        static readonly SnapshotPartKind[] Order =
        {
            SnapshotPartKind.World, SnapshotPartKind.Players, SnapshotPartKind.Weapons,
            SnapshotPartKind.Enemies, SnapshotPartKind.Kit, SnapshotPartKind.Labyrinth,
            SnapshotPartKind.Pad,
        };


        public static IReadOnlyList<SnapshotPartKind> TableOrder => Order;

        /// <summary>Every part of a snapshot of the sim, encoded and in send order, ending with the End part.</summary>
        public static List<byte[]> Build(WorldSim sim, byte snapId)
        {
            var parts = new List<byte[]>(Order.Length + 1);
            for (var i = 0; i < Order.Length; i++) AppendTable(sim, snapId, Order[i], parts);
            var end = new SnapshotPartMsg();
            end.snapId = snapId;
            end.kind = SnapshotPartKind.End;
            end.partIndex = 0;
            end.partCount = 1;
            end.body = Array.Empty<byte>();
            parts.Add(end.Encode());
            return parts;
        }

        /// <summary>The raw table bytes, without the writer's type byte.</summary>
        public static byte[] TableBytes(WorldSim sim, SnapshotPartKind kind)
        {
            var w = new NetWriter(MsgId.Snapshot, 1024);
            sim.WritePart(kind, w);
            var all = w.ToArray();
            var body = new byte[all.Length - 1];
            Array.Copy(all, 1, body, 0, body.Length);
            return body;
        }

        /// <summary>Feeds assembled table bytes back into the sim; false when the reader failed or left bytes over.</summary>
        public static bool ReadTable(WorldSim sim, SnapshotPartKind kind, byte[] body)
        {
            if (body == null) return false;
            var r = new NetReader(body, 0);
            if (!sim.ReadPart(kind, r)) return false;
            return r.Remaining == 0;
        }

        static void AppendTable(WorldSim sim, byte snapId, SnapshotPartKind kind, List<byte[]> into)
        {
            var body = TableBytes(sim, kind);
            var count = body.Length == 0 ? 1 : (body.Length + SnapshotPartMsg.MaxBody - 1) / SnapshotPartMsg.MaxBody;
            if (count > byte.MaxValue)
                throw new InvalidOperationException($"snapshot table {kind} needs {count} parts, more than 255");
            for (var index = 0; index < count; index++)
            {
                var start = index * SnapshotPartMsg.MaxBody;
                var len = Math.Min(SnapshotPartMsg.MaxBody, body.Length - start);
                var chunk = new byte[len];
                Array.Copy(body, start, chunk, 0, len);
                var part = new SnapshotPartMsg();
                part.snapId = snapId;
                part.kind = kind;
                part.partIndex = (byte)index;
                part.partCount = (byte)count;
                part.body = chunk;
                into.Add(part.Encode());
            }
        }
    }
}
