using System;

namespace MasterOfPuppets.IPC;

[AttributeUsage(AttributeTargets.Method)]
internal class IpcHandleAttribute : Attribute
{
    public IpcMessageType MessageType { get; }

    public IpcHandleAttribute(IpcMessageType messageType)
    {
        MessageType = messageType;
    }
}
