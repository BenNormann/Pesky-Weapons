using Pesky.Protocol;

namespace Pesky.Session
{
    /// <summary>
    /// Section 6 of the architecture: HELLO once per peer as in M0; the host
    /// sends SESSION_INFO on PeerJoined; the client records the sender as
    /// the host and answers JOIN_REQUEST; the host assigns a slot, queues
    /// PEER_SLOTS and then the SNAPSHOT parts in one frame ahead of any new
    /// event; RESYNC_REQ gets a fresh snapshot. Also the lifecycle edges:
    /// Ready, PeerLeft, a lost host.
    /// </summary>
    internal sealed class JoinFlow
    {
        readonly NetSession _s;
        bool _joinSent;

        public JoinFlow(NetSession session)
        {
            _s = session;
        }

        public void Reset()
        {
            _joinSent = false;
        }

        // ---- lifecycle, both sides ----

        public void OnReady(string selfId)
        {
            _s.Slots.SetLocal(selfId, _s.PlayerName);
            if (!_s.IsHost) return;
            _s.HostId = selfId;
            _s.Slots.Assign(selfId, _s.PlayerName, true, _s.Clock.NowMs);
            _s.Sim.Apply(_s.Slots.ToMessage());
            _s.RaiseSlotsChanged();
            _s.MarkReady();
        }

        public void OnPeerJoined(string peerId)
        {
            SendHelloOnce(peerId);
            if (!_s.IsHost) return;
            _s.Outbox.Enqueue(peerId, BuildSessionInfo().Encode());
        }

        public void OnPeerLeft(string peerId)
        {
            _s.Slots.ForgetPeer(peerId);
            _s.Outbox.Remove(peerId);
            if (_s.IsHost)
            {
                if (_s.Slots.Release(peerId, _s.Clock.NowMs)) BroadcastSlots();
                return;
            }
            if (_s.HostId == null || peerId != _s.HostId) return;
            var end = new SessionEndMsg();
            end.header = new EventHeader(_s.Sim.Tick);
            end.reason = SessionEndReason.HostLeft;
            end.finalScore = _s.Sim.FinalScore;
            _s.Sim.Apply(end);
            _s.Pending.Clear();
            _s.CheckPhase();
            _s.RaiseHostLost();
        }

        /// <summary>M0: HELLO goes to a peer exactly once, on join or in reply to its HELLO.</summary>
        public void SendHelloOnce(string peerId)
        {
            if (!_s.Slots.MarkHelloSent(peerId)) return;
            _s.Transport.SendTo(peerId, M0Messages.EncodeHello(_s.PlayerName));
        }

        // ---- client side ----

        public void OnSessionInfo(string peerId, byte[] payload)
        {
            if (!SessionInfoMsg.TryDecode(payload, out var info)) return;
            if (_s.HostId == null) _s.HostId = peerId;
            else if (peerId != _s.HostId) return;
            if (info.protocolVersion != Wire.ProtocolVersion)
            {
                _s.RaiseError($"host protocol version {info.protocolVersion}, ours {Wire.ProtocolVersion}");
                return;
            }
            _s.Sim.Apply(info);
            _s.Clock.CoarseCheck(Protocol.Tick.ToMs(info.header.tick), _s.Clock.LocalMs);
            _s.RaiseEventApplied(MsgId.SessionInfo, payload);
            _s.CheckPhase();
            if (_joinSent) return;
            _joinSent = true;
            var request = new JoinRequestMsg();
            request.name = _s.PlayerName;
            request.protocolVersion = Wire.ProtocolVersion;
            request.appearance = _s.Slots.LocalAppearance;
            _s.Outbox.Enqueue(_s.HostId, request.Encode());
        }

        public void OnPeerSlots(byte[] payload)
        {
            if (!PeerSlotsMsg.TryDecode(payload, out var msg)) return;
            _s.Slots.Apply(msg);
            _s.Sim.Apply(msg);
            _s.RaiseSlotsChanged();
            if (_s.Slots.HasLocalSlot && !_s.Pinger.IsArmed) _s.Pinger.Arm(_s.Clock.Tick);
        }

        public void OnSnapshotPart(byte[] payload)
        {
            if (!SnapshotPartMsg.TryDecode(payload, out var part)) return;
            if (!_s.Snapshots.Receive(part)) return;
            if (!_s.Snapshots.ApplyTo(_s.Sim))
            {
                _s.RaiseError("snapshot failed to read");
                return;
            }
            _s.Pending.Clear();
            _s.IsSynced = true;
            _s.CheckPhase();
            _s.RaiseSlotsChanged();
            _s.MarkReady();
        }

        // ---- host side ----

        public void OnJoinRequest(string peerId, byte[] payload)
        {
            if (!JoinRequestMsg.TryDecode(payload, out var request)) return;
            if (request.protocolVersion != Wire.ProtocolVersion)
            {
                _s.RaiseError($"peer {peerId} protocol version {request.protocolVersion}, ours {Wire.ProtocolVersion}");
                return;
            }
            var slot = _s.Slots.Assign(peerId, request.name, false, _s.Clock.NowMs, request.appearance);
            if (slot == Wire.NoSlot)
            {
                _s.RaiseError($"room full, refused {peerId}");
                return;
            }
            BroadcastSlots();
            BroadcastSlots();
            SendSnapshot(peerId);
        }

        public void OnResyncRequest(string peerId) => SendSnapshot(peerId);

        /// <summary>After the host's clock jumped: every slotted peer gets a fresh snapshot.</summary>
        public void ResnapshotAll()
        {
            var peers = _s.Transport.PeerIds;
            for (var i = 0; i < peers.Count; i++)
            {
                if (_s.Slots.SlotOf(peers[i]) != Wire.NoSlot) SendSnapshot(peers[i]);
            }
        }

        void BroadcastSlots()
        {
            var msg = _s.Slots.ToMessage();
            _s.Sim.Apply(msg);
            _s.Outbox.EnqueueAll(_s.Transport.PeerIds, msg.Encode());
            _s.RaiseSlotsChanged();
        }

        void SendSnapshot(string peerId)
        {
            var parts = SnapshotCodec.Build(_s.Sim, _s.Authority.Ids.NextSnapshot());
            for (var i = 0; i < parts.Count; i++) _s.Outbox.Enqueue(peerId, parts[i]);
        }

        /// <summary>
        /// Host: a fresh SESSION_INFO to everybody. The lobby's field name
        /// travels on it (docs/INDUCTION.md section 7), so a host who types over
        /// the generated one restates the card rather than waiting for the next
        /// joiner.
        /// </summary>
        public void BroadcastSessionInfo()
        {
            if (!_s.IsHost) return;
            _s.Outbox.EnqueueAll(_s.Transport.PeerIds, BuildSessionInfo().Encode());
        }

        SessionInfoMsg BuildSessionInfo()
        {
            var info = new SessionInfoMsg();
            info.header = new EventHeader(_s.Sim.Tick);
            info.protocolVersion = Wire.ProtocolVersion;
            info.worldSeed = _s.Sim.WorldSeed;
            info.phase = _s.Sim.Phase;
            info.floorId = _s.Sim.FloorId;
            info.phaseStartTick = _s.Sim.PhaseStartTick;
            return info;
        }
    }
}
