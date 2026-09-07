using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MasterOfPuppets.LuaScripting.Synchronization;

internal sealed record LuaChatSyncFragment(
    string ScriptName,
    Guid MessageId,
    int Index,
    int Count,
    string Payload);

internal static class LuaChatSyncFragmentCodec {
    public const string CommandName = "mopluachunk";
    public const int MaximumChatBytes = 500;
    public const int MaximumFragments = 8;
    public const int MaximumEncodedEnvelopeChars = 2048;

    public static bool TryCreateCommands(
        string chatPrefix,
        string scriptName,
        string messageId,
        string encodedEnvelope,
        out IReadOnlyList<string> commands,
        out string error) {
        commands = [];
        chatPrefix = chatPrefix?.Trim() ?? string.Empty;
        scriptName = scriptName?.Trim() ?? string.Empty;
        encodedEnvelope = encodedEnvelope?.Trim() ?? string.Empty;
        if (chatPrefix.Length == 0 || scriptName.Length == 0 || scriptName.Contains('"')) {
            error = "the chat prefix or script name is invalid";
            return false;
        }
        if (!Guid.TryParse(messageId, out var id) || id == Guid.Empty) {
            error = "the synchronization message ID is invalid";
            return false;
        }
        if (encodedEnvelope.Length == 0 || encodedEnvelope.Length > MaximumEncodedEnvelopeChars
            || !encodedEnvelope.All(IsBase64Character)) {
            error = "the encoded synchronization envelope is invalid or too large";
            return false;
        }

        var compactId = id.ToString("N");
        var conservativePrefix = BuildPrefix(
            chatPrefix,
            scriptName,
            compactId,
            MaximumFragments,
            MaximumFragments);
        var chunkCapacity = MaximumChatBytes - Encoding.UTF8.GetByteCount(conservativePrefix);
        if (chunkCapacity < 32) {
            error = "the chat prefix or script name leaves no room for synchronization data";
            return false;
        }

        var count = (encodedEnvelope.Length + chunkCapacity - 1) / chunkCapacity;
        if (count is < 2 or > MaximumFragments) {
            error = count < 2
                ? "fragmentation was requested for an envelope that already fits one message"
                : $"the synchronization envelope requires more than {MaximumFragments} chat fragments";
            return false;
        }

        var result = new List<string>(count);
        for (var index = 1; index <= count; index++) {
            var offset = (index - 1) * chunkCapacity;
            var payload = encodedEnvelope.Substring(offset, Math.Min(chunkCapacity, encodedEnvelope.Length - offset));
            var command = BuildPrefix(chatPrefix, scriptName, compactId, index, count) + payload;
            if (Encoding.UTF8.GetByteCount(command) > MaximumChatBytes) {
                error = $"synchronization fragment {index}/{count} exceeds the chat limit";
                return false;
            }
            result.Add(command);
        }
        commands = result;
        error = string.Empty;
        return true;
    }

    public static bool TryParseArguments(
        IReadOnlyList<string> args,
        out LuaChatSyncFragment fragment,
        out string error) {
        fragment = null!;
        if (args.Count != 5) {
            error = "expected script, message ID, index, count, and payload";
            return false;
        }
        var scriptName = args[0]?.Trim() ?? string.Empty;
        var payload = args[4]?.Trim() ?? string.Empty;
        if (scriptName.Length is 0 or > 200 || scriptName.Contains('"')) {
            error = "the fragment script name is invalid";
            return false;
        }
        if (!Guid.TryParseExact(args[1], "N", out var messageId) || messageId == Guid.Empty) {
            error = "the fragment message ID is invalid";
            return false;
        }
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            || count is < 2 or > MaximumFragments
            || index < 1
            || index > count) {
            error = "the fragment index or count is invalid";
            return false;
        }
        if (payload.Length is 0 or > MaximumChatBytes || !payload.All(IsBase64Character)) {
            error = "the fragment payload is invalid";
            return false;
        }
        fragment = new LuaChatSyncFragment(scriptName, messageId, index, count, payload);
        error = string.Empty;
        return true;
    }

    private static string BuildPrefix(
        string chatPrefix,
        string scriptName,
        string compactId,
        int index,
        int count) =>
        $"{chatPrefix} {CommandName} \"{scriptName}\" {compactId} {index.ToString(CultureInfo.InvariantCulture)} {count.ToString(CultureInfo.InvariantCulture)} ";

    private static bool IsBase64Character(char value) =>
        value is >= 'A' and <= 'Z'
        or >= 'a' and <= 'z'
        or >= '0' and <= '9'
        or '+' or '/' or '=';
}

internal enum LuaChatSyncAssemblyStatus {
    Pending,
    Complete,
    Rejected,
}

internal sealed class LuaChatSyncFragmentAssembler {
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    public const int DefaultMaximumAssemblies = 64;

    private readonly Dictionary<AssemblyKey, PendingAssembly> _pending = new();
    private readonly TimeSpan _timeout;
    private readonly int _maximumAssemblies;

    public LuaChatSyncFragmentAssembler(
        TimeSpan? timeout = null,
        int maximumAssemblies = DefaultMaximumAssemblies) {
        _timeout = timeout ?? DefaultTimeout;
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (maximumAssemblies <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumAssemblies));
        _maximumAssemblies = maximumAssemblies;
    }

    public LuaChatSyncAssemblyStatus Accept(
        LuaChatSyncFragment fragment,
        string senderName,
        DateTimeOffset now,
        out string scriptName,
        out string encodedEnvelope,
        out string error) {
        ArgumentNullException.ThrowIfNull(fragment);
        senderName = senderName?.Trim() ?? string.Empty;
        scriptName = string.Empty;
        encodedEnvelope = string.Empty;
        if (senderName.Length == 0) {
            error = "the fragment sender is missing";
            return LuaChatSyncAssemblyStatus.Rejected;
        }

        Prune(now);
        var key = new AssemblyKey(senderName.ToUpperInvariant(), fragment.MessageId);
        if (!_pending.TryGetValue(key, out var assembly)) {
            if (_pending.Count >= _maximumAssemblies)
                RemoveOldest();
            assembly = new PendingAssembly(fragment.ScriptName, fragment.Count, now);
            _pending.Add(key, assembly);
        } else if (!assembly.ScriptName.Equals(fragment.ScriptName, StringComparison.Ordinal)
            || assembly.Count != fragment.Count) {
            _pending.Remove(key);
            error = "fragment metadata conflicts with the existing assembly";
            return LuaChatSyncAssemblyStatus.Rejected;
        }

        var slot = fragment.Index - 1;
        if (assembly.Payloads[slot] != null) {
            if (assembly.Payloads[slot]!.Equals(fragment.Payload, StringComparison.Ordinal)) {
                error = string.Empty;
                return LuaChatSyncAssemblyStatus.Pending;
            }
            _pending.Remove(key);
            error = "a duplicate fragment contained conflicting data";
            return LuaChatSyncAssemblyStatus.Rejected;
        }

        assembly.Payloads[slot] = fragment.Payload;
        assembly.Received++;
        assembly.TotalChars += fragment.Payload.Length;
        assembly.LastObservedAt = now;
        if (assembly.TotalChars > LuaChatSyncFragmentCodec.MaximumEncodedEnvelopeChars) {
            _pending.Remove(key);
            error = "the assembled synchronization envelope exceeds its size limit";
            return LuaChatSyncAssemblyStatus.Rejected;
        }
        if (assembly.Received != assembly.Count) {
            error = string.Empty;
            return LuaChatSyncAssemblyStatus.Pending;
        }

        _pending.Remove(key);
        scriptName = assembly.ScriptName;
        encodedEnvelope = string.Concat(assembly.Payloads);
        error = string.Empty;
        return LuaChatSyncAssemblyStatus.Complete;
    }

    public int Count => _pending.Count;
    public void Clear() => _pending.Clear();

    private void Prune(DateTimeOffset now) {
        foreach (var (key, assembly) in _pending.ToArray())
            if (now - assembly.LastObservedAt > _timeout)
                _pending.Remove(key);
    }

    private void RemoveOldest() {
        AssemblyKey oldestKey = default;
        var oldest = DateTimeOffset.MaxValue;
        foreach (var (key, assembly) in _pending)
            if (assembly.LastObservedAt < oldest) {
                oldest = assembly.LastObservedAt;
                oldestKey = key;
            }
        if (oldestKey != default)
            _pending.Remove(oldestKey);
    }

    private readonly record struct AssemblyKey(string Sender, Guid MessageId);

    private sealed class PendingAssembly {
        public PendingAssembly(string scriptName, int count, DateTimeOffset observedAt) {
            ScriptName = scriptName;
            Count = count;
            Payloads = new string?[count];
            LastObservedAt = observedAt;
        }

        public string ScriptName { get; }
        public int Count { get; }
        public string?[] Payloads { get; }
        public int Received { get; set; }
        public int TotalChars { get; set; }
        public DateTimeOffset LastObservedAt { get; set; }
    }
}
