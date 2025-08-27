using System;

namespace MasterOfPuppets.Ipc;

[AttributeUsage(AttributeTargets.Method)]
internal class IpcHandleAttribute : Attribute
{
    public IpcMessageType MessageType { get; }

    public IpcHandleAttribute(IpcMessageType messageType)
    {
        MessageType = messageType;
    }
}
