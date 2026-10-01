using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>
    /// The vote half of the shared world (docs/VOTING.md): who has been BANISHED and the role each one was
    /// revealed to have, how many calls each player has spent, the meeting running right now (caller,
    /// deadline, who may vote, who is a candidate, every vote as it lands) and the last meeting's result.
    /// Public knowledge: votes are open, and a banished player's role is revealed to all. Rides the
    /// snapshot (SnapshotPartKind.Vote) so a late joiner knows who is out and whether a meeting is on.
    /// Nothing secret is here: a living player's role stays in the host's LabyrinthRule.
    /// </summary>
    public sealed class VoteState
    {
        readonly LabyrinthRole[] _revealed = new LabyrinthRole[Wire.MaxPlayers];
        readonly byte[] _callsUsed = new byte[Wire.MaxPlayers];
        readonly byte[] _voteOf = new byte[Wire.MaxPlayers];
        readonly byte[] _lastTally = new byte[Wire.MaxPlayers];

        /// <summary>One bit per slot: out of the run for good (a ghost).</summary>
        public byte BanishedMask { get; private set; }

        /// <summary>A meeting is running: votes are being taken.</summary>
        public bool MeetingOpen { get; private set; }
        public byte CallerSlot { get; private set; }
        /// <summary>The sim tick the meeting closes at whatever has been cast. Room time: the meeting runs while the game is paused.</summary>
        public uint DeadlineTick { get; private set; }
        /// <summary>One bit per slot that may vote in this meeting.</summary>
        public byte EligibleMask { get; private set; }
        /// <summary>One bit per slot that may be voted for in this meeting.</summary>
        public byte CandidateMask { get; private set; }
        public VoteFlags Flags { get; private set; }
        /// <summary>The tutorial's practice vote: the dummy is the only candidate.</summary>
        public bool Practice { get { return (Flags & VoteFlags.Practice) != 0; } }

        /// <summary>A VOTE_END has landed this round: the last result below is valid.</summary>
        public bool HasResult { get; private set; }
        /// <summary>Who the last meeting banished: a slot, VoteTarget.Dummy, or VoteTarget.None.</summary>
        public byte LastBanished { get; private set; }
        public LabyrinthRole LastRole { get; private set; }
        /// <summary>The sim tick the last meeting ended at.</summary>
        public uint LastEndTick { get; private set; }
        /// <summary>The GAME tick the last meeting ended at: the group cooldown counts from here.</summary>
        public uint LastEndGameTick { get; private set; }
        public int MeetingsHeld { get; private set; }

        /// <summary>Bumped on every change, so a view can notice cheaply.</summary>
        public uint Rev { get; private set; }

        public VoteState()
        {
            ResetRound();
        }

        // ---------------------------------------------------------------- reads

        public bool IsBanished(int slot) { return slot >= 0 && slot < Wire.MaxPlayers && (BanishedMask & (1 << slot)) != 0; }

        /// <summary>The role VOTE_END revealed for a banished slot. Weapon for anybody else (it says nothing about a living player).</summary>
        public LabyrinthRole RevealedRole(int slot) { return slot >= 0 && slot < Wire.MaxPlayers ? _revealed[slot] : LabyrinthRole.Weapon; }

        public int CallsUsed(int slot) { return slot >= 0 && slot < Wire.MaxPlayers ? _callsUsed[slot] : 0; }

        public bool IsEligible(int slot) { return slot >= 0 && slot < Wire.MaxPlayers && (EligibleMask & (1 << slot)) != 0; }

        public bool IsCandidate(int slot) { return slot >= 0 && slot < Wire.MaxPlayers && (CandidateMask & (1 << slot)) != 0; }

        /// <summary>What a slot has voted in the running meeting: a slot, VoteTarget.Skip, VoteTarget.Dummy, or VoteTarget.None.</summary>
        public byte VoteOf(int slot) { return slot >= 0 && slot < Wire.MaxPlayers ? _voteOf[slot] : VoteTarget.None; }

        public bool HasVoted(int slot) { return VoteOf(slot) != VoteTarget.None; }

        /// <summary>The last meeting's final votes, by voter slot.</summary>
        public byte LastTallyOf(int slot) { return slot >= 0 && slot < Wire.MaxPlayers ? _lastTally[slot] : VoteTarget.None; }

        /// <summary>True when every eligible voter still present has voted (a leaver's missing vote does not hold the meeting).</summary>
        public bool AllVoted(PlayerTable players)
        {
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if (!IsEligible(i)) continue;
                PlayerState p = players != null ? players[i] : null;
                if (p != null && p.present && _voteOf[i] == VoteTarget.None) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- writes (from messages only)

        public void ResetRound()
        {
            BanishedMask = 0;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                _revealed[i] = LabyrinthRole.Weapon;
                _callsUsed[i] = 0;
                _lastTally[i] = VoteTarget.None;
            }
            CloseMeeting();
            HasResult = false;
            LastBanished = VoteTarget.None;
            LastRole = LabyrinthRole.Weapon;
            LastEndTick = 0;
            LastEndGameTick = 0;
            MeetingsHeld = 0;
            Rev++;
        }

        /// <summary>A slot that emptied (PEER_SLOTS) takes its banishment, its calls and its vote with it, so a newcomer in that slot starts clean.</summary>
        public void ClearAbsent(PlayerTable players)
        {
            bool changed = false;
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                PlayerState p = players != null ? players[i] : null;
                if (p != null && p.present) continue;
                if (!IsBanished(i) && _callsUsed[i] == 0 && _voteOf[i] == VoteTarget.None) continue;
                BanishedMask &= (byte)~(1 << i);
                _revealed[i] = LabyrinthRole.Weapon;
                _callsUsed[i] = 0;
                _voteOf[i] = VoteTarget.None;
                changed = true;
            }
            if (changed) Rev++;
        }

        void CloseMeeting()
        {
            MeetingOpen = false;
            CallerSlot = Wire.NoSlot;
            DeadlineTick = 0;
            EligibleMask = 0;
            CandidateMask = 0;
            Flags = VoteFlags.None;
            for (int i = 0; i < _voteOf.Length; i++) _voteOf[i] = VoteTarget.None;
        }

        public void Apply(in VoteStartMsg msg)
        {
            CloseMeeting();
            MeetingOpen = true;
            CallerSlot = msg.caller;
            DeadlineTick = msg.deadlineTick;
            EligibleMask = msg.eligibleMask;
            CandidateMask = msg.candidateMask;
            Flags = msg.flags;
            if (msg.caller < Wire.MaxPlayers && _callsUsed[msg.caller] < 255) _callsUsed[msg.caller]++;
            MeetingsHeld++;
            Rev++;
        }

        public void Apply(in VoteTallyMsg msg)
        {
            if (!MeetingOpen || msg.voter >= Wire.MaxPlayers) return;
            _voteOf[msg.voter] = msg.target;
            Rev++;
        }

        /// <summary><paramref name="gameTick"/> is the game tick of the message's header: the pause is still on at that tick, so it is the game tick the pause began at, the same on every peer.</summary>
        public void Apply(in VoteEndMsg msg, uint gameTick)
        {
            for (int i = 0; i < Wire.MaxPlayers; i++)
                _lastTally[i] = msg.tally != null && i < msg.tally.Length ? msg.tally[i] : VoteTarget.None;
            if (VoteTarget.IsPlayer(msg.banished))
            {
                BanishedMask |= (byte)(1 << msg.banished);
                _revealed[msg.banished] = msg.role;
            }
            HasResult = true;
            LastBanished = msg.banished;
            LastRole = msg.role;
            LastEndTick = msg.header.tick;
            LastEndGameTick = gameTick;
            CloseMeeting();
            Rev++;
        }

        // ---------------------------------------------------------------- snapshot (55 bytes)

        public void Write(NetWriter w)
        {
            w.U8(BanishedMask);
            for (int i = 0; i < Wire.MaxPlayers; i++) w.U8((byte)_revealed[i]);
            for (int i = 0; i < Wire.MaxPlayers; i++) w.U8(_callsUsed[i]);
            w.Bool(MeetingOpen).U8(CallerSlot).U32(DeadlineTick).U8(EligibleMask).U8(CandidateMask).U8((byte)Flags);
            for (int i = 0; i < Wire.MaxPlayers; i++) w.U8(_voteOf[i]);
            w.Bool(HasResult).U8(LastBanished).U8((byte)LastRole).U32(LastEndTick).U32(LastEndGameTick);
            w.U16((ushort)(MeetingsHeld > 65535 ? 65535 : MeetingsHeld));
            for (int i = 0; i < Wire.MaxPlayers; i++) w.U8(_lastTally[i]);
        }

        public void Read(NetReader r)
        {
            BanishedMask = r.U8();
            for (int i = 0; i < Wire.MaxPlayers; i++) _revealed[i] = (LabyrinthRole)r.U8();
            for (int i = 0; i < Wire.MaxPlayers; i++) _callsUsed[i] = r.U8();
            MeetingOpen = r.Bool();
            CallerSlot = r.U8();
            DeadlineTick = r.U32();
            EligibleMask = r.U8();
            CandidateMask = r.U8();
            Flags = (VoteFlags)r.U8();
            for (int i = 0; i < Wire.MaxPlayers; i++) _voteOf[i] = r.U8();
            HasResult = r.Bool();
            LastBanished = r.U8();
            LastRole = (LabyrinthRole)r.U8();
            LastEndTick = r.U32();
            LastEndGameTick = r.U32();
            MeetingsHeld = r.U16();
            for (int i = 0; i < Wire.MaxPlayers; i++) _lastTally[i] = r.U8();
            Rev++;
        }
    }
}
