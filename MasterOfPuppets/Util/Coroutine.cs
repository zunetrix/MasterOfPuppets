using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;

namespace MasterOfPuppets.Util;

internal static class Coroutine {
    internal static bool StopAll = false;

    internal static void StartRunOnFramework(
        Action runFunction,
        Action? callback = null,
        Func<bool>? stopWhen = null,
        int timeoutMs = -1,
        CancellationToken cancellationToken = default) {
        var timer = timeoutMs > 0 ? Stopwatch.StartNew() : null;
        DalamudApi.Framework.Update += OnUpdate;

        void Unsubscribe() => DalamudApi.Framework.Update -= OnUpdate;

        void OnUpdate(IFramework framework) {
            try {
                if (StopAll
                    || cancellationToken.IsCancellationRequested
                    || stopWhen?.Invoke() == true
                    || timer != null && timer.ElapsedMilliseconds > timeoutMs) {
                    Unsubscribe();
                    callback?.Invoke();
                    return;
                }

                runFunction();
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, "Coroutine.StartRunOnFramework Error");
                Unsubscribe();
            }
        }
    }

    /// <summary>
    ///     Blocks while condition is true or timeout occurs.
    /// </summary>
    /// <param name="condition">The condition that will perpetuate the block.</param>
    /// <param name="frequency">The frequency at which the condition will be checked, in milliseconds.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <exception cref="TimeoutException"></exception>
    internal static Task WaitWhile(Func<bool> condition, int timeout = -1, int frequency = 25)
        => WaitCore(() => !condition(), timeout, frequency);

    /// <summary>
    ///     Blocks until condition is true or timeout occurs.
    /// </summary>
    /// <param name="condition">The break condition.</param>
    /// <param name="frequency">The frequency at which the condition will be checked, in milliseconds.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <exception cref="TimeoutException"></exception>
    internal static Task WaitUntil(Func<bool> condition, int timeout = -1, int frequency = 25)
        => WaitCore(condition, timeout, frequency);

    internal static bool WaitUntilSync(Func<bool> condition, int frequency = 25, int timeout = int.MaxValue) {
        var elapsed = 0;
        while (elapsed < timeout) {
            if (condition()) return true;
            elapsed += frequency;
            Thread.Sleep(frequency);
        }
        return condition();
    }

    private static async Task WaitCore(Func<bool> predicate, int timeout, int frequency) {
        var waitTask = Task.Run(async () => {
            while (!predicate()) await Task.Delay(frequency);
        });

        if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout)))
            throw new TimeoutException();
    }
}
