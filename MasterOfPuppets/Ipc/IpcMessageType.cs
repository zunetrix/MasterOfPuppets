namespace MasterOfPuppets.Ipc;

public enum IpcMessageType {
    Hello = 1,
    Bye,
    Acknowledge,
    RunMacro,
    StopMacroExecution,
    StopMovement,
    SyncConfiguration,
    ExecuteTextCommand,
    ExecuteActionCommand,
    ExecuteGeneralActionCommand,
    ExecuteItemCommand,
    ExecuteTargetMyTarget,
    ExecuteMoveToMyTarget,
    ExecuteToggleWalking,
    ExecuteTargetClear,
    ExecuteAbandonDuty,
    EnqueueMacroActions,
    EnqueueCharacterMacroActions,
    SetGameSettingsObjectQuantity,
    ExecuteChangeGearset
}
