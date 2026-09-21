using System;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using Pesky.Transport;

namespace Pesky.Session
{
    /// <summary>
    /// The one object Game talks to. Owns the transport, the room clock,
    /// the slot table, the WorldSim, the inbox and outbox, and on the host
    /// the HostAuthority. Update runs the five per-frame steps: drain the
    /// inbox (FRAME unwrap, raw M0 path), route, advance the sim under the
    /// catch-up cap, and flush one FRAME per peer per tick. Send queues a
    /// stream or intent for this tick's FRAME; SendNow broadcasts a raw
    /// TRANSFORM at once. Both loop back locally. Plain C#: a MonoBehaviour
    /// in Game calls Update each frame.
    /// </summary>
    public sealed class NetSession
    {
        public const int MaxCatchUpTicks = 20;
        public const int ResyncBehindTicks = 5 * Protocol.Tick.PerSecond;
        public const long ResyncRetryMs = 5000;

        readonly SessionRouter _router;
        readonly JoinFlow _join;
        SessionPhase _lastPhase;
        uint _lastFlushTick = uint.MaxValue;
        long _lastResyncMs = long.MinValue;

        public INetTransport Transport { get; private set; }
        public IVoiceControl Voice { get; private set; }
        public WorldSim Sim { get; private set; }
        public GameData Data { get; private set; }
        public PeerSlots Slots { get; } = new PeerSlots();
        public RoomClock Clock { get; }
        /// <summary>The host's rules and validators; null on a client. Assign before Start to replace the default set.</summary>
        public HostAuthority Authority { get; set; }
        public string RoomCode { get; private set; } = "";
        public string PlayerName { get; private set; } = "";
        /// <summary>The transport id of the host: our own on the host, the SESSION_INFO sender on a client; null until known.</summary>
        public string HostId { get; internal set; }
        public bool IsHost { get; private set; }
        public bool IsStarted { get; private set; }
        /// <summary>Host: the slot table is up. Client: the first snapshot has been applied.</summary>
        public bool IsReady { get; private set; }
        /// <summary>True once the sim mirrors the host (always on the host).</summary>
        public bool IsSynced { get; internal set; }
        public byte LocalSlot => Slots.LocalSlot;
        public SessionPhase Phase => Sim != null ? Sim.Phase : SessionPhase.Lobby;

        internal Inbox Inbox { get; } = new Inbox();
        internal Outbox Outbox { get; } = new Outbox();
        internal PendingEvents Pending { get; } = new PendingEvents();
        internal ClockPinger Pinger { get; } = new ClockPinger();
        internal SnapshotReceiver Snapshots { get; } = new SnapshotReceiver();
        internal EventSink Sink { get; private set; }
        internal JoinFlow Join => _join;

        public event Action Ready;
        public event Action<SessionPhase> PhaseChanged;
        public event Action SlotsChanged;
        public event Action HostLost;
        public event Action<string> Error;
        /// <summary>An Event message the sim just applied (host and client): (msgId, payload).</summary>
        public event Action<byte, byte[]> EventApplied;
        /// <summary>A Stream message from a remote slot, after the sim took what it keeps: (msgId, payload).</summary>
        public event Action<byte, byte[]> StreamReceived;
        /// <summary>A Reply addressed to this peer (ATC ack, hit result, purchase result): (msgId, payload).</summary>
        public event Action<byte, byte[]> ReplyReceived;

        /// <summary>Default: a Stopwatch drives the clock. Tests pass their own millisecond source.</summary>
        public NetSession(Func<long> localMsSource = null)
        {
            Clock = new RoomClock(localMsSource);
            _router = new SessionRouter(this);
            _join = new JoinFlow(this);
        }

        public void Start(INetTransport transport, string roomCode, bool isHost, string playerName, GameData data)
        {
            if (IsStarted) Leave();
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Voice = transport as IVoiceControl;
            RoomCode = (roomCode ?? "").ToUpperInvariant();
            PlayerName = playerName ?? "";
            Data = data;
            IsHost = isHost;
            IsReady = false;
            IsSynced = isHost;
            HostId = null;
            _lastFlushTick = uint.MaxValue;
            _lastResyncMs = long.MinValue;
            Slots.Clear();
            Inbox.Clear();
            Outbox.Clear();
            Pending.Clear();
            Pinger.Disarm();
            Snapshots.Clear();
            _router.Reset();
            _join.Reset();

            var seed = isHost ? NewSeed() : 0u;
            Sim = new WorldSim(data, seed);
            _lastPhase = Sim.Phase;
            if (isHost)
            {
                if (Authority == null) Authority = HostAuthority.CreateDefault();
                Sink = new EventSink(Sim, Authority.Ids, Clock, Outbox, Slots, Transport);
                Sink.Applied += OnSinkApplied;
                Sink.LocalReply += RaiseReply;
                Sink.World = _hostWorld;

                Clock.StartHost();
            }
            else
            {
                Authority = null;
                Sink = null;
                Clock.StartClient();
            }
            IsStarted = true;
            Inbox.Attach(Transport);
            Transport.Start(RoomCode, isHost);
        }

        /// <summary>The five per-frame steps; call once per frame from Game.</summary>
        public void Update()
        {
            if (!IsStarted) return;
            while (Inbox.TryPop(out var item)) _router.Handle(item);
            if (!IsHost && HostId != null && Pinger.Due(Clock.Tick))
            {
                var ping = Pinger.Next(Clock.Tick, Clock.LocalMs);
                Transport.SendTo(HostId, ping.Encode());
            }
            if (IsHost) AdvanceHost();
            else AdvanceClient();
            var tick = Clock.Tick;
            if (tick != _lastFlushTick)
            {
                _lastFlushTick = tick;
                Outbox.Flush(Transport);
            }
        }

        /// <summary>Queues a stream or intent for this tick's FRAME and loops it back locally.</summary>
        public void Send(byte[] payload)
        {
            if (!IsStarted || payload == null || payload.Length == 0) return;
            var id = payload[0];
            switch (MessageInfo.KindOf(id))
            {
                case MsgKind.Stream:
                    MessageApplier.Apply(Sim, payload);
                    Outbox.EnqueueAll(Transport.PeerIds, payload);
                    break;
                case MsgKind.Intent:
                    if (IsHost)
                    {
                        if (Slots.HasLocalSlot) Authority.Handle(LocalSlot, payload, Sim, Sim.Tick, Sink);
                    }
                    else if (HostId != null)
                    {
                        Outbox.Enqueue(HostId, payload);
                    }
                    break;
                default:
                    RaiseError($"Send refused message 0x{id:X2}: Game only sends streams and intents");
                    break;
            }
        }

        /// <summary>Broadcasts a raw M0 TRANSFORM at once (15 Hz timing untouched) and updates the local row. Anything else goes through Send.</summary>
public void SendNow(byte[] payload)
        {
            if (!IsStarted || payload == null || payload.Length == 0) return;
            if (payload[0] != MsgId.Transform)
            {
                Send(payload);
                return;
            }
            if (Slots.HasLocalSlot && M0Messages.TryDecodePose(payload, out var flags, out var pos, out var rot, out var vel, out var seq))
            {
                _router.AcceptPose(LocalSlot, flags, pos, rot, vel, seq);
            }
            Transport.Broadcast(payload);
        }

        /// <summary>
        /// Host only: a Game-side host system publishes an Event or a State row. Pesky's goblin brains are
        /// NavMesh MonoBehaviours and its kit pieces are physics, so some host facts are born in Game and
        /// not in a Session rule; this is the same EventSink.Emit a rule uses. False on a client.
        /// </summary>
        public bool HostEmit(byte[] payload)
        {
            if (!IsHost || !IsStarted || Sink == null || payload == null || payload.Length == 0) return false;
            var kind = MessageInfo.KindOf(payload[0]);
            if (kind != MsgKind.Event && kind != MsgKind.State)
            {
                RaiseError($"HostEmit refused message 0x{payload[0]:X2}: only events and state rows");
                return false;
            }
            return Sink.Emit(payload);
        }

        /// <summary>The header a host event takes: the sim tick plus the lead.</summary>
        public EventHeader HostHeader() => Sink != null ? Sink.Header() : new EventHeader(Sim != null ? Sim.Tick : 0);

        /// <summary>The Game-side half of the host: enemy rows and kit checks that need the scene. Set by Game on every peer; only the host uses it.</summary>
        public IHostWorld HostWorld
        {
            get { return _hostWorld; }
            set { _hostWorld = value; if (Sink != null) Sink.World = value; }
        }
        IHostWorld _hostWorld;

        /// <summary>Host: Lobby to Playing, leaving every peer on the floor its sim already has.</summary>
        public void StartGame() => StartGame(0);

        /// <summary>
        /// Host: Lobby to Playing. The floor the lobby picked rides
        /// SESSION_PHASE, so every peer's sim adopts the same run from the
        /// message that starts it. A floor of 0 leaves every peer's own alone.
        /// </summary>
        public void StartGame(byte floorId)
        {
            if (!IsHost || !IsStarted || Sim.Phase != SessionPhase.Lobby) return;
            var msg = new SessionPhaseMsg();
            msg.header = Sink.Header();
            msg.phase = SessionPhase.Playing;
            msg.floorId = floorId;
            Sink.Emit(msg.Encode());
        }

        /// <summary>Host: every slotted peer gets a fresh snapshot.</summary>
        public void ResnapshotAll()
        {
            if (IsHost && IsStarted) _join.ResnapshotAll();
        }


        /// <summary>
        /// Host: a finished room goes back to the lobby, so the crew can start another run from the
        /// room page. It rides the same SESSION_PHASE that starts one, so every peer's sim takes it
        /// from one message. The host's own sim has it before this returns.
        /// </summary>
        public void ReturnToLobby()
        {
            if (!IsHost || !IsStarted || Sim.Phase == SessionPhase.Lobby) return;
            var msg = new SessionPhaseMsg();
            msg.header = Sink.Header();
            msg.phase = SessionPhase.Lobby;
            msg.floorId = 0;
            Sink.Emit(msg.Encode());
        }


        /// <summary>Host: ends the session because the host chose to.</summary>
        public void EndGame() => EndSession(SessionEndReason.HostEnded);

        /// <summary>Host: ends the run for a reason a rule reached (Escaped, CrewLost). A rule would normally emit SESSION_END itself; this is the same path for Game.</summary>
        public void EndRun(SessionEndReason reason) => EndSession(reason);

        public void Leave()
        {
            if (!IsStarted) return;
            if (IsHost && Sim.Phase != SessionPhase.Ended) EndSession(SessionEndReason.HostLeft);
            Outbox.Flush(Transport);
            Inbox.Detach();
            Transport.Leave();
            if (Sink != null)
            {
                Sink.Applied -= OnSinkApplied;
                Sink.LocalReply -= RaiseReply;
            }
            IsStarted = false;
            IsReady = false;
            IsSynced = false;
        }

        // ---- advance ----

        void AdvanceHost()
        {
            var target = Clock.Tick;
            if (target <= Sim.Tick) return;
            if (target - Sim.Tick > ResyncBehindTicks)
            {
                Clock.JumpTo(Protocol.Tick.ToMs(Sim.Tick + 1));
                target = Sim.Tick + 1;
                _join.ResnapshotAll();
            }
            var end = Math.Min(target, Sim.Tick + MaxCatchUpTicks);
            for (var t = Sim.Tick + 1; t <= end; t++)
            {
                Sim.AdvanceTo(t);
                Authority.Tick(Sim, t, Sink);
            }
            CheckPhase();
        }

        void AdvanceClient()
        {
            if (!IsSynced || !Clock.HasEstimate) return;
            var target = Clock.Tick;
            if (target <= Sim.Tick) return;
            if (target - Sim.Tick > ResyncBehindTicks)
            {
                RequestResync();
                return;
            }
            var end = Math.Min(target, Sim.Tick + MaxCatchUpTicks);
            for (var t = Sim.Tick + 1; t <= end; t++)
            {
                while (Pending.TryPeekTick(out var due) && due <= t) _router.ApplyEvent(Pending.Pop());
                Sim.AdvanceTo(t);
            }
            CheckPhase();
        }

        void RequestResync()
        {
            if (HostId == null || Clock.NowMs - _lastResyncMs < ResyncRetryMs) return;
            _lastResyncMs = Clock.NowMs;
            Transport.SendTo(HostId, new ResyncReqMsg().Encode());
        }

        void EndSession(SessionEndReason reason)
        {
            if (!IsHost || !IsStarted || Sim.Phase == SessionPhase.Ended) return;
            var msg = new SessionEndMsg();
            msg.header = Sink.Header();
            msg.reason = reason;
            // Stage 1 keeps no score; the escape or wipe outcome fills this in later.
            msg.finalScore = 0;
            Sink.Emit(msg.Encode());
        }

        // ---- internal plumbing for the router and join flow ----

        void OnSinkApplied(byte id, byte[] payload)
        {
            if (MessageInfo.KindOf(id) == MsgKind.Event) EventApplied?.Invoke(id, payload);
            CheckPhase();
        }

        internal void RaiseEventApplied(byte id, byte[] payload) => EventApplied?.Invoke(id, payload);
        internal void RaiseStream(byte id, byte[] payload) => StreamReceived?.Invoke(id, payload);
        internal void RaiseReply(byte id, byte[] payload)
        {
            // A reply can be a secret addressed to this peer alone (its role, its compass). Apply it to
            // this peer's sim before Game hears it; ApplyReply ignores every other kind of reply.
            MessageApplier.ApplyReply(Sim, payload);
            ReplyReceived?.Invoke(id, payload);
        }
        internal void RaiseSlotsChanged() => SlotsChanged?.Invoke();
        internal void RaiseHostLost() => HostLost?.Invoke();
        internal void RaiseError(string message) => Error?.Invoke(message);

        internal void MarkReady()
        {
            if (IsReady) return;
            IsReady = true;
            Ready?.Invoke();
        }

        internal void CheckPhase()
        {
            if (Sim == null || Sim.Phase == _lastPhase) return;
            _lastPhase = Sim.Phase;
            PhaseChanged?.Invoke(_lastPhase);
        }

        static uint NewSeed()
        {
            var seed = unchecked((uint)Environment.TickCount) ^ (uint)Guid.NewGuid().GetHashCode();
            return seed == 0 ? 1u : seed;
        }
    }
}
