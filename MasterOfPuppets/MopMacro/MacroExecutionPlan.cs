using System;
using System.Collections.Generic;

namespace MasterOfPuppets;

/// <summary>
/// Holds the unexpanded actions and mutable variables for one queued macro run.
/// Variables are resolved immediately before each action executes so an active
/// macro can receive updated values without being restarted.
/// </summary>
public sealed class MacroExecutionPlan {
    private readonly object _variablesLock = new();
    private readonly Dictionary<string, string> _variables;

    public string[] ActionTemplates { get; }

    public MacroExecutionPlan(string[] actionTemplates, Dictionary<string, string>? variables = null) {
        ActionTemplates = actionTemplates ?? Array.Empty<string>();
        _variables = variables == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(variables);
    }

    public string ResolveAction(string actionTemplate) {
        lock (_variablesLock)
            return Command.SubstituteVariables([actionTemplate], _variables)[0];
    }

    public string[] ResolveAllActions() {
        lock (_variablesLock)
            return Command.SubstituteVariables(ActionTemplates, _variables);
    }

    public void UpdateVariables(IReadOnlyDictionary<string, string> variables) {
        lock (_variablesLock) {
            foreach (var (name, value) in variables)
                _variables[name] = value;
        }
    }

    public bool TryGetVariable(string name, out string? value) {
        lock (_variablesLock)
            return _variables.TryGetValue(name, out value);
    }
}
