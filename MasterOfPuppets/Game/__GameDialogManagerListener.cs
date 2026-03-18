using System;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MasterOfPuppets;

internal sealed unsafe class GameDialogManagerListener : IDisposable {
    private nint _selectYesno;
    private nint _selectOk;
    // private nint _contentsFinderConfirm;
    // private nint _journalAccept;
    // private nint _journalResult;
    // private nint _talk;
    private nint _repair;

    public GameDialogManagerListener() {
        var lc = DalamudApi.AddonLifecycle;
        lc.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonOpen);
        lc.RegisterListener(AddonEvent.PreFinalize, "SelectYesno", OnAddonClose);
        lc.RegisterListener(AddonEvent.PostSetup, "SelectOk", OnAddonOpen);
        lc.RegisterListener(AddonEvent.PreFinalize, "SelectOk", OnAddonClose);
        // lc.RegisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnAddonOpen);
        // lc.RegisterListener(AddonEvent.PreFinalize, "ContentsFinderConfirm", OnAddonClose);
        // lc.RegisterListener(AddonEvent.PostSetup, "JournalAccept", OnAddonOpen);
        // lc.RegisterListener(AddonEvent.PreFinalize, "JournalAccept", OnAddonClose);
        // lc.RegisterListener(AddonEvent.PostSetup, "JournalResult", OnAddonOpen);
        // lc.RegisterListener(AddonEvent.PreFinalize, "JournalResult", OnAddonClose);
        // lc.RegisterListener(AddonEvent.PostSetup, "Talk", OnAddonOpen);
        // lc.RegisterListener(AddonEvent.PostUpdate, "Talk", OnAddonOpen);
        // lc.RegisterListener(AddonEvent.PreFinalize, "Talk", OnAddonClose);
        lc.RegisterListener(AddonEvent.PostSetup, "Repair", OnAddonOpen);
        lc.RegisterListener(AddonEvent.PreFinalize, "Repair", OnAddonClose);
    }

    public void Dispose() {
        var lc = DalamudApi.AddonLifecycle;
        lc.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonOpen);
        lc.UnregisterListener(AddonEvent.PreFinalize, "SelectYesno", OnAddonClose);
        lc.UnregisterListener(AddonEvent.PostSetup, "SelectOk", OnAddonOpen);
        lc.UnregisterListener(AddonEvent.PreFinalize, "SelectOk", OnAddonClose);
        // lc.UnregisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnAddonOpen);
        // lc.UnregisterListener(AddonEvent.PreFinalize, "ContentsFinderConfirm", OnAddonClose);
        // lc.UnregisterListener(AddonEvent.PostSetup, "JournalAccept", OnAddonOpen);
        // lc.UnregisterListener(AddonEvent.PreFinalize, "JournalAccept", OnAddonClose);
        // lc.UnregisterListener(AddonEvent.PostSetup, "JournalResult", OnAddonOpen);
        // lc.UnregisterListener(AddonEvent.PreFinalize, "JournalResult", OnAddonClose);
        // lc.UnregisterListener(AddonEvent.PostSetup, "Talk", OnAddonOpen);
        // lc.UnregisterListener(AddonEvent.PostUpdate, "Talk", OnAddonOpen);
        // lc.UnregisterListener(AddonEvent.PreFinalize, "Talk", OnAddonClose);
        lc.UnregisterListener(AddonEvent.PostSetup, "Repair", OnAddonOpen);
        lc.UnregisterListener(AddonEvent.PreFinalize, "Repair", OnAddonClose);
    }

    //  Tracking

    private void OnAddonOpen(AddonEvent evt, AddonArgs args) {
        switch (args.AddonName) {
            case "SelectYesno": _selectYesno = args.Addon; break;
            case "SelectOk": _selectOk = args.Addon; break;
            // case "ContentsFinderConfirm": _contentsFinderConfirm = args.Addon; break;
            // case "JournalAccept": _journalAccept = args.Addon; break;
            // case "JournalResult": _journalResult = args.Addon; break;
            // case "Talk": _talk = args.Addon; break;
            case "Repair": _repair = args.Addon; break;
        }
    }

    private void OnAddonClose(AddonEvent evt, AddonArgs args) {
        switch (args.AddonName) {
            case "SelectYesno": _selectYesno = 0; break;
            case "SelectOk": _selectOk = 0; break;
            // case "ContentsFinderConfirm": _contentsFinderConfirm = 0; break;
            // case "JournalAccept": _journalAccept = 0; break;
            // case "JournalResult": _journalResult = 0; break;
            // case "Talk": _talk = 0; break;
            case "Repair": _repair = 0; break;
        }
    }

    //  Public API

    /// <summary>Click "Yes" on an open SelectYesno dialog.</summary>
    public bool ClickYes() {
        if (_selectYesno == 0) return false;
        var addon = (AddonSelectYesno*)_selectYesno;
        return ClickButton(addon->YesButton, (AtkUnitBase*)addon);
    }

    /// <summary>Click "No" on an open SelectYesno dialog.</summary>
    public bool ClickNo() {
        if (_selectYesno == 0) return false;
        var addon = (AddonSelectYesno*)_selectYesno;
        return ClickButton(addon->NoButton, (AtkUnitBase*)addon);
    }

    /// <summary>Click "OK" on an open SelectOk dialog.</summary>
    public bool ClickOk() {
        if (_selectOk == 0) return false;
        var addon = (AddonSelectOk*)_selectOk;
        return ClickButton(addon->OkButton, (AtkUnitBase*)addon);
    }

    /// <summary>Click "Commence" on the duty-finder confirmation popup.</summary>
    // public bool ClickCommence() {
    //     if (_contentsFinderConfirm == 0) return false;
    //     var addon = (AddonContentsFinderConfirm*)_contentsFinderConfirm;
    //     return ClickButton(addon->CommenceButton, (AtkUnitBase*)addon);
    // }

    /// <summary>Click "Withdraw" on the duty-finder confirmation popup.</summary>
    // public bool ClickWithdraw() {
    //     if (_contentsFinderConfirm == 0) return false;
    //     var addon = (AddonContentsFinderConfirm*)_contentsFinderConfirm;
    //     return ClickButton(addon->WithdrawButton, (AtkUnitBase*)addon);
    // }

    /// <summary>Click "Accept" on a quest accept window (JournalAccept).</summary>
    // public bool ClickJournalAccept() {
    //     if (_journalAccept == 0) return false;
    //     var base_ = (AtkUnitBase*)_journalAccept;
    //     var btn = base_->GetComponentButtonById(44);
    //     return btn != null && ClickButton(btn, base_);
    // }

    /// <summary>Click "Decline" on a quest accept window (JournalAccept).</summary>
    // public bool ClickJournalDecline() {
    //     if (_journalAccept == 0) return false;
    //     var base_ = (AtkUnitBase*)_journalAccept;
    //     var btn = base_->GetComponentButtonById(45);
    //     return btn != null && ClickButton(btn, base_);
    // }

    /// <summary>Click "Complete" on a quest completion window (JournalResult).</summary>
    // public bool ClickJournalComplete() {
    //     if (_journalResult == 0) return false;
    //     var addon = (AddonJournalResult*)_journalResult;
    //     return ClickButton(addon->CompleteButton, (AtkUnitBase*)addon);
    // }

    /// <summary>Advance a Talk (subtitle) dialog box.</summary>
    // public bool ClickTalk() {
    //     if (_talk == 0) return false;
    //     var base_ = (AtkUnitBase*)_talk;
    //     var evt = stackalloc AtkEvent[1];
    //     evt[0] = new AtkEvent {
    //         Listener = (AtkEventListener*)base_,
    //         Target = &AtkStage.Instance()->AtkEventTarget,
    //         State = new AtkEventState { StateFlags = (AtkEventStateFlags)132 }
    //     };
    //     var data = stackalloc AtkEventData[1];
    //     for (var i = 0; i < sizeof(AtkEventData); i++) ((byte*)data)[i] = 0;
    //     base_->ReceiveEvent(AtkEventType.MouseDown, 0, evt, data);
    //     base_->ReceiveEvent(AtkEventType.MouseClick, 0, evt, data);
    //     base_->ReceiveEvent(AtkEventType.MouseUp, 0, evt, data);
    //     return true;
    // }

    /// <summary>Click "Repair All" on the repair window.</summary>
    public bool ClickRepairAll() {
        if (_repair == 0) return false;
        var addon = (AddonRepair*)_repair;
        return ClickButton(addon->RepairAllButton, (AtkUnitBase*)addon);
    }

    //  Helpers

    private static bool ClickButton(AtkComponentButton* button, AtkUnitBase* owner) {
        if (button == null) return false;
        if (!button->IsEnabled || !button->AtkResNode->IsVisible()) return false;
        var btnRes = button->AtkComponentBase.OwnerNode->AtkResNode;
        var evt = (AtkEvent*)btnRes.AtkEventManager.Event;
        owner->ReceiveEvent(evt->State.EventType, (int)evt->Param, btnRes.AtkEventManager.Event);
        return true;
    }
}
