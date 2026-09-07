using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MasterOfPuppets.LuaScripting.Runtime;

public sealed class LuaRunQuota {
    private readonly object _lock = new();
    private readonly LuaRuntimeLimits _limits;
    private readonly Queue<long> _recentChatActions = new();
    private readonly Queue<long> _recentGameActions = new();
    private int _logLines;
    private int _logBytes;
    private int _pendingWaiters;
    private int _chatActions;
    private int _gameActions;

    public LuaRunQuota(LuaRuntimeLimits? limits = null) {
        _limits = (limits ?? LuaRuntimeLimits.Default).Validate();
    }

    public void WriteLog(string? text, Action<string>? sink) {
        text ??= string.Empty;
        var bytes = Encoding.UTF8.GetByteCount(text);
        lock (_lock) {
            if (_logLines >= _limits.MaximumLogLines
                || bytes > _limits.MaximumLogUtf8Bytes - _logBytes)
                throw new LuaQuotaExceededException("log", "Lua run log quota exceeded.");
            _logLines++;
            _logBytes += bytes;
        }
        sink?.Invoke(text);
    }

    public IDisposable EnterWaiter() {
        lock (_lock) {
            if (_pendingWaiters >= _limits.MaximumPendingWaiters)
                throw new LuaQuotaExceededException("waiter", "Lua pending waiter quota exceeded.");
            _pendingWaiters++;
        }
        return new WaiterLease(this);
    }

    public void ConsumeChatAction() {
        lock (_lock) {
            if (_chatActions >= _limits.MaximumChatActions)
                throw new LuaQuotaExceededException("chat-action", "Lua chat/action run quota exceeded.");

            var now = Environment.TickCount64;
            var oldestAllowed = now - (long)_limits.ChatActionWindow.TotalMilliseconds;
            while (_recentChatActions.TryPeek(out var observed) && observed <= oldestAllowed)
                _recentChatActions.Dequeue();
            if (_recentChatActions.Count >= _limits.MaximumChatActionsPerWindow)
                throw new LuaQuotaExceededException(
                    "chat-rate",
                    $"Lua chat/action rate exceeds {_limits.MaximumChatActionsPerWindow} per {_limits.ChatActionWindow.TotalSeconds:0.###} seconds.");
            _recentChatActions.Enqueue(now);
            _chatActions++;
        }
    }

    public void ConsumeGameAction() {
        lock (_lock) {
            if (_gameActions >= _limits.MaximumGameActions)
                throw new LuaQuotaExceededException("game-action", "Lua game-action run quota exceeded.");

            var now = Environment.TickCount64;
            var oldestAllowed = now - (long)_limits.GameActionWindow.TotalMilliseconds;
            while (_recentGameActions.TryPeek(out var observed) && observed <= oldestAllowed)
                _recentGameActions.Dequeue();
            if (_recentGameActions.Count >= _limits.MaximumGameActionsPerWindow)
                throw new LuaQuotaExceededException(
                    "game-action-rate",
                    $"Lua game-action rate exceeds {_limits.MaximumGameActionsPerWindow} per {_limits.GameActionWindow.TotalSeconds:0.###} seconds.");
            _recentGameActions.Enqueue(now);
            _gameActions++;
        }
    }

    public LuaQuotaSnapshot Snapshot() {
        lock (_lock)
            return new LuaQuotaSnapshot(_logLines, _logBytes, _pendingWaiters, _chatActions, _gameActions);
    }

    private void ExitWaiter() {
        lock (_lock) {
            if (_pendingWaiters > 0)
                _pendingWaiters--;
        }
    }

    private sealed class WaiterLease : IDisposable {
        private LuaRunQuota? _owner;
        public WaiterLease(LuaRunQuota owner) => _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ExitWaiter();
    }
}

public readonly record struct LuaQuotaSnapshot(int LogLines, int LogUtf8Bytes, int PendingWaiters, int ChatActions, int GameActions);

public sealed class LuaQuotaExceededException : InvalidOperationException {
    public LuaQuotaExceededException(string quota, string message) : base(message) => Quota = quota;
    public string Quota { get; }
}
