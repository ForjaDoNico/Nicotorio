namespace Factory.Simulation
{
    /// <summary>
    /// SplitMix64. Owned by the simulation so a seed produces the same sequence on every
    /// runtime (System.Random's algorithm is not guaranteed across .NET implementations).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = _state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % span);
        }
    }
}
