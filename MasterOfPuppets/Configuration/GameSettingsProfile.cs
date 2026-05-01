using System.Collections.Generic;

namespace MasterOfPuppets;

public class GameSettingsSnapshot {
    public Dictionary<string, uint> UIntSettings { get; set; } = new();
    public Dictionary<string, float> FloatSettings { get; set; } = new();
    public Dictionary<string, string> StringSettings { get; set; } = new();
}

public class GameSettingsProfile {
    public string Name { get; set; } = string.Empty;
    public GameSettingsSnapshot System { get; set; } = new();
    public GameSettingsSnapshot Ui { get; set; } = new();
    public GameSettingsSnapshot UiControl { get; set; } = new();

    public GameSettingsProfile Clone() {
        return new GameSettingsProfile {
            Name = Name,
            System = CloneSnapshot(System),
            Ui = CloneSnapshot(Ui),
            UiControl = CloneSnapshot(UiControl)
        };
    }

    private GameSettingsSnapshot CloneSnapshot(GameSettingsSnapshot snapshot) {
        return new GameSettingsSnapshot {
            UIntSettings = new Dictionary<string, uint>(snapshot.UIntSettings),
            FloatSettings = new Dictionary<string, float>(snapshot.FloatSettings),
            StringSettings = new Dictionary<string, string>(snapshot.StringSettings)
        };
    }
}
