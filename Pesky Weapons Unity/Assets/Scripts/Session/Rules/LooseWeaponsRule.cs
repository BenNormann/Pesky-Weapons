using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// LOOSE_WEAPONS at 10 Hz: the host simulates every unheld weapon (it is the peer that reports their contacts),
    /// and this is how the other peers learn where they ended up. EnemyStateRule's shape: the rows come from the
    /// Game side through IHostWorld (the bodies live there), this rule owns the schedule and the batching. Nothing is
    /// sent for a weapon that has not moved: the Game side keeps the last pose it sent and only returns the changes,
    /// plus a one second keepalive so a peer that missed a row, or a late joiner, converges.
    /// </summary>
    public sealed class LooseWeaponsRule : IHostRule
    {
        /// <summary>Every 2 ticks = 10 Hz.</summary>
        public const int StateIntervalTicks = Protocol.Tick.PerSecond / 10;

        readonly List<LooseWeaponsMsg.Row> _rows = new List<LooseWeaponsMsg.Row>(16);
        uint _nextStateTick;

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            var world = events.World;
            if (world == null || tick < _nextStateTick) return;
            _nextStateTick = tick + StateIntervalTicks;

            _rows.Clear();
            world.CollectLooseWeaponRows(_rows);
            for (var start = 0; start < _rows.Count; start += Wire.MaxBatch)
            {
                var msg = new LooseWeaponsMsg();
                var n = System.Math.Min(Wire.MaxBatch, _rows.Count - start);
                msg.tick = tick;
                msg.rows = _rows.GetRange(start, n);
                events.Emit(msg.Encode());
            }
        }
    }
}
