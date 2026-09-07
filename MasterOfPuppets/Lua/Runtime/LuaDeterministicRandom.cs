using System;

namespace MasterOfPuppets.LuaScripting.Runtime;

/// <summary>XorShift64* stream with stable output across .NET/runtime versions.</summary>
public sealed class LuaDeterministicRandom {
    private const ulong ZeroSeedReplacement = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    public LuaDeterministicRandom(long seed) => Reset(seed);

    public void Reset(long seed) {
        _state = unchecked((ulong)seed);
        if (_state == 0)
            _state = ZeroSeedReplacement;
        // Diffuse nearby integer seeds before the first result.
        for (var index = 0; index < 4; index++)
            _ = NextUInt64();
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public long NextInt64(long minimumInclusive, long maximumInclusive) {
        if (minimumInclusive > maximumInclusive)
            throw new ArgumentOutOfRangeException(nameof(minimumInclusive), "minimum must not exceed maximum");
        var range = unchecked((ulong)(maximumInclusive - minimumInclusive)) + 1UL;
        if (range == 0)
            return unchecked((long)NextUInt64());
        var limit = ulong.MaxValue - (ulong.MaxValue % range);
        ulong sample;
        do sample = NextUInt64(); while (sample >= limit);
        return checked(minimumInclusive + (long)(sample % range));
    }

    private ulong NextUInt64() {
        var value = _state;
        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        _state = value;
        return value * 2685821657736338717UL;
    }
}
