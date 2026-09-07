using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.LuaScripting.Mirror.Coordination;

public sealed record MirrorFallbackCandidate(
    string CandidateId,
    MirrorParticipantMask EligibleParticipants,
    int PreferenceRank) {

    public MirrorFallbackCandidate Validate(MirrorParticipantMask eventParticipants) {
        if (string.IsNullOrWhiteSpace(CandidateId))
            throw new ArgumentException("Fallback candidate ID is required.", nameof(CandidateId));
        if (!EligibleParticipants.IsSubsetOf(eventParticipants))
            throw new ArgumentException("Fallback eligibility contains a participant outside the event.", nameof(EligibleParticipants));
        if (PreferenceRank < 0)
            throw new ArgumentOutOfRangeException(nameof(PreferenceRank));
        return this with { CandidateId = CandidateId.Trim() };
    }
}

public sealed record MirrorFallbackRequest(
    MirrorEventStamp Stamp,
    string Category,
    string DesiredObjectId,
    MirrorParticipantMask Participants,
    MirrorParticipantMask ExactParticipants,
    IReadOnlyList<MirrorFallbackCandidate> Candidates);

/// <summary>
/// Coordinator-selected result. UnresolvedParticipants is intentionally data,
/// not a terminal policy: OD-B decides what the product does when it is nonempty.
/// </summary>
public sealed record MirrorFallbackSelection(
    MirrorEventStamp Stamp,
    string Category,
    string DesiredObjectId,
    MirrorParticipantMask ExactParticipants,
    string FallbackObjectId,
    MirrorParticipantMask FallbackParticipants,
    MirrorParticipantMask UnresolvedParticipants) {

    public bool RequiresOwnerDefinedNoUniversalFallbackOutcome => !UnresolvedParticipants.IsEmpty;
}

public static class MirrorFallbackResolver {
    public static MirrorFallbackSelection Select(MirrorFallbackRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        var stamp = request.Stamp.Validate();
        if (string.IsNullOrWhiteSpace(request.Category))
            throw new ArgumentException("Fallback category is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DesiredObjectId))
            throw new ArgumentException("Desired object ID is required.", nameof(request));
        if (request.Participants.IsEmpty)
            throw new ArgumentException("Fallback event requires at least one participant.", nameof(request));
        if (!request.ExactParticipants.IsSubsetOf(request.Participants))
            throw new ArgumentException("Exact participant mask must be a subset of event participants.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Candidates);

        var required = request.Participants.Except(request.ExactParticipants);
        if (required.IsEmpty)
            return new MirrorFallbackSelection(
                stamp,
                request.Category.Trim().ToLowerInvariant(),
                request.DesiredObjectId.Trim(),
                request.ExactParticipants,
                string.Empty,
                MirrorParticipantMask.Empty,
                MirrorParticipantMask.Empty);

        var validatedCandidates = request.Candidates
            .Select(candidate => candidate.Validate(request.Participants))
            .ToArray();
        if (validatedCandidates.Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count()
            != validatedCandidates.Length)
            throw new ArgumentException("Fallback candidate IDs must be unique within an event.", nameof(request));

        var candidates = validatedCandidates
            .Select(candidate => new {
                Candidate = candidate,
                Covered = candidate.EligibleParticipants.Intersect(required),
            })
            .Where(value => !value.Covered.IsEmpty)
            .OrderByDescending(value => value.Covered.Count)
            .ThenBy(value => value.Candidate.PreferenceRank)
            .ThenBy(value => value.Candidate.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();

        var selectedId = candidates?.Candidate.CandidateId ?? string.Empty;
        var fallbackParticipants = candidates?.Covered ?? MirrorParticipantMask.Empty;
        return new MirrorFallbackSelection(
            stamp,
            request.Category.Trim().ToLowerInvariant(),
            request.DesiredObjectId.Trim(),
            request.ExactParticipants,
            selectedId,
            fallbackParticipants,
            required.Except(fallbackParticipants));
    }
}
