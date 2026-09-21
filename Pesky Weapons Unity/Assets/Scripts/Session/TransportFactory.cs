using Pesky.Transport;

namespace Pesky.Session
{
    /// <summary>
    /// The only code that names a concrete transport. A WebGL player gets the NetBridge
    /// GameObject (WebRtcTransport); the editor and every standalone player get the desktop
    /// TcpTransport when they host or join; Play Offline gets a LoopbackTransport on every
    /// platform. Game may not reference Pesky.Transport, so this is the seam it calls through.
    /// </summary>
    public static class TransportFactory
    {
        /// <summary>WebRTC through net.js on a WebGL build; LAN TCP in the editor and standalone.</summary>
        public static INetTransport ForPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebRtcTransport.Create();
#else
            return new TcpTransport();
#endif
        }

        /// <summary>The no-network transport for solo play.</summary>
        public static INetTransport Offline() => new LoopbackTransport();

        /// <summary>
        /// The default port the desktop host listens on. On WebGL there is no port: the room
        /// code is a trystero room, not an address.
        /// </summary>
        public static int DefaultPort =>
#if UNITY_WEBGL && !UNITY_EDITOR
            0;
#else
            TcpTransport.DefaultPort;
#endif

        /// <summary>
        /// What a desktop host should show as its room code: every LAN IPv4 address of this
        /// machine with the port appended, "localhost:port" last for a second instance on the
        /// same desktop. Empty on WebGL, where the room code is the six-character trystero code.
        /// </summary>
        public static string[] HostAddresses(int port = 0)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new string[0];
#else
            return TcpTransport.LocalAddresses(port > 0 ? port : TcpTransport.DefaultPort);
#endif
        }
    }
}
