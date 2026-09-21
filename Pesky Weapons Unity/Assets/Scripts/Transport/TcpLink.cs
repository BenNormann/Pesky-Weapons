#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Pesky.Transport
{
    /// <summary>
    /// The opcodes of the desktop TCP wire. These frames belong to the transport and
    /// never reach Session; only the payload carried inside Relay / Deliver does.
    /// </summary>
    internal static class TcpOp
    {
        /// <summary>client -&gt; host, first frame: magic u32 + wire version u8.</summary>
        public const byte Hello = 0x01;
        /// <summary>host -&gt; client: your id, the host id, then every peer already here.</summary>
        public const byte Welcome = 0x02;
        /// <summary>host -&gt; client: a peer id that joined.</summary>
        public const byte Joined = 0x03;
        /// <summary>host -&gt; client: a peer id that left.</summary>
        public const byte Left = 0x04;
        /// <summary>client -&gt; host: target id ("" = every other peer) + payload. The host routes it.</summary>
        public const byte Relay = 0x05;
        /// <summary>host -&gt; client: origin id + payload. Becomes Message(origin, payload).</summary>
        public const byte Deliver = 0x06;
        /// <summary>either way, empty body: feeds the other side's liveness timer.</summary>
        public const byte Ping = 0x07;
    }

    /// <summary>
    /// Framing for the TCP wire: length u32 little endian (counting the opcode),
    /// opcode u8, body. Strings are a u16 length then UTF-8.
    /// </summary>
    internal static class TcpWire
    {
        public static readonly byte[] Empty = new byte[0];

        /// <summary>Wraps an opcode and body into one length-prefixed frame.</summary>
        public static byte[] Wrap(byte op, byte[] body)
        {
            var bodyLen = body == null ? 0 : body.Length;
            var len = 1 + bodyLen;
            var frame = new byte[4 + len];
            frame[0] = (byte)len;
            frame[1] = (byte)(len >> 8);
            frame[2] = (byte)(len >> 16);
            frame[3] = (byte)(len >> 24);
            frame[4] = op;
            if (bodyLen > 0) Buffer.BlockCopy(body, 0, frame, 5, bodyLen);
            return frame;
        }

        public static void PutString(List<byte> into, string s)
        {
            var bytes = string.IsNullOrEmpty(s) ? Empty : Encoding.UTF8.GetBytes(s);
            into.Add((byte)(bytes.Length & 0xff));
            into.Add((byte)((bytes.Length >> 8) & 0xff));
            for (var i = 0; i < bytes.Length; i++) into.Add(bytes[i]);
        }

        public static void PutBytes(List<byte> into, byte[] bytes)
        {
            if (bytes == null) return;
            for (var i = 0; i < bytes.Length; i++) into.Add(bytes[i]);
        }

        public static bool TryGetString(byte[] body, ref int pos, out string s)
        {
            s = null;
            if (body == null || pos + 2 > body.Length) return false;
            var len = body[pos] | (body[pos + 1] << 8);
            pos += 2;
            if (len < 0 || pos + len > body.Length) return false;
            s = len == 0 ? "" : Encoding.UTF8.GetString(body, pos, len);
            pos += len;
            return true;
        }

        public static byte[] Tail(byte[] body, int pos)
        {
            if (body == null || pos >= body.Length) return Empty;
            var tail = new byte[body.Length - pos];
            Buffer.BlockCopy(body, pos, tail, 0, tail.Length);
            return tail;
        }

        public static uint GetU32(byte[] body, int pos)
        {
            return (uint)(body[pos] | (body[pos + 1] << 8) | (body[pos + 2] << 16) | (body[pos + 3] << 24));
        }

        public static void PutU32(List<byte> into, uint v)
        {
            into.Add((byte)(v & 0xff));
            into.Add((byte)((v >> 8) & 0xff));
            into.Add((byte)((v >> 16) & 0xff));
            into.Add((byte)((v >> 24) & 0xff));
        }
    }

    /// <summary>
    /// One TCP connection: a reader thread that blocks on the socket and a writer thread
    /// that drains a queue, so the Unity main thread never blocks on a socket. The reader
    /// hands whole frames to the owner (off the main thread, as the INetTransport contract
    /// allows) and reports the connection dying exactly once through onClosed. The socket's
    /// receive timeout IS the liveness timeout: the writer sends a Ping whenever it has been
    /// idle for PingIntervalMs, so silence longer than LivenessMs means the peer is gone.
    /// </summary>
    internal sealed class TcpLink
    {
        public const int MaxFrameBytes = 32 * 1024;   // Protocol FRAME tops out at 16,000
        public const int PingIntervalMs = 2000;
        public const int LivenessMs = 10000;
        const int MaxQueuedFrames = 512;

        static readonly byte[] PingFrame = TcpWire.Wrap(TcpOp.Ping, null);

        /// <summary>The peer id this link talks to; null on the host until Hello is accepted.</summary>
        public string Id;

        /// <summary>True on the client's single link to the host.</summary>
        public readonly bool IsToHost;

        readonly TcpClient _client;
        readonly NetworkStream _stream;
        readonly Action<TcpLink, byte, byte[]> _onFrame;
        readonly Action<TcpLink, string> _onClosed;
        readonly Queue<byte[]> _out = new Queue<byte[]>();
        readonly object _outLock = new object();
        readonly byte[] _header = new byte[5];
        Thread _reader;
        Thread _writer;
        volatile bool _closed;
        int _reported;

        public bool IsClosed => _closed;

        public TcpLink(TcpClient client, bool isToHost, Action<TcpLink, byte, byte[]> onFrame, Action<TcpLink, string> onClosed)
        {
            _client = client;
            _client.NoDelay = true;
            _client.ReceiveTimeout = LivenessMs;
            _client.SendTimeout = LivenessMs;
            _stream = client.GetStream();
            IsToHost = isToHost;
            _onFrame = onFrame;
            _onClosed = onClosed;
        }

        public void Start(string name)
        {
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = name + " read" };
            _writer = new Thread(WriteLoop) { IsBackground = true, Name = name + " write" };
            _reader.Start();
            _writer.Start();
        }

        /// <summary>Queues a whole frame; safe from any thread. Never blocks on the socket.</summary>
        public void Send(byte[] frame)
        {
            if (_closed || frame == null || frame.Length == 0) return;
            var overflowed = false;
            lock (_outLock)
            {
                if (_closed) return;
                if (_out.Count >= MaxQueuedFrames) overflowed = true;
                else
                {
                    _out.Enqueue(frame);
                    Monitor.Pulse(_outLock);
                }
            }
            if (overflowed) Fail("send queue overflowed (peer too slow)");
        }

        /// <summary>Shuts the socket down. Blocking reads and writes fall out of their calls.</summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;
            lock (_outLock)
            {
                _out.Clear();
                Monitor.PulseAll(_outLock);
            }
            try { _stream.Close(); } catch { }
            try { _client.Close(); } catch { }
        }

        /// <summary>Waits for both threads to stop. Never joins the calling thread itself.</summary>
        public void Join(int ms)
        {
            JoinOne(_reader, ms);
            JoinOne(_writer, ms);
        }

        static void JoinOne(Thread t, int ms)
        {
            if (t == null || t == Thread.CurrentThread || !t.IsAlive) return;
            try
            {
                if (!t.Join(ms)) Debug.LogWarning($"[tcp] thread '{t.Name}' did not stop within {ms} ms");
            }
            catch { }
        }

        void ReadLoop()
        {
            try
            {
                while (!_closed)
                {
                    if (!ReadExactly(_header, 5)) break;
                    var len = _header[0] | (_header[1] << 8) | (_header[2] << 16) | (_header[3] << 24);
                    if (len < 1 || len > MaxFrameBytes)
                    {
                        Fail($"bad frame length {len}");
                        return;
                    }
                    var op = _header[4];
                    var body = TcpWire.Empty;
                    if (len > 1)
                    {
                        body = new byte[len - 1];
                        if (!ReadExactly(body, body.Length)) break;
                    }
                    if (op != TcpOp.Ping && _onFrame != null) _onFrame(this, op, body);
                }
            }
            catch (Exception e)
            {
                if (!_closed) Fail(Describe(e));
                return;
            }
            if (!_closed) Fail("peer closed the connection");
        }

        void WriteLoop()
        {
            try
            {
                while (!_closed)
                {
                    byte[] frame = null;
                    lock (_outLock)
                    {
                        if (_out.Count == 0) Monitor.Wait(_outLock, PingIntervalMs);
                        if (_closed) return;
                        if (_out.Count > 0) frame = _out.Dequeue();
                    }
                    if (frame == null) frame = PingFrame;   // idle: keep the other side's timer fed
                    _stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception e)
            {
                if (!_closed) Fail(Describe(e));
            }
        }

        bool ReadExactly(byte[] into, int count)
        {
            var got = 0;
            while (got < count)
            {
                var n = _stream.Read(into, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        /// <summary>Closes and reports, once, whichever thread notices first.</summary>
        void Fail(string reason)
        {
            var first = Interlocked.CompareExchange(ref _reported, 1, 0) == 0;
            Close();
            if (first && _onClosed != null) _onClosed(this, reason);
        }

        static string Describe(Exception e)
        {
            var io = e as IOException;
            var se = (io != null ? io.InnerException : e) as SocketException;
            if (se != null && se.SocketErrorCode == SocketError.TimedOut)
                return $"peer went quiet for {LivenessMs} ms";
            return e.Message;
        }
    }
}
#endif
