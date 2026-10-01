using System;

namespace Pesky.Protocol
{
    /// <summary>
    /// Why the host froze the game. Rides PAUSE_BEGIN and PAUSE_END (docs/VOTING.md). The pause state is
    /// reusable: a new reason is one value here plus one host rule that emits the two events. Stable: append only.
    /// </summary>
    public enum PauseReason : byte
    {
        None = 0,
        /// <summary>A vote meeting: everybody freezes until the result has been shown.</summary>
        Vote = 1,
    }

    /// <summary>Why the host said no to a VOTE_CALL_REQ or a VOTE_CAST_REQ. Sent to the asker alone. Stable: append only.</summary>
    public enum VoteRefusal : byte
    {
        /// <summary>No round is open, or the asker is not a present player.</summary>
        NoRound = 0,
        /// <summary>A banished player neither calls nor votes.</summary>
        Banished = 1,
        /// <summary>RunDef.voteCallsPerPlayer are spent for this run.</summary>
        NoCallsLeft = 2,
        /// <summary>RunDef.voteGroupCooldown has not passed since the last meeting ended (waitTenths says how long).</summary>
        GroupCooldown = 3,
        /// <summary>The run timer has not started, or RunDef.voteNoVoteBeforeSeconds have not passed since it did (waitTenths when known).</summary>
        TooEarly = 4,
        /// <summary>A meeting (or another pause) is already running.</summary>
        AlreadyPaused = 5,
        /// <summary>A cast with no meeting open.</summary>
        NoMeeting = 6,
        /// <summary>A cast from a slot the meeting did not list as a voter.</summary>
        NotEligible = 7,
        /// <summary>One final vote each: this slot already voted.</summary>
        AlreadyVoted = 8,
        /// <summary>The target is not a candidate of this meeting and not SKIP.</summary>
        BadTarget = 9,
        /// <summary>The tutorial's practice vote was asked for where there is no dummy to vote out.</summary>
        NoTarget = 10,
    }

    /// <summary>The flags byte of VOTE_START. Stable: append only.</summary>
    [Flags]
    public enum VoteFlags : byte
    {
        None = 0,
        /// <summary>The tutorial's practice vote: the only candidate is the practice dummy (VoteTarget.Dummy).</summary>
        Practice = 1,
    }

    /// <summary>The reserved values of a vote target byte beside the eight player slots (0-7).</summary>
    public static class VoteTarget
    {
        /// <summary>SKIP VOTE: a cast for nobody.</summary>
        public const byte Skip = 0xFD;
        /// <summary>The tutorial's practice dummy: the same reserved id the nudge and the curses use (0xFE).</summary>
        public const byte Dummy = NudgeReqMsg.PracticeTarget;
        /// <summary>Not voted yet; also "nobody was banished" on VOTE_END (0xFF).</summary>
        public const byte None = Wire.NoSlot;

        public static bool IsPlayer(byte target) { return target < Wire.MaxPlayers; }
    }
}
