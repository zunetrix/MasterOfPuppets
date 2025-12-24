using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public interface IMacroActionHandler {
    string Command { get; }
    Task ExecuteAsync(string macroId, string args, CancellationToken token);
}


public interface IMacroActionHandler<TArgs> {
    string Command { get; }
    Task<MacroActionResult> ExecuteAsync(string macroId, TArgs args, CancellationToken token);
}

public interface IMacroArgsBinder {
    bool TryBind<T>(string rawArgs, out T result, out string error);
}
