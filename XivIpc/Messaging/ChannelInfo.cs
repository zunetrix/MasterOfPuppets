namespace XivIpc.Messaging;

internal class ChannelInfo {
    public string Name { get; private set; }
    public int Size { get; private set; }

    public ChannelInfo(string name, int size) {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Channel name must be non-empty and non-whitespace.", nameof(name));

        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Channel size must be positive.");

        Name = name;
        Size = size;
    }
}

