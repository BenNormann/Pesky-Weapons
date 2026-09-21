namespace Pesky.Protocol
{
    /// <summary>
    /// The tick an event takes effect at: the first field after the type byte
    /// of every Event message. The host stamps now + 3 ticks so every peer
    /// applies it on the same tick.
    /// </summary>
    public struct EventHeader
    {
        public uint tick;

        public EventHeader(uint tick) { this.tick = tick; }

        public void Write(NetWriter w) { w.U32(tick); }

        public static EventHeader Read(NetReader r) => new EventHeader(r.U32());
    }
}
