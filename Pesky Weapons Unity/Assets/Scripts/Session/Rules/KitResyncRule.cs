using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// KIT_STATE again, every two seconds, for every kit piece that has changed since the level loaded: the host's
    /// kit table re-sent as the same events that built it. A KIT_STATE is a one-shot. A peer that misses one (a frame
    /// lost while its relay socket reconnected, a peer the transport had forgotten for a moment) keeps the old state
    /// for good, because nothing is ever sent again for a piece that does not change again: a latching lever, the
    /// gate it holds up. Round 31: the Porter Room's grate stood open on one screen and shut on another.
    /// Every ApplyKit case is idempotent (a piece already in that state is left alone), so a repeat costs a client
    /// nothing. The kinds whose apply does something beyond the piece are left out: Rune (the modifier would attach
    /// again), Anvil (a heal), Lightning (a bolt), PorterCarry (a moment, not a state).
    /// </summary>
    public sealed class KitResyncRule : IHostRule
    {
        /// <summary>Every 2 seconds.</summary>
        public const int IntervalTicks = Protocol.Tick.PerSecond * 2;

        uint _nextTick;

        /// <summary>True for the kinds whose KIT_STATE may be repeated without a side effect.</summary>
        public static bool Resyncs(KitKind kind)
        {
            switch (kind)
            {
                case KitKind.Rune:
                case KitKind.Anvil:
                case KitKind.Lightning:
                case KitKind.PorterCarry:
                    return false;
                default:
                    return true;
            }
        }

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            if (tick < _nextTick) return;
            _nextTick = tick + IntervalTicks;
            var rows = sim.Kit.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!Resyncs(row.kind)) continue;
                var msg = new KitStateMsg();
                msg.header = events.Header();
                msg.kind = row.kind;
                msg.pieceId = row.pieceId;
                msg.state = row.state;
                msg.actor = row.actor;
                msg.value = row.value;
                msg.clockMs = row.clockMs;
                events.Emit(msg.Encode());
            }
        }
    }
}
