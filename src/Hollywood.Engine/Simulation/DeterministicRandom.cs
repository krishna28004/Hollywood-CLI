namespace Hollywood.Engine.Simulation;

public sealed class DeterministicRandom
{
    private uint state;
    public DeterministicRandom(uint seed) => state = seed == 0 ? 0x9E3779B9u : seed;
    public uint NextUInt()
    {
        var value = state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }
    public int Next(int maxExclusive) => (int)(NextUInt() % (uint)maxExclusive);
}
