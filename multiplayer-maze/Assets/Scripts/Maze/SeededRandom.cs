/// <summary>
/// SplitMix64. System.Random's sequence is not guaranteed to match across runtimes,
/// and every machine must get the same maze and item layout, so the generator is our own.
/// </summary>
public struct SeededRandom
{
    private ulong _state;

    public SeededRandom(int seed) => _state = (ulong)(uint)seed;

    // Returns a value in [0, maxExclusive). The modulo bias is negligible for tiny ranges.
    public int Next(int maxExclusive) => (int)(NextUInt64() % (ulong)maxExclusive);

    private ulong NextUInt64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
