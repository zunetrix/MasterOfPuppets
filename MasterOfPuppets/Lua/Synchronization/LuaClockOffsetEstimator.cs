using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public readonly record struct LuaClockExchange(
    double LocalSendSeconds,
    double RemoteReceiveSeconds,
    double RemoteSendSeconds,
    double LocalReceiveSeconds);

public readonly record struct LuaClockEstimate(
    bool IsReady,
    double OffsetSeconds,
    double RoundTripSeconds,
    double JitterSeconds,
    int SampleCount);

/// <summary>Bounded NTP-style shared-clock offset filter using best-RTT samples.</summary>
public sealed class LuaClockOffsetEstimator {
    private const int MaximumSamples = 32;
    private const double MaximumAcceptedRoundTrip = 2.0;
    private const double MaximumCorrectionPerSample = 0.050;
    private const double Smoothing = 0.25;
    private readonly Queue<(double Offset, double RoundTrip)> _samples = new();
    private readonly object _sync = new();
    private bool _ready;
    private double _offset;
    private double _roundTrip;

    public bool TryAdd(LuaClockExchange exchange, out LuaClockEstimate estimate, out string reason) {
        lock (_sync) {
            var values = new[] {
                exchange.LocalSendSeconds,
                exchange.RemoteReceiveSeconds,
                exchange.RemoteSendSeconds,
                exchange.LocalReceiveSeconds,
            };
            if (values.Any(value => !double.IsFinite(value))) {
                estimate = Snapshot();
                reason = "clock exchange contains a non-finite timestamp";
                return false;
            }
            if (exchange.LocalReceiveSeconds < exchange.LocalSendSeconds
                || exchange.RemoteSendSeconds < exchange.RemoteReceiveSeconds) {
                estimate = Snapshot();
                reason = "clock exchange timestamps are not monotonic";
                return false;
            }

            var roundTrip = (exchange.LocalReceiveSeconds - exchange.LocalSendSeconds)
                - (exchange.RemoteSendSeconds - exchange.RemoteReceiveSeconds);
            if (roundTrip < 0 || roundTrip > MaximumAcceptedRoundTrip) {
                estimate = Snapshot();
                reason = "clock exchange round-trip is outside the accepted range";
                return false;
            }
            var offset = ((exchange.RemoteReceiveSeconds - exchange.LocalSendSeconds)
                + (exchange.RemoteSendSeconds - exchange.LocalReceiveSeconds)) / 2.0;
            _samples.Enqueue((offset, roundTrip));
            while (_samples.Count > MaximumSamples)
                _samples.Dequeue();

            var bestRoundTrip = _samples.Min(sample => sample.RoundTrip);
            var candidates = _samples.Where(sample => sample.RoundTrip <= bestRoundTrip + 0.025).ToArray();
            var target = candidates.Average(sample => sample.Offset);
            if (!_ready) {
                _offset = target;
                _ready = true;
            } else {
                var correction = Math.Clamp((target - _offset) * Smoothing, -MaximumCorrectionPerSample, MaximumCorrectionPerSample);
                _offset += correction;
            }
            _roundTrip = bestRoundTrip;
            estimate = Snapshot();
            reason = string.Empty;
            return true;
        }
    }

    public double ToSharedSeconds(double localMonotonicSeconds) {
        if (!double.IsFinite(localMonotonicSeconds))
            throw new ArgumentOutOfRangeException(nameof(localMonotonicSeconds));
        lock (_sync)
            return localMonotonicSeconds + (_ready ? _offset : 0.0);
    }

    public LuaClockEstimate Snapshot() {
        lock (_sync) {
            if (_samples.Count == 0)
                return new LuaClockEstimate(false, 0, 0, 0, 0);
            var jitter = Math.Sqrt(_samples.Average(sample => Math.Pow(sample.Offset - _offset, 2)));
            return new LuaClockEstimate(_ready, _offset, _roundTrip, jitter, _samples.Count);
        }
    }
}
