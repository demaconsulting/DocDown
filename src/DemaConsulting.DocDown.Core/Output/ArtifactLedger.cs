namespace DocDown.Core;

/// <summary>
///     The completeness ledger: one <see cref="ArtifactEntry"/> per standard output artifact,
///     forming the machine-checkable statement of what the extraction produced.
/// </summary>
/// <param name="Summary">The ledger entry for <c>summary.txt</c>.</param>
/// <param name="Manifest">The ledger entry for <c>manifest.json</c>.</param>
/// <param name="Metadata">The ledger entry for <c>metadata.json</c>.</param>
/// <param name="Content">The ledger entry for <c>content.md</c>.</param>
/// <param name="Images">The ledger entry for the <c>images/</c> folder.</param>
/// <param name="Pages">The ledger entry for the <c>pages/</c> folder.</param>
/// <remarks>
///     The ledger is central to the honesty guarantee: the contract verifier reconciles each
///     entry against the actual filesystem and against the reported gaps, so a claimed status
///     that disagrees with disk is a detectable violation rather than a silent lie. Instances are
///     immutable and thread-safe.
/// </remarks>
public sealed record ArtifactLedger(ArtifactEntry Summary, ArtifactEntry Manifest,
    ArtifactEntry Metadata, ArtifactEntry Content, ArtifactEntry Images, ArtifactEntry Pages);
