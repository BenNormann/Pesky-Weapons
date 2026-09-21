using Pesky.Sim;

namespace Pesky.Session
{
    /// <summary>
    /// One host-side validator per intent family, keyed by MsgId in
    /// HostAuthority. It decodes the intent from the slot that sent it,
    /// checks it against the sim, and answers with a reply, an event, or
    /// silence through the sink.
    /// </summary>
    public interface IIntentValidator
    {
        void Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events);
    }
}
