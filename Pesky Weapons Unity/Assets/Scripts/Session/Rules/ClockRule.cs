using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// The host's clock duties: TIME_SYNC at 0.5 Hz as a rule, and the
    /// CLOCK_PONG answer to every CLOCK_PING as a validator. The pong goes
    /// out raw and at once so queue delay does not skew the client's
    /// estimate.
    /// </summary>
    public sealed class ClockRule : IHostRule, IIntentValidator
    {
        public const int SyncIntervalTicks = 2 * Protocol.Tick.PerSecond;

        uint _nextSyncTick;

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            if (tick < _nextSyncTick) return;
            _nextSyncTick = tick + SyncIntervalTicks;
            var sync = new TimeSyncMsg();
            sync.hostRoomMs = unchecked((uint)events.NowMs);
            events.Emit(sync.Encode());
        }

        public void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            if (!ClockPingMsg.TryDecode(payload, out var ping)) return;
            var pong = new ClockPongMsg();
            pong.pingId = ping.pingId;
            pong.clientMs = ping.clientMs;
            pong.hostRoomMs = unchecked((uint)events.NowMs);
            events.ReplyNow(fromSlot, pong.Encode());
        }
    }
}
