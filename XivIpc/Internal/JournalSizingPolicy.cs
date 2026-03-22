namespace XivIpc.Internal;

internal readonly record struct JournalSizing(
    int RequestedBufferBytes,
    int BudgetBytes,
    int MaxPayloadBytes,
    int ImageSize,
    bool WasCapped);

internal static class JournalSizingPolicy {
    internal const int MaxBudgetBytes = 128 * 1024 * 1024;

    internal static JournalSizing Compute(int requestedBufferBytes, int headerBytes) {
        if (requestedBufferBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedBufferBytes));

        if (headerBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(headerBytes));

        long doubledBudget = checked((long)requestedBufferBytes * 2L);
        bool wasCapped = doubledBudget > MaxBudgetBytes;
        int budgetBytes = (int)Math.Min(MaxBudgetBytes, doubledBudget);
        if (budgetBytes <= headerBytes)
            throw new InvalidOperationException($"Requested buffer size {requestedBufferBytes} bytes is too small for journal header size {headerBytes}.");

        int maxPayloadBytes = Math.Min(requestedBufferBytes, budgetBytes - headerBytes);
        int imageSize = checked(headerBytes + budgetBytes);
        return new JournalSizing(requestedBufferBytes, budgetBytes, maxPayloadBytes, imageSize, wasCapped);
    }
}
