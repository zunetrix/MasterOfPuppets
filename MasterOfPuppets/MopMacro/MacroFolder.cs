using System;
using System.Linq;
using System.Collections.Generic;

public class MacroFolder {
    public string Name { get; set; }
    public List<MacroFolder> Children { get; set; } = new();
    public List<Macro> Macros { get; set; } = new();

    public static MacroFolder BuildTree(List<Macro> macros) {
        var root = new MacroFolder { Name = "/" };

        foreach (var macro in macros) {
            var current = root;
            var parts = macro.Path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (var p in parts) {
                var next = current.Children.FirstOrDefault(x => x.Name == p);
                if (next == null) {
                    next = new MacroFolder { Name = p };
                    current.Children.Add(next);
                }
                current = next;
            }

            current.Macros.Add(macro);
        }

        return root;
    }
}

