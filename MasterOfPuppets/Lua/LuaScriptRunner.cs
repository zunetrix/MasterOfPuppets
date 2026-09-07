using System;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting;

/// <summary>
/// Backward-compatible facade for one sandboxed Lua run. Runtime setup and API
/// registration live in the runtime host and discoverable capability providers.
/// </summary>
public sealed class LuaScriptRunner : IDisposable {
    private readonly LuaRuntimeHost _host;

    public LuaScriptRunner(LuaScriptContext context) {
        _host = new LuaRuntimeHost(context ?? throw new ArgumentNullException(nameof(context)));
    }

    public Task RunAsync(string script, CancellationToken cancellationToken) =>
        _host.RunAsync(script, cancellationToken);

    public void Dispose() => _host.Dispose();
}
