using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MasterOfPuppets.LuaScripting.Mirror.Core;

public readonly record struct MirrorModuleId {
    public MirrorModuleId(string value) {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A Mirror module ID is required.", nameof(value));
        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public enum MirrorDirectiveKind {
    DesiredState,
    Event,
}

/// <summary>
/// Module-neutral desired-state or one-shot-event contract. Runtime-specific payloads
/// remain strongly typed while sharing versioning, identity, and outcome semantics.
/// </summary>
public sealed record MirrorDirective<TPayload>(
    Guid DirectiveId,
    MirrorModuleId Module,
    MirrorVersionStamp Stamp,
    MirrorDirectiveKind Kind,
    TPayload Payload) {
    public static MirrorDirective<TPayload> DesiredState(
        MirrorModuleId module,
        MirrorVersionStamp stamp,
        TPayload payload) =>
        new(Guid.NewGuid(), module, stamp, MirrorDirectiveKind.DesiredState, payload);

    public static MirrorDirective<TPayload> Event(
        MirrorModuleId module,
        MirrorVersionStamp stamp,
        TPayload payload) =>
        new(Guid.NewGuid(), module, stamp, MirrorDirectiveKind.Event, payload);

    public MirrorDirectiveReference ToReference() => new(DirectiveId, Module, Stamp, Kind);
}

public readonly record struct MirrorDirectiveReference(
    Guid DirectiveId,
    MirrorModuleId Module,
    MirrorVersionStamp Stamp,
    MirrorDirectiveKind Kind);

public enum MirrorExecutionDisposition {
    Pending,
    Succeeded,
    RetryableFailure,
    PermanentlyIneligible,
    Skipped,
    Cancelled,
}

public sealed record MirrorParticipantOutcome {
    public MirrorParticipantOutcome(
        MirrorPlayerIdentity participant,
        MirrorExecutionDisposition disposition,
        int attemptCount,
        string detail = null) {
        ArgumentNullException.ThrowIfNull(participant);
        if (attemptCount < 0)
            throw new ArgumentOutOfRangeException(nameof(attemptCount));

        Participant = participant;
        Disposition = disposition;
        AttemptCount = attemptCount;
        Detail = detail;
    }

    public MirrorPlayerIdentity Participant { get; }
    public MirrorExecutionDisposition Disposition { get; }
    public int AttemptCount { get; }
    public string Detail { get; }
}

/// <summary>
/// Immutable result set usable by every Mirror module. Policy layers decide how a
/// retryable or permanently ineligible result affects their specific category.
/// </summary>
public sealed class MirrorDirectiveOutcome {
    private MirrorDirectiveOutcome(
        MirrorDirectiveReference directive,
        ImmutableArray<MirrorParticipantOutcome> participants) {
        Directive = directive;
        Participants = participants;
    }

    public MirrorDirectiveReference Directive { get; }
    public ImmutableArray<MirrorParticipantOutcome> Participants { get; }

    public static MirrorDirectiveOutcome Create(
        MirrorDirectiveReference directive,
        IEnumerable<MirrorParticipantOutcome> participantOutcomes) {
        ArgumentNullException.ThrowIfNull(participantOutcomes);

        var builder = ImmutableArray.CreateBuilder<MirrorParticipantOutcome>();
        var identities = new HashSet<MirrorPlayerIdentity>();
        foreach (var outcome in participantOutcomes) {
            if (outcome is null)
                throw new ArgumentException("An outcome cannot be null.", nameof(participantOutcomes));
            if (!identities.Add(outcome.Participant))
                throw new ArgumentException(
                    $"A directive cannot contain duplicate outcomes for {outcome.Participant}.",
                    nameof(participantOutcomes));
            builder.Add(outcome);
        }

        return new MirrorDirectiveOutcome(directive, builder.ToImmutable());
    }

    public ImmutableArray<MirrorPlayerIdentity> RetryCandidates() {
        var builder = ImmutableArray.CreateBuilder<MirrorPlayerIdentity>();
        foreach (var outcome in Participants) {
            if (outcome.Disposition == MirrorExecutionDisposition.RetryableFailure)
                builder.Add(outcome.Participant);
        }
        return builder.ToImmutable();
    }
}
