using System;
using System.Text;
using UnityEngine;

namespace Pesky.Protocol
{
    /// <summary>
    /// The three M0 messages, ported byte-for-byte from After Hours and never
    /// changed: 0x01 TRANSFORM (19 bytes at 15 Hz), 0x02 HELLO (reply on
    /// receive so late joiners learn every name), 0x03 PING (one byte, sent by
    /// net.js on a wall-clock timer, never by C#). Gameplay owns 0x10 to 0x7F.
    /// </summary>
    public static class M0Messages
    {
        public const byte TypeTransform = 0x01;
        public const byte TypeHello = 0x02;
        public const byte TypePing = 0x03;

        /// <summary>0x01 POSE: type u8 | flags u8 (PoseFlags) | pos f32 x3 | rot u32 smallest-three | vel i16 x3 (1/256 m/s) | seq u16.</summary>
        public const int PoseSize = 26;
        public const int MaxNameBytes = 32;

        // 0x01 POSE (Pesky Weapons respecification of ATCK's TRANSFORM, protocol version 2). Raw, never in a FRAME.
public static byte[] EncodePose(PoseFlags flags, Vector3 pos, Quaternion rot, Vector3 vel, ushort seq)
        {
            var w = new NetWriter(TypeTransform, PoseSize);
            w.U8((byte)flags).Pos(pos).Quat(rot).Vel3(vel).U16(seq);
            return w.ToArray();
        }

public static bool TryDecodePose(byte[] b, out PoseFlags flags, out Vector3 pos, out Quaternion rot, out Vector3 vel, out ushort seq)
        {
            flags = PoseFlags.None; pos = default; rot = Quaternion.identity; vel = default; seq = 0;
            if (b == null || b.Length < PoseSize || b[0] != TypeTransform) return false;
            var r = new NetReader(b);
            flags = (PoseFlags)r.U8();
            pos = r.Pos();
            rot = r.Quat();
            vel = r.Vel3();
            seq = r.U16();
            if (r.Failed) return false;
            // A NaN from a broken peer must never reach a Rigidbody.
            return !(float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)
                || float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z));
        }

        // 0x02 HELLO: type u8 | nameLen u8 (max 32) | name UTF-8
        public static byte[] EncodeHello(string name)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name ?? "");
            var len = Mathf.Min(nameBytes.Length, MaxNameBytes);
            var b = new byte[2 + len];
            b[0] = TypeHello;
            b[1] = (byte)len;
            Array.Copy(nameBytes, 0, b, 2, len);
            return b;
        }

        public static bool TryDecodeHello(byte[] b, out string name)
        {
            name = null;
            if (b == null || b.Length < 2 || b[0] != TypeHello) return false;
            var len = Mathf.Min(b[1], Mathf.Min(MaxNameBytes, b.Length - 2));
            name = Encoding.UTF8.GetString(b, 2, len);
            return true;
        }

        /// <summary>Wrap-safe sequence comparison: is a newer than b?</summary>
        public static bool SeqIsNewer(ushort a, ushort b) => (short)(a - b) > 0;

        static void WriteF32(byte[] b, int offset, float v)
        {
            var bytes = BitConverter.GetBytes(v);
            b[offset] = bytes[0]; b[offset + 1] = bytes[1];
            b[offset + 2] = bytes[2]; b[offset + 3] = bytes[3];
        }
    }
}
