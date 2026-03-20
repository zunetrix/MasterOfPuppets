namespace MasterOfPuppets.Ipc;

public enum IpcMessageType {
    Hello = 1,
    Bye,
    Acknowledge,
    RunMacro,
    StopMacroExecution,
    PauseMacroExecution,
    ResumeMacroExecution,
    StopMovement,
    SyncConfiguration,
    RefreshCommands,
    ExecuteTextCommand,
    ExecuteActionCommand,
    ExecuteGeneralActionCommand,
    ExecuteItemCommand,
    ExecuteTargetMyTarget,
    ExecuteInteractWithMyTarget,
    ExecuteInteractWithTarget,
    ExecuteMoveToMyTarget,
    ExecuteStackOnMe,
    ExecuteToggleWalking,
    ExecuteTargetClear,
    ExecuteAbandonDuty,
    EnqueueMacroActions,
    EnqueueCharacterMacroActions,
    SetGameSettingsObjectQuantity,
    ExecuteChangeGearset,
    SetWindowTitle,
    ExecuteFormation,

    // peer data request
    RequestEmoteList,
    EmoteList,
    RequestCharacterData,
    CharacterData,

    // house
    EnterHouse,
    ExitHouse,
    MoveToFrontDoor,

    // key broadcast
    KeyboardBroadcastToggle,
    KeyboardInput,

    // party
    InviteToParty,
    DisbandParty,
    RequestPartyLeader,
    RequestInviteAllToParty,

    // teleport
    TeleportToWard,
    TravelToWorld,
    TeleportToEstate,

    //camera
    EnableCamHack,
    DisableCamHack,

    // game exit
    ExecuteLogout,
    ExecuteShutdown,
}
