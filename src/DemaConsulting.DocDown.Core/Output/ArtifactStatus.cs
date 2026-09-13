namespace DocDown.Core;

/// <summary>
///     The presence status of an artifact in the completeness ledger.
/// </summary>
/// <remarks>
///     A three-state status is what makes the ledger machine-checkable against the filesystem:
///     <see cref="Present"/> asserts the artifact is fully there, <see cref="Partial"/> that
///     some but not all expected content exists, and <see cref="Absent"/> that it is missing —
///     each of which must reconcile with the actual files and the reported gaps.
/// </remarks>
public enum ArtifactStatus
{
    /// <summary>The artifact is fully present.</summary>
    Present,

    /// <summary>The artifact is present but incomplete.</summary>
    Partial,

    /// <summary>The artifact is absent.</summary>
    Absent
}
