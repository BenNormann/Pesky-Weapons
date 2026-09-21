using System;
using System.Collections.Generic;

namespace Pesky.Protocol
{
    /// <summary>
    /// 0x10 SESSION_INFO. The host's identity card: protocol version, world
    /// seed, phase, the floor of the labyrinth the run is on and the tick the
    /// phase began, so a peer joining mid-run knows where it is before its
    /// first snapshot. Broadcast as an event and also sent to each newcomer on
    /// PeerJoined; whoever sends it is the host. A floor of 0 means the sender
    /// carried no floor: the receiver keeps the one it has.
    /// </summary>
    public struct SessionInfoMsg
    {
        public const byte Id = MsgId.SessionInfo;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort protocolVersion;
        public uint worldSeed;
        public SessionPhase phase;
        /// <summary>The labyrinth floor, 1 based; 0 means this message carries no floor.</summary>
        public byte floorId;
        public uint phaseStartTick;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 20);
            header.Write(w);
            w.U16(protocolVersion).U32(worldSeed).U8((byte)phase).U8(floorId).U32(phaseStartTick);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out SessionInfoMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.protocolVersion = r.U16();
            msg.worldSeed = r.U32();
            msg.phase = (SessionPhase)r.U8();
            msg.floorId = r.U8();
            msg.phaseStartTick = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>0x11 TIME_SYNC. Host room milliseconds at 0.5 Hz, a coarse check on the CLOCK_PING estimate.</summary>
    public struct TimeSyncMsg
    {
        public const byte Id = MsgId.TimeSync;
        public const MsgKind Kind = MsgKind.State;

        public uint hostRoomMs;

        public byte[] Encode() => new NetWriter(Id, 8).U32(hostRoomMs).ToArray();

        public static bool TryDecode(byte[] payload, out TimeSyncMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.hostRoomMs = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>0x12 CLOCK_PING. A client's clock probe: its id and the client's send time.</summary>
    public struct ClockPingMsg
    {
        public const byte Id = MsgId.ClockPing;
        public const MsgKind Kind = MsgKind.Intent;

        public ushort pingId;
        public uint clientMs;

        public byte[] Encode() => new NetWriter(Id, 8).U16(pingId).U32(clientMs).ToArray();

        public static bool TryDecode(byte[] payload, out ClockPingMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.pingId = r.U16();
            msg.clientMs = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>0x13 CLOCK_PONG. The host's answer: the ping echoed, plus host room ms at receipt.</summary>
    public struct ClockPongMsg
    {
        public const byte Id = MsgId.ClockPong;
        public const MsgKind Kind = MsgKind.Reply;

        public ushort pingId;
        public uint clientMs;
        public uint hostRoomMs;

        public byte[] Encode() => new NetWriter(Id, 12).U16(pingId).U32(clientMs).U32(hostRoomMs).ToArray();

        public static bool TryDecode(byte[] payload, out ClockPongMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.pingId = r.U16();
            msg.clientMs = r.U32();
            msg.hostRoomMs = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>0x14 JOIN_REQUEST. Sent to the host after SESSION_INFO arrives: display name and protocol version.</summary>
    public struct JoinRequestMsg
    {
        public const byte Id = MsgId.JoinRequest;
        public const MsgKind Kind = MsgKind.Intent;

        public string name;
        public ushort protocolVersion;
        /// <summary>The joiner's look; the host copies it into the slot table.</summary>
        public Appearance appearance;

        public byte[] Encode()
        {
            var w = new NetWriter(Id).Str(name).U16(protocolVersion);
            appearance.Write(w);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out JoinRequestMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.name = r.Str();
            msg.protocolVersion = r.U16();
            msg.appearance = Appearance.Read(r);
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x15 SNAPSHOT. One part of a world snapshot for a joining or resyncing
    /// peer. The body is opaque here: Session's SnapshotCodec fills and reads
    /// it per kind. Each part is sized so it fits one FRAME on its own.
    /// </summary>
    public struct SnapshotPartMsg
    {
        public const byte Id = MsgId.Snapshot;
        public const MsgKind Kind = MsgKind.Reply;

        /// <summary>Type, snapId, kind, partIndex, partCount and the u16 body length.</summary>
        public const int FixedBytes = 7;

        /// <summary>The largest body that still lets the part travel alone in one FRAME.</summary>
        public const int MaxBody = Frame.MaxBytes - Frame.HeaderBytes - Frame.PerMessageBytes - FixedBytes;

        public byte snapId;
        public SnapshotPartKind kind;
        public byte partIndex;
        public byte partCount;
        public byte[] body;

        public byte[] Encode()
        {
            var len = body == null ? 0 : body.Length;
            if (len > MaxBody)
                throw new InvalidOperationException($"snapshot body of {len} bytes exceeds {MaxBody}");
            var w = new NetWriter(Id, FixedBytes + len);
            w.U8(snapId).U8((byte)kind).U8(partIndex).U8(partCount).U16((ushort)len);
            if (len > 0) w.Bytes(body, 0, len);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out SnapshotPartMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.snapId = r.U8();
            msg.kind = (SnapshotPartKind)r.U8();
            msg.partIndex = r.U8();
            msg.partCount = r.U8();
            var len = r.U16();
            msg.body = r.Bytes(len);
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x16 SESSION_PHASE. Lobby, playing or ended, at a tick. The message
    /// that starts the run carries the floor the lobby chose for it; a floor
    /// of 0 means the sender chose nothing and the receiver keeps its own.
    /// </summary>
    public struct SessionPhaseMsg
    {
        public const byte Id = MsgId.SessionPhase;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public SessionPhase phase;
        /// <summary>The labyrinth floor the run starts on; 0 leaves the receiver's own alone.</summary>
        public byte floorId;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 8);
            header.Write(w);
            w.U8((byte)phase).U8(floorId);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out SessionPhaseMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.phase = (SessionPhase)r.U8();
            msg.floorId = r.U8();
            return !r.Failed;
        }
    }

    /// <summary>0x17 PEER_SLOTS. The whole slot table, sent on change: slot, transport peer id, name, host flag, appearance.</summary>
    public struct PeerSlotsMsg
    {
        public const byte Id = MsgId.PeerSlots;
        public const MsgKind Kind = MsgKind.State;

        public struct Row
        {
            public byte slot;
            public string peerId;
            public string name;
            public bool isHost;
            /// <summary>The six appearance bytes, so a late joiner gets everyone's look with the table.</summary>
            public Appearance appearance;
        }

        public List<Row> rows;

        public byte[] Encode()
        {
            var count = Wire.BatchCount(rows);
            var w = new NetWriter(Id, 2 + count * 54);
            w.U8(count);
            for (var i = 0; i < count; i++)
            {
                var row = rows[i];
                w.U8(row.slot).Str(row.peerId).Str(row.name).Bool(row.isHost);
                row.appearance.Write(w);
            }
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PeerSlotsMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            var count = r.U8();
            msg.rows = new List<Row>(count);
            for (var i = 0; i < count && !r.Failed; i++)
            {
                var row = new Row();
                row.slot = r.U8();
                row.peerId = r.Str();
                row.name = r.Str();
                row.isHost = r.Bool();
                row.appearance = Appearance.Read(r);
                msg.rows.Add(row);
            }
            return !r.Failed;
        }
    }

    /// <summary>0x18 SESSION_END. Why the session ended and the final score, at a tick.</summary>
    public struct SessionEndMsg
    {
        public const byte Id = MsgId.SessionEnd;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public SessionEndReason reason;
        public int finalScore;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 12);
            header.Write(w);
            w.U8((byte)reason).I32(finalScore);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out SessionEndMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.reason = (SessionEndReason)r.U8();
            msg.finalScore = r.I32();
            return !r.Failed;
        }
    }

    /// <summary>0x19 RESYNC_REQ. A client more than 5 s behind asks the host for a fresh SNAPSHOT. No fields.</summary>
    public struct ResyncReqMsg
    {
        public const byte Id = MsgId.ResyncReq;
        public const MsgKind Kind = MsgKind.Intent;

        public byte[] Encode() => new byte[] { Id };

        public static bool TryDecode(byte[] payload, out ResyncReqMsg msg)
        {
            msg = default;
            return Wire.HasId(payload, Id);
        }
    }
}
