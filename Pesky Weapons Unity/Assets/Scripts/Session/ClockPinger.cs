using Pesky.Protocol;

namespace Pesky.Session
{
    /// <summary>
    /// The client's CLOCK_PING schedule: four pings two ticks apart right
    /// after the join, then one every ten seconds. Hands out ping ids; the
    /// pong echoes the client's send time, so nothing is remembered here.
    /// </summary>
    public sealed class ClockPinger
    {
        public const int BurstCount = 4;
        public const int BurstSpacingTicks = 2;
        public const int IntervalTicks = 10 * Protocol.Tick.PerSecond;

        ushort _nextId;
        int _sent;
        uint _nextTick;
        bool _armed;

        public bool IsArmed => _armed;
        public int Sent => _sent;

        /// <summary>Starts the burst at this tick.</summary>
        public void Arm(uint tick)
        {
            _armed = true;
            _sent = 0;
            _nextTick = tick;
        }

        public void Disarm()
        {
            _armed = false;
        }

        public bool Due(uint tick) => _armed && tick >= _nextTick;

        /// <summary>The next ping, stamped with the local send time; advances the schedule.</summary>
        public ClockPingMsg Next(uint tick, long localMs)
        {
            var ping = new ClockPingMsg();
            ping.pingId = _nextId++;
            ping.clientMs = unchecked((uint)localMs);
            _sent++;
            _nextTick = tick + (uint)(_sent < BurstCount ? BurstSpacingTicks : IntervalTicks);
            return ping;
        }
    }
}
