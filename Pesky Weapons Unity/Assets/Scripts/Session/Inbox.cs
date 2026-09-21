using System;
using System.Collections.Generic;
using Pesky.Transport;

namespace Pesky.Session
{
    /// <summary>
    /// The queue every transport event lands in. Transport events fire
    /// outside the Unity frame on WebGL, so the handlers here only enqueue;
    /// NetSession.Update drains the queue in order. Lifecycle events (Ready,
    /// Error, PeerJoined, PeerLeft) queue alongside messages so nothing is
    /// handled out of the order it arrived.
    /// </summary>
    public sealed class Inbox
    {
        readonly object _lock = new object();
        readonly Queue<InboxItem> _queue = new Queue<InboxItem>(256);
        readonly Action<string> _onReady;
        readonly Action<string> _onError;
        readonly Action<string> _onJoined;
        readonly Action<string> _onLeft;
        readonly Action<string, byte[]> _onMessage;
        INetTransport _attached;

        public Inbox()
        {
            _onReady = id => Push(InboxKind.Ready, id, null);
            _onError = msg => Push(InboxKind.Error, msg, null);
            _onJoined = id => Push(InboxKind.PeerJoined, id, null);
            _onLeft = id => Push(InboxKind.PeerLeft, id, null);
            _onMessage = (id, payload) => Push(InboxKind.Message, id, payload);
        }

        public int Count
        {
            get { lock (_lock) return _queue.Count; }
        }

        public void Push(InboxKind kind, string peerId, byte[] payload)
        {
            var item = new InboxItem();
            item.kind = kind;
            item.peerId = peerId;
            item.payload = payload;
            lock (_lock) _queue.Enqueue(item);
        }

        public bool TryPop(out InboxItem item)
        {
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    item = default;
                    return false;
                }
                item = _queue.Dequeue();
                return true;
            }
        }

        public void Clear()
        {
            lock (_lock) _queue.Clear();
        }

        /// <summary>Subscribes to every event of the transport; Detach undoes it.</summary>
        public void Attach(INetTransport transport)
        {
            if (_attached != null) Detach();
            if (transport == null) return;
            _attached = transport;
            transport.Ready += _onReady;
            transport.Error += _onError;
            transport.PeerJoined += _onJoined;
            transport.PeerLeft += _onLeft;
            transport.Message += _onMessage;
        }

        public void Detach()
        {
            var t = _attached;
            if (t == null) return;
            t.Ready -= _onReady;
            t.Error -= _onError;
            t.PeerJoined -= _onJoined;
            t.PeerLeft -= _onLeft;
            t.Message -= _onMessage;
            _attached = null;
        }
    }
}
