using Pesky.Protocol;

namespace Pesky.Sim
{
    /// <summary>
    /// The host-owned PAUSE (docs/VOTING.md): whether the game is frozen, since which tick, and how many
    /// ticks every pause so far has taken out of the game. ROOM time never stops (the sim tick, the clocks and
    /// a vote meeting run on it); GAME time does: GameTick(tick) and PausedMsAt(roomMs) take the pauses out,
    /// and everything scheduled reads those (LevelClock through SessionRunner.LevelMs, the run timer, the
    /// host's cooldowns). Public, rides the snapshot (SnapshotPartKind.Vote), reset on every phase change.
    /// Any host rule may begin and end a pause with BeginPayload / EndPayload; the vote is its first user.
    /// </summary>
    public sealed class PauseState
    {
        public bool Paused { get; private set; }
        public PauseReason Reason { get; private set; }

        /// <summary>The sim tick the current pause began at (the PAUSE_BEGIN header tick).</summary>
        public uint PauseStartTick { get; private set; }

        /// <summary>Ticks taken out of the game by every pause that has already ended.</summary>
        public uint PausedTotalTicks { get; private set; }

        /// <summary>Bumped on every change, so a view can notice cheaply.</summary>
        public uint Rev { get; private set; }

        /// <summary>The game tick at a sim tick: the sim tick minus every paused tick, the current pause included. It stands still while paused.</summary>
        public uint GameTick(uint tick)
        {
            uint lost = PausedTotalTicks;
            if (Paused && tick > PauseStartTick) lost += tick - PauseStartTick;
            return tick > lost ? tick - lost : 0u;
        }

        /// <summary>Milliseconds the pauses have taken out of the game by a room time, the current pause included.</summary>
        public long PausedMsAt(long roomMs)
        {
            long lost = Protocol.Tick.ToMs(PausedTotalTicks);
            if (Paused)
            {
                long start = Protocol.Tick.ToMs(PauseStartTick);
                if (roomMs > start) lost += roomMs - start;
            }
            return lost;
        }

        public void Reset()
        {
            Paused = false;
            Reason = PauseReason.None;
            PauseStartTick = 0;
            PausedTotalTicks = 0;
            Rev++;
        }

        public void Apply(in PauseBeginMsg msg)
        {
            if (Paused) return;
            Paused = true;
            Reason = msg.reason;
            PauseStartTick = msg.header.tick;
            Rev++;
        }

        public void Apply(in PauseEndMsg msg)
        {
            if (!Paused) return;
            if (msg.header.tick > PauseStartTick) PausedTotalTicks += msg.header.tick - PauseStartTick;
            Paused = false;
            Reason = PauseReason.None;
            Rev++;
        }

        /// <summary>The PAUSE_BEGIN a host rule emits.</summary>
        public static byte[] BeginPayload(EventHeader header, PauseReason reason)
        {
            PauseBeginMsg msg = new PauseBeginMsg();
            msg.header = header;
            msg.reason = reason;
            return msg.Encode();
        }

        /// <summary>The PAUSE_END a host rule emits.</summary>
        public static byte[] EndPayload(EventHeader header, PauseReason reason)
        {
            PauseEndMsg msg = new PauseEndMsg();
            msg.header = header;
            msg.reason = reason;
            return msg.Encode();
        }

        // ---------------------------------------------------------------- snapshot

        public void Write(NetWriter w)
        {
            w.Bool(Paused).U8((byte)Reason).U32(PauseStartTick).U32(PausedTotalTicks);
        }

        public void Read(NetReader r)
        {
            Paused = r.Bool();
            Reason = (PauseReason)r.U8();
            PauseStartTick = r.U32();
            PausedTotalTicks = r.U32();
            Rev++;
        }
    }
}
