using System.Collections.Generic;
using Pesky.Protocol;
using UnityEngine;

namespace Pesky.Session
{
    /// <summary>
    /// Step one and two of NetSession.Update: takes each inbox item, unwraps
    /// a FRAME into its messages (anything else is a single raw message:
    /// M0 TRANSFORM, HELLO, PING, or a raw clock message) and routes by kind.
    /// Events wait in PendingEvents for their tick or apply at once when
    /// late; state rows overwrite; replies land on the clock, the snapshot
    /// receiver or Game; intents reach HostAuthority on the host; streams
    /// update their slot's row. Also owns the per-slot TRANSFORM sequence.
    /// </summary>
    internal sealed class SessionRouter
    {
        readonly NetSession _s;
        readonly List<byte[]> _unpacked = new List<byte[]>(Frame.MaxCount);
        readonly ushort[] _poseSeq = new ushort[Wire.MaxPlayers];
        readonly bool[] _poseSeen = new bool[Wire.MaxPlayers];

        public SessionRouter(NetSession session)
        {
            _s = session;
            _s.Slots.SlotRemoved += slot => { if (slot < _poseSeen.Length) _poseSeen[slot] = false; };
        }

        public void Reset()
        {
            for (var i = 0; i < _poseSeen.Length; i++) _poseSeen[i] = false;
        }

        public void Handle(in InboxItem item)
        {
            switch (item.kind)
            {
                case InboxKind.Ready: _s.Join.OnReady(item.peerId); break;
                case InboxKind.Error: _s.RaiseError(item.peerId); break;
                case InboxKind.PeerJoined: _s.Join.OnPeerJoined(item.peerId); break;
                case InboxKind.PeerLeft: _s.Join.OnPeerLeft(item.peerId); break;
                case InboxKind.Message: OnMessage(item.peerId, item.payload); break;
            }
        }

        void OnMessage(string peerId, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;
            if (!Frame.IsFrame(payload))
            {
                Route(peerId, payload);
                return;
            }
            _unpacked.Clear();
            if (Frame.TryUnpack(payload, _unpacked))
            {
                for (var i = 0; i < _unpacked.Count; i++) Route(peerId, _unpacked[i]);
            }
            _unpacked.Clear();
        }

        void Route(string peerId, byte[] payload)
        {
            var id = payload[0];
            switch (id)
            {
                case MsgId.Transform: OnTransform(peerId, payload); return;
                case MsgId.Hello: OnHello(peerId, payload); return;
                case MsgId.Ping: return;
            }
            switch (MessageInfo.KindOf(id))
            {
                case MsgKind.Event: OnEvent(peerId, payload); break;
                case MsgKind.State: OnState(peerId, payload); break;
                case MsgKind.Reply: OnReply(peerId, payload); break;
                case MsgKind.Intent: OnIntent(peerId, payload); break;
                case MsgKind.Stream: OnStream(peerId, payload); break;
            }
        }

        bool FromHost(string peerId) => _s.HostId != null && peerId == _s.HostId;

        void OnEvent(string peerId, byte[] payload)
        {
            if (_s.IsHost) return;
            if (payload[0] == MsgId.SessionInfo)
            {
                _s.Join.OnSessionInfo(peerId, payload);
                return;
            }
            if (!FromHost(peerId) || !_s.IsSynced) return;
            if (!MessageApplier.TryEventTick(payload, out var tick)) return;
            if (tick <= _s.Sim.Tick) ApplyEvent(payload);
            else _s.Pending.Push(tick, payload);
        }

        /// <summary>Applies an event whose tick has come and tells Game.</summary>
        public void ApplyEvent(byte[] payload)
        {
            if (!MessageApplier.Apply(_s.Sim, payload)) return;
            _s.RaiseEventApplied(payload[0], payload);
            _s.CheckPhase();
        }

        void OnState(string peerId, byte[] payload)
        {
            if (_s.IsHost || !FromHost(peerId)) return;
            switch (payload[0])
            {
                case MsgId.PeerSlots:
                    _s.Join.OnPeerSlots(payload);
                    break;
                case MsgId.TimeSync:
                    if (TimeSyncMsg.TryDecode(payload, out var sync))
                    {
                        _s.Sim.Apply(sync);
                        _s.Clock.CoarseCheck(sync.hostRoomMs, _s.Clock.LocalMs);
                    }
                    break;
                default:
                    MessageApplier.Apply(_s.Sim, payload);
                    break;
            }
        }

        void OnReply(string peerId, byte[] payload)
        {
            if (_s.IsHost || !FromHost(peerId)) return;
            switch (payload[0])
            {
                case MsgId.ClockPong: OnPong(payload); break;
                case MsgId.Snapshot: _s.Join.OnSnapshotPart(payload); break;
                default: _s.RaiseReply(payload[0], payload); break;
            }
        }

        void OnPong(byte[] payload)
        {
            if (!ClockPongMsg.TryDecode(payload, out var pong)) return;
            var recv = _s.Clock.LocalMs;
            _s.Clock.AddSample(Unwrap(pong.clientMs, recv), pong.hostRoomMs, recv);
        }

        /// <summary>Restores the full local ms a truncated u32 stamp came from, given a later reference time.</summary>
        static long Unwrap(uint low, long reference)
        {
            var sent = (reference & ~0xFFFFFFFFL) | low;
            if (sent > reference) sent -= 0x100000000L;
            return sent;
        }

        void OnIntent(string peerId, byte[] payload)
        {
            if (!_s.IsHost) return;
            if (payload[0] == MsgId.JoinRequest)
            {
                _s.Join.OnJoinRequest(peerId, payload);
                return;
            }
            var slot = _s.Slots.SlotOf(peerId);
            if (slot == Wire.NoSlot) return;
            if (payload[0] == MsgId.ResyncReq)
            {
                _s.Join.OnResyncRequest(peerId);
                return;
            }
            _s.Authority.Handle(slot, payload, _s.Sim, _s.Sim.Tick, _s.Sink);
        }

        void OnStream(string peerId, byte[] payload)
        {
            var slot = _s.Slots.SlotOf(peerId);
            if (slot == Wire.NoSlot || payload.Length < 2 || payload[1] != slot) return;
            MessageApplier.Apply(_s.Sim, payload);
            _s.RaiseStream(payload[0], payload);
        }

void OnTransform(string peerId, byte[] payload)
        {
            var slot = _s.Slots.SlotOf(peerId);
            if (slot == Wire.NoSlot) return;
            if (!M0Messages.TryDecodePose(payload, out var flags, out var pos, out var rot, out var vel, out var seq)) return;
            NetStats.CountPoseIn();
            AcceptPose(slot, flags, pos, rot, vel, seq);
        }

        /// <summary>Latest sequence wins per slot; the first pose for a slot is always taken.</summary>
public void AcceptPose(byte slot, PoseFlags flags, Vector3 pos, Quaternion rot, Vector3 vel, ushort seq)
        {
            if (slot >= _poseSeen.Length) return;
            if (_poseSeen[slot] && !M0Messages.SeqIsNewer(seq, _poseSeq[slot])) return;
            _poseSeen[slot] = true;
            _poseSeq[slot] = seq;
            _s.Sim.SetPlayerPose(slot, flags, pos, rot, vel, seq);
        }

        void OnHello(string peerId, byte[] payload)
        {
            if (!M0Messages.TryDecodeHello(payload, out var name)) return;
            _s.Slots.OnHello(peerId, name);
            _s.Join.SendHelloOnce(peerId);
        }
    }
}
