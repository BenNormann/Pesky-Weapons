namespace Pesky.Sim
{
    /// <summary>FNV-1a 64-bit over a byte range: the state hash the determinism test compares.</summary>
    public static class SimHash
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public static ulong Fnv1a64(byte[] bytes, int offset, int count)
        {
            var h = Offset;
            if (bytes == null) return h;
            var end = offset + count;
            if (end > bytes.Length) end = bytes.Length;
            for (var i = offset; i < end; i++)
            {
                h ^= bytes[i];
                h *= Prime;
            }
            return h;
        }
    }
}
