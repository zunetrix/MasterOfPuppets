using System.Collections.Generic;

public class MacroFolder {
    public string Name { get; set; }
    public List<MacroFolder> Children { get; set; } = new();
    public List<Macro> Macros { get; set; } = new();
}

