using System;
using System.Diagnostics;

namespace MasterOfPuppets;

/// <summary>
/// Maintains absolute macro phase deadlines so transient execution latency does
/// not accumulate across an indefinitely repeating sequence.
/// </summary>
public sealed class MacroPhaseClock {
    private readonly Func<long> _getTimestamp;
    private readonly double _timestampFrequency;
    private readonly long _startedAt;
    private double _targetSeconds;

    public MacroPhaseClock()
        : this(Stopwatch.GetTimestamp, Stopwatch.Frequency) {
    }

    public MacroPhaseClock(Func<long> getTimestamp, long timestampFrequency) {
        ArgumentNullException.ThrowIfNull(getTimestamp);
        if (timestampFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

        _getTimestamp = getTimestamp;
        _timestampFrequency = timestampFrequency;
        _startedAt = getTimestamp();
    }

    /// <summary>
    /// Advances the absolute phase deadline and returns only the time still
    /// remaining until that deadline. An overdue phase returns zero without
    /// rebasing later phases.
    /// </summary>
    public TimeSpan Advance(double seconds) {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));

        _targetSeconds += seconds;
        var elapsedSeconds = (_getTimestamp() - _startedAt) / _timestampFrequency;
        var remainingSeconds = Math.Max(0, _targetSeconds - elapsedSeconds);
        return TimeSpan.FromSeconds(remainingSeconds);
    }
}
