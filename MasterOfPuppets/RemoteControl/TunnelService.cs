using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.RemoteControl;

internal enum TunnelStatus {
    Stopped,
    Starting,
    Running,
    Error,
}

/// <summary>
/// Manages a tunnel process (e.g. Cloudflare Tunnel or ngrok) that exposes
/// the local remote-control HTTP server via a public URL.
///
/// Only one instance per port is allowed at a time; a named OS Mutex
/// (MasterOfPuppets_Tunnel_{port}) enforces this across plugin instances.
/// </summary>
internal sealed class TunnelService : IDisposable {
    // Matches common tunnel public URLs:
    //   https://xxxx.trycloudflare.com
    //   https://xxxx.ngrok-free.app / https://xxxx.ngrok.io / https://xxxx.ngrok.dev
    private static readonly Regex PublicUrlRegex = new(
        @"https://[a-zA-Z0-9\-]+\.(?:trycloudflare\.com|cloudflareaccess\.com|ngrok(?:-free)?\.(?:app|io|dev))[^\s]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Process _process;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public TunnelStatus Status { get; private set; } = TunnelStatus.Starting;
    public string? PublicUrl { get; private set; }
    public string? LastError { get; private set; }

    private TunnelService(Process process, Mutex mutex) {
        _process = process;
        _mutex = mutex;
    }

    /// <summary>
    /// Tries to start a tunnel for the given port.
    /// <paramref name="command"/> is the full command string, e.g.
    /// <c>cloudflared tunnel --url http://localhost:{port}</c> or just
    /// <c>ngrok http {port}</c>. <c>{port}</c> in the command is replaced
    /// with the actual port value.
    /// Returns <c>null</c> if another instance already holds the tunnel
    /// mutex for this port, or if the command is empty.
    /// </summary>
    public static TunnelService? TryStart(string command, int port) {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var resolvedCommand = command.Replace("{port}", port.ToString());

        var firstSpace = resolvedCommand.IndexOf(' ');
        var binary = firstSpace < 0 ? resolvedCommand : resolvedCommand[..firstSpace];
        var arguments = firstSpace < 0 ? string.Empty : resolvedCommand[(firstSpace + 1)..];

        var mutex = new Mutex(false, $"MasterOfPuppets_Tunnel_{port}");
        bool acquired;
        try {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) {
                acquired = true;
                DalamudApi.PluginLog.Warning($"[Tunnel] Port-{port} mutex was abandoned; taking over.");
            }
        } catch {
            mutex.Dispose();
            throw;
        }

        if (!acquired) {
            mutex.Dispose();
            DalamudApi.PluginLog.Information($"[Tunnel] Another instance is already running the tunnel on port {port}.");
            return null;
        }

        ProcessStartInfo psi;
        try {
            psi = new ProcessStartInfo {
                FileName = binary,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
        } catch (Exception ex) {
            try { mutex.ReleaseMutex(); } catch { /* ignore */ }
            mutex.Dispose();
            throw new InvalidOperationException($"Failed to build tunnel process start info: {ex.Message}", ex);
        }

        Process process;
        try {
            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();
        } catch (Exception ex) {
            try { mutex.ReleaseMutex(); } catch { /* ignore */ }
            mutex.Dispose();
            throw new InvalidOperationException($"Failed to start tunnel process '{binary}': {ex.Message}", ex);
        }

        var service = new TunnelService(process, mutex);
        _ = service.MonitorOutputAsync();
        return service;
    }

    private async Task MonitorOutputAsync() {
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

        var stdoutTask = ReadStreamAsync(_process.StandardOutput, combined.Token);
        var stderrTask = ReadStreamAsync(_process.StandardError, combined.Token);

        _ = Task.Run(async () => {
            try { await _process.WaitForExitAsync(combined.Token); }
            catch (OperationCanceledException) { return; }

            if (!_disposed && Status != TunnelStatus.Running) {
                Status = TunnelStatus.Error;
                LastError = $"Tunnel process exited (code {_process.ExitCode})";
                DalamudApi.PluginLog.Warning($"[Tunnel] Process exited with code {_process.ExitCode}.");
            }
        });

        await Task.WhenAll(stdoutTask, stderrTask);
    }

    private async Task ReadStreamAsync(System.IO.StreamReader reader, CancellationToken token) {
        try {
            while (!token.IsCancellationRequested) {
                var line = await reader.ReadLineAsync(token);
                if (line == null) break;

                DalamudApi.PluginLog.Debug($"[Tunnel] {line}");

                if (PublicUrl == null) {
                    var match = PublicUrlRegex.Match(line);
                    if (match.Success) {
                        PublicUrl = match.Value;
                        Status = TunnelStatus.Running;
                        DalamudApi.PluginLog.Information($"[Tunnel] Public URL detected: {PublicUrl}");
                    }
                }
            }
        } catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[Tunnel] Error reading process output.");
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        Status = TunnelStatus.Stopped;
        _cts.Cancel();

        try {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[Tunnel] Error killing tunnel process.");
        } finally {
            _process.Dispose();
            _cts.Dispose();
        }

        try { _mutex.ReleaseMutex(); }
        catch (ApplicationException) { /* already released or not owned */ }
        finally { _mutex.Dispose(); }
    }
}
