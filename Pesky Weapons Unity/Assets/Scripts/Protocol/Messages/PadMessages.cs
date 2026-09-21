using System;

namespace Pesky.Protocol
{
    /// <summary>
    /// What kind of mark a scratch-pad stroke is. An ERASER is not a delete: it is a stroke painted in the
    /// canvas colour on top of what is there, so every peer replays the same list in the same order and
    /// ends up with the same picture.
    /// </summary>
    [Flags]
    public enum PadFlags : byte
    {
        None = 0,
        Erase = 1,
    }

    /// <summary>
    /// 0x3A PAD_STROKE_REQ, intent: one polyline the local player just drew on the team's shared scratch
    /// pad. 4 + 4n bytes: type u8 | flags u8 (PadFlags) | width u8 (class 0..2) | count u8 |
    /// (x u16, y u16) x count.
    ///
    /// Points are NORMALISED canvas coordinates quantised to u16: 0 is the left / top edge, 65535 the
    /// right / bottom, so the picture is the same on every screen whatever the window size. At most
    /// <see cref="MaxPoints"/> points ride one message (260 bytes, far below the 16,000 byte FRAME
    /// ceiling); a longer stroke is split into several messages that share their joining point.
    /// </summary>
    public struct PadStrokeReqMsg
    {
        public const byte Id = MsgId.PadStrokeReq;
        public const MsgKind Kind = MsgKind.Intent;

        /// <summary>The most points one message may carry. 64 x 4 bytes + 4 = 260 bytes.</summary>
        public const int MaxPoints = 64;

        /// <summary>The widest pen class. 0 thin, 1 medium, 2 thick.</summary>
        public const byte MaxWidth = 2;

        public PadFlags flags;
        public byte width;

        /// <summary>x, y pairs: two entries per point.</summary>
        public ushort[] points;

        public int PointCount { get { return points == null ? 0 : points.Length / 2; } }

        public byte[] Encode()
        {
            int n = PointCount;
            if (n > MaxPoints) n = MaxPoints;
            NetWriter w = new NetWriter(Id, 4 + n * 4);
            w.U8((byte)flags).U8(width).U8((byte)n);
            for (int i = 0; i < n * 2; i++) w.U16(points[i]);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PadStrokeReqMsg msg)
        {
            msg = default(PadStrokeReqMsg);
            if (!Wire.HasId(payload, Id)) return false;
            NetReader r = new NetReader(payload);
            msg.flags = (PadFlags)r.U8();
            msg.width = r.U8();
            int n = r.U8();
            if (r.Failed || n > MaxPoints || n * 4 > r.Remaining) return false;
            msg.points = n == 0 ? Array.Empty<ushort>() : new ushort[n * 2];
            for (int i = 0; i < n * 2; i++) msg.points[i] = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x3B PAD_STROKE, event: a stroke the host accepted, with the sequence number every peer draws it in
    /// and the slot that drew it (which is where its colour comes from). 11 + 4n bytes: type u8 |
    /// tick u32 | seq u16 | slot u8 | flags u8 | width u8 | count u8 | (x u16, y u16) x count.
    ///
    /// The pad is public: nothing here is a secret and every peer keeps the same list.
    /// </summary>
    public struct PadStrokeMsg
    {
        public const byte Id = MsgId.PadStroke;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort seq;
        public byte slot;
        public PadFlags flags;
        public byte width;
        public ushort[] points;

        public int PointCount { get { return points == null ? 0 : points.Length / 2; } }

        public byte[] Encode()
        {
            int n = PointCount;
            if (n > PadStrokeReqMsg.MaxPoints) n = PadStrokeReqMsg.MaxPoints;
            NetWriter w = new NetWriter(Id, 11 + n * 4);
            header.Write(w);
            w.U16(seq).U8(slot).U8((byte)flags).U8(width).U8((byte)n);
            for (int i = 0; i < n * 2; i++) w.U16(points[i]);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PadStrokeMsg msg)
        {
            msg = default(PadStrokeMsg);
            if (!Wire.HasId(payload, Id)) return false;
            NetReader r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.seq = r.U16();
            msg.slot = r.U8();
            msg.flags = (PadFlags)r.U8();
            msg.width = r.U8();
            int n = r.U8();
            if (r.Failed || n > PadStrokeReqMsg.MaxPoints || n * 4 > r.Remaining) return false;
            msg.points = n == 0 ? Array.Empty<ushort>() : new ushort[n * 2];
            for (int i = 0; i < n * 2; i++) msg.points[i] = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x3C PAD_CLEAR, event: wipe the shared scratch pad. 5 bytes: type u8 | tick u32. The host sends one
    /// at the start of every round, and the host alone may send one from the pad's CLEAR button.
    /// </summary>
    public struct PadClearMsg
    {
        public const byte Id = MsgId.PadClear;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;

        public byte[] Encode()
        {
            NetWriter w = new NetWriter(Id, 5);
            header.Write(w);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PadClearMsg msg)
        {
            msg = default(PadClearMsg);
            if (!Wire.HasId(payload, Id)) return false;
            NetReader r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            return !r.Failed;
        }
    }
}
