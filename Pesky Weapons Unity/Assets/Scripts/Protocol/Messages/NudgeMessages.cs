using UnityEngine;

namespace Pesky.Protocol
{
    /// <summary>The Mage's mid-air power: left click NUDGES along his view, right click PULLS toward him. Stable: append only.</summary>
    public enum NudgeMode : byte { Nudge = 0, Pull = 1 }

    /// <summary>Why the host said no to a NUDGE_REQ. Sent back to the asker alone. Stable: append only.</summary>
    public enum NudgeRefusal : byte
    {
        /// <summary>Not another player holding a weapon (or the tutorial dummy): a soul, an empty slot, yourself.</summary>
        NoTarget = 0,
        NotAirborne = 1,
        OutOfRange = 2,
        NoLineOfSight = 3,
        Cooldown = 4,
    }

    /// <summary>
    /// 0x29 NUDGE_REQ, intent: a Mage fragment asks to nudge or pull an airborne player.
    /// 9 bytes: type u8 | targetSlot u8 (PracticeTarget = the tutorial dummy) | mode u8 (NudgeMode) |
    /// viewDir i16 x3 (a unit vector at 1/256 per component, the Vel3 codec).
    /// </summary>
    public struct NudgeReqMsg
    {
        public const byte Id = MsgId.NudgeReq;
        public const MsgKind Kind = MsgKind.Intent;

        /// <summary>
        /// The target "slot" of the tutorial's practice dummy: out of the eight real ones and not NoSlot. The
        /// host asks IHostWorld for its position and airborne state instead of a player row. Tutorial only.
        /// </summary>
        public const byte PracticeTarget = 0xFE;

        public byte targetSlot;
        public NudgeMode mode;
        public Vector3 viewDir;

        public byte[] Encode() => new NetWriter(Id, 9).U8(targetSlot).U8((byte)mode).Vel3(viewDir).ToArray();

        public static bool TryDecode(byte[] payload, out NudgeReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.targetSlot = r.U8();
            msg.mode = (NudgeMode)r.U8();
            msg.viewDir = r.Vel3();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x2A NUDGE_EVENT, event to everybody: a validated nudge. The target's OWNER applies the velocity
    /// change to its own body; a peer whose player is a free soul shows a faint wisp there. It deliberately
    /// carries NO requester: nothing on the wire says who did it.
    /// 13 bytes: type u8 | tick u32 | targetSlot u8 | mode u8 | velocityChange i16 x3 (1/256 m/s).
    /// </summary>
    public struct NudgeEventMsg
    {
        public const byte Id = MsgId.NudgeEvent;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte targetSlot;
        public NudgeMode mode;
        public Vector3 velocityChange;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 13);
            header.Write(w);
            w.U8(targetSlot).U8((byte)mode).Vel3(velocityChange);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out NudgeEventMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.targetSlot = r.U8();
            msg.mode = (NudgeMode)r.U8();
            msg.velocityChange = r.Vel3();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x2B NUDGE_REFUSED, reply to the asking Mage alone: why the host said no, so his screen can say so.
    /// Never sent for "not a Mage" or "no round", which stay silent like every other Mage refusal.
    /// 5 bytes: type u8 | reason u8 (NudgeRefusal) | targetSlot u8 | waitTenths u16 (cooldown left, 0.1 s).
    /// </summary>
    public struct NudgeRefusedMsg
    {
        public const byte Id = MsgId.NudgeRefused;
        public const MsgKind Kind = MsgKind.Reply;

        public NudgeRefusal reason;
        public byte targetSlot;
        public ushort waitTenths;

        public byte[] Encode() => new NetWriter(Id, 5).U8((byte)reason).U8(targetSlot).U16(waitTenths).ToArray();

        public static bool TryDecode(byte[] payload, out NudgeRefusedMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.reason = (NudgeRefusal)r.U8();
            msg.targetSlot = r.U8();
            msg.waitTenths = r.U16();
            return !r.Failed;
        }
    }
}
