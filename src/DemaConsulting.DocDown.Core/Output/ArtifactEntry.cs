namespace DocDown.Core;

/// <summary>
///     A single row of the completeness ledger describing one output artifact.
/// </summary>
/// <param name="Path">The relative path or folder the entry describes (for example <c>images/</c>).</param>
/// <param name="Status">Whether the artifact is present, partial, or absent.</param>
/// <param name="Obtained">
///     The count actually obtained (for example images written), or <see langword="null"/> when a
///     count is not meaningful.
/// </param>
/// <param name="Found">
///     The count discovered as expected (for example images found in the document), or
///     <see langword="null"/> when a count is not meaningful.
/// </param>
/// <remarks>
///     Pairing <see cref="Obtained"/> with <see cref="Found"/> lets a caller see partial success
///     precisely (for example 3 of 4 images), and lets the contract verifier reconcile the claim
///     against the filesystem. Instances are immutable and thread-safe.
/// </remarks>
public sealed record ArtifactEntry(string Path, ArtifactStatus Status, int? Obtained = null, int? Found = null);
