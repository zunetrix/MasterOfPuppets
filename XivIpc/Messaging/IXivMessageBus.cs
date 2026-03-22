namespace XivIpc.Messaging;

public interface IXivMessageBus : IDisposable, IAsyncDisposable {
    event EventHandler<XivMessageReceivedEventArgs>? MessageReceived;
    Task PublishAsync(byte[] message);
}
