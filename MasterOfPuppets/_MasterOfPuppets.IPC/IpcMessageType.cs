namespace MasterOfPuppets.IPC;

public enum IpcMessageType
{
    Hello = 1,
    Bye,
    Acknowledge,
    RunMacro,
    StopMacroExecution,
    SyncConfiguration,
    ExecuteTextCommand,
    ExecuteActionCommand,
    ExecuteItemCommand
}
