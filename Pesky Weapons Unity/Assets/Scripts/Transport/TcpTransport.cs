#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Pesky.Transport
{
    /// <summary>
    /// The desktop transport: plain TCP, so two editors or two standalone players can meet
    /// on a LAN (the browser build still uses WebRtcTransport). The room code IS the address:
    /// a host may pass "" or ":7778" to pick a port, a client passes "host" or "host:port".
    ///
    /// Topology is a star that pretends to be a mesh, which INetTransport explicitly allows:
    /// the host holds one socket per client and relays, so Broadcast from anybody reaches
    /// everybody else and SendTo(peer) reaches that peer even between two clients. Peer ids
    /// are "host" and "p1", "p2", ... assigned by the host and never reused in a session.
    ///
    /// Threading: every socket has a reader and a writer thread (see TcpLink), so Ready,
    /// Error, PeerJoined, PeerLeft and Message fire OFF the Unity frame — which is the
    /// contract WebGL already imposes, and why Inbox only enqueues. Every thread is a
    /// background thread, and Leave (also run from play mode exit, quit and domain reload)
    /// closes the sockets and joins the threads.
    /// </summary>
    public sealed class TcpTransport : INetTransport
    {
        public const int DefaultPort = 7777;

        /// <summary>The host's own peer id; a client learns it in the welcome frame.</summary>
        public const string HostPeerId = "host";

        /// <summary>Wire.MaxPlayers (8) minus the host. Transport may not reference Protocol.</summary>
        public const int MaxPeers = 7;

        const uint Magic = 0x31_4E_57_50;   // "PWN1", little endian on the wire
        const byte WireVersion = 1;
        const int ConnectTimeoutMs = 5000;
        const int ThreadJoinMs = 500;

        static readonly List<TcpTransport> Live = new List<TcpTransport>();
        static readonly object LiveLock = new object();
        static bool _hooked;

        readonly object _lock = new object();
        readonly Dictionary<string, TcpLink> _links = new Dictionary<string, TcpLink>(8);  // host only: peer id -> socket
        readonly List<TcpLink> _pending = new List<TcpLink>(4);                            // host only: accepted, not yet named
        readonly List<string> _peers = new List<string>(8);

        volatile string[] _peerIds = new string[0];
        volatile TcpLink[] _linkSnapshot = new TcpLink[0];
        volatile string _selfId = "";
        volatile string _hostPeerId = HostPeerId;
        volatile bool _connected;
        volatile bool _running;

        TcpListener _listener;
        Thread _accept;
        Thread _connect;
        volatile TcpLink _hostLink; // client only; volatile so the main thread sees it as soon as _connected turns true
        int _nextPeer;

        public string SelfId => _selfId;
        public bool IsConnected => _connected;
        public IReadOnlyList<string> PeerIds => _peerIds;
        public bool IsHost { get; private set; }

        /// <summary>The address this transport was started with, as typed.</summary>
        public string Address { get; private set; } = "";

        /// <summary>The port actually listened on or connected to.</summary>
        public int Port { get; private set; } = DefaultPort;

        public event Action<string> Ready;
        public event Action<string> Error;
        public event Action<string> PeerJoined;
        public event Action<string> PeerLeft;
        public event Action<string, byte[]> Message;

        // ---- INetTransport ----

        public void Start(string roomCode, bool isHost)
        {
            Leave();                       // starting twice is a restart, not a leak
            EnsureHooks();
            Address = roomCode ?? "";
            IsHost = isHost;
            _selfId = "";
            _nextPeer = 0;
            _running = true;
            lock (LiveLock) { if (!Live.Contains(this)) Live.Add(this); }

            // Two instances on one desktop both have to keep ticking while unfocused.
            Application.runInBackground = true;

            if (isHost) StartHost(); else StartClient();
        }

        public void Leave()
        {
            _running = false;
            _connected = false;

            TcpListener listener;
            var links = new List<TcpLink>(8);
            lock (_lock)
            {
                listener = _listener;
                _listener = null;
                links.AddRange(_links.Values);
                links.AddRange(_pending);
                if (_hostLink != null) links.Add(_hostLink);
                _links.Clear();
                _pending.Clear();
                _hostLink = null;
                _peers.Clear();
                RebuildLocked();
            }

            try { listener?.Stop(); } catch { }
            for (var i = 0; i < links.Count; i++) links[i].Close();

            JoinThread(_accept);
            JoinThread(_connect);
            _accept = null;
            _connect = null;
            for (var i = 0; i < links.Count; i++) links[i].Join(ThreadJoinMs);

            lock (LiveLock) Live.Remove(this);
        }

        public void Broadcast(byte[] payload)
        {
            if (!_connected || payload == null || payload.Length == 0) return;
            if (IsHost)
            {
                var frame = BuildDeliver(_selfId, payload);
                var links = _linkSnapshot;
                for (var i = 0; i < links.Length; i++) links[i].Send(frame);
                return;
            }
            var host = _hostLink;
            if (host != null) host.Send(BuildRelay("", payload));
        }

        public void SendTo(string peerId, byte[] payload)
        {
            if (!_connected || string.IsNullOrEmpty(peerId) || payload == null || payload.Length == 0) return;
            if (IsHost)
            {
                TcpLink link;
                lock (_lock) _links.TryGetValue(peerId, out link);
                if (link != null) link.Send(BuildDeliver(_selfId, payload));
                return;
            }
            var host = _hostLink;
            if (host != null) host.Send(BuildRelay(peerId, payload));   // the host routes it, to itself or on
        }

        // ---- host ----

        void StartHost()
        {
            Port = PortOf(Address, DefaultPort);
            _selfId = HostPeerId;
            _hostPeerId = HostPeerId;
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
            }
            catch (Exception e)
            {
                _listener = null;
                _running = false;
                RaiseError($"could not listen on port {Port}: {e.Message}");
                return;
            }
            var listener = _listener;
            _accept = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "Pesky TCP accept" };
            _accept.Start();
            _connected = true;
            Debug.Log($"[tcp] hosting on port {Port}; peers join with {string.Join("  or  ", LocalAddresses(Port))}");
            if (Ready != null) Ready(_selfId);
        }

        void AcceptLoop(TcpListener listener)
        {
            try
            {
                while (_running)
                {
                    var client = listener.AcceptTcpClient();
                    if (!_running)
                    {
                        try { client.Close(); } catch { }
                        break;
                    }
                    var link = new TcpLink(client, false, OnFrame, OnLinkClosed);
                    lock (_lock) _pending.Add(link);
                    link.Start("Pesky TCP peer");
                }
            }
            catch (Exception e)
            {
                if (_running) RaiseError("stopped listening: " + e.Message);
            }
        }

        void HostHello(TcpLink link, byte[] body)
        {
            if (body.Length < 5 || TcpWire.GetU32(body, 0) != Magic || body[4] != WireVersion)
            {
                RaiseError("refused a connection: not a Pesky Weapons peer, or a different wire version");
                DropSilently(link);
                return;
            }
            string id = null;
            string[] existing = null;
            lock (_lock)
            {
                if (!_pending.Remove(link)) return;          // already dropped
                if (_links.Count < MaxPeers)
                {
                    id = "p" + (++_nextPeer);
                    link.Id = id;
                    existing = _peers.ToArray();
                    _links[id] = link;
                    _peers.Add(id);
                    RebuildLocked();
                }
            }
            if (id == null)
            {
                RaiseError($"refused a connection: {MaxPeers} peers are already here");
                DropSilently(link);
                return;
            }

            link.Send(BuildWelcome(id, existing));
            var joined = BuildId(TcpOp.Joined, id);
            var links = _linkSnapshot;
            for (var i = 0; i < links.Length; i++)
                if (!ReferenceEquals(links[i], link)) links[i].Send(joined);

            Debug.Log($"[tcp] peer joined: {id}");
            if (PeerJoined != null) PeerJoined(id);
        }

        /// <summary>A client asked the host to route a payload: to one peer, or to everybody else.</summary>
        void HostRelay(TcpLink from, byte[] body)
        {
            if (from.Id == null) return;                     // spoke before it was welcomed
            var pos = 0;
            string target;
            if (!TcpWire.TryGetString(body, ref pos, out target)) return;
            var payload = TcpWire.Tail(body, pos);
            if (payload.Length == 0) return;

            if (string.IsNullOrEmpty(target))
            {
                var deliver = BuildDeliver(from.Id, payload);
                var links = _linkSnapshot;
                for (var i = 0; i < links.Length; i++)
                    if (!ReferenceEquals(links[i], from)) links[i].Send(deliver);
                if (Message != null) Message(from.Id, payload);   // the host is a peer of the sender too
                return;
            }
            if (target == _selfId)
            {
                if (Message != null) Message(from.Id, payload);
                return;
            }
            TcpLink to;
            lock (_lock) _links.TryGetValue(target, out to);
            if (to != null) to.Send(BuildDeliver(from.Id, payload));
        }

        // ---- client ----

        void StartClient()
        {
            string host;
            int port;
            if (!ParseAddress(Address, out host, out port))
            {
                _running = false;
                RaiseError($"'{Address}' is not a host address; join with \"host\" or \"host:port\"");
                return;
            }
            Port = port;
            _connect = new Thread(() => ConnectLoop(host, port)) { IsBackground = true, Name = "Pesky TCP connect" };
            _connect.Start();
        }

        void ConnectLoop(string host, int port)
        {
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                var pending = client.BeginConnect(host, port, null, null);
                if (!pending.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                    throw new TimeoutException($"no answer in {ConnectTimeoutMs} ms");
                client.EndConnect(pending);
            }
            catch (Exception e)
            {
                try { client?.Close(); } catch { }
                if (_running) RaiseError($"could not reach {host}:{port} — {e.Message}");
                return;
            }
            if (!_running)
            {
                try { client.Close(); } catch { }
                return;
            }
            var link = new TcpLink(client, true, OnFrame, OnLinkClosed);
            lock (_lock) _hostLink = link;
            link.Start("Pesky TCP host");
            link.Send(BuildHello());
            // Ready waits for the host's welcome: only then is there a SelfId.
        }

        void ClientWelcome(byte[] body)
        {
            var pos = 0;
            string self, host;
            if (!TcpWire.TryGetString(body, ref pos, out self) ||
                !TcpWire.TryGetString(body, ref pos, out host) ||
                pos >= body.Length ||
                string.IsNullOrEmpty(self) || string.IsNullOrEmpty(host))
            {
                RaiseError("malformed welcome from the host");
                return;
            }
            var count = body[pos++];
            var others = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                string other;
                if (!TcpWire.TryGetString(body, ref pos, out other))
                {
                    RaiseError("malformed welcome from the host");
                    return;
                }
                if (!string.IsNullOrEmpty(other) && other != self && other != host) others.Add(other);
            }
            lock (_lock)
            {
                _peers.Clear();
                _peers.Add(host);
                _peers.AddRange(others);
                RebuildLocked();
            }
            _selfId = self;
            _hostPeerId = host;
            _connected = true;
            Debug.Log($"[tcp] connected as {self}; host is {host}, {others.Count} other peer(s)");
            if (Ready != null) Ready(self);
            if (PeerJoined != null)
            {
                PeerJoined(host);
                for (var i = 0; i < others.Count; i++) PeerJoined(others[i]);
            }
        }

        void ClientPeerJoined(byte[] body)
        {
            var pos = 0;
            string id;
            if (!TcpWire.TryGetString(body, ref pos, out id) || string.IsNullOrEmpty(id) || id == _selfId) return;
            lock (_lock)
            {
                if (_peers.Contains(id)) return;
                _peers.Add(id);
                RebuildLocked();
            }
            if (PeerJoined != null) PeerJoined(id);
        }

        void ClientPeerLeft(byte[] body)
        {
            var pos = 0;
            string id;
            if (!TcpWire.TryGetString(body, ref pos, out id) || string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                if (!_peers.Remove(id)) return;
                RebuildLocked();
            }
            if (PeerLeft != null) PeerLeft(id);
        }

        void ClientDeliver(byte[] body)
        {
            var pos = 0;
            string from;
            if (!TcpWire.TryGetString(body, ref pos, out from) || string.IsNullOrEmpty(from)) return;
            var payload = TcpWire.Tail(body, pos);
            if (payload.Length == 0) return;
            if (Message != null) Message(from, payload);
        }

        // ---- link callbacks (reader / writer threads) ----

        void OnFrame(TcpLink link, byte op, byte[] body)
        {
            if (!_running) return;
            if (IsHost)
            {
                if (op == TcpOp.Relay) HostRelay(link, body);
                else if (op == TcpOp.Hello) HostHello(link, body);
                return;
            }
            switch (op)
            {
                case TcpOp.Deliver: ClientDeliver(body); break;
                case TcpOp.Welcome: ClientWelcome(body); break;
                case TcpOp.Joined: ClientPeerJoined(body); break;
                case TcpOp.Left: ClientPeerLeft(body); break;
            }
        }

        void OnLinkClosed(TcpLink link, string reason)
        {
            if (!_running) return;
            string id = null;
            var lostHost = false;
            lock (_lock)
            {
                _pending.Remove(link);
                TcpLink known;
                if (link.Id != null && _links.TryGetValue(link.Id, out known) && ReferenceEquals(known, link))
                {
                    id = link.Id;
                    _links.Remove(id);
                    _peers.Remove(id);
                    RebuildLocked();
                }
                if (ReferenceEquals(_hostLink, link))
                {
                    lostHost = true;
                    _hostLink = null;
                    _peers.Clear();
                    RebuildLocked();
                }
            }

            if (lostHost)
            {
                _connected = false;
                Debug.LogWarning($"[tcp] lost the host: {reason}");
                if (PeerLeft != null) PeerLeft(_hostPeerId);   // JoinFlow turns this into HostLost
                if (Error != null) Error("lost the host: " + reason);
                return;
            }
            if (id == null) return;

            var left = BuildId(TcpOp.Left, id);
            var links = _linkSnapshot;
            for (var i = 0; i < links.Length; i++) links[i].Send(left);
            Debug.Log($"[tcp] peer left: {id} ({reason})");
            if (PeerLeft != null) PeerLeft(id);
        }

        void DropSilently(TcpLink link)
        {
            lock (_lock)
            {
                _pending.Remove(link);
                if (link.Id != null)
                {
                    _links.Remove(link.Id);
                    _peers.Remove(link.Id);
                    RebuildLocked();
                }
            }
            link.Close();
        }

        // ---- frames ----

        static byte[] BuildHello()
        {
            var b = new List<byte>(8);
            TcpWire.PutU32(b, Magic);
            b.Add(WireVersion);
            return TcpWire.Wrap(TcpOp.Hello, b.ToArray());
        }

        byte[] BuildWelcome(string id, string[] existing)
        {
            var b = new List<byte>(64);
            TcpWire.PutString(b, id);
            TcpWire.PutString(b, _selfId);
            var count = existing == null ? 0 : existing.Length;
            if (count > 255) count = 255;
            b.Add((byte)count);
            for (var i = 0; i < count; i++) TcpWire.PutString(b, existing[i]);
            return TcpWire.Wrap(TcpOp.Welcome, b.ToArray());
        }

        static byte[] BuildId(byte op, string id)
        {
            var b = new List<byte>(16);
            TcpWire.PutString(b, id);
            return TcpWire.Wrap(op, b.ToArray());
        }

        static byte[] BuildRelay(string target, byte[] payload)
        {
            var b = new List<byte>(payload.Length + 8);
            TcpWire.PutString(b, target);
            TcpWire.PutBytes(b, payload);
            return TcpWire.Wrap(TcpOp.Relay, b.ToArray());
        }

        static byte[] BuildDeliver(string from, byte[] payload)
        {
            var b = new List<byte>(payload.Length + 8);
            TcpWire.PutString(b, from);
            TcpWire.PutBytes(b, payload);
            return TcpWire.Wrap(TcpOp.Deliver, b.ToArray());
        }

        // ---- bookkeeping ----

        /// <summary>Rebuilds the two lock-free snapshots the main thread reads. Call inside _lock.</summary>
        void RebuildLocked()
        {
            _peerIds = _peers.ToArray();
            var arr = new TcpLink[_links.Count];
            _links.Values.CopyTo(arr, 0);
            _linkSnapshot = arr;
        }

        void RaiseError(string message)
        {
            Debug.LogWarning("[tcp] " + message);
            if (Error != null) Error(message);
        }

        static void JoinThread(Thread t)
        {
            if (t == null || t == Thread.CurrentThread || !t.IsAlive) return;
            try
            {
                if (!t.Join(ThreadJoinMs)) Debug.LogWarning($"[tcp] thread '{t.Name}' did not stop within {ThreadJoinMs} ms");
            }
            catch { }
        }

        // ---- addresses ----

        /// <summary>"host", "host:port", "192.168.1.5:7777". IPv4 and names only, no IPv6 literals.</summary>
        public static bool ParseAddress(string address, out string host, out int port)
        {
            host = "";
            port = DefaultPort;
            var s = (address ?? "").Trim();
            if (s.Length == 0) return false;
            var colon = s.LastIndexOf(':');
            if (colon >= 0)
            {
                int parsed;
                if (int.TryParse(s.Substring(colon + 1), out parsed) && parsed > 0 && parsed <= 65535)
                {
                    port = parsed;
                    s = s.Substring(0, colon);
                }
            }
            host = s.Trim();
            return host.Length > 0;
        }

        /// <summary>The port in an address, or the fallback. A host only needs this half.</summary>
        public static int PortOf(string address, int fallback)
        {
            var s = (address ?? "").Trim();
            var colon = s.LastIndexOf(':');
            int parsed;
            if (colon >= 0 && int.TryParse(s.Substring(colon + 1), out parsed) && parsed > 0 && parsed <= 65535)
                return parsed;
            return fallback;
        }

        /// <summary>
        /// Every LAN IPv4 address of this machine with the port appended, for the host to
        /// show as its "room code". Same machine? "localhost:port" always works.
        /// </summary>
        public static string[] LocalAddresses(int port)
        {
            var found = new List<string>(4);
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address == null || ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var text = ua.Address.ToString();
                        if (text.StartsWith("169.254")) continue;      // self-assigned, nobody can reach it
                        var entry = text + ":" + port;
                        if (!found.Contains(entry)) found.Add(entry);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[tcp] could not list local addresses: " + e.Message);
            }
            found.Add("localhost:" + port);
            return found.ToArray();
        }

        // ---- no thread outlives play mode ----

        static void EnsureHooks()
        {
            if (_hooked) return;
            _hooked = true;
            Application.quitting += CloseAllLive;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CloseAllLive;
#endif
        }

#if UNITY_EDITOR
        static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode) CloseAllLive();
        }
#endif

        static void CloseAllLive()
        {
            TcpTransport[] all;
            lock (LiveLock) all = Live.ToArray();
            if (all.Length == 0) return;
            Debug.Log($"[tcp] closing {all.Length} transport(s): play mode exit, quit or domain reload");
            for (var i = 0; i < all.Length; i++) all[i].Leave();
        }
    }
}
#endif
