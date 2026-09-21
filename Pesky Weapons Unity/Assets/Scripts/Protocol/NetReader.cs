using System;
using System.Text;
using UnityEngine;

namespace Pesky.Protocol
{
    /// <summary>
    /// Sequential little-endian reader over one payload. Every accessor is
    /// bounds-safe: a short buffer sets <see cref="Failed"/> and returns a
    /// default instead of throwing, so a decoder ends with one check.
    /// </summary>
    public sealed class NetReader
    {
        readonly byte[] _buf;
        readonly int _end;
        int _pos;

        public bool Failed { get; private set; }
        public int Position => _pos;
        public int Remaining => _end - _pos;

        /// <summary>Starts at offset 1 by default, skipping the type byte.</summary>
        public NetReader(byte[] payload, int startAt = 1) : this(payload, startAt, payload.Length - startAt) { }

        public NetReader(byte[] payload, int startAt, int count)
        {
            _buf = payload;
            _pos = startAt;
            _end = Math.Min(payload.Length, startAt + count);
        }

        bool Need(int n)
        {
            if (Failed || _pos + n > _end) { Failed = true; return false; }
            return true;
        }

        public byte U8() { if (!Need(1)) return 0; return _buf[_pos++]; }
        public bool Bool() => U8() != 0;
        public sbyte I8() => unchecked((sbyte)U8());

        public ushort U16()
        {
            if (!Need(2)) return 0;
            var v = (ushort)(_buf[_pos] | (_buf[_pos + 1] << 8));
            _pos += 2;
            return v;
        }

        public short I16() => unchecked((short)U16());

        public uint U32()
        {
            if (!Need(4)) return 0;
            var v = (uint)(_buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24));
            _pos += 4;
            return v;
        }

        public int I32() => unchecked((int)U32());

        public float F32()
        {
            if (!Need(4)) return 0f;
            var v = BitConverter.ToSingle(_buf, _pos);
            _pos += 4;
            return v;
        }

        public string Str()
        {
            var len = U8();
            if (!Need(len)) return "";
            var s = Encoding.UTF8.GetString(_buf, _pos, len);
            _pos += len;
            return s;
        }

        public byte[] Bytes(int count)
        {
            if (!Need(count)) return Array.Empty<byte>();
            var out_ = new byte[count];
            Array.Copy(_buf, _pos, out_, 0, count);
            _pos += count;
            return out_;
        }

        public float Dm() => I16() * NetWriter.MetresPerDm;
        public Vector3 PosDm() => new Vector3(Dm(), Dm(), Dm());

        public float Dir() => I16() / NetWriter.DirScale;
        public Vector3 Dir3() => new Vector3(Dir(), Dir(), Dir());

        // ---- Pesky Weapons pose and combat helpers (mirror NetWriter) ----

        public Vector3 Pos() => new Vector3(F32(), F32(), F32());

        public float Vel() => I16() / NetWriter.VelScale;
        public Vector3 Vel3() => new Vector3(Vel(), Vel(), Vel());

        public float Hp() => U16() / NetWriter.HpScale;

        public float Speed() => U16() / NetWriter.SpeedScale;

        public float Yaw() => U16() * (360f / 65536f);

        public Quaternion Quat() => NetWriter.UnpackQuat(U32());

    }
}
