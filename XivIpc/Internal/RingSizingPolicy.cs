namespace XivIpc.Internal;

internal readonly record struct RingSizing(
    int RequestedBufferBytes,
    int BudgetBytes,
    int SlotCount,
    int SlotPayloadBytes,
    int ImageSize,
    bool WasCapped);

internal static class RingSizingPolicy {
    internal const int MaxBudgetBytes = 128 * 1024 * 1024;

    internal static RingSizing Compute(int requestedBufferBytes, int slotCount, int headerSize, int slotHeaderSize) {
        if (requestedBufferBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedBufferBytes));

        if (slotCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotCount));

        if (headerSize < 0)
            throw new ArgumentOutOfRangeException(nameof(headerSize));

        if (slotHeaderSize < 0)
            throw new ArgumentOutOfRangeException(nameof(slotHeaderSize));

        long doubledBudget = checked((long)requestedBufferBytes * 2L);
        bool wasCapped = doubledBudget > MaxBudgetBytes;
        int budgetBytes = (int)Math.Min(MaxBudgetBytes, doubledBudget);

        long payloadBytes = ((long)budgetBytes - headerSize) / slotCount - slotHeaderSize;
        if (payloadBytes <= 0) {
            throw new InvalidOperationException(
                $"Requested buffer size {requestedBufferBytes} bytes is too small for slotCount={slotCount}, " +
                $"headerSize={headerSize}, slotHeaderSize={slotHeaderSize}, budgetBytes={budgetBytes}.");
        }

        int slotPayloadBytes = checked((int)payloadBytes);
        int imageSize = checked(headerSize + (slotCount * checked(slotHeaderSize + slotPayloadBytes)));
        return new RingSizing(requestedBufferBytes, budgetBytes, slotCount, slotPayloadBytes, imageSize, wasCapped);
    }
}
