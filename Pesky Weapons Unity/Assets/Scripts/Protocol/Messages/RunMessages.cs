using System;

namespace Pesky.Protocol
{
    /// <summary>
    /// 0x60 RUN_LAYOUT, state: the rooms of this run in order, from the first room after the start room to
    /// the last one before the Exit room. Sent once the host's scene has answered which rooms are in the
    /// pool, and inside every snapshot's Run part. 2 + 2n bytes: type u8 | count u8 | roomIds u16 x count.
    /// </summary>
    public struct RunLayoutMsg
    {
        public const byte Id = MsgId.RunLayout;
        public const MsgKind Kind = MsgKind.State;

        public ushort[] roomIds;

        public byte[] Encode()
        {
            int count = roomIds != null ? roomIds.Length : 0;
            if (count > Wire.MaxBatch) count = Wire.MaxBatch;
            var w = new NetWriter(Id, 2 + count * 2);
            w.U8((byte)count);
            for (int i = 0; i < count; i++) w.U16(roomIds[i]);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out RunLayoutMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            int count = r.U8();
            if (r.Failed || count > r.Remaining / 2) return false;
            msg.roomIds = count == 0 ? Array.Empty<ushort>() : new ushort[count];
            for (int i = 0; i < count; i++) msg.roomIds[i] = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x61 RUN_START, event: the first player left the start room and the timer is running. 13 bytes:
    /// type u8 | tick u32 | startTick u32 | deadlineTick u32. Both are room-clock sim ticks (Tick.PerSecond
    /// per second), so every peer counts down the same number.
    /// </summary>
    public struct RunStartMsg
    {
        public const byte Id = MsgId.RunStart;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public uint startTick;
        public uint deadlineTick;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 13);
            header.Write(w);
            w.U32(startTick).U32(deadlineTick);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out RunStartMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.startTick = r.U32();
            msg.deadlineTick = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x2C CURSE_REQ, intent: a Mage fragment asks to curse the player under his crosshair. 3 bytes:
    /// type u8 | targetSlot u8 (NudgeReqMsg.PracticeTarget = the tutorial dummy) | curse u8 (CurseKind).
    /// </summary>
    public struct CurseReqMsg
    {
        public const byte Id = MsgId.CurseReq;
        public const MsgKind Kind = MsgKind.Intent;

        public byte targetSlot;
        public CurseKind curse;

        public byte[] Encode() => new NetWriter(Id, 3).U8(targetSlot).U8((byte)curse).ToArray();

        public static bool TryDecode(byte[] payload, out CurseReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.targetSlot = r.U8();
            msg.curse = (CurseKind)r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x2D CURSE_EVENT, event to everybody: a validated curse. Only the target's OWNER does anything with
    /// it (applies the effect to its own body and screen); every other peer ignores it. It deliberately
    /// carries NO requester: nothing on the wire says who did it. 9 bytes: type u8 | tick u32 |
    /// targetSlot u8 | curse u8 (CurseKind) | durationTenths u16 (0.1 s).
    /// </summary>
    public struct CurseEventMsg
    {
        public const byte Id = MsgId.CurseEvent;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte targetSlot;
        public CurseKind curse;
        public ushort durationTenths;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 9);
            header.Write(w);
            w.U8(targetSlot).U8((byte)curse).U16(durationTenths);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out CurseEventMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.targetSlot = r.U8();
            msg.curse = (CurseKind)r.U8();
            msg.durationTenths = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x2E CURSE_REFUSED, reply to the asking Mage alone: why the host said no. Never sent for "not a
    /// Mage" or "no round", which stay silent like every other Mage refusal. 5 bytes: type u8 |
    /// reason u8 (CurseRefusal) | targetSlot u8 | waitTenths u16 (cooldown left, 0.1 s).
    /// </summary>
    public struct CurseRefusedMsg
    {
        public const byte Id = MsgId.CurseRefused;
        public const MsgKind Kind = MsgKind.Reply;

        public CurseRefusal reason;
        public byte targetSlot;
        public ushort waitTenths;

        public byte[] Encode() => new NetWriter(Id, 5).U8((byte)reason).U8(targetSlot).U16(waitTenths).ToArray();

        public static bool TryDecode(byte[] payload, out CurseRefusedMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.reason = (CurseRefusal)r.U8();
            msg.targetSlot = r.U8();
            msg.waitTenths = r.U16();
            return !r.Failed;
        }
    }
}
