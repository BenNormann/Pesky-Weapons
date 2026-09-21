using System;

namespace Pesky.Protocol
{
    /// <summary>
    /// Every enum the message tables imply, each one byte on the wire with an
    /// explicit value so a reorder in source can never change a layout. Owned
    /// by the wire contract; Sim and Game reuse these rather than mirroring them.
    /// </summary>
    public enum MsgKind : byte
    {
        Unknown = 0,    // not a gameplay id (M0 ids and unassigned bytes)
        Event = 1,      // host -> all, a fact at a tick, applied once in order
        Intent = 2,     // client -> host, a request
        Reply = 3,      // host -> one, the answer to one intent
        State = 4,      // host -> all, absolute rows that overwrite
        Stream = 5,     // owner -> all, player-owned cosmetics
        Container = 6,  // FRAME
    }

    public enum SessionPhase : byte { Lobby = 0, Playing = 1, Ended = 2 }

    /// <summary>Why a run ended. Stable: append only, never renumber.</summary>
    public enum SessionEndReason : byte
    {
        HostEnded = 0,
        HostLeft = 1,
        /// <summary>The crew reached the exit: the labyrinth let them out.</summary>
        Escaped = 2,
        /// <summary>Every weapon is down and there is nothing left to possess.</summary>
        CrewLost = 3,
    }

    /// <summary>
    /// The tables a snapshot carries, in the order SnapshotCodec sends them.
    /// End is the terminator and is never a table, so it must stay last:
    /// SnapshotReceiver sizes its table array from its value. Enemies, Doors
    /// and Pickups are inserted before End as their tables land.
    /// </summary>
    public enum SnapshotPartKind : byte
    {
        World = 0, Players = 1, Weapons = 2, Enemies = 3, Kit = 4, Labyrinth = 5, Pad = 6, End = 7,
    }

    /// <summary>What a hit claim says it hit. World means the blast found nothing claimable.</summary>
    public enum VictimKind : byte { Weapon = 0, Enemy = 1, Prop = 2, World = 3 }

    /// <summary>Why the host refused a hit claim.</summary>
    public enum HitRejectReason : byte
    {
        UnknownVictim = 0, VictimDead = 1, OutOfRange = 2, RateExceeded = 3,
        StaleTick = 4, DuplicateSeq = 5, NotPlaying = 6,
    }

    /// <summary>What put a weapon down. Stable: append only.</summary>
    public enum DeathCause : byte { Impact = 0, Enemy = 1, Fall = 2, Hazard = 3, OutOfBounds = 4 }

    /// <summary>
    /// The owner-streamed weapon bits. Stage 1 declares only what the solo
    /// movement slice already has; curse, traitor and rune bits arrive with
    /// the messages that carry them.
    /// </summary>
    [Flags]
    public enum PlayerFlags : byte
    {
        None = 0,
        /// <summary>The weapon is winding up a launch.</summary>
        Charging = 1,
        /// <summary>The weapon is off the ground after a launch.</summary>
        Airborne = 2,
        /// <summary>The player has left the weapon and is flying as a soul.</summary>
        Soul = 4,
        /// <summary>The weapon is at rest, which is what the goblins ignore.</summary>
        Still = 8,
    }

    [Flags]
    public enum VoiceFlags : byte { None = 0, MicLive = 1, Speaking = 2 }
}
