using System.Threading;

namespace Pesky.Session
{
    /// <summary>
    /// Cumulative counters of what the session put on the wire and took off it, for the debug overlay.
    /// Counted at the Session boundary (payload bytes; the base64 and WebRTC framing the browser adds
    /// are not included). A desktop transport raises Message from another thread, so the counters are
    /// interlocked. Reset on session start.
    /// </summary>
    public static class NetStats
    {
        static long _bytesIn, _bytesOut, _msgsIn, _msgsOut, _posesIn;

        public static long BytesIn { get { return Interlocked.Read(ref _bytesIn); } }
        public static long BytesOut { get { return Interlocked.Read(ref _bytesOut); } }
        public static long MsgsIn { get { return Interlocked.Read(ref _msgsIn); } }
        public static long MsgsOut { get { return Interlocked.Read(ref _msgsOut); } }
        /// <summary>Raw POSE (0x01) messages received from other peers.</summary>
        public static long PosesIn { get { return Interlocked.Read(ref _posesIn); } }

        /// <summary>One transport message (a FRAME or a raw message) arrived.</summary>
        public static void CountIn(int bytes)
        {
            Interlocked.Add(ref _bytesIn, bytes);
            Interlocked.Increment(ref _msgsIn);
        }

        /// <summary>One transport message went out to this many peers.</summary>
        public static void CountOut(int bytes, int peers)
        {
            if (peers <= 0) return;
            Interlocked.Add(ref _bytesOut, (long)bytes * peers);
            Interlocked.Add(ref _msgsOut, peers);
        }

        public static void CountPoseIn()
        {
            Interlocked.Increment(ref _posesIn);
        }

        public static void Reset()
        {
            Interlocked.Exchange(ref _bytesIn, 0);
            Interlocked.Exchange(ref _bytesOut, 0);
            Interlocked.Exchange(ref _msgsIn, 0);
            Interlocked.Exchange(ref _msgsOut, 0);
            Interlocked.Exchange(ref _posesIn, 0);
        }
    }

    /// <summary>
    /// Debug console lines from the Session assembly. Game arms it from DebugGate at startup, so the
    /// lines exist only in the Editor, in development builds and on ?debug=1 pages. Note that the host's
    /// labyrinth rule uses it to say who it bent and why it refused: on a debug page the host's own
    /// console is no longer secret, which is the point of a debug page.
    /// </summary>
    public static class NetDebug
    {
        public static bool Enabled;

        public static void Log(string line)
        {
            if (Enabled) UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null, "[pesky] {0}", line);
        }
    }
}
