using System;

namespace MasterOfPuppets.LuaScripting.Mirror.Core;

/// <summary>
/// Stable identity for a locally observed, runtime-validated real player character.
/// Transient object identifiers are deliberately excluded.
/// </summary>
public sealed class MirrorPlayerIdentity : IEquatable<MirrorPlayerIdentity> {
    private MirrorPlayerIdentity(ulong contentId, uint homeWorldId, string displayName) {
        ContentId = contentId;
        HomeWorldId = homeWorldId;
        DisplayName = displayName;
    }

    public ulong ContentId { get; }
    public uint HomeWorldId { get; }
    public string DisplayName { get; }

    /// <summary>
    /// Creates an identity only after the runtime adapter has classified the observed entity.
    /// This boundary prevents non-player entities from entering the Mirror core accidentally.
    /// </summary>
    public static MirrorPlayerIdentity CreateValidated(
        ulong contentId,
        uint homeWorldId,
        string displayName,
        bool isRealPlayerCharacter) {
        if (!isRealPlayerCharacter)
            throw new ArgumentException("A Mirror identity must represent a real player character.", nameof(isRealPlayerCharacter));
        if (contentId == 0)
            throw new ArgumentOutOfRangeException(nameof(contentId), "A player content ID must be non-zero.");
        if (homeWorldId == 0)
            throw new ArgumentOutOfRangeException(nameof(homeWorldId), "A player home-world ID must be non-zero.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A player display name is required.", nameof(displayName));

        return new MirrorPlayerIdentity(contentId, homeWorldId, displayName.Trim());
    }

    public bool Equals(MirrorPlayerIdentity other) =>
        other is not null
        && ContentId == other.ContentId
        && HomeWorldId == other.HomeWorldId;

    public override bool Equals(object obj) => Equals(obj as MirrorPlayerIdentity);

    public override int GetHashCode() => HashCode.Combine(ContentId, HomeWorldId);

    public override string ToString() => $"{DisplayName} ({ContentId}@{HomeWorldId})";
}
