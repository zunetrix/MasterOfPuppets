using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

using InteropGenerator.Runtime;

namespace MasterOfPuppets;

internal sealed unsafe class AutoLoginManager : IDisposable {
    private enum LoginState {
        Idle,
        WaitingTitleMenu,
        WaitingWorldList,
        WaitingCharacterList,
        ConfirmingLogin,
    }

    private readonly Plugin _plugin;
    private readonly Stopwatch _stateTimer = new();
    private LoginState _state = LoginState.Idle;
    private int _stateDelayMs;
    private List<AutoLoginCandidate> _candidates = [];
    private AutoLoginTarget? _target;
    private string _pendingCharacterName = string.Empty;
    private int _pendingCharacterIndex = -1;
    private bool _hasPendingCharacterSelection;
    private bool _loggedWorldSelectorSnapshot;
    private bool _loggedCharacterListSnapshot;
    private IActiveNotification? _cancelNotification;

    private const int StateTimeoutMs = 30_000;
    private const int ConfirmingLoginTimeoutMs = 120_000;
    private const int CharacterSelectDelayMinMs = 2_000;
    private const int CharacterSelectDelayMaxMs = 4_000;

    private static AtkArrayDataHolder* AtkArrayDataHolder =>
        &Framework.Instance()->GetUIModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;

    private static NumberArrayData** NumberArrays => AtkArrayDataHolder->NumberArrays;
    private static StringArrayData** StringArrays => AtkArrayDataHolder->StringArrays;

    public bool IsRunning => _state != LoginState.Idle;

    public AutoLoginManager(Plugin plugin) {
        _plugin = plugin;
    }

    public void Start() {
        if (IsRunning || DalamudApi.ClientState.IsLoggedIn)
            return;

        _candidates = GetLoginCandidates();
        if (_candidates.Count == 0) {
            DalamudApi.PluginLog.Warning("[AutoLogin] No enabled login characters configured.");
            return;
        }

        _target = null;
        _loggedWorldSelectorSnapshot = false;
        _loggedCharacterListSnapshot = false;
        ClearPendingCharacterSelection();
        DalamudApi.PluginLog.Information(
            $"[AutoLogin] Starting with candidates: {string.Join(", ", _candidates.Select(c => $"{c.Name}({c.ContentId})"))}.");
        ShowCancelNotification();
        SetState(LoginState.WaitingTitleMenu);
        DalamudApi.Framework.Update += OnFrameworkUpdate;
    }

    public void Stop() => Stop(dismissNotification: true);

    private void Stop(bool dismissNotification) {
        if (!IsRunning)
            return;

        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        _state = LoginState.Idle;
        _stateDelayMs = 0;
        _stateTimer.Reset();
        _candidates = [];
        _target = null;
        _loggedWorldSelectorSnapshot = false;
        _loggedCharacterListSnapshot = false;
        ClearPendingCharacterSelection();
        if (dismissNotification)
            DismissCancelNotification();
    }

    private void OnFrameworkUpdate(IFramework framework) {
        try {
            if (ImGui.GetIO().KeyShift) {
                DalamudApi.PluginLog.Information("[AutoLogin] Cancelled by Shift.");
                Stop();
                return;
            }

            if (DalamudApi.ClientState.IsLoggedIn) {
                Stop();
                return;
            }

            if (_stateTimer.ElapsedMilliseconds > GetStateTimeoutMs()) {
                LogTimeout();
                Stop();
                return;
            }

            switch (_state) {
                case LoginState.WaitingTitleMenu:
                    HandleWaitingTitleMenu();
                    break;
                case LoginState.WaitingWorldList:
                    HandleWaitingWorldList();
                    break;
                case LoginState.WaitingCharacterList:
                    HandleWaitingCharacterList();
                    break;
                case LoginState.ConfirmingLogin:
                    HandleConfirmingLogin();
                    break;
            }
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[AutoLogin] Failed.");
            Stop();
        }
    }

    private void HandleWaitingTitleMenu() {
        if (!GameDialogManager.IsAddonVisible("_TitleMenu"))
            return;

        if (SendTitleAction("_TitleMenu", 3, 4)) {
            DalamudApi.PluginLog.Debug("[AutoLogin] Sent title menu character select action.");
            SetState(LoginState.WaitingWorldList);
        }
    }

    private void HandleWaitingWorldList() {
        if (TryResumeFromOpenCharacterList())
            return;

        if (!GameDialogManager.IsAddonVisible("_CharaSelectWorldServer"))
            return;

        var visibleWorlds = WorldNames;
        if (visibleWorlds.Count == 0)
            return;

        var lobbyEntries = ReadLobbyEntries();
        LogWorldSelectorSnapshot(visibleWorlds, lobbyEntries);
        if (lobbyEntries.Count == 0) {
            DalamudApi.PluginLog.Warning("[AutoLogin] Lobby character data was unavailable.");
            DalamudApi.PluginLog.Warning($"[AutoLogin] Lobby snapshot: {FormatLobbySnapshot(lobbyEntries)}");
            Stop();
            return;
        }

        _target = AutoLoginPlanner.ResolveTarget(_candidates, lobbyEntries);
        if (_target == null) {
            DalamudApi.PluginLog.Warning("[AutoLogin] Could not resolve an enabled auto-login character from lobby data.");
            DalamudApi.PluginLog.Warning($"[AutoLogin] Lobby snapshot: {FormatLobbySnapshot(lobbyEntries)}");
            Stop();
            return;
        }

        DalamudApi.PluginLog.Information(
            $"[AutoLogin] Resolved auto-login target: {_target.Value.CharacterName} on {_target.Value.WorldName}.");
        if (!SelectWorld(_target.Value.WorldName, out var worldIndex)) {
            DalamudApi.PluginLog.Warning(
                $"[AutoLogin] Resolved world '{_target.Value.WorldName}' was not visible in the world selector.");
            Stop();
            return;
        }

        DalamudApi.PluginLog.Information(
            $"[AutoLogin] Checking resolved character {_target.Value.CharacterName} on {_target.Value.WorldName} at index {worldIndex}.");
        SetState(LoginState.WaitingCharacterList);
    }

    private bool TryResumeFromOpenCharacterList() {
        if (!GameDialogManager.IsAddonVisible("_CharaSelectListMenu"))
            return false;

        var visibleCharacters = CharacterNames;
        if (visibleCharacters.Count == 0)
            return false;

        var lobbyEntries = ReadLobbyEntries();
        LogCharacterListSnapshot(visibleCharacters, lobbyEntries);

        if (!AutoLoginPlanner.TryResolveDirectCharacterListTarget(
                _candidates,
                lobbyEntries,
                visibleCharacters,
                out var target,
                out var characterIndex,
                out var reason)) {
            DalamudApi.PluginLog.Warning(
                $"[AutoLogin] Character list is open, but no whitelisted auto-login character is selectable: {reason}. Checked: {string.Join(", ", _candidates.Select(c => c.Name))}. Visible: {string.Join(", ", visibleCharacters)}. Lobby: {FormatLobbySnapshot(lobbyEntries)}.");
            if (GameDialogManager.IsAddonVisible("_CharaSelectWorldServer") && WorldNames.Count > 0) {
                DalamudApi.PluginLog.Debug("[AutoLogin] World selector is visible; falling back to world selector path.");
                return false;
            }

            Stop();
            return true;
        }

        _target = target;
        DalamudApi.PluginLog.Information(
            $"[AutoLogin] Character list already open; checking resolved character {_target.Value.CharacterName} at index {characterIndex}. Resolution: {reason}.");
        SetState(LoginState.WaitingCharacterList);
        return true;
    }

    private void HandleWaitingCharacterList() {
        if (!GameDialogManager.IsAddonVisible("_CharaSelectListMenu"))
            return;

        var visibleCharacters = CharacterNames;
        if (visibleCharacters.Count == 0)
            return;

        LogCharacterListSnapshot(visibleCharacters, ReadLobbyEntries());

        if (!_hasPendingCharacterSelection ||
            _pendingCharacterIndex < 0 ||
            _pendingCharacterIndex >= visibleCharacters.Count ||
            !visibleCharacters[_pendingCharacterIndex].Equals(_pendingCharacterName, StringComparison.InvariantCultureIgnoreCase)) {
            if (_target == null) {
                DalamudApi.PluginLog.Warning("[AutoLogin] Resolved auto-login target was cleared before character selection.");
                Stop();
                return;
            }

            if (!AutoLoginPlanner.TryFindVisibleCandidate(
                    [new AutoLoginCandidate(0, _target.Value.CharacterName)],
                    visibleCharacters,
                    out _pendingCharacterName,
                    out _pendingCharacterIndex)) {
                DalamudApi.PluginLog.Warning(
                    $"[AutoLogin] Resolved character '{_target.Value.CharacterName}' was not visible after selecting {_target.Value.WorldName}.");
                Stop();
                return;
            }

            _hasPendingCharacterSelection = true;
            _stateDelayMs = Random.Shared.Next(CharacterSelectDelayMinMs, CharacterSelectDelayMaxMs + 1);
            _stateTimer.Restart();
            DalamudApi.PluginLog.Debug(
                $"[AutoLogin] Found {_pendingCharacterName} at index {_pendingCharacterIndex}; waiting {_stateDelayMs}ms before character selection.");
            return;
        }

        if (_stateTimer.ElapsedMilliseconds < _stateDelayMs) {
            DalamudApi.PluginLog.Debug(
                $"[AutoLogin] Waiting before selecting character: {_stateTimer.ElapsedMilliseconds}/{_stateDelayMs}ms.");
            return;
        }

        var selected = SendTitleAction("_CharaSelectListMenu", 3, 29, 3, 0, 3, _pendingCharacterIndex);
        DalamudApi.PluginLog.Debug(
            $"[AutoLogin] Character callback success={selected}; checked={string.Join(", ", _candidates.Select(c => c.Name))}; visible={string.Join(", ", visibleCharacters)}; selected={_pendingCharacterName}; index={_pendingCharacterIndex}.");
        if (selected) {
            DalamudApi.PluginLog.Information(
                $"[AutoLogin] Selected character {_pendingCharacterName} at index {_pendingCharacterIndex} after {_stateTimer.ElapsedMilliseconds}ms.");
            SendTitleAction(GameDialogManager.AddonName.SelectYesno, 3, 0);
            SetState(LoginState.ConfirmingLogin);
        } else {
            DalamudApi.PluginLog.Warning(
                $"[AutoLogin] No checked character was visible. Checked: {string.Join(", ", _candidates.Select(c => c.Name))}. Visible: {string.Join(", ", visibleCharacters)}.");
            Stop();
        }
    }

    private void HandleConfirmingLogin() {
        if (GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectYesno))
            SendTitleAction(GameDialogManager.AddonName.SelectYesno, 3, 0);
    }

    private int GetStateTimeoutMs() => _state == LoginState.ConfirmingLogin
        ? ConfirmingLoginTimeoutMs
        : StateTimeoutMs;

    private void LogTimeout() {
        if (_state == LoginState.ConfirmingLogin) {
            DalamudApi.PluginLog.Information(
                $"[AutoLogin] Login confirmation still pending after {ConfirmingLoginTimeoutMs}ms; stopping auto-login watcher after character selection was submitted.");
            return;
        }

        DalamudApi.PluginLog.Warning(BuildStateDiagnostics());
        DalamudApi.PluginLog.Warning($"[AutoLogin] Timed out in state {_state}.");
    }

    private string BuildStateDiagnostics() {
        var worldNames = TryReadList(() => WorldNames);
        var characterNames = TryReadList(() => CharacterNames);
        var lobbyEntries = TryReadList(ReadLobbyEntries);

        return
            $"[AutoLogin] State diagnostics: state={_state}; " +
            $"{FormatAddonState("_TitleMenu")}; " +
            $"{FormatAddonState("_CharaSelectWorldServer")}; " +
            $"{FormatAddonState("_CharaSelectListMenu")}; " +
            $"{FormatAddonState(GameDialogManager.AddonName.SelectYesno)}; " +
            $"worlds={worldNames.Count} [{Preview(worldNames)}]; " +
            $"characters={characterNames.Count} [{Preview(characterNames)}]; " +
            $"lobbyEntries={lobbyEntries.Count} [{FormatLobbySnapshot(lobbyEntries)}]; " +
            $"candidates=[{string.Join(", ", _candidates.Select(c => c.Name))}]";
    }

    private void LogWorldSelectorSnapshot(IReadOnlyList<string> worldNames, IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries) {
        if (_loggedWorldSelectorSnapshot)
            return;

        _loggedWorldSelectorSnapshot = true;
        DalamudApi.PluginLog.Debug(
            $"[AutoLogin] World selector snapshot: worlds={worldNames.Count} [{Preview(worldNames)}]; lobbyEntries={lobbyEntries.Count} [{FormatLobbySnapshot(lobbyEntries)}].");
    }

    private void LogCharacterListSnapshot(IReadOnlyList<string> characterNames, IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries) {
        if (_loggedCharacterListSnapshot)
            return;

        _loggedCharacterListSnapshot = true;
        DalamudApi.PluginLog.Debug(
            $"[AutoLogin] Character list snapshot: characters={characterNames.Count} [{Preview(characterNames)}]; lobbyEntries={lobbyEntries.Count} [{FormatLobbySnapshot(lobbyEntries)}].");
    }

    private static string FormatAddonState(string addonName) {
        try {
            return $"{addonName}=open:{GameDialogManager.IsAddonOpen(addonName)},visible:{GameDialogManager.IsAddonVisible(addonName)}";
        } catch (Exception ex) {
            return $"{addonName}=error:{ex.GetType().Name}";
        }
    }

    private static List<T> TryReadList<T>(Func<List<T>> read) {
        try {
            return read();
        } catch {
            return [];
        }
    }

    private static string Preview(IReadOnlyList<string> values) =>
        string.Join(", ", values.Take(8));

    private static string FormatLobbySnapshot(IReadOnlyList<AutoLoginLobbyEntry> entries) {
        if (entries.Count == 0)
            return "<empty>";

        return string.Join("; ", entries.Select(FormatLobbyEntry));
    }

    private static string FormatLobbyEntry(AutoLoginLobbyEntry entry, int index) {
        var rawJsonLength = string.IsNullOrEmpty(entry.RawJson) ? 0 : entry.RawJson.Length;
        return
            $"idx={index} cid={entry.ContentId} " +
            $"name=\"{LogValue(entry.Name)}\" " +
            $"current=\"{LogValue(entry.CurrentWorldName)}\" currentId={entry.CurrentWorldId} " +
            $"home=\"{LogValue(entry.HomeWorldName)}\" homeId={entry.HomeWorldId} " +
            $"clientSelectWorld=\"{LogValue(entry.ClientSelectWorldName)}\" rawJsonLen={rawJsonLength}";
    }

    private static string LogValue(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "<blank>"
            : value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private List<AutoLoginCandidate> GetLoginCandidates() {
        return _plugin.Config.Characters
            .Where(c => c.AutoLoginEnabled)
            .Select(AutoLoginCandidate.FromCharacter)
            .Where(c => c != null)
            .Select(c => c!.Value)
            .ToList();
    }

    private void SetState(LoginState state) {
        _state = state;
        _stateDelayMs = 0;
        if (state == LoginState.WaitingCharacterList)
            ClearPendingCharacterSelection();
        _stateTimer.Restart();
    }

    private static bool SelectWorld(string worldName, out int index) {
        index = WorldNames.FindIndex(i => i.Equals(worldName, StringComparison.InvariantCultureIgnoreCase));
        if (index < 0)
            return false;

        if (!GameDialogManager.IsAddonVisible("_CharaSelectWorldServer"))
            return false;

        return SendTitleAction("_CharaSelectWorldServer", 3, 25, 3, 0, 3, index);
    }

    private void ClearPendingCharacterSelection() {
        _pendingCharacterName = string.Empty;
        _pendingCharacterIndex = -1;
        _hasPendingCharacterSelection = false;
    }

    private void ShowCancelNotification() {
        DismissCancelNotification();

        _cancelNotification = DalamudApi.NotificationManager.AddNotification(new Notification {
            Title = "Master Of Puppets",
            Content = "Auto-login running. Hold Shift or click Cancel to stop.",
            Type = NotificationType.Info,
            InitialDuration = TimeSpan.MaxValue,
            HardExpiry = DateTime.MaxValue,
            UserDismissable = true,
        });

        _cancelNotification.DrawActions += _ => {
            if (ImGui.Button("Cancel")) {
                DalamudApi.PluginLog.Information("[AutoLogin] Cancelled from notification.");
                Stop();
            }
        };

        _cancelNotification.Dismiss += _ => {
            if (IsRunning)
                Stop(dismissNotification: false);
            _cancelNotification = null;
        };
    }

    private void DismissCancelNotification() {
        var notification = _cancelNotification;
        _cancelNotification = null;
        notification?.DismissNow();
    }

    // TODO: replace by GameDialogManager.FireCallback?
    private static bool SendTitleAction(string addonName, params long[] args) {
        if (args.Length % 2 != 0)
            throw new ArgumentException("The parameter length must be an integer multiple of 2.", nameof(args));

        var addon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addonName, 1).Address;
        if (addon == null)
            return false;

        fixed (long* values = args) {
            addon->FireCallback((uint)args.Length / 2u, (AtkValue*)values, true);
        }

        return true;
    }

    private static List<string> WorldNames {
        get {
            var result = new List<string>();
            for (var i = 0; i < 128; i++) {
                var text = ReadStringArray(1, i);
                if (string.IsNullOrWhiteSpace(text))
                    break;
                result.Add(text);
            }
            return result;
        }
    }

    private static List<string> CharacterNames {
        get {
            var result = new List<string>();
            var count = CharacterCount;
            var limit = count > 0 ? Math.Min(count, 40) : 40;
            for (var i = 0; i < limit; i++) {
                var text = ReadStringArray(1, 60 + i);
                if (string.IsNullOrWhiteSpace(text))
                    break;
                result.Add(text);
            }
            return result;
        }
    }

    private static List<AutoLoginLobbyEntry> ReadLobbyEntries() {
        var lobby = AgentLobby.Instance();
        if (lobby == null)
            return [];

        var entries = lobby->LobbyData.CharaSelectEntries;
        var result = new List<AutoLoginLobbyEntry>((int)entries.Count);
        for (var i = 0; i < entries.Count; i++) {
            var entry = entries[i].Value;
            if (entry == null)
                continue;

            result.Add(new AutoLoginLobbyEntry(
                entry->ContentId,
                entry->NameString,
                entry->CurrentWorldNameString,
                entry->CurrentWorldId,
                entry->HomeWorldNameString,
                entry->HomeWorldId,
                entry->RawJsonString,
                entry->ClientSelectData.WorldNameString));
        }

        return result;
    }

    private static int CharacterCount {
        get {
            var numberArray = NumberArrays[2];
            return numberArray == null ? 0 : numberArray->IntArray[1];
        }
    }

    private static string ReadStringArray(int arrayIndex, int stringIndex) {
        var stringArray = StringArrays[arrayIndex];
        if (stringArray == null || stringIndex < 0 || stringIndex >= stringArray->Size)
            return string.Empty;

        return (*(CStringPointer*)((byte*)stringArray->StringArray + stringIndex * (nint)Unsafe.SizeOf<CStringPointer>())).ToString();
    }

    public void Dispose() {
        Stop();
    }

}
