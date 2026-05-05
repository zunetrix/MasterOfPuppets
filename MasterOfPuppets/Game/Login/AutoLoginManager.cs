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
    private List<string> _worldQueue = [];
    private int _worldIndex;
    private AutoLoginTarget? _target;
    private string _pendingCharacterName = string.Empty;
    private int _pendingCharacterIndex = -1;
    private bool _hasPendingCharacterSelection;
    private bool _loggedWorldSelectorSnapshot;
    private bool _loggedCharacterListSnapshot;
    private bool _loggedDirectCharacterListMismatch;
    private bool _loginConfirmationSubmitted;
    private bool _loggedWaitingLoginConfirmation;
    private bool _loggedWaitingWorldListAfterTitleAction;
    private bool _waitingForWorldSelectionSettle;
    private bool _loggedWaitingForWorldSelectionSettle;
    private IActiveNotification? _cancelNotification;

    private const int StateTimeoutMs = 30_000;
    private const int ConfirmingLoginTimeoutMs = 120_000;
    private const int CharacterSelectDelayMinMs = 2_000;
    private const int CharacterSelectDelayMaxMs = 4_000;
    private const int WorldSelectSettleMs = 1_000;

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
        _worldQueue = [];
        _worldIndex = 0;
        _loggedWorldSelectorSnapshot = false;
        _loggedCharacterListSnapshot = false;
        _loggedDirectCharacterListMismatch = false;
        _loginConfirmationSubmitted = false;
        _loggedWaitingLoginConfirmation = false;
        _loggedWaitingWorldListAfterTitleAction = false;
        _waitingForWorldSelectionSettle = false;
        _loggedWaitingForWorldSelectionSettle = false;
        ClearPendingCharacterSelection();
        DalamudApi.PluginLog.Information(
            $"[AutoLogin] Starting with candidates: {string.Join(", ", _candidates.Select(FormatCandidate))}.");
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
        _worldQueue = [];
        _worldIndex = 0;
        _target = null;
        _loggedWorldSelectorSnapshot = false;
        _loggedCharacterListSnapshot = false;
        _loggedDirectCharacterListMismatch = false;
        _loginConfirmationSubmitted = false;
        _loggedWaitingLoginConfirmation = false;
        _loggedWaitingWorldListAfterTitleAction = false;
        _waitingForWorldSelectionSettle = false;
        _loggedWaitingForWorldSelectionSettle = false;
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

        if (!GameDialogManager.IsAddonOpen("_CharaSelectWorldServer")) {
            if (GameDialogManager.IsAddonVisible("_TitleMenu") && !_loggedWaitingWorldListAfterTitleAction) {
                _loggedWaitingWorldListAfterTitleAction = true;
                DalamudApi.PluginLog.Debug("[AutoLogin] Waiting for character select world list after title menu action; not resubmitting while title menu remains visible.");
            }

            return;
        }

        var visibleWorlds = WorldNames;
        if (visibleWorlds.Count == 0)
            return;

        var lobbyEntries = ReadLobbyEntries();
        LogWorldSelectorSnapshot(visibleWorlds, lobbyEntries);
        if (_worldQueue.Count == 0) {
            _worldQueue = AutoLoginPlanner.BuildWorldQueue(_candidates, lobbyEntries, visibleWorlds);
            _worldIndex = 0;
            DalamudApi.PluginLog.Information(
                $"[AutoLogin] World scan queue (lobby current worlds first, then visible fallback): {(_worldQueue.Count == 0 ? "<empty>" : string.Join(", ", _worldQueue))}.");
        }

        if (_worldQueue.Count == 0) {
            DalamudApi.PluginLog.Warning("[AutoLogin] Could not build an auto-login world scan queue.");
            DalamudApi.PluginLog.Warning($"[AutoLogin] World selector snapshot: worlds={visibleWorlds.Count} [{Preview(visibleWorlds)}]; lobby={FormatLobbySnapshot(lobbyEntries)}; candidates=[{string.Join(", ", _candidates.Select(FormatCandidate))}]");
            Stop();
            return;
        }

        SelectNextWorldCandidate();
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
            if (!_loggedDirectCharacterListMismatch) {
                _loggedDirectCharacterListMismatch = true;
                DalamudApi.PluginLog.Warning(
                    $"[AutoLogin] Character list is open, but no whitelisted auto-login character is selectable yet: {reason}. Continuing to wait for world selector. Checked: {string.Join(", ", _candidates.Select(FormatCandidate))}. Visible: {string.Join(", ", visibleCharacters)}. Lobby: {FormatLobbySnapshot(lobbyEntries)}.");
            }

            return false;
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

        if (_waitingForWorldSelectionSettle && _stateTimer.ElapsedMilliseconds < WorldSelectSettleMs) {
            if (!_loggedWaitingForWorldSelectionSettle) {
                _loggedWaitingForWorldSelectionSettle = true;
                DalamudApi.PluginLog.Debug(
                    $"[AutoLogin] Waiting {WorldSelectSettleMs}ms for character list to settle after selecting {CurrentWorldLabel}.");
            }

            return;
        }

        _waitingForWorldSelectionSettle = false;

        var visibleCharacters = CharacterNames;
        if (visibleCharacters.Count == 0)
            return;

        LogCharacterListSnapshot(visibleCharacters, ReadLobbyEntries());

        if (!_hasPendingCharacterSelection ||
            _pendingCharacterIndex < 0 ||
            _pendingCharacterIndex >= visibleCharacters.Count ||
            !visibleCharacters[_pendingCharacterIndex].Equals(_pendingCharacterName, StringComparison.InvariantCultureIgnoreCase)) {
            if (!AutoLoginPlanner.TryFindVisibleCandidate(
                    _candidates,
                    visibleCharacters,
                    out _pendingCharacterName,
                    out _pendingCharacterIndex)) {
                DalamudApi.PluginLog.Information(
                    $"[AutoLogin] No whitelisted character visible on {CurrentWorldLabel}. Checked: {string.Join(", ", _candidates.Select(c => c.Name))}. Visible: {string.Join(", ", visibleCharacters)}.");
                SelectNextWorldCandidate();
                return;
            }

            _target = new AutoLoginTarget(_pendingCharacterName, _target?.WorldName ?? string.Empty);
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
            SetState(LoginState.ConfirmingLogin);
        } else {
            DalamudApi.PluginLog.Warning(
                $"[AutoLogin] No checked character was visible. Checked: {string.Join(", ", _candidates.Select(c => c.Name))}. Visible: {string.Join(", ", visibleCharacters)}.");
            Stop();
        }
    }

    private void HandleConfirmingLogin() {
        if (_loginConfirmationSubmitted)
            return;

        if (!GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectYesno)) {
            if (!_loggedWaitingLoginConfirmation) {
                _loggedWaitingLoginConfirmation = true;
                DalamudApi.PluginLog.Debug("[AutoLogin] Waiting for login confirmation SelectYesno.");
            }

            return;
        }

        _loginConfirmationSubmitted = true;
        _stateTimer.Restart();
        if (SendTitleAction(GameDialogManager.AddonName.SelectYesno, 3, 0)) {
            DalamudApi.PluginLog.Debug("[AutoLogin] Submitted login confirmation SelectYesno once; waiting for login completion.");
        } else {
            DalamudApi.PluginLog.Warning("[AutoLogin] Login confirmation SelectYesno was visible, but callback submission failed; waiting for login completion.");
        }
    }

    private void SelectNextWorldCandidate() {
        ClearPendingCharacterSelection();
        while (_worldIndex < _worldQueue.Count) {
            var world = _worldQueue[_worldIndex++];
            if (!SelectWorld(world, out var selectedWorldIndex)) {
                DalamudApi.PluginLog.Debug($"[AutoLogin] World '{world}' was not selectable from the current world list.");
                continue;
            }

            _target = new AutoLoginTarget(string.Empty, world);
            DalamudApi.PluginLog.Information(
                $"[AutoLogin] Checking whitelisted characters on {world} at world index {selectedWorldIndex} ({_worldIndex}/{_worldQueue.Count}).");
            _waitingForWorldSelectionSettle = true;
            _loggedWaitingForWorldSelectionSettle = false;
            SetState(LoginState.WaitingCharacterList);
            return;
        }

        DalamudApi.PluginLog.Warning(
            $"[AutoLogin] None of the configured characters were found on the current data center. Checked worlds: {(_worldQueue.Count == 0 ? "<empty>" : string.Join(", ", _worldQueue))}. Candidates: {string.Join(", ", _candidates.Select(FormatCandidate))}.");
        Stop();
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
            $"worldQueueIndex={_worldIndex}; worldQueue=[{string.Join(", ", _worldQueue)}]; " +
            $"loginConfirmationSubmitted={_loginConfirmationSubmitted}; " +
            $"candidates=[{string.Join(", ", _candidates.Select(FormatCandidate))}]";
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

    private static string FormatCandidate(AutoLoginCandidate candidate) =>
        string.IsNullOrWhiteSpace(candidate.HomeWorldName)
            ? $"{candidate.Name}({candidate.ContentId})"
            : $"{candidate.Name}@{candidate.HomeWorldName}({candidate.ContentId})";

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
        if (state == LoginState.ConfirmingLogin) {
            _loginConfirmationSubmitted = false;
            _loggedWaitingLoginConfirmation = false;
        }
        _stateTimer.Restart();
    }

    private static bool SelectWorld(string worldName, out int index) {
        index = WorldNames.FindIndex(i => i.Equals(worldName, StringComparison.InvariantCultureIgnoreCase));
        if (index < 0)
            return false;

        if (!GameDialogManager.IsAddonOpen("_CharaSelectWorldServer"))
            return false;

        return SendTitleAction("_CharaSelectWorldServer", 3, 25, 3, 0, 3, index);
    }

    private string CurrentWorldLabel => _target == null || string.IsNullOrWhiteSpace(_target.Value.WorldName)
        ? "the selected world"
        : _target.Value.WorldName;

    private void ClearPendingCharacterSelection() {
        _pendingCharacterName = string.Empty;
        _pendingCharacterIndex = -1;
        _hasPendingCharacterSelection = false;
        _stateDelayMs = 0;
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
