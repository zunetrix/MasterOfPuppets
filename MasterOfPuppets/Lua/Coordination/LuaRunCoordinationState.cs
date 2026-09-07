using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MasterOfPuppets.LuaScripting.Events;

namespace MasterOfPuppets.LuaScripting.Coordination;

/// <summary>
/// Bounded per-run coordination state. Distributed transports inject validated
/// frames; local runs use the same API without a transport.
/// </summary>
public sealed class LuaRunCoordinationState : ILuaCoordinationFacade {
    public const int MaximumVariables = 64;
    public const int MaximumMessages = 256;
    public const int MaximumNameBytes = 32;
    public const int MaximumPayloadBytes = 96;
    private readonly object _sync = new();
    private readonly Dictionary<string, LuaSharedVariableSnapshot> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<LuaParticipantMessageSnapshot> _messages = new();
    private readonly Dictionary<ulong, long> _lastMessageSequence = new();
    private readonly HashSet<Guid> _seenMessageIds = new();
    private readonly Queue<Guid> _seenMessageOrder = new();
    private Func<string, string, long, LuaCoordinationResult>? _sendVariable;
    private Func<Guid, string, string, int, long, ulong, LuaCoordinationResult>? _sendMessage;
    private LuaEventHub? _events;
    private ulong[] _participantContentIds;
    private long _localSequence;
    private long _lastVariableTransportSequence;
    private long _lastPublishedVariableSequence;

    public LuaRunCoordinationState(
        ulong localContentId,
        IReadOnlyList<ulong> participantContentIds,
        bool isConductor,
        bool isDistributed = false,
        ulong conductorContentId = 0) {
        LocalContentId = localContentId;
        _participantContentIds = participantContentIds.Where(cid => cid != 0).Distinct().ToArray();
        IsConductor = isConductor;
        IsDistributed = isDistributed;
        ConductorContentId = conductorContentId != 0
            ? conductorContentId
            : isConductor
                ? localContentId
                : _participantContentIds.FirstOrDefault();
    }

    public bool IsDistributed { get; }
    public bool IsConductor { get; }
    public ulong LocalContentId { get; }
    public ulong ConductorContentId { get; }
    public IReadOnlyList<ulong> ParticipantContentIds => _participantContentIds;
    public IReadOnlyList<LuaSharedVariableSnapshot> SharedVariables {
        get {
            lock (_sync)
                return _variables.Values.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public void ConfigureTransport(
        Func<string, string, long, LuaCoordinationResult> sendVariable,
        Func<Guid, string, string, int, long, ulong, LuaCoordinationResult> sendMessage) {
        _sendVariable = sendVariable ?? throw new ArgumentNullException(nameof(sendVariable));
        _sendMessage = sendMessage ?? throw new ArgumentNullException(nameof(sendMessage));
    }

    public void AttachEvents(LuaEventHub events) => _events = events ?? throw new ArgumentNullException(nameof(events));

    public bool TryUpdateParticipantRoster(IReadOnlyList<ulong> participants, out string reason) {
        ArgumentNullException.ThrowIfNull(participants);
        var normalized = participants.Where(cid => cid != 0).Distinct().ToArray();
        lock (_sync) {
            if (normalized.Length is 0 or > 32
                || _participantContentIds.Length == 0
                || normalized[0] != _participantContentIds[0]) {
                reason = "participant roster update must preserve the conductor in slot zero and contain at most 32 recipients";
                return false;
            }
            _participantContentIds = normalized;
            // A conductor-authenticated rebroadcast is the admission boundary for
            // restarted/rejoined local clients, whose process-local sequence starts
            // again at one. Message IDs remain replay-cached across the boundary.
            _lastMessageSequence.Clear();
            _lastVariableTransportSequence = 0;
        }
        reason = string.Empty;
        return true;
    }

    public bool TryGetParticipantSlot(ulong contentId, out int slot) {
        lock (_sync) {
            slot = Array.IndexOf(_participantContentIds, contentId);
            return slot >= 0;
        }
    }

    public LuaCoordinationResult SetShared(string key, string value) {
        key = ValidateText(key, MaximumNameBytes, "shared variable key", allowEmpty: false);
        value = ValidateText(value, MaximumPayloadBytes, "shared variable value", allowEmpty: true);
        if (!IsConductor)
            return LuaCoordinationResult.Failure("only the distributed run conductor may set shared variables");
        long sequence;
        lock (_sync)
            sequence = ++_localSequence;
        var transport = _sendVariable?.Invoke(key, value, sequence);
        if (transport is { Ok: false })
            return transport;
        ApplyVariable(key, value, sequence, LocalContentId, DateTimeOffset.UtcNow, out _);
        return transport ?? LuaCoordinationResult.Success("updated");
    }

    public bool TryGetShared(string key, out LuaSharedVariableSnapshot? value) {
        key = key?.Trim() ?? string.Empty;
        lock (_sync)
            return _variables.TryGetValue(key, out value);
    }

    public LuaCoordinationResult SendMessage(string topic, string payload, int schemaVersion, ulong targetContentId) {
        topic = ValidateText(topic, MaximumNameBytes, "message topic", allowEmpty: false);
        payload = ValidateText(payload, MaximumPayloadBytes, "message payload", allowEmpty: true);
        if (schemaVersion is < 1 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "message schema version must be between 1 and 255");
        if (targetContentId != 0 && !ParticipantContentIds.Contains(targetContentId))
            return LuaCoordinationResult.Failure($"target content ID {targetContentId} is not in the run roster");
        long sequence;
        lock (_sync)
            sequence = ++_localSequence;
        var messageId = Guid.NewGuid();
        var transport = _sendMessage?.Invoke(messageId, topic, payload, schemaVersion, sequence, targetContentId);
        if (transport is { Ok: false })
            return transport;
        ApplyMessage(new LuaParticipantMessageSnapshot(
            messageId,
            topic,
            payload,
            schemaVersion,
            sequence,
            LocalContentId,
            targetContentId,
            DateTimeOffset.UtcNow), out _);
        return transport ?? LuaCoordinationResult.Success("sent");
    }

    public bool TryReadMessage(string? topic, out LuaParticipantMessageSnapshot? message) {
        var normalized = topic?.Trim();
        lock (_sync) {
            while (_messages.Count > 0) {
                var candidate = _messages.Dequeue();
                if (string.IsNullOrWhiteSpace(normalized)
                    || candidate.Topic.Equals(normalized, StringComparison.OrdinalIgnoreCase)) {
                    message = candidate;
                    return true;
                }
            }
        }
        message = null;
        return false;
    }

    public bool ApplyVariable(
        string key,
        string value,
        long sequence,
        ulong senderContentId,
        DateTimeOffset receivedAt,
        out string reason) {
        key = ValidateText(key, MaximumNameBytes, "shared variable key", allowEmpty: false);
        value = ValidateText(value, MaximumPayloadBytes, "shared variable value", allowEmpty: true);
        if (sequence <= 0 || senderContentId == 0) {
            reason = "shared variable sequence and sender are required";
            return false;
        }
        if (ConductorContentId != 0 && senderContentId != ConductorContentId) {
            reason = "shared variable sender is not the run conductor";
            return false;
        }
        long publishedSequence;
        lock (_sync) {
            if (sequence <= _lastVariableTransportSequence) {
                reason = "shared variable update is duplicate or out of order";
                return false;
            }
            if (!_variables.ContainsKey(key) && _variables.Count >= MaximumVariables) {
                reason = "shared variable capacity is exhausted";
                return false;
            }
            _lastVariableTransportSequence = sequence;
            publishedSequence = ++_lastPublishedVariableSequence;
            _variables[key] = new LuaSharedVariableSnapshot(
                key, value, publishedSequence, senderContentId, receivedAt);
        }
        _events?.Publish("sync.variable", new Dictionary<string, string> {
            ["key"] = key,
            ["value"] = value,
            ["sequence"] = publishedSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sender_content_id"] = senderContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        reason = string.Empty;
        return true;
    }

    public bool ApplyMessage(LuaParticipantMessageSnapshot message, out string reason) {
        ValidateText(message.Topic, MaximumNameBytes, "message topic", allowEmpty: false);
        ValidateText(message.Payload, MaximumPayloadBytes, "message payload", allowEmpty: true);
        if (message.MessageId == Guid.Empty || message.Sequence <= 0 || message.SenderContentId == 0) {
            reason = "participant message identity, sequence, and sender are required";
            return false;
        }
        if (message.SchemaVersion is < 1 or > byte.MaxValue) {
            reason = "participant message schema version is invalid";
            return false;
        }
        if (!_participantContentIds.Contains(message.SenderContentId)) {
            reason = "participant message sender is outside the run roster";
            return false;
        }
        lock (_sync) {
            if (_seenMessageIds.Contains(message.MessageId)) {
                reason = "participant message ID was already received";
                return false;
            }
            if (_lastMessageSequence.TryGetValue(message.SenderContentId, out var last) && message.Sequence <= last) {
                reason = "participant message is duplicate or out of order";
                return false;
            }
            _lastMessageSequence[message.SenderContentId] = message.Sequence;
            _seenMessageIds.Add(message.MessageId);
            _seenMessageOrder.Enqueue(message.MessageId);
            while (_seenMessageOrder.Count > MaximumMessages * 2)
                _seenMessageIds.Remove(_seenMessageOrder.Dequeue());
            if (message.TargetContentId != 0 && message.TargetContentId != LocalContentId) {
                reason = string.Empty;
                return true;
            }
            _messages.Enqueue(message);
            while (_messages.Count > MaximumMessages)
                _messages.Dequeue();
        }
        _events?.Publish("sync.message", new Dictionary<string, string> {
            ["message_id"] = message.MessageId.ToString("D"),
            ["topic"] = message.Topic,
            ["payload"] = message.Payload,
            ["schema_version"] = message.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sequence"] = message.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sender_content_id"] = message.SenderContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["target_content_id"] = message.TargetContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        reason = string.Empty;
        return true;
    }

    private static string ValidateText(string? value, int maximumBytes, string label, bool allowEmpty) {
        value = value?.Trim() ?? string.Empty;
        if (!allowEmpty && value.Length == 0)
            throw new ArgumentException($"{label} cannot be empty");
        if (value.Contains('\0') || value.Contains('\r') || value.Contains('\n'))
            throw new ArgumentException($"{label} must be a single line without NUL characters");
        if (Encoding.UTF8.GetByteCount(value) > maximumBytes)
            throw new ArgumentException($"{label} exceeds {maximumBytes} UTF-8 bytes");
        return value;
    }
}
