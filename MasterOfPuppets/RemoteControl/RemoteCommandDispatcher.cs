using System;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.RemoteControl;

/// <summary>
/// Centralises the parsing and execution of remote commands received via HTTP/WebSocket.
/// All command strings follow the same convention as ChatWatcher / PluginCommandManager:
///   "/mop &lt;subcommand&gt; [args]"  - routed to PluginCommandManager.ExecuteSubcommand
///   "mopbr &lt;args&gt;"             - routed to IpcProvider.EnqueueMacroActions (includeSelf: true)
///   "mopbrn &lt;args&gt;"            - routed to IpcProvider.EnqueueMacroActions (includeSelf: false)
///   "mopbrc &lt;character&gt; &lt;args&gt;" - routed to IpcProvider.EnqueueCharacterMacroActions
///   "mopbrg &lt;group&gt; &lt;args&gt;"    - routed to IpcProvider.EnqueueGroupMacroActions
/// </summary>
internal sealed class RemoteCommandDispatcher {
    private readonly Plugin _plugin;
    private const string MopPrefix = "/mop ";

    public RemoteCommandDispatcher(Plugin plugin) {
        _plugin = plugin;
    }

    /// <summary>
    /// Dispatches a raw command string. The command may optionally start with "/" (slash prefix).
    /// Returns a <see cref="CommandResult"/> indicating success or error.
    /// </summary>
    public CommandResult Dispatch(string raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return CommandResult.Fail("Empty command.");

        raw = raw.Trim();

        try {
            // /mop <subcommand args>
            if (raw.StartsWith(MopPrefix, StringComparison.OrdinalIgnoreCase)) {
                var arguments = raw[MopPrefix.Length..].Trim();
                _plugin.PluginCommandManager.ExecuteSubcommand(arguments);
                return CommandResult.Success();
            }

            // mopbr / mopbrn / mopbrc / mopbrg  (without slash, as in ChatWatcher)
            var parts = ArgumentParser.ParseCommandArgs(raw);
            if (parts.Count == 0)
                return CommandResult.Fail("Could not parse command.");

            var verb = parts[0];

            if (verb.Equals("mopbr", StringComparison.OrdinalIgnoreCase)) {
                var args = string.Join(" ", parts.GetRange(1, parts.Count - 1));
                if (string.IsNullOrWhiteSpace(args))
                    return CommandResult.Fail("mopbr requires at least one argument.");
                _plugin.IpcProvider.EnqueueMacroActions(args, includeSelf: true);
                return CommandResult.Success();
            }

            if (verb.Equals("mopbrn", StringComparison.OrdinalIgnoreCase)) {
                var args = string.Join(" ", parts.GetRange(1, parts.Count - 1));
                if (string.IsNullOrWhiteSpace(args))
                    return CommandResult.Fail("mopbrn requires at least one argument.");
                _plugin.IpcProvider.EnqueueMacroActions(args, includeSelf: false);
                return CommandResult.Success();
            }

            if (verb.Equals("mopbrc", StringComparison.OrdinalIgnoreCase)) {
                if (parts.Count < 3)
                    return CommandResult.Fail("mopbrc requires <character> <command>.");
                _plugin.IpcProvider.EnqueueCharacterMacroActions(parts[2], parts[1]);
                return CommandResult.Success();
            }

            if (verb.Equals("mopbrg", StringComparison.OrdinalIgnoreCase)) {
                if (parts.Count < 3)
                    return CommandResult.Fail("mopbrg requires <group> <command>.");
                _plugin.IpcProvider.EnqueueGroupMacroActions(parts[2], parts[1]);
                return CommandResult.Success();
            }

            return CommandResult.Fail($"Unknown verb: '{verb}'. Use /mop <subcommand> or mopbr/mopbrn/mopbrc/mopbrg.");
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, $"[RemoteControl] Error executing command: {raw}");
            return CommandResult.Fail(ex.Message);
        }
    }
}
