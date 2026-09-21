using System;
using System.Collections.Generic;

namespace Pesky.Protocol
{
    /// <summary>
    /// Wire-wide constants and the two checks every codec shares: the type
    /// byte test at the top of TryDecode and the u8 batch count that guards
    /// every List field on encode. Owns the protocol version number.
    /// </summary>
    public static class Wire
    {
        /// <summary>Bump when any layout in Messages/ changes, or when an existing field gains a value older peers would simulate differently. JOIN_REQUEST and SESSION_INFO carry it.</summary>
                public const ushort ProtocolVersion = 4;

        /// <summary>A slot meaning nobody: no killer, no repairer, no issuer.</summary>
        public const byte NoSlot = 0xFF;

        /// <summary>A u16 id meaning none: no enemy, no door, a reply that created nothing.</summary>
        public const ushort NoId = 0xFFFF;

        public const int MaxPlayers = 8;

        /// <summary>Every batched list is prefixed by a u8 count, so 255 rows is the ceiling.</summary>
        public const int MaxBatch = 255;

        public static bool HasId(byte[] payload, byte id) =>
            payload != null && payload.Length >= 1 && payload[0] == id;

        /// <summary>The u8 count for a batch; null counts as empty. Throws past 255 rather than dropping rows silently.</summary>
        public static byte BatchCount<T>(List<T> list)
        {
            if (list == null) return 0;
            if (list.Count > MaxBatch)
                throw new InvalidOperationException($"batch of {list.Count} rows exceeds {MaxBatch}");
            return (byte)list.Count;
        }
    }
}
