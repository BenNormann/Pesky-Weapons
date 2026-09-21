using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Pesky.Protocol
{
    /// <summary>
    /// Sequential little-endian payload writer. The first byte written is the
    /// message type. Positions go on the wire as i16 decimetres, unit
    /// directions as i16 / 32767. Small and boring on purpose.
    /// </summary>
    public sealed class NetWriter
    {
        public const float MetresPerDm = 0.1f;
        public const float DirScale = 32767f;

        readonly List<byte> _bytes;

        public NetWriter(byte messageType, int capacity = 64)
        {
            _bytes = new List<byte>(capacity);
            _bytes.Add(messageType);
        }

        public int Length => _bytes.Count;

        public NetWriter U8(byte v) { _bytes.Add(v); return this; }
        public NetWriter U8(int v) { _bytes.Add((byte)Mathf.Clamp(v, 0, 255)); return this; }
        public NetWriter Bool(bool v) { _bytes.Add(v ? (byte)1 : (byte)0); return this; }
        public NetWriter I8(sbyte v) { _bytes.Add(unchecked((byte)v)); return this; }

        public NetWriter U16(ushort v)
        {
            _bytes.Add((byte)(v & 0xff));
            _bytes.Add((byte)(v >> 8));
            return this;
        }

        public NetWriter I16(short v) => U16(unchecked((ushort)v));

        public NetWriter U32(uint v)
        {
            _bytes.Add((byte)(v & 0xff));
            _bytes.Add((byte)((v >> 8) & 0xff));
            _bytes.Add((byte)((v >> 16) & 0xff));
            _bytes.Add((byte)(v >> 24));
            return this;
        }

        public NetWriter I32(int v) => U32(unchecked((uint)v));

        public NetWriter F32(float v)
        {
            // BitConverter is little-endian on every platform Unity targets.
            _bytes.AddRange(BitConverter.GetBytes(v));
            return this;
        }

        /// <summary>u8 length prefix + UTF-8 bytes, capped at 255 bytes.</summary>
        public NetWriter Str(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            var len = Mathf.Min(bytes.Length, 255);
            _bytes.Add((byte)len);
            for (var i = 0; i < len; i++) _bytes.Add(bytes[i]);
            return this;
        }

        /// <summary>Raw bytes with no length prefix; the reader must know the count.</summary>
        public NetWriter Bytes(byte[] raw, int offset, int count)
        {
            for (var i = 0; i < count; i++) _bytes.Add(raw[offset + i]);
            return this;
        }

        /// <summary>A metre value as i16 decimetres (±3,276.7 m).</summary>
        public NetWriter Dm(float metres) =>
            I16((short)Mathf.Clamp(Mathf.RoundToInt(metres / MetresPerDm), short.MinValue, short.MaxValue));

        public NetWriter PosDm(Vector3 p) => Dm(p.x).Dm(p.y).Dm(p.z);

        /// <summary>A unit component as i16 / 32767.</summary>
        public NetWriter Dir(float unit) =>
            I16((short)Mathf.Clamp(Mathf.RoundToInt(unit * DirScale), short.MinValue, short.MaxValue));

        public NetWriter Dir3(Vector3 d) => Dir(d.x).Dir(d.y).Dir(d.z);

        // ---- Pesky Weapons pose and combat helpers ----

        public const float VelScale = 256f;
        public const float HpScale = 10f;
        public const float SpeedScale = 100f;
        const float QuatBound = 0.70710678f;

        /// <summary>A world position as three f32. The labyrinth is wider than the 327 m an i16 centimetre reaches, so positions are not quantised.</summary>
        public NetWriter Pos(Vector3 p) => F32(p.x).F32(p.y).F32(p.z);

        /// <summary>A velocity component as i16 / 256 m/s (about 128 m/s either way).</summary>
        public NetWriter Vel(float metresPerSecond) =>
            I16((short)Mathf.Clamp(Mathf.RoundToInt(metresPerSecond * VelScale), short.MinValue, short.MaxValue));

        public NetWriter Vel3(Vector3 v) => Vel(v.x).Vel(v.y).Vel(v.z);

        /// <summary>Hit points as u16 tenths (0 to 6553.5).</summary>
        public NetWriter Hp(float hp) =>
            U16((ushort)Mathf.Clamp(Mathf.RoundToInt(hp * HpScale), 0, ushort.MaxValue));

        /// <summary>A speed as u16 hundredths of a metre per second (0 to 655.35).</summary>
        public NetWriter Speed(float metresPerSecond) =>
            U16((ushort)Mathf.Clamp(Mathf.RoundToInt(metresPerSecond * SpeedScale), 0, ushort.MaxValue));

        /// <summary>A yaw in degrees as u16 over the full circle.</summary>
        public NetWriter Yaw(float degrees) =>
            U16((ushort)(Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) * (65536f / 360f)) & 0xFFFF));

        /// <summary>A rotation as a 32 bit smallest-three quaternion (about 0.1 degree of error).</summary>
        public NetWriter Quat(Quaternion q) => U32(PackQuat(q));

        public static uint PackQuat(Quaternion q)
        {
            float ax = Mathf.Abs(q.x), ay = Mathf.Abs(q.y), az = Mathf.Abs(q.z), aw = Mathf.Abs(q.w);
            int largest = 0;
            float m = ax;
            if (ay > m) { largest = 1; m = ay; }
            if (az > m) { largest = 2; m = az; }
            if (aw > m) { largest = 3; m = aw; }
            float a, b, c;
            switch (largest)
            {
                case 0: a = q.y; b = q.z; c = q.w; break;
                case 1: a = q.x; b = q.z; c = q.w; break;
                case 2: a = q.x; b = q.y; c = q.w; break;
                default: a = q.x; b = q.y; c = q.z; break;
            }
            // The dropped term is rebuilt as a positive root, so flip the other three when it was negative.
            if (q[largest] < 0f) { a = -a; b = -b; c = -c; }
            return ((uint)largest << 30) | ((uint)Pack10(a) << 20) | ((uint)Pack10(b) << 10) | (uint)Pack10(c);
        }

        public static Quaternion UnpackQuat(uint packed)
        {
            int largest = (int)(packed >> 30);
            float a = Unpack10(packed >> 20), b = Unpack10(packed >> 10), c = Unpack10(packed);
            float d = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));
            Quaternion q;
            switch (largest)
            {
                case 0: q = new Quaternion(d, a, b, c); break;
                case 1: q = new Quaternion(a, d, b, c); break;
                case 2: q = new Quaternion(a, b, d, c); break;
                default: q = new Quaternion(a, b, c, d); break;
            }
            return q.normalized;
        }

        static int Pack10(float v) =>
            Mathf.Clamp(Mathf.RoundToInt((v / QuatBound * 0.5f + 0.5f) * 1023f), 0, 1023);

        static float Unpack10(uint bits) => ((bits & 1023u) / 1023f - 0.5f) * 2f * QuatBound;

        
public byte[] ToArray() => _bytes.ToArray();
    }
}
