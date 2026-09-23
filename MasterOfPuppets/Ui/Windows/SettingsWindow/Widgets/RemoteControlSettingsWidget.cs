using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.RemoteControl;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class RemoteControlSettingsWidget : Widget {
    public override string Title => "Remote Control";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Wifi;

    public RemoteControlSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        var Plugin = Context.Plugin;
        var cfg = Plugin.Config;

        // Server
        if (ImGui.CollapsingHeader("Remote Control Server", ImGuiTreeNodeFlags.DefaultOpen)) {
            using (ImGuiGroupPanel.BeginGroupPanel("Server")) {
                var enabled = cfg.RemoteControlEnabled;
                if (ImGui.Checkbox("Enable Remote Control##RCEnabled", ref enabled)) {
                    cfg.RemoteControlEnabled = enabled;
                    cfg.Save();
                    Plugin.RefreshRemoteControlServer();
                }
                ImGuiUtil.HelpMarker(
                    "Starts a local HTTP server that exposes a REST API and WebSocket endpoint.\n" +
                    "Allows controlling the plugin from a web browser");

                ImGui.Spacing();

                // Port
                ImGui.Text("Port");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                var port = cfg.RemoteControlPort;
                if (ImGui.InputInt("##RCPort", ref port))
                    cfg.RemoteControlPort = port; // live clamp on deactivate

                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    cfg.RemoteControlPort = Math.Clamp(cfg.RemoteControlPort, 1024, 65535);
                    cfg.Save();
                    if (cfg.RemoteControlEnabled)
                        Plugin.RefreshRemoteControlServer();
                }

                ImGui.Spacing();

                // Token
                ImGui.Text("API Token");
                ImGuiUtil.HelpMarker(
                    "Clients must include this token as 'Authorization: Bearer <token>' header\n" +
                    "Leave empty to allow unauthenticated access (localhost only)");

                var displayToken = cfg.RemoteControlToken ?? string.Empty;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 160);
                using (ImRaii.Disabled())
                    ImGui.InputText("##RCToken", ref displayToken, 256,
                        ImGuiInputTextFlags.Password | ImGuiInputTextFlags.ReadOnly);

                ImGui.SameLine();
                using (ImRaii.Disabled(string.IsNullOrWhiteSpace(cfg.RemoteControlToken))) {
                    if (ImGui.Button("Copy##RCTokenCopy"))
                        ImGui.SetClipboardText(cfg.RemoteControlToken);
                }

                ImGui.SameLine();
                if (ImGui.Button("Regenerate##RCTokenRegen")) {
                    Plugin.RegenerateRemoteControlToken();
                    if (cfg.RemoteControlEnabled)
                        Plugin.RefreshRemoteControlServer();
                }

                ImGui.Spacing();

                // Services table
                if (cfg.RemoteControlEnabled) {
                    DrawServicesTable(cfg);
                    DrawConnectedClientsTable();
                }
            }

            ImGui.Spacing();

            // Tunnel
            using (ImGuiGroupPanel.BeginGroupPanel("HTTP Tunnel")) {
                using (ImRaii.Disabled(!cfg.RemoteControlEnabled)) {
                    var tunnelEnabled = cfg.TunnelEnabled;
                    if (ImGui.Checkbox("Enable Tunnel##TunnelEnabled", ref tunnelEnabled)) {
                        cfg.TunnelEnabled = tunnelEnabled;
                        cfg.Save();
                        Plugin.RefreshTunnelService();
                    }
                }
                ImGuiUtil.HelpMarker(
                    "Runs an external tunnel process (Cloudflare or NGROK) to expose the\n" +
                    "local server via a public HTTPS URL. Requires the tunnel binary to be installed. (if you install the tunnel while game is open you will need restart the game client)");

                if (!cfg.RemoteControlEnabled) {
                    ImGui.SameLine();
                    ImGui.TextDisabled("(enable Remote Control first)");
                }

                ImGui.Spacing();
                ImGui.Text("Tunnel Command");
                ImGuiUtil.HelpMarker("Use {port} as a placeholder for the configured port.");

                var tunnelCmd = cfg.TunnelCommand ?? string.Empty;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText("##TunnelCmd", ref tunnelCmd, 512))
                    cfg.TunnelCommand = tunnelCmd;

                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    cfg.Save();
                    if (cfg.TunnelEnabled && cfg.RemoteControlEnabled)
                        Plugin.RefreshTunnelService();
                }

                ImGui.Spacing();

                // Preset buttons
                if (ImGui.Button("Cloudflare##PresetCF")) {
                    cfg.TunnelCommand = "cloudflared tunnel --url http://localhost:{port}";
                    cfg.Save();
                    if (cfg.TunnelEnabled && cfg.RemoteControlEnabled)
                        Plugin.RefreshTunnelService();
                }
                ImGui.SameLine();
                if (ImGui.Button("ngrok##PresetNgrok")) {
                    cfg.TunnelCommand = "ngrok http {port}";
                    cfg.Save();
                    if (cfg.TunnelEnabled && cfg.RemoteControlEnabled)
                        Plugin.RefreshTunnelService();
                }
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();

        // Client
        if (ImGui.CollapsingHeader("Client")) {
            using (ImGuiGroupPanel.BeginGroupPanel("Client Mode")) {
                ImGui.TextWrapped(
                    "Connect this plugin instance to a remote server" +
                    "Received pluginCommand messages will be dispatched locally");

                ImGui.Spacing();

                var clientEnabled = cfg.RemoteControlClientEnabled;
                if (ImGui.Checkbox("Connect to server##ClientEnabled", ref clientEnabled)) {
                    cfg.RemoteControlClientEnabled = clientEnabled;
                    cfg.Save();
                    Plugin.RefreshRemoteControlClient();
                }
                ImGuiUtil.HelpMarker(
                    "When enabled, this instance acts as a client:\n" +
                    "it connects via WebSocket to the server and executes\n" +
                    "any 'pluginCommand' messages received from it.");

                ImGui.Spacing();

                ImGui.Text("Remote Server URL");
                ImGuiUtil.HelpMarker(
                    "The HTTP or HTTPS URL of the master's Remote Control Server.\n" +
                    "e.g. http://192.168.1.10:4782 or https://xxxx.trycloudflare.com");

                var clientUrl = cfg.RemoteControlClientUrl ?? string.Empty;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText("##ClientUrl", ref clientUrl, 512))
                    cfg.RemoteControlClientUrl = clientUrl;
                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    Plugin.IpcProvider.SyncConfiguration();
                    if (cfg.RemoteControlClientEnabled)
                        Plugin.RefreshRemoteControlClient();
                }

                ImGui.Spacing();
                ImGui.Text("Remote Server Token");
                var clientToken = cfg.RemoteControlClientToken ?? string.Empty;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100);
                bool tokenChanged = ImGui.InputText("##ClientToken", ref clientToken, 256,
                    ImGuiInputTextFlags.Password);
                if (tokenChanged) cfg.RemoteControlClientToken = clientToken;
                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    Plugin.IpcProvider.SyncConfiguration();

                    if (cfg.RemoteControlClientEnabled)
                        Plugin.RefreshRemoteControlClient();
                }

                ImGui.Spacing();

                // Status
                var clientStatus = Plugin.RemoteControlClientStatus;
                var isConnected = Plugin.RemoteControlClient?.IsConnected == true;
                var statusColor = isConnected
                    ? new Vector4(.2f, .8f, .4f, 1f)
                    : new Vector4(.8f, .4f, .2f, 1f);
                ImGui.Text("Status:");
                ImGui.SameLine();
                using (ImRaii.PushColor(ImGuiCol.Text, statusColor))
                    ImGui.TextUnformatted(clientStatus);

                ImGui.SameLine();
                if (ImGui.Button("Reconnect##ClientReconnect"))
                    Plugin.RefreshRemoteControlClient();
            }
        }
    }

    // Services status table
    private void DrawServicesTable(Configuration cfg) {
        var Plugin = Context.Plugin;

        var serverUrl = $"http://localhost:{cfg.RemoteControlPort}/";
        var serverWsUrl = $"ws://localhost:{cfg.RemoteControlPort}/ws";
        var accessUrl = string.IsNullOrWhiteSpace(cfg.RemoteControlToken)
            ? serverUrl
            : serverUrl + "#token=" + Uri.EscapeDataString(cfg.RemoteControlToken);

        var tunnelUrl = Plugin.TunnelService?.PublicUrl;
        var tunnelWsUrl = tunnelUrl != null
            ? tunnelUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/ws"
            : null;
        var tunnelAccessUrl = string.IsNullOrWhiteSpace(cfg.RemoteControlToken) || tunnelUrl == null
            ? tunnelUrl
            : tunnelUrl + "/#server=" + Uri.EscapeDataString(tunnelUrl) +
              "&token=" + Uri.EscapeDataString(cfg.RemoteControlToken);

        var serverOk = Plugin.RemoteControlServer?.IsListening == true;
        var tunnelRunning = Plugin.TunnelService?.Status == TunnelStatus.Running;
        var tunnelStarting = Plugin.TunnelService?.Status == TunnelStatus.Starting;

        using var table = ImRaii.Table("##RCServicesTable", 5,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("Service", ImGuiTableColumnFlags.WidthFixed, 62);
        ImGui.TableSetupColumn("HTTP URL", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("WebSocket URL", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableHeadersRow();

        // Server row
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); DrawStatusIcon(serverOk, false);
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Local");
        ImGui.TableNextColumn(); ImGui.TextUnformatted(serverUrl);
        ImGui.TableNextColumn();
        ImGui.TextDisabled(serverWsUrl);
        if (!string.IsNullOrWhiteSpace(cfg.RemoteControlToken)) {
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, "##SrvWsCopy", "Copy WS URL"))
                ImGui.SetClipboardText(serverWsUrl + "?token=" + cfg.RemoteControlToken);
        }
        ImGui.TableNextColumn();
        DrawUrlActions("Srv", serverUrl, accessUrl, cfg.RemoteControlToken);

        // Tunnel row
        if (cfg.TunnelEnabled) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); DrawStatusIcon(tunnelRunning, tunnelStarting);
            ImGui.TableNextColumn(); ImGui.TextUnformatted("Tunnel");
            ImGui.TableNextColumn();
            if (tunnelUrl != null) ImGui.TextUnformatted(tunnelUrl);
            else ImGui.TextDisabled("-");
            ImGui.TableNextColumn();
            if (tunnelWsUrl != null) {
                ImGui.TextDisabled(tunnelWsUrl);
                if (!string.IsNullOrWhiteSpace(cfg.RemoteControlToken)) {
                    ImGui.SameLine();
                    if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, "##TunWsCopy", "Copy WS URL"))
                        ImGui.SetClipboardText(tunnelWsUrl + "?token=" + cfg.RemoteControlToken);
                }
            } else {
                ImGui.TextDisabled("-");
            }
            ImGui.TableNextColumn();
            using (ImRaii.Disabled(tunnelUrl == null))
                DrawUrlActions("Tun", tunnelUrl ?? string.Empty, tunnelAccessUrl ?? string.Empty, cfg.RemoteControlToken);
        }
    }

    private void DrawConnectedClientsTable() {
        var clients = Context.Plugin.RemoteControlServer?.WsManager.ActiveClients;
        if (clients == null) return;

        ImGui.Spacing();
        ImGui.TextUnformatted("Connected Clients");

        using var table = ImRaii.Table("##RCSClientsTable", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(0, 150));
        if (!table) return;

        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 30);
        // ImGui.TableSetupColumn("IP Address", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Connected At", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Client Name", ImGuiTableColumnFlags.WidthStretch);
        // ImGui.TableSetupColumn("UA", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        var any = false;
        foreach (var client in clients) {
            any = true;
            ImGui.TableNextRow();

            // ID
            ImGui.TableNextColumn();
            ImGuiUtil.IconButton(FontAwesomeIcon.IdBadge, $"##ID_{client.Id}");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(client.Id.ToString());

            // IP
            // ImGui.TableNextColumn();
            // ImGui.TextUnformatted(client.IpAddress);

            // Connected At
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(client.ConnectedAt.ToString("HH:mm:ss"));

            // Name
            ImGui.TableNextColumn();
            string ua = client.UserAgent ?? "Unknown";
            string name = ua;
            if (ua.StartsWith("MasterOfPuppets / ")) {
                name = ua.Substring("MasterOfPuppets / ".Length);
            }
            ImGui.TextWrapped(name);

            // UA
            // ImGui.TableNextColumn();
            // ImGuiUtil.IconButton(FontAwesomeIcon.InfoCircle, $"##UA_{client.Id}");
            // if (ImGui.IsItemHovered()) ImGui.SetTooltip(ua);
        }

        if (!any) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No clients connected.");
        }
    }

    private static void DrawUrlActions(string id, string url, string accessUrl, string token) {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##{id}Copy", "Copy URL")) ImGui.SetClipboardText(url);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExternalLinkAlt, $"##{id}Open", "Open in browser")) WindowsApi.OpenUrl(accessUrl);

        if (!string.IsNullOrWhiteSpace(token)) {
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Link, $"##{id}Token", "Copy Access URL")) ImGui.SetClipboardText(accessUrl);
        }
    }

    private static void DrawStatusIcon(bool green, bool yellow) {
        var col = green ? new Vector4(.2f, .8f, .4f, 1f)
                : yellow ? new Vector4(.9f, .7f, .2f, 1f)
                : new Vector4(.8f, .2f, .2f, 1f);
        using (ImRaii.PushColor(ImGuiCol.Text, col))
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.Text(green ? FontAwesomeIcon.Check.ToIconString()
                     : yellow ? FontAwesomeIcon.Sync.ToIconString()
                     : FontAwesomeIcon.Times.ToIconString());
    }
}
