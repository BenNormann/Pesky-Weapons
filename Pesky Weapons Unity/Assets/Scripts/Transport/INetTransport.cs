using System;
using System.Collections.Generic;

namespace Pesky.Transport
{
    /// <summary>
    /// The byte carrier Session talks to; it moves bytes and never reads them.
    /// Contract: delivery is reliable and ordered per peer pair; a transport that cannot
    /// guarantee that must implement it itself. Broadcast reaches every other peer in the
    /// session; a star transport (dedicated server) satisfies that by relaying inside the
    /// transport, so Session never knows the topology. Message fires outside the Unity
    /// frame on WebGL and must only enqueue.
    /// </summary>
    public interface INetTransport
    {
        string SelfId { get; }
        bool IsConnected { get; }
        IReadOnlyList<string> PeerIds { get; }          // maintained from PeerJoined / PeerLeft
        void Start(string roomCode, bool isHost);
        void Leave();
        void Broadcast(byte[] payload);
        void SendTo(string peerId, byte[] payload);
        event Action<string> Ready, Error, PeerJoined, PeerLeft;
        event Action<string, byte[]> Message;            // peerId, payload
    }
}
