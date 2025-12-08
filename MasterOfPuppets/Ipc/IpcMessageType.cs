namespace MasterOfPuppets.Ipc;

public enum IpcMessageType {
    Hello = 1,
    Bye,
    Acknowledge,
    RunMacro,
    StopMacroExecution,
    SyncConfiguration,
    ExecuteTextCommand,
    ExecuteActionCommand,
    ExecuteGeneralActionCommand,
    ExecuteItemCommand,
    ExecuteTargetMyTarget,
    ExecuteTargetClear,
    ExecuteAbandonDuty,
    EnqueueMacroActions,
    EnqueueCharacterMacroActions,
    SetGameSettingsObjectQuantity
}
