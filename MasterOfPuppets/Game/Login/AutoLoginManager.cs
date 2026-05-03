using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    private int _worldIndex;
    private int _stateDelayMs;
    private List<LoginCandidate> _candidates = [];
    private List<string> _worldNames = [];
    private IActiveNotification? _cancelNotification;

    private const int StateTimeoutMs = 30_000;
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

        _worldIndex = 0;
        _worldNames = [];
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
        _worldNames = [];
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

            if (_stateTimer.ElapsedMilliseconds > StateTimeoutMs) {
                DalamudApi.PluginLog.Warning($"[AutoLogin] Timed out in state {_state}.");
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

        if (SendTitleAction("_TitleMenu", 3, 4))
            SetState(LoginState.WaitingWorldList);
    }

    private void HandleWaitingWorldList() {
        if (!GameDialogManager.IsAddonOpen("_CharaSelectWorldServer"))
            return;

        if (_worldNames.Count == 0) {
            _worldNames = WorldNames;
            if (_worldNames.Count == 0)
                return;
        }

        SelectNextWorldCandidate();
    }

    private void HandleWaitingCharacterList() {
        if (!GameDialogManager.IsAddonOpen("_CharaSelectListMenu"))
            return;

        var visibleCharacters = CharacterNames;
        if (visibleCharacters.Count == 0)
            return;

        if (_stateTimer.ElapsedMilliseconds < _stateDelayMs) {
            DalamudApi.PluginLog.Debug(
                $"[AutoLogin] Waiting before selecting character: {_stateTimer.ElapsedMilliseconds}/{_stateDelayMs}ms.");
            return;
        }

        var selected = TrySelectVisibleCheckedCharacter(visibleCharacters, out var characterName, out var characterIndex);
        if (selected) {
            DalamudApi.PluginLog.Information(
                $"[AutoLogin] Selected character {characterName} at index {characterIndex} after {_stateTimer.ElapsedMilliseconds}ms.");
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

    private void SelectNextWorldCandidate() {
        while (_worldIndex < _worldNames.Count) {
            var world = _worldNames[_worldIndex++];
            if (!SelectWorld(world, out var worldIndex)) {
                DalamudApi.PluginLog.Debug($"[AutoLogin] World '{world}' not available in current list.");
                continue;
            }

            DalamudApi.PluginLog.Information($"[AutoLogin] Checking checked characters on {world} at index {worldIndex}.");
            SetState(LoginState.WaitingCharacterList);
            return;
        }

        DalamudApi.PluginLog.Warning("[AutoLogin] None of the configured characters were found on the current data center.");
        Stop();
    }

    private List<LoginCandidate> GetLoginCandidates() {
        return _plugin.Config.Characters
            .Where(c => c.AutoLoginEnabled)
            .Select(c => LoginCandidate.FromCharacterName(c.Name))
            .Where(c => c != null)
            .Select(c => c!.Value)
            .ToList();
    }

    private void SetState(LoginState state) {
        _state = state;
        _stateDelayMs = state == LoginState.WaitingCharacterList
            ? Random.Shared.Next(CharacterSelectDelayMinMs, CharacterSelectDelayMaxMs + 1)
            : 0;
        _stateTimer.Restart();
        if (_stateDelayMs > 0)
            DalamudApi.PluginLog.Debug($"[AutoLogin] Waiting {_stateDelayMs}ms before character selection.");
    }

    private static bool SelectWorld(string worldName, out int index) {
        index = WorldNames.FindIndex(i => i.Equals(worldName, StringComparison.InvariantCultureIgnoreCase));
        if (index < 0)
            return false;

        return SendTitleAction("_CharaSelectWorldServer", 3, 25, 3, 0, 3, index);
    }

    private bool TrySelectVisibleCheckedCharacter(List<string> visibleCharacters, out string characterName, out int index) {
        var checkedNames = _candidates.Select(c => c.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        index = visibleCharacters.FindIndex(name => checkedNames.Contains(name));
        if (index < 0) {
            characterName = string.Empty;
            return false;
        }

        characterName = visibleCharacters[index];
        var success = SendTitleAction("_CharaSelectListMenu", 3, 29, 3, 0, 3, index);
        DalamudApi.PluginLog.Debug(
            $"[AutoLogin] Character callback success={success}; checked={string.Join(", ", checkedNames)}; visible={string.Join(", ", visibleCharacters)}; selected={characterName}; index={index}.");
        return success;
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

    private readonly record struct LoginCandidate(string Name) {
        public static LoginCandidate? FromCharacterName(string fullName) {
            if (string.IsNullOrWhiteSpace(fullName))
                return null;

            var separatorIndex = fullName.LastIndexOf('@');
            var name = separatorIndex > 0
                ? fullName[..separatorIndex].Trim()
                : fullName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new LoginCandidate(name);
        }
    }
}
