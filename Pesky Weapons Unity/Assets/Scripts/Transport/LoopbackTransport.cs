using System;
using System.Collections.Generic;

namespace Pesky.Transport
{
    /// <summary>
    /// The no-network transport: editor play mode, and offline solo when no relay
    /// answers. Plain C#, no peers ever. Start marks it connected as "local" and raises
    /// Ready synchronously; Broadcast and SendTo drop their bytes; every voice call is a
    /// no-op. Session's local loopback (a stream updating the local slot, an intent
    /// reaching HostAuthority) is Session's own job and does not go through here.
    /// </summary>
    public sealed class LoopbackTransport : INetTransport, IVoiceControl
    {
        public const string LocalSelfId = "local";

        static readonly string[] NoPeers = Array.Empty<string>();

        public string SelfId { get; private set; } = "";
        public string RoomCode { get; private set; } = "";
        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public IReadOnlyList<string> PeerIds => NoPeers;

#pragma warning disable 67 // events that never fire here are still part of the contract
        public event Action<string> Ready;
        public event Action<string> Error;
        public event Action<string> PeerJoined;
        public event Action<string> PeerLeft;
        public event Action<string, byte[]> Message;
        public event Action<bool> Activity;
        public event Action<string> VoiceError;
#pragma warning restore 67

        event Action<string> IVoiceControl.Error
        {
            add => VoiceError += value;
            remove => VoiceError -= value;
        }

        public void Start(string roomCode, bool isHost)
        {
            RoomCode = (roomCode ?? "").ToUpperInvariant();
            IsHost = isHost;
            SelfId = LocalSelfId;
            IsConnected = true;
            Ready?.Invoke(SelfId);
        }

        public void Leave()
        {
            IsConnected = false;
        }

        public void Broadcast(byte[] payload) { }

        public void SendTo(string peerId, byte[] payload) { }

        public void SetMode(VoiceMode mode) { }

        public void SetPeerGain(string peerId, float gain) { }

        public void SetThreshold(float dbfs) { }
    }
}
