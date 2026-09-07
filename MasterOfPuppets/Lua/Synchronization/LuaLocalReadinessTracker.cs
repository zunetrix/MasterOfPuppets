using System;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public enum LuaLocalReadinessSignal {
    None,
    Stage,
    Ready,
    Regression,
}

/// <summary>Pure local settled-state detector used before emitting phase frames.</summary>
public sealed class LuaLocalReadinessTracker {
    private readonly DateTimeOffset _earliestStageAt;
    private readonly TimeSpan _settleDuration;
    private DateTimeOffset? _settledSince;
    private bool _stageSent;
    private bool _readySent;

    public LuaLocalReadinessTracker(DateTimeOffset earliestStageAt, TimeSpan? settleDuration = null) {
        _earliestStageAt = earliestStageAt;
        _settleDuration = settleDuration ?? TimeSpan.FromMilliseconds(600);
        if (_settleDuration <= TimeSpan.Zero || _settleDuration > TimeSpan.FromSeconds(10))
            throw new ArgumentOutOfRangeException(nameof(settleDuration));
    }

    public LuaLocalReadinessSignal Observe(DateTimeOffset now, bool movementActive) {
        if (movementActive) {
            _settledSince = null;
            if (!_stageSent && !_readySent)
                return LuaLocalReadinessSignal.None;
            _stageSent = false;
            _readySent = false;
            return LuaLocalReadinessSignal.Regression;
        }
        if (now < _earliestStageAt)
            return LuaLocalReadinessSignal.None;
        if (!_stageSent) {
            _stageSent = true;
            _settledSince = now;
            return LuaLocalReadinessSignal.Stage;
        }
        if (!_readySent && _settledSince.HasValue && now - _settledSince.Value >= _settleDuration) {
            _readySent = true;
            return LuaLocalReadinessSignal.Ready;
        }
        return LuaLocalReadinessSignal.None;
    }
}
