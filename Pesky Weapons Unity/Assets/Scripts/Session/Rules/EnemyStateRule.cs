using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Sim;

namespace Pesky.Session.Rules
{
    /// <summary>
    /// ENEMY_STATE at 10 Hz plus at once on a state change: ATCK's TowerRule pattern (a next-state tick
    /// and a dirty flag). The rows come from the Game-side brains through IHostWorld, because the
    /// NavMeshAgents live there; this rule only owns the schedule and the batching.
    /// </summary>
    public sealed class EnemyStateRule : IHostRule
    {
        /// <summary>Every 2 ticks = 10 Hz.</summary>
        public const int StateIntervalTicks = Protocol.Tick.PerSecond / 10;

        readonly List<EnemyStateMsg.Row> _rows = new List<EnemyStateMsg.Row>(64);
        uint _nextStateTick;

        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            var world = events.World;
            if (world == null) return;

            var due = tick >= _nextStateTick;
            _rows.Clear();
            var dirty = world.CollectEnemyRows(_rows, due);
            if (due) _nextStateTick = tick + StateIntervalTicks;
            if (_rows.Count == 0 || (!due && !dirty)) return;

            for (var start = 0; start < _rows.Count; start += Wire.MaxBatch)
            {
                var msg = new EnemyStateMsg();
                var n = System.Math.Min(Wire.MaxBatch, _rows.Count - start);
                msg.rows = _rows.GetRange(start, n);
                events.Emit(msg.Encode());
            }
        }
    }
}
