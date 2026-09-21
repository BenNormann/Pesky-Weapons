namespace Pesky.Protocol
{
    /// <summary>
    /// 0x50 KIT_STATE, event: the one generic kit message. Doors, plates, keys, runes, anvils, magnets,
    /// lifts, ropes, pots, levers, counterweights, scales, cracked walls, porter gates, lightning bolts
    /// and the porter's carry all travel as (kind, piece id, state) with three optional extras.
    /// 19 bytes: type u8 | tick u32 | kind u8 (KitKind) | pieceId u16 | state u8 | actor u16 (weapon id,
    /// 0xFFFF = none) | value i32 | clockMs u32 (the LevelClock millisecond the change counts from).
    /// </summary>
    public struct KitStateMsg
    {
        public const byte Id = MsgId.KitState;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public KitKind kind;
        public ushort pieceId;
        public byte state;
        public ushort actor;
        public int value;
        public uint clockMs;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 19);
            header.Write(w);
            w.U8((byte)kind).U16(pieceId).U8(state).U16(actor).I32(value).U32(clockMs);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out KitStateMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.kind = (KitKind)r.U8();
            msg.pieceId = r.U16();
            msg.state = r.U8();
            msg.actor = r.U16();
            msg.value = r.I32();
            msg.clockMs = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x51 KIT_REQ, intent: a client's own weapon did something to a kit piece (touched a key, cut a rope,
    /// flipped a lever). 9 bytes: type u8 | kind u8 | pieceId u16 | state u8 | actor u16 | speed u16 (1/100 m/s).
    /// </summary>
    public struct KitReqMsg
    {
        public const byte Id = MsgId.KitReq;
        public const MsgKind Kind = MsgKind.Intent;

        public KitKind kind;
        public ushort pieceId;
        public byte state;
        public ushort actor;
        public float speed;

        public byte[] Encode() =>
            new NetWriter(Id, 9).U8((byte)kind).U16(pieceId).U8(state).U16(actor).Speed(speed).ToArray();

        public static bool TryDecode(byte[] payload, out KitReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.kind = (KitKind)r.U8();
            msg.pieceId = r.U16();
            msg.state = r.U8();
            msg.actor = r.U16();
            msg.speed = r.Speed();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x52 WORLD_RESET, state: the host loaded a level, so every peer empties its weapon, enemy and kit
    /// tables before the new level's rows arrive. 5 bytes: type u8 | sceneHash u32.
    /// </summary>
    public struct WorldResetMsg
    {
        public const byte Id = MsgId.WorldReset;
        public const MsgKind Kind = MsgKind.State;

        public uint sceneHash;

        public byte[] Encode() => new NetWriter(Id, 5).U32(sceneHash).ToArray();

        public static bool TryDecode(byte[] payload, out WorldResetMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.sceneHash = r.U32();
            return !r.Failed;
        }
    }
}
