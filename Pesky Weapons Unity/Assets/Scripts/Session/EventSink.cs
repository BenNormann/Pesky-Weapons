using System;
using Pesky.Protocol;
using Pesky.Sim;
using Pesky.Transport;

namespace Pesky.Session
{
    /// <summary>
    /// The one way a host rule or validator produces an outcome. Emit applies
    /// an event or state row to the host's own WorldSim through the same
    /// Apply path every peer uses, then queues it for every peer; Reply
    /// queues a reply for one slot (delivered locally when the slot is the
    /// host's own). Owns the tick stamp helpers: events take effect at the
    /// current tick plus LeadTicks. Outbox, slots and transport may be null
    /// in tests, in which case nothing leaves the process.
    /// </summary>
    public sealed class EventSink
    {
        /// <summary>Events are stamped this many ticks ahead so every peer applies them on the same tick.</summary>
        public const int LeadTicks = 3;

        readonly RoomClock _clock;
        readonly Outbox _outbox;
        readonly PeerSlots _slots;
        readonly INetTransport _transport;

        public WorldSim Sim { get; }
        public HostIds Ids { get; }

        /// <summary>The Game-side half of the host (scene knowledge a plain C# rule cannot have). Null in tests and until Game registers.</summary>
        public IHostWorld World { get; set; }


        /// <summary>Raised after Emit applied a payload locally: (msgId, payload).</summary>
        public event Action<byte, byte[]> Applied;

        /// <summary>A reply addressed to the host's own slot: (msgId, payload).</summary>
        public event Action<byte, byte[]> LocalReply;

        /// <summary>Every payload Emit accepted, in order; tests record the sequence here.</summary>
        public event Action<byte[]> Emitted;

        public EventSink(WorldSim sim, HostIds ids, RoomClock clock = null, Outbox outbox = null,
            PeerSlots slots = null, INetTransport transport = null)
        {
            Sim = sim;
            Ids = ids ?? new HostIds();
            _clock = clock;
            _outbox = outbox;
            _slots = slots;
            _transport = transport;
        }

        /// <summary>The sim tick the authority is running at.</summary>
        public uint Tick => Sim.Tick;

        /// <summary>The tick a new event takes effect: now plus the lead.</summary>
        public uint EventTick => Sim.Tick + LeadTicks;

        /// <summary>Room milliseconds now; the sim tick's ms when there is no clock.</summary>
        public long NowMs => _clock != null && _clock.IsRunning ? _clock.NowMs : Protocol.Tick.ToMs(Sim.Tick);

        public EventHeader Header() => new EventHeader(EventTick);

        /// <summary>Applies the event or state row locally and queues it for every peer. False when the sim rejected it.</summary>
        public bool Emit(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return false;
            var id = payload[0];
            var applied = MessageApplier.Apply(Sim, payload);
            if (!applied)
            {
                var kind = MessageInfo.KindOf(id);
                if (kind != MsgKind.Event && kind != MsgKind.State) return false;
            }
            Emitted?.Invoke(payload);
            if (applied) Applied?.Invoke(id, payload);
            if (_outbox != null && _transport != null) _outbox.EnqueueAll(_transport.PeerIds, payload);
            return true;
        }

        /// <summary>Queues a reply for one slot in this tick's FRAME; the host's own slot gets it through LocalReply.</summary>
        public void Reply(byte slot, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;
            if (IsLocal(slot))
            {
                LocalReply?.Invoke(payload[0], payload);
                return;
            }
            var peerId = _slots != null ? _slots.PeerIdOf(slot) : "";
            if (peerId.Length == 0 || _outbox == null) return;
            _outbox.Enqueue(peerId, payload);
        }

        /// <summary>Sends a reply raw and at once, for the clock pong where queue delay would skew the estimate.</summary>
        public void ReplyNow(byte slot, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;
            if (IsLocal(slot))
            {
                LocalReply?.Invoke(payload[0], payload);
                return;
            }
            var peerId = _slots != null ? _slots.PeerIdOf(slot) : "";
            if (peerId.Length == 0 || _transport == null) return;
            _transport.SendTo(peerId, payload);
        }

        bool IsLocal(byte slot) => _slots != null && _slots.HasLocalSlot && _slots.LocalSlot == slot;
    }
}
