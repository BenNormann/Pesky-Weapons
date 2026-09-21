using System;

namespace Pesky.Protocol
{
    /// <summary>
    /// 0x30 SWAP_REQ, intent: a Mage asks the host to exchange the rooms in two orthogonally adjacent
    /// cells. 5 bytes: type u8 | cellA u16 | cellB u16. The host drops it in silence when the asker is not
    /// a Mage, so probing tells a weapon nothing.
    /// </summary>
    public struct SwapReqMsg
    {
        public const byte Id = MsgId.SwapReq;
        public const MsgKind Kind = MsgKind.Intent;

        public ushort cellA;
        public ushort cellB;

        public byte[] Encode() => new NetWriter(Id, 5).U16(cellA).U16(cellB).ToArray();

        public static bool TryDecode(byte[] payload, out SwapReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.cellA = r.U16();
            msg.cellB = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x31 COMPASS_BEND_REQ, intent: a Mage asks for a set of players' compasses to lie. 5 bytes:
    /// type u8 | slots u8 (one bit per player slot) | target u8 (CompassTargetKind; GoodEnd un-bends) |
    /// cell u16 (used only by Cell). The host enforces the minority limit and the cooldown.
    /// </summary>
    public struct CompassBendReqMsg
    {
        public const byte Id = MsgId.CompassBendReq;
        public const MsgKind Kind = MsgKind.Intent;

        public byte slots;
        public CompassTargetKind target;
        public ushort cell;

        public byte[] Encode() => new NetWriter(Id, 5).U8(slots).U8((byte)target).U16(cell).ToArray();

        public static bool TryDecode(byte[] payload, out CompassBendReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.slots = r.U8();
            msg.target = (CompassTargetKind)r.U8();
            msg.cell = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x32 LAB_LAYOUT, state: the whole cell -> room id table, sent when a round starts and inside every
    /// snapshot's labyrinth part. 12 + 2 * count bytes: type u8 | width u8 | height u8 | startCell u16 |
    /// badEndCell u16 | goodEndCell u16 | exitDir u8 (Heading) | count u16 | cells u16 x count.
    /// A 5x5 grid is 62 bytes. The count is a raw u16, not a Wire batch, so it is not capped at 255.
    /// </summary>
    public struct LabLayoutMsg
    {
        public const byte Id = MsgId.LabLayout;
        public const MsgKind Kind = MsgKind.State;

        public byte width;
        public byte height;
        public ushort startCell;
        public ushort badEndCell;
        public ushort goodEndCell;
        public Heading exitDir;
        public ushort[] cells;

        public byte[] Encode()
        {
            int count = cells != null ? cells.Length : 0;
            var w = new NetWriter(Id, 12 + count * 2);
            w.U8(width).U8(height).U16(startCell).U16(badEndCell).U16(goodEndCell).U8((byte)exitDir);
            w.U16((ushort)count);
            for (var i = 0; i < count; i++) w.U16(cells[i]);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out LabLayoutMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.width = r.U8();
            msg.height = r.U8();
            msg.startCell = r.U16();
            msg.badEndCell = r.U16();
            msg.goodEndCell = r.U16();
            msg.exitDir = (Heading)r.U8();
            int count = r.U16();
            if (r.Failed || count > r.Remaining / 2) return false;
            msg.cells = count == 0 ? Array.Empty<ushort>() : new ushort[count];
            for (var i = 0; i < count; i++) msg.cells[i] = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x33 LAB_SWAPPED, event: the rooms in two cells changed places. 9 bytes: type u8 | tick u32 |
    /// cellA u16 | cellB u16. It carries NO author: the Mage is never revealed by a message everyone reads.
    /// </summary>
    public struct LabSwappedMsg
    {
        public const byte Id = MsgId.LabSwapped;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public ushort cellA;
        public ushort cellB;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 9);
            header.Write(w);
            w.U16(cellA).U16(cellB);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out LabSwappedMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.cellA = r.U16();
            msg.cellB = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x34 COMPASS_TARGETS, reply: what THIS player's compass points at. 4 bytes: type u8 |
    /// target u8 (CompassTargetKind) | cell u16. Host to one peer only, and it never says who set it.
    /// </summary>
    public struct CompassTargetsMsg
    {
        public const byte Id = MsgId.CompassTargets;
        public const MsgKind Kind = MsgKind.Reply;

        public CompassTargetKind target;
        public ushort cell;

        public byte[] Encode() => new NetWriter(Id, 4).U8((byte)target).U16(cell).ToArray();

        public static bool TryDecode(byte[] payload, out CompassTargetsMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.target = (CompassTargetKind)r.U8();
            msg.cell = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x35 ROLE_ASSIGN, reply: "you are a Mage". 2 bytes: type u8 | role u8 (LabyrinthRole). Sent to each
    /// Mage alone at round start and to nobody else: a player who is told nothing is a weapon.
    /// </summary>
    public struct RoleAssignMsg
    {
        public const byte Id = MsgId.RoleAssign;
        public const MsgKind Kind = MsgKind.Reply;

        public LabyrinthRole role;

        public byte[] Encode() => new NetWriter(Id, 2).U8((byte)role).ToArray();

        public static bool TryDecode(byte[] payload, out RoleAssignMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.role = (LabyrinthRole)r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x36 ROUND_RESULT, event: the round is over and the Mages are revealed. 8 bytes: type u8 |
    /// tick u32 | outcome u8 (RoundOutcome) | escaped u8 (slot bits) | mages u8 (slot bits). This is the
    /// ONE message that names the Mages, and it is only sent once the round can no longer be played.
    /// </summary>
    public struct RoundResultMsg
    {
        public const byte Id = MsgId.RoundResult;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public RoundOutcome outcome;
        public byte escapedMask;
        public byte mageMask;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 8);
            header.Write(w);
            w.U8((byte)outcome).U8(escapedMask).U8(mageMask);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out RoundResultMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.outcome = (RoundOutcome)r.U8();
            msg.escapedMask = r.U8();
            msg.mageMask = r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x37 PLAYER_DOWN, event: a player is out of the round until respawnTick. 10 bytes: type u8 |
    /// tick u32 | slot u8 | respawnTick u32.
    /// </summary>
    public struct PlayerDownMsg
    {
        public const byte Id = MsgId.PlayerDown;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte slot;
        public uint respawnTick;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 10);
            header.Write(w);
            w.U8(slot).U32(respawnTick);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PlayerDownMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.slot = r.U8();
            msg.respawnTick = r.U32();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x38 PLAYER_RESPAWN, event: a downed player comes back beside anchorSlot, in cell. 9 bytes:
    /// type u8 | tick u32 | slot u8 | anchorSlot u8 (0xFF nobody) | cell u16 (0xFFFF unknown).
    /// </summary>
    public struct PlayerRespawnMsg
    {
        public const byte Id = MsgId.PlayerRespawn;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte slot;
        public byte anchorSlot;
        public ushort cell;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 9);
            header.Write(w);
            w.U8(slot).U8(anchorSlot).U16(cell);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PlayerRespawnMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.slot = r.U8();
            msg.anchorSlot = r.U8();
            msg.cell = r.U16();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x39 LEGEND, event: a player's legend is now this. 10 bytes: type u8 | tick u32 | slot u8 |
    /// legend i32. Absolute, never a delta, so a dropped event cannot drift.
    /// </summary>
    public struct LegendMsg
    {
        public const byte Id = MsgId.Legend;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte slot;
        public int legend;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 10);
            header.Write(w);
            w.U8(slot).I32(legend);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out LegendMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.slot = r.U8();
            msg.legend = r.I32();
            return !r.Failed;
        }
    }
}
