using ProtoBuf;

namespace MasterOfPuppets.Ipc;

[ProtoContract]
internal class IpcMessage {
    [ProtoMember(1)] public IpcMessageType MessageType { get; init; }
    [ProtoMember(2)] public long BroadcasterId { get; init; }
    // [ProtoMember(3)] public long PartyId { get; init; }
    // [ProtoMember(4)] public int ProcessId { get; init; }
    // [ProtoMember(5)] public DateTime TimeStamp { get; init; }
    [ProtoMember(3)] public byte[] Data { get; init; }
    [ProtoMember(4)] public string[] StringData { get; init; }
    // private static readonly int processId = Process.GetCurrentProcess().Id;

    public IpcMessage(IpcMessageType messageType, byte[] data, params string[] stringData) {
        MessageType = messageType;
        BroadcasterId = (long)DalamudApi.Player.ContentId;
        // PartyId = DalamudApi.PartyList.PartyId;
        // ProcessId = processId;
        // TimeStamp = DateTime.Now;
        Data = data;
        StringData = stringData;
    }

    private IpcMessage() { }

    public static IpcMessage Create<T>(IpcMessageType messageType, T data) where T : unmanaged => new(messageType, data.ToBytesUnmanaged());

    public static IpcMessage Create(IpcMessageType messageType, byte[] data) => new(messageType, data);

    public static IpcMessage Create(IpcMessageType messageType, params string[] stringData) => new(messageType, null, stringData);

    public byte[] Serialize() {
        // var sw = Stopwatch.StartNew();
        var protoSerialize = this.ProtoSerialize();
        // DalamudApi.PluginLog.Verbose($"proto serialized in {sw.Elapsed.TotalMilliseconds}ms");

        var serialized = protoSerialize.Compress();
        // DalamudApi.PluginLog.Verbose($"data compressed in {sw.Elapsed.TotalMilliseconds}ms");
        // sw.Stop();
        return serialized;
    }

    public T DataStruct<T>() where T : unmanaged => Data.ToStructUnmanaged<T>();
}
