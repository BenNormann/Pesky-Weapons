using System;
using System.Collections.Generic;

namespace Pesky.Protocol
{
    /// <summary>
    /// The 0x7F FRAME container: one per peer per tick, holding every message
    /// due that tick as count:u8 then [len:u16 body] per message. Owns the
    /// 16,000 byte ceiling that keeps a frame inside one Trystero chunk, so
    /// send order equals arrival order.
    /// </summary>
    public static class Frame
    {
        public const byte Id = MsgId.Frame;
        public const int MaxBytes = 16000;
        public const int MaxCount = 255;

        /// <summary>Type byte plus count byte.</summary>
        public const int HeaderBytes = 2;

        /// <summary>The u16 length in front of each body.</summary>
        public const int PerMessageBytes = 2;

        public static bool IsFrame(byte[] payload) =>
            payload != null && payload.Length >= HeaderBytes && payload[0] == Id;

        /// <summary>
        /// Packs the messages in order. Throws InvalidOperationException if
        /// there are more than 255, any is empty, or the total would exceed
        /// MaxBytes; the caller splits across frames instead.
        /// </summary>
        public static byte[] Pack(List<byte[]> messages)
        {
            var count = messages == null ? 0 : messages.Count;
            if (count > MaxCount)
                throw new InvalidOperationException($"frame of {count} messages exceeds {MaxCount}");

            var total = HeaderBytes;
            for (var i = 0; i < count; i++)
            {
                var body = messages[i];
                if (body == null || body.Length == 0)
                    throw new InvalidOperationException($"message {i} is empty");
                total += PerMessageBytes + body.Length;
            }
            if (total > MaxBytes)
                throw new InvalidOperationException($"frame of {total} bytes exceeds {MaxBytes}");

            var frame = new byte[total];
            frame[0] = Id;
            frame[1] = (byte)count;
            var pos = HeaderBytes;
            for (var i = 0; i < count; i++)
            {
                var body = messages[i];
                frame[pos] = (byte)(body.Length & 0xff);
                frame[pos + 1] = (byte)(body.Length >> 8);
                pos += PerMessageBytes;
                Array.Copy(body, 0, frame, pos, body.Length);
                pos += body.Length;
            }
            return frame;
        }

        /// <summary>
        /// Appends every body to <paramref name="into"/>. On any fault (not a
        /// frame, truncated, empty body, trailing bytes) nothing is appended
        /// and false is returned.
        /// </summary>
        public static bool TryUnpack(byte[] payload, List<byte[]> into)
        {
            if (into == null || !IsFrame(payload)) return false;

            var start = into.Count;
            var count = payload[1];
            var pos = HeaderBytes;
            for (var i = 0; i < count; i++)
            {
                if (pos + PerMessageBytes > payload.Length) return Fail(into, start);
                var len = payload[pos] | (payload[pos + 1] << 8);
                pos += PerMessageBytes;
                if (len == 0 || pos + len > payload.Length) return Fail(into, start);
                var body = new byte[len];
                Array.Copy(payload, pos, body, 0, len);
                pos += len;
                into.Add(body);
            }
            if (pos != payload.Length) return Fail(into, start);
            return true;
        }

        static bool Fail(List<byte[]> into, int start)
        {
            into.RemoveRange(start, into.Count - start);
            return false;
        }
    }
}
