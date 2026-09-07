using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets.LuaScripting;

internal sealed class LuaChatMessageCoordinator {
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromSeconds(5);
    private const int MaximumHistoryCount = 256;

    private readonly object _lock = new();
    private readonly List<ObservedMessage> _history = new();
    private readonly List<Waiter> _waiters = new();

    public void BeginRun() {
        lock (_lock)
            _history.Clear();
    }

    public void Publish(string speaker, string message) {
        if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(message))
            return;

        Waiter[] completed;
        lock (_lock) {
            var now = DateTimeOffset.UtcNow;
            PruneHistory(now);
            var observed = new ObservedMessage(now, speaker.Trim(), message.Trim());
            if (_history.Count >= MaximumHistoryCount)
                _history.RemoveRange(0, _history.Count - MaximumHistoryCount + 1);
            _history.Add(observed);
            completed = _waiters.Where(waiter => Matches(observed, waiter.Speaker, waiter.Message)).ToArray();
            foreach (var waiter in completed)
                _waiters.Remove(waiter);
        }

        foreach (var waiter in completed)
            waiter.Completion.TrySetResult(true);
    }

    public async Task<bool> WaitForAsync(
        string speaker,
        string message,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        var waiter = new Waiter(speaker.Trim(), message.Trim());
        lock (_lock) {
            var now = DateTimeOffset.UtcNow;
            PruneHistory(now);
            if (_history.Any(observed => Matches(observed, waiter.Speaker, waiter.Message)))
                return true;
            _waiters.Add(waiter);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try {
            return await waiter.Completion.Task.WaitAsync(timeoutCts.Token);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return false;
        } finally {
            lock (_lock)
                _waiters.Remove(waiter);
        }
    }

    private void PruneHistory(DateTimeOffset now) =>
        _history.RemoveAll(message => now - message.ObservedAt > ReplayWindow);

    private static bool Matches(ObservedMessage observed, string speaker, string message) =>
        SpeakerMatches(observed.Speaker, speaker)
        && string.Equals(observed.Message, message, StringComparison.Ordinal);

    private static bool SpeakerMatches(string actual, string expected) {
        actual = FormationCharacterName.NormalizeWorldSeparator(actual);
        expected = FormationCharacterName.NormalizeWorldSeparator(expected);
        var actualWorldSeparator = actual.IndexOf('@');
        if (actualWorldSeparator >= 0)
            actual = actual[..actualWorldSeparator];
        var expectedWorldSeparator = expected.IndexOf('@');
        if (expectedWorldSeparator >= 0)
            expected = expected[..expectedWorldSeparator];
        return string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ObservedMessage(DateTimeOffset ObservedAt, string Speaker, string Message);

    private sealed record Waiter(string Speaker, string Message) {
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
