using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public interface IMacroActionHandler
{
    string Command { get; }
    Task ExecuteAsync(string macroId, string args, CancellationToken token);
}
