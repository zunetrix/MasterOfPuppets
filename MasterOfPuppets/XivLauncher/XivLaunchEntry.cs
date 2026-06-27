namespace MasterOfPuppets;

internal sealed class XivLaunchEntry {
    public string Name { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool AutoLogin { get; set; } = true;
    public bool UseSteamServiceAccount { get; set; } = false;
    public bool UseOtp { get; set; } = false;
    public string RoamingPath { get; set; } = string.Empty;
    public string XivLauncherPath { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
