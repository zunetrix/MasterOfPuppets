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

    // gearset
    ChangeGearset,
    RenameGearset,
    ReorderGearset,

    // formation
    ExecuteFormation,
    ExecuteFormationMove,

    // peer data request
    RequestEmoteList,
    EmoteList,
    RequestUnlockedState,
    UnlockedState,
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

    // render
    EnableRenderHack,
    DisableRenderHack,

    // game exit
    ExecuteLogout,
    ExecuteShutdown,

    //follow
    StartFollow,
    StopFollow,

    // window layouts
    ApplyWindowLayout,
    RequestWindowInfo,
    WindowInfo,
    ApplyAutoTiledLayout,
    SetWindowTitle,
    SetWindowResize,

    // game settings
    SetGameSettingsObjectQuantity,
    SetGameSettingsAlwaysInput,
    ApplyGameSettingsProfile,
}
