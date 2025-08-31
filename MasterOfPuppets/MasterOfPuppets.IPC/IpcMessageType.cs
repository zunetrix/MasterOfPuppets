namespace MasterOfPuppets.Ipc;

public enum IpcMessageType
{
    Hello = 1,
    Bye,
    Acknowledge,
    RunMacro,
    StopMacroExecution,
    SyncConfiguration,
    BroadcastTextCommand,
    BroadcastActionCommand
}
