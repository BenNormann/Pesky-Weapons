using Pesky.Sim;

namespace Pesky.Session
{
    /// <summary>
    /// One host-side rule per domain (traffic, landing, respawn, clock...),
    /// run in order once per sim tick by HostAuthority. A rule reads the sim
    /// and emits events or state rows through the sink; it never mutates
    /// the sim directly.
    /// </summary>
    public interface IHostRule
    {
        void Tick(WorldSim sim, uint tick, EventSink events);
    }
}
