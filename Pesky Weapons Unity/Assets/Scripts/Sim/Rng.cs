namespace Pesky.Sim
{
    /// <summary>
    /// A seeded xorshift32 generator: the host's world RNG and the cosmetic
    /// per-event RNG (debris, reels) every peer reseeds from an event. Owns
    /// nothing but its 32-bit state, which is never zero.
    /// </summary>
    public sealed class Rng
    {
        const uint ZeroSeedReplacement = 0x9E3779B9u;

        uint _state;

        public Rng(uint seed)
        {
            State = seed;
        }

        /// <summary>The raw state, exposed so a snapshot or a test can copy it. Never zero.</summary>
        public uint State
        {
            get => _state;
            set => _state = value == 0 ? ZeroSeedReplacement : value;
        }

        public uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform integer in [minInclusive, maxExclusive); returns minInclusive when the range is empty.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>True with the given probability.</summary>
        public bool Chance(float probability) => NextFloat() < probability;
    }
}
