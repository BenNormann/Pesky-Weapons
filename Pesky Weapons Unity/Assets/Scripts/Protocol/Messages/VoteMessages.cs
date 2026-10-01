namespace Pesky.Protocol
{
    /// <summary>
    /// 0x70 PAUSE_BEGIN, event: the host froze the game (docs/VOTING.md). From the header tick every peer
    /// stops its own body and inputs, its remote views, its goblin brains and its level clock. 6 bytes:
    /// type u8 | tick u32 | reason u8 (PauseReason).
    /// </summary>
    public struct PauseBeginMsg
    {
        public const byte Id = MsgId.PauseBegin;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public PauseReason reason;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 6);
            header.Write(w);
            w.U8((byte)reason);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PauseBeginMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.reason = (PauseReason)r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x71 PAUSE_END, event: the game runs again from the header tick. The ticks between PAUSE_BEGIN and
    /// this are taken out of game time (PauseState.PausedTotalTicks). 6 bytes: type u8 | tick u32 | reason u8.
    /// </summary>
    public struct PauseEndMsg
    {
        public const byte Id = MsgId.PauseEnd;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public PauseReason reason;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 6);
            header.Write(w);
            w.U8((byte)reason);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out PauseEndMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.reason = (PauseReason)r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x72 VOTE_CALL_REQ, intent: V. 2 bytes: type u8 | candidate u8 - VoteTarget.None for a real vote
    /// (every present, non-banished player is a candidate), VoteTarget.Dummy for the tutorial's practice
    /// vote (the dummy is the only candidate; the limits and the timing rules do not apply).
    /// </summary>
    public struct VoteCallReqMsg
    {
        public const byte Id = MsgId.VoteCallReq;
        public const MsgKind Kind = MsgKind.Intent;

        public byte candidate;

        public byte[] Encode() => new NetWriter(Id, 2).U8(candidate).ToArray();

        public static bool TryDecode(byte[] payload, out VoteCallReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.candidate = r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x73 VOTE_START, event to everybody: a meeting is open (the host sent PAUSE_BEGIN just before it, at
    /// the same tick). 13 bytes: type u8 | tick u32 | caller u8 | deadlineTick u32 (a sim tick: the meeting
    /// runs on room time while the game is paused) | eligibleMask u8 (one bit per slot that may vote) |
    /// candidateMask u8 (one bit per slot that may be voted for) | flags u8 (VoteFlags).
    /// </summary>
    public struct VoteStartMsg
    {
        public const byte Id = MsgId.VoteStart;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte caller;
        public uint deadlineTick;
        public byte eligibleMask;
        public byte candidateMask;
        public VoteFlags flags;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 13);
            header.Write(w);
            w.U8(caller).U32(deadlineTick).U8(eligibleMask).U8(candidateMask).U8((byte)flags);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out VoteStartMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.caller = r.U8();
            msg.deadlineTick = r.U32();
            msg.eligibleMask = r.U8();
            msg.candidateMask = r.U8();
            msg.flags = (VoteFlags)r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x74 VOTE_CAST_REQ, intent: one final vote. 2 bytes: type u8 | target u8 - a candidate slot,
    /// VoteTarget.Skip, or VoteTarget.Dummy in a practice vote. A second cast from the same slot is refused.
    /// </summary>
    public struct VoteCastReqMsg
    {
        public const byte Id = MsgId.VoteCastReq;
        public const MsgKind Kind = MsgKind.Intent;

        public byte target;

        public byte[] Encode() => new NetWriter(Id, 2).U8(target).ToArray();

        public static bool TryDecode(byte[] payload, out VoteCastReqMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.target = r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x75 VOTE_TALLY, event to everybody: a validated vote, as it lands. Votes are public. 7 bytes:
    /// type u8 | tick u32 | voter u8 | target u8.
    /// </summary>
    public struct VoteTallyMsg
    {
        public const byte Id = MsgId.VoteTally;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte voter;
        public byte target;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 7);
            header.Write(w);
            w.U8(voter).U8(target);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out VoteTallyMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.voter = r.U8();
            msg.target = r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x76 VOTE_END, event to everybody: the meeting is over. The one message, beside ROUND_RESULT, that
    /// ever names a role: the banished player's. 15 bytes: type u8 | tick u32 | banished u8 (a slot,
    /// VoteTarget.Dummy, or VoteTarget.None for nobody) | role u8 (LabyrinthRole of the banished; Weapon when
    /// nobody) | tally u8 x 8 (each slot's final vote: a target, Skip, or None for no vote).
    /// PAUSE_END follows RunDef.voteResultSeconds later.
    /// </summary>
    public struct VoteEndMsg
    {
        public const byte Id = MsgId.VoteEnd;
        public const MsgKind Kind = MsgKind.Event;

        public EventHeader header;
        public byte banished;
        public LabyrinthRole role;
        public byte[] tally;

        public byte[] Encode()
        {
            var w = new NetWriter(Id, 15);
            header.Write(w);
            w.U8(banished).U8((byte)role);
            for (int i = 0; i < Wire.MaxPlayers; i++) w.U8(tally != null && i < tally.Length ? tally[i] : VoteTarget.None);
            return w.ToArray();
        }

        public static bool TryDecode(byte[] payload, out VoteEndMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.header = EventHeader.Read(r);
            msg.banished = r.U8();
            msg.role = (LabyrinthRole)r.U8();
            msg.tally = new byte[Wire.MaxPlayers];
            for (int i = 0; i < Wire.MaxPlayers; i++) msg.tally[i] = r.U8();
            return !r.Failed;
        }
    }

    /// <summary>
    /// 0x77 VOTE_REFUSED, reply to the asker alone: why the host said no to a call or a cast. 4 bytes:
    /// type u8 | reason u8 (VoteRefusal) | waitTenths u16 (how long until it would be allowed, 0.1 s, when known).
    /// </summary>
    public struct VoteRefusedMsg
    {
        public const byte Id = MsgId.VoteRefused;
        public const MsgKind Kind = MsgKind.Reply;

        public VoteRefusal reason;
        public ushort waitTenths;

        public byte[] Encode() => new NetWriter(Id, 4).U8((byte)reason).U16(waitTenths).ToArray();

        public static bool TryDecode(byte[] payload, out VoteRefusedMsg msg)
        {
            msg = default;
            if (!Wire.HasId(payload, Id)) return false;
            var r = new NetReader(payload);
            msg.reason = (VoteRefusal)r.U8();
            msg.waitTenths = r.U16();
            return !r.Failed;
        }
    }
}
