namespace Pesky.Protocol
{
    /// <summary>
    /// The simulation tick: 20 Hz, 50 ms. Every event carries the tick it takes
    /// effect. Room time (host milliseconds since session start) converts to a
    /// tick here and nowhere else.
    /// </summary>
    public static class Tick
    {
        public const int Ms = 50;
        public const int PerSecond = 1000 / Ms;

        public static uint FromMs(long roomMs) => roomMs <= 0 ? 0u : (uint)(roomMs / Ms);
        public static long ToMs(uint tick) => (long)tick * Ms;
    }
}
