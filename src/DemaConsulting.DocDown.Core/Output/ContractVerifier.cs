using System.Security.Cryptography;
using System.Text.Json;

namespace DocDown.Core;

/// <summary>
///     Verifies a completed extraction folder against its own <c>manifest.json</c> and the library's
///     honesty invariants, reporting every discrepancy rather than trusting the manifest.
/// </summary>
/// <remarks>
///     <para>
///         This is the highest-value check in the repository: it makes honesty machine-checkable. It
///         re-reads the manifest from disk, walks <strong>every file beneath the scratch root at any
///         depth</strong>, recomputes sizes and SHA-256 digests, and reconciles in <em>both</em>
///         directions — every artifact the manifest claims must exist on disk, and every file on
///         disk must be accounted for in the manifest. It also confirms the completeness ledger
///         agrees with the filesystem, that every partial or absent artifact is explained by a gap,
///         that no gap has an empty reason, that <c>summary.txt</c> names the backend the manifest
///         says ran, and that the <c>complete</c> flag equals <c>gaps.length == 0</c>.
///     </para>
///     <para>
///         <strong>Explicit integrity scope.</strong> Images and pages carry a recorded size and
///         SHA-256, so their <em>bodies</em> are verified. Parts do not: the manifest schema records
///         no hash for a part, so a part is <em>existence-checked only</em> and a modified part body
///         will pass. That limit is stated here rather than left ambiguous, because a verifier that
///         quietly implied more than it checks would violate the very honesty rule it exists to
///         enforce. Adding part hashes would be a manifest schema change.
///     </para>
///     <para>
///         <strong>Inability to verify is not verification.</strong> When a directory read fails,
///         the verifier does not treat the directory as empty — it reports <c>DD0723</c> and
///         withholds the conclusion that depended on the listing. A run whose verification was
///         blocked is therefore <em>incomplete</em>, and is distinguishable from a clean pass by
///         the same means as any other finding: the returned list is not empty.
///     </para>
///     <para>
///         <see cref="Verify(string)"/> <strong>never throws for a contract violation</strong> — it returns
///         the violations as data so a caller can enumerate every problem in one pass; an honest
///         extraction yields an empty list. It performs read-only filesystem I/O (reading files and
///         recomputing hashes) and holds no state, so it is safe to call concurrently against
///         different folders.
///     </para>
/// </remarks>
public static class ContractVerifier
{
    /// <summary>The schema version this verifier understands.</summary>
    /// <remarks>Pinned so a manifest from a future, incompatible schema is flagged rather than misread.</remarks>
    private const string SupportedSchemaVersion = "1.2";

    /// <summary>
    ///     Verifies the extraction folder and returns every contract violation found.
    /// </summary>
    /// <param name="scratchFolder">The absolute path of the extraction folder to verify. Must not be null or empty.</param>
    /// <returns>
    ///     The list of <see cref="ContractViolation"/> instances found, empty when the extraction is
    ///     honest and internally consistent.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scratchFolder"/> is null or empty.</exception>
    /// <remarks>
    ///     Reads <c>manifest.json</c> and <c>summary.txt</c> and walks the resource folders, recomputing
    ///     hashes from disk. Never throws for a violation; an unreadable or unparsable manifest is
    ///     itself reported as a violation. Read-only I/O.
    /// </remarks>
    public static IReadOnlyList<ContractViolation> Verify(string scratchFolder)
    {
        // A folder is required to have anything to verify
        ArgumentException.ThrowIfNullOrEmpty(scratchFolder);

        return Verify(scratchFolder, FileSystemDirectoryEnumerator.Instance);
    }

    /// <summary>
    ///     Verifies the extraction folder using a supplied directory-enumeration boundary.
    /// </summary>
    /// <param name="scratchFolder">The absolute path of the extraction folder to verify.</param>
    /// <param name="enumerator">The directory-enumeration boundary to read the folder through.</param>
    /// <returns>The list of <see cref="ContractViolation"/> instances found.</returns>
    /// <remarks>
    ///     The implementation behind the public <see cref="Verify(string)"/> entry point. It is
    ///     <see langword="internal"/> solely so tests can supply an enumerator that fails, which is
    ///     the only portable way to exercise the unreadable-folder path: <c>chmod 000</c> is a
    ///     no-op when the test process runs as root, and the Windows equivalent needs ACL
    ///     manipulation that is not available in every runner. The production path always uses the
    ///     real file system. Read-only I/O.
    /// </remarks>
    internal static IReadOnlyList<ContractViolation> Verify(string scratchFolder, IDirectoryEnumerator enumerator)
    {
        var violations = new List<ContractViolation>();

        // The manifest must be present and parsable before any deeper check is meaningful
        var manifest = LoadManifest(scratchFolder, violations);
        if (manifest is null)
        {
            return violations;
        }

        // Structural preconditions: schema version, mandatory fields, and the summary file
        VerifySchemaVersion(manifest, violations);
        VerifyMandatoryFields(manifest, violations);
        var summaryText = LoadSummary(scratchFolder, violations);

        // Resource reconciliation in both directions
        VerifyListedResources(scratchFolder, manifest, violations);
        VerifyNoUnlistedFiles(scratchFolder, manifest, enumerator, violations);

        // Ledger, gap, and cross-document consistency
        VerifyLedger(scratchFolder, manifest, enumerator, violations);
        VerifyGapCoverage(manifest, violations);
        VerifyGapReasons(manifest, violations);
        VerifyBackendNamed(manifest, summaryText, violations);
        VerifyCompleteFlag(manifest, violations);

        return violations;
    }

    /// <summary>
    ///     Loads and deserializes the manifest, recording a violation on failure.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <returns>The parsed manifest, or <see langword="null"/> when it is missing or unparsable.</returns>
    /// <remarks>
    ///     A missing manifest (<c>DD0710</c>) or an unparsable one (<c>DD0711</c>) is a terminal
    ///     violation because no further check can proceed without it. Read-only I/O.
    /// </remarks>
    private static ExtractionManifest? LoadManifest(string scratchFolder, List<ContractViolation> violations)
    {
        // A missing manifest is the most fundamental violation
        var manifestPath = Path.Combine(scratchFolder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.ManifestMissing, "manifest.json is missing."));
            return null;
        }

        try
        {
            // Deserialize through the source-generated context, matching how it was written
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(json, DocDownJsonContext.Default.ExtractionManifest);
            if (manifest is null)
            {
                violations.Add(new ContractViolation(DiagnosticCodes.ManifestUnparsable, "manifest.json deserialized to null."));
            }

            return manifest;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Any read or parse failure is reported rather than thrown, per the report-not-throw contract
            violations.Add(new ContractViolation(DiagnosticCodes.ManifestUnparsable, $"manifest.json could not be parsed: {exception.Message}"));
            return null;
        }
    }

    /// <summary>
    ///     Loads <c>summary.txt</c>, recording a violation when it is missing.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <returns>The summary text, or <see langword="null"/> when it is missing or unreadable.</returns>
    /// <remarks>
    ///     The summary is needed for the backend-naming cross-check; its absence is violation
    ///     <c>DD0714</c>. Read-only I/O.
    /// </remarks>
    private static string? LoadSummary(string scratchFolder, List<ContractViolation> violations)
    {
        // The summary must exist as part of the invariant layout
        var summaryPath = Path.Combine(scratchFolder, "summary.txt");
        if (!File.Exists(summaryPath))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.SummaryMissing, "summary.txt is missing."));
            return null;
        }

        try
        {
            return File.ReadAllText(summaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Treat an unreadable summary as missing for the purpose of the naming cross-check
            violations.Add(new ContractViolation(DiagnosticCodes.SummaryMissing, $"summary.txt could not be read: {exception.Message}"));
            return null;
        }
    }

    /// <summary>
    ///     Verifies the manifest declares the supported schema version.
    /// </summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>An unexpected version (<c>DD0712</c>) warns that the rest of the shape may not be trustworthy. Pure.</remarks>
    private static void VerifySchemaVersion(ExtractionManifest manifest, List<ContractViolation> violations)
    {
        // A different schema version means later checks may misinterpret the shape
        if (!string.Equals(manifest.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.UnsupportedSchemaVersion,
                $"Unsupported schemaVersion '{manifest.SchemaVersion}'; expected '{SupportedSchemaVersion}'."));
        }
    }

    /// <summary>
    ///     Verifies the manifest's mandatory top-level fields are present.
    /// </summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Guards against a manifest that parsed but omitted a required block (<c>DD0713</c>), which
    ///     would otherwise surface as a confusing null later. Pure.
    /// </remarks>
    private static void VerifyMandatoryFields(ExtractionManifest manifest, List<ContractViolation> violations)
    {
        // Each mandatory block underpins later checks; a missing one is reported here
        if (manifest.Tool is null)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.MandatoryFieldMissing, "Mandatory field 'tool' is missing."));
        }

        if (manifest.Source is null)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.MandatoryFieldMissing, "Mandatory field 'source' is missing."));
        }

        if (manifest.Artifacts is null)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.MandatoryFieldMissing, "Mandatory field 'artifacts' is missing."));
        }

        if (string.IsNullOrEmpty(manifest.Status))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.MandatoryFieldMissing, "Mandatory field 'status' is missing."));
        }
    }

    /// <summary>
    ///     Verifies every listed image, page, and part exists on disk with the recorded size and hash.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Missing files are <c>DD0715</c>; a size or SHA-256 mismatch is <c>DD0716</c>. Parts record
    ///     no hash, so only their existence is checked. The four fixed root artifacts are also
    ///     checked here: when the ledger claims <c>summary.txt</c>, <c>manifest.json</c>,
    ///     <c>metadata.json</c>, or <c>content.md</c> is <c>present</c> or <c>partial</c> but the file
    ///     is absent on disk, that is a broken promise in the same direction as a missing image.
    ///     Read-only I/O.
    /// </remarks>
    private static void VerifyListedResources(string scratchFolder, ExtractionManifest manifest, List<ContractViolation> violations)
    {
        // Images: existence plus size and hash integrity
        foreach (var image in manifest.Images ?? [])
        {
            VerifyHashedResource(scratchFolder, image.Path, image.SizeBytes, image.Sha256, violations);
        }

        // Pages: existence plus size and hash integrity
        foreach (var page in manifest.Pages ?? [])
        {
            VerifyHashedResource(scratchFolder, page.Path, page.SizeBytes, page.Sha256, violations);
        }

        // Parts: existence only, since parts carry no recorded hash
        foreach (var partPath in (manifest.Parts ?? []).Select(part => part.Path))
        {
            if (File.Exists(ToDiskPath(scratchFolder, partPath)))
            {
                continue;
            }

            violations.Add(new ContractViolation(DiagnosticCodes.ListedResourceMissing, $"Listed part '{partPath}' is missing on disk."));
        }

        // Root artifacts: a ledger claim of presence must be backed by a file on disk
        var artifacts = manifest.Artifacts;
        if (artifacts is not null)
        {
            VerifyClaimedRootArtifact(scratchFolder, "summary.txt", artifacts.Summary, violations);
            VerifyClaimedRootArtifact(scratchFolder, "manifest.json", artifacts.Manifest, violations);
            VerifyClaimedRootArtifact(scratchFolder, "metadata.json", artifacts.Metadata, violations);
            VerifyClaimedRootArtifact(scratchFolder, "content.md", artifacts.Content, violations);
        }
    }

    /// <summary>
    ///     Verifies a root artifact the ledger claims is present actually exists on disk.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="fileName">The root artifact file name.</param>
    /// <param name="entry">The ledger entry for that artifact, or <see langword="null"/> when missing.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Closes the forward direction of the reconciliation for the three files the manifest cannot
    ///     list in its resource arrays. A <c>present</c> or <c>partial</c> claim with no file behind
    ///     it is <c>DD0715</c>, the same code a missing listed image raises. Read-only I/O.
    /// </remarks>
    private static void VerifyClaimedRootArtifact(
        string scratchFolder, string fileName, ManifestArtifactEntry? entry, List<ContractViolation> violations)
    {
        // Nothing is claimed when there is no entry; the mandatory-field check covers that case
        if (entry is null || (entry.Status is not ("present" or "partial")))
        {
            return;
        }

        if (!File.Exists(Path.Combine(scratchFolder, fileName)))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.ListedResourceMissing,
                $"Listed artifact '{fileName}' is '{entry.Status}' in the manifest but is missing on disk."));
        }
    }

    /// <summary>
    ///     Verifies one hashed resource exists and matches its recorded size and SHA-256.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="relativePath">The relative resource path.</param>
    /// <param name="expectedSize">The size recorded in the manifest.</param>
    /// <param name="expectedHash">The SHA-256 recorded in the manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Recomputes the hash from the bytes on disk so a tampered or truncated file is detected.
    ///     Read-only I/O; a read error is reported as a missing resource.
    /// </remarks>
    private static void VerifyHashedResource(
        string scratchFolder, string relativePath, long expectedSize, string expectedHash, List<ContractViolation> violations)
    {
        // A listed file that is not on disk is a broken promise
        var diskPath = ToDiskPath(scratchFolder, relativePath);
        if (!File.Exists(diskPath))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.ListedResourceMissing, $"Listed resource '{relativePath}' is missing on disk."));
            return;
        }

        try
        {
            // Recompute size and hash from the actual bytes to detect corruption or tampering
            var bytes = File.ReadAllBytes(diskPath);
            if (bytes.LongLength != expectedSize)
            {
                violations.Add(new ContractViolation(DiagnosticCodes.ResourceHashMismatch,
                    $"Resource '{relativePath}' size {bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture)} does not match manifest {expectedSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}."));
            }

            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new ContractViolation(DiagnosticCodes.ResourceHashMismatch,
                    $"Resource '{relativePath}' SHA-256 does not match the manifest."));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable listed file is reported as missing rather than throwing
            violations.Add(new ContractViolation(DiagnosticCodes.ListedResourceMissing, $"Resource '{relativePath}' could not be read: {exception.Message}"));
        }
    }

    /// <summary>
    ///     Verifies every file anywhere under the scratch root is accounted for by the manifest.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="enumerator">The directory-enumeration boundary.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Catches the reverse discrepancy from <see cref="VerifyListedResources"/>: an on-disk file
    ///     the manifest omits (<c>DD0717</c>) is a silent, undocumented artifact. The walk is
    ///     <strong>exhaustive and recursive over the whole scratch tree</strong>, not limited to the
    ///     direct children of the resource folders, so a rogue root-level file or one nested inside
    ///     <c>images/</c> can no longer slip through. What counts as accounted for is defined once,
    ///     by <see cref="ArtifactInventory"/>, which is the same definition the scratch folder's
    ///     reuse guard applies, so the two cannot drift. Paths are normalized to forward slashes
    ///     before comparison so the match works identically on every platform.
    ///     <para>
    ///         When the walk itself fails, the result is <c>DD0723</c> and <strong>no</strong>
    ///         <c>DD0717</c> conclusion: a blocked read proves nothing about what the tree
    ///         contains, and reporting "no unlisted files" on the strength of a listing that was
    ///         never obtained would be exactly the dishonesty this verifier exists to detect.
    ///     </para>
    ///     Read-only I/O.
    /// </remarks>
    private static void VerifyNoUnlistedFiles(string scratchFolder, ExtractionManifest manifest, IDirectoryEnumerator enumerator, List<ContractViolation> violations)
    {
        // Read the tree first: without a listing there is no honest conclusion to draw
        var listing = enumerator.Recursive(scratchFolder);
        if (listing.Error is not null)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.VerificationIncomplete,
                $"The folder '{listing.Path}' could not be read, so unlisted-file verification is incomplete: {listing.Error}"));
            return;
        }

        // Every file anywhere beneath the scratch root must appear in the shared accounted set
        var accounted = ArtifactInventory.AccountedRelativePaths(manifest);
        foreach (var relative in ArtifactInventory.UnaccountedFiles(scratchFolder, accounted, listing.Files))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.UnlistedFile, $"File '{relative}' exists on disk but is not listed in the manifest."));
        }
    }

    /// <summary>
    ///     Verifies the completeness ledger agrees with the filesystem.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="enumerator">The directory-enumeration boundary.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Reconciles the content, images, and pages ledger entries against actual files and counts
    ///     (<c>DD0718</c>): a folder's obtained count must equal its file count, and each status must
    ///     be consistent with what exists. Read-only I/O.
    /// </remarks>
    private static void VerifyLedger(string scratchFolder, ExtractionManifest manifest, IDirectoryEnumerator enumerator, List<ContractViolation> violations)
    {
        // Without the artifacts block there is nothing to reconcile (already reported as DD0713)
        var artifacts = manifest.Artifacts;
        if (artifacts is null)
        {
            return;
        }

        // Content: presence must match the ledger status
        var contentExists = File.Exists(Path.Combine(scratchFolder, "content.md"));
        var contentStatus = artifacts.Content?.Status ?? "absent";
        if ((contentStatus is "present" or "partial") && !contentExists)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.LedgerContradiction, $"content.md is '{contentStatus}' in the ledger but does not exist on disk."));
        }
        else if (contentStatus == "absent" && contentExists)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.LedgerContradiction, "content.md is 'absent' in the ledger but exists on disk."));
        }

        // Folders: obtained counts and status must match the files present
        VerifyFolderLedger(scratchFolder, "images", artifacts.Images, enumerator, violations);
        VerifyFolderLedger(scratchFolder, "pages", artifacts.Pages, enumerator, violations);
    }

    /// <summary>
    ///     Verifies a folder ledger entry's count and status against the files on disk.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="folderName">The resource folder name (<c>images</c> or <c>pages</c>).</param>
    /// <param name="entry">The ledger entry, or <see langword="null"/> when missing.</param>
    /// <param name="enumerator">The directory-enumeration boundary.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     The obtained count must equal the file count, an absent entry must have no files, and a
    ///     partial entry must have at least one.
    ///     <para>
    ///         When the folder cannot be read, the count comparison is <strong>skipped</strong> and
    ///         <c>DD0723</c> is reported instead. An unknown count must never be substituted with
    ///         zero: that would invent a contradiction the evidence does not support, raising a
    ///         false <c>DD0718</c> against an honest manifest, and it would equally hide a real one.
    ///         "I could not count" is the only honest statement available.
    ///     </para>
    ///     Read-only I/O.
    /// </remarks>
    private static void VerifyFolderLedger(string scratchFolder, string folderName, ManifestArtifactEntry? entry, IDirectoryEnumerator enumerator, List<ContractViolation> violations)
    {
        // A missing folder entry is already covered by the mandatory-field check
        if (entry is null)
        {
            return;
        }

        // An absent folder is a known count of zero; a present one must actually be read
        var directory = Path.Combine(scratchFolder, folderName);
        var onDisk = 0;
        if (Directory.Exists(directory))
        {
            var listing = enumerator.TopLevel(directory);
            if (listing.Error is not null)
            {
                violations.Add(new ContractViolation(DiagnosticCodes.VerificationIncomplete,
                    $"The folder '{listing.Path}' could not be read, so the {folderName}/ ledger could not be reconciled: {listing.Error}"));
                return;
            }

            onDisk = listing.Files.Count;
        }

        var obtained = entry.Obtained ?? 0;

        // The claimed count must match reality exactly
        if (obtained != onDisk)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.LedgerContradiction,
                $"{folderName}/ ledger obtained {obtained.ToString(System.Globalization.CultureInfo.InvariantCulture)} does not match {onDisk.ToString(System.Globalization.CultureInfo.InvariantCulture)} files on disk."));
        }

        // An absent folder cannot have files; a partial folder must have some
        if (entry.Status == "absent" && onDisk > 0)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.LedgerContradiction, $"{folderName}/ is 'absent' in the ledger but has files on disk."));
        }
        else if (entry.Status == "partial" && onDisk == 0)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.LedgerContradiction, $"{folderName}/ is 'partial' in the ledger but has no files on disk."));
        }
    }

    /// <summary>
    ///     Verifies every partial or absent artifact is explained by a matching gap.
    /// </summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Enforces the no-silent-absence invariant from the reader's side (<c>DD0719</c>): a ledger
    ///     entry claiming partial or absent must have a gap naming its path. Pure.
    /// </remarks>
    private static void VerifyGapCoverage(ExtractionManifest manifest, List<ContractViolation> violations)
    {
        var artifacts = manifest.Artifacts;
        if (artifacts is null)
        {
            return;
        }

        // Index the gap targets so coverage is a simple membership test
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gap in manifest.Gaps ?? [])
        {
            targets.Add(gap.Target);
        }

        // Each partial or absent artifact must be named by at least one gap
        foreach (var entry in new[] { artifacts.Content, artifacts.Images, artifacts.Pages })
        {
            if (entry is null)
            {
                continue;
            }

            if ((entry.Status is "partial" or "absent") && !targets.Contains(entry.Path))
            {
                violations.Add(new ContractViolation(DiagnosticCodes.UnexplainedAbsenceViolation,
                    $"Artifact '{entry.Path}' is '{entry.Status}' but no gap explains it."));
            }
        }
    }

    /// <summary>
    ///     Verifies no gap has an empty reason.
    /// </summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>A reasonless gap (<c>DD0720</c>) defeats the purpose of the gap mechanism. Pure.</remarks>
    private static void VerifyGapReasons(ExtractionManifest manifest, List<ContractViolation> violations)
    {
        // Every gap must carry a non-empty explanation
        foreach (var gap in manifest.Gaps ?? [])
        {
            if (!string.IsNullOrWhiteSpace(gap.Reason))
            {
                continue;
            }

            violations.Add(new ContractViolation(DiagnosticCodes.GapEmptyReason, $"Gap '{gap.Id}' has an empty reason."));
        }
    }

    /// <summary>
    ///     Verifies <c>summary.txt</c> names the backend the manifest says ran.
    /// </summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="summaryText">The summary text, or <see langword="null"/> when unavailable.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     Cross-checks the two documents so they cannot disagree about which backend produced the
    ///     output (<c>DD0721</c>). The check is anchored on the exact <c>  Selected  : </c> line the
    ///     summary's Backend block emits, and requires the identifier before the first <c> - </c> to
    ///     equal <c>extractor.Id</c> with an ordinal comparison and the segment after it to begin with
    ///     <c>extractor.DisplayName</c>. A mention of the identifier anywhere else in the summary —
    ///     in the candidate trace, in narrative text, or in a failure explanation — no longer
    ///     satisfies the check, so the backend name cannot be spoofed by an incidental substring.
    ///     Skipped when there was no selected extractor or no summary. Pure.
    /// </remarks>
    private static void VerifyBackendNamed(ExtractionManifest manifest, string? summaryText, List<ContractViolation> violations)
    {
        // Only meaningful when a backend ran and the summary is available to inspect
        var extractor = manifest.Extractor;
        if (extractor is null || summaryText is null)
        {
            return;
        }

        // The one authoritative statement of which backend ran is the Backend block's Selected line
        const string selectedPrefix = "  Selected  : ";
        const string separator = " - ";
        var named = false;
        foreach (var line in summaryText.Split('\n'))
        {
            if (!line.StartsWith(selectedPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // Split the declaration into "{id} - {displayName}      {modeNote}"
            var declaration = line[selectedPrefix.Length..].TrimEnd('\r');
            var separatorIndex = declaration.IndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var id = declaration[..separatorIndex];
            var remainder = declaration[(separatorIndex + separator.Length)..];
            if (string.Equals(id, extractor.Id, StringComparison.Ordinal)
                && (string.IsNullOrEmpty(extractor.DisplayName) || remainder.StartsWith(extractor.DisplayName, StringComparison.Ordinal)))
            {
                named = true;
                break;
            }
        }

        if (!named)
        {
            violations.Add(new ContractViolation(DiagnosticCodes.BackendMismatch,
                $"summary.txt does not name the backend '{extractor.Id}' the manifest says ran."));
        }
    }

    /// <summary>
    ///     Verifies the <c>complete</c> flag equals whether the gap list is empty.
    /// </summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <param name="violations">The violation list to append to.</param>
    /// <remarks>
    ///     The single boolean an agent branches on must be consistent with the gaps it summarizes
    ///     (<c>DD0722</c>). Pure.
    /// </remarks>
    private static void VerifyCompleteFlag(ExtractionManifest manifest, List<ContractViolation> violations)
    {
        // complete is defined as "there are no gaps"; enforce that definition
        var gapCount = manifest.Gaps?.Count ?? 0;
        if (manifest.Complete != (gapCount == 0))
        {
            violations.Add(new ContractViolation(DiagnosticCodes.CompleteFlagMismatch,
                $"complete is {manifest.Complete.ToString().ToLowerInvariant()} but there are {gapCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} gaps."));
        }
    }

    /// <summary>
    ///     Converts a manifest relative path to an absolute disk path.
    /// </summary>
    /// <param name="scratchFolder">The extraction folder.</param>
    /// <param name="relativePath">The forward-slash relative path from the manifest.</param>
    /// <returns>The absolute disk path.</returns>
    /// <remarks>Translates forward slashes to the platform separator so the lookup works on any OS. Pure.</remarks>
    private static string ToDiskPath(string scratchFolder, string relativePath) =>
        Path.Combine(scratchFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));

}

/// <summary>
///     The outcome of one attempt to read a directory: either the files it holds, or the reason the
///     read did not happen.
/// </summary>
/// <param name="Files">The absolute paths read, empty when the read failed.</param>
/// <param name="Path">The directory the attempt was made against.</param>
/// <param name="Error">The failure reason, or <see langword="null"/> when the read succeeded.</param>
/// <remarks>
///     The distinction this type exists to preserve is the difference between "this directory is
///     empty" and "I could not look inside this directory". Returning a bare list forces those two
///     facts into one value, and the verifier then reports an unknown as a clean result. Carrying
///     the failure alongside the listing makes that conflation impossible to express. Immutable and
///     thread-safe.
/// </remarks>
internal readonly record struct FileListing(IReadOnlyList<string> Files, string? Path, string? Error);

/// <summary>
///     The boundary through which <see cref="ContractVerifier"/> reads directories.
/// </summary>
/// <remarks>
///     Exists so the unreadable-directory path can be exercised deterministically by tests. It is
///     not a general abstraction over the file system and has exactly one production
///     implementation, <see cref="FileSystemDirectoryEnumerator"/>; nothing outside this file
///     depends on it except the tests that supply a failing enumerator.
/// </remarks>
internal interface IDirectoryEnumerator
{
    /// <summary>
    ///     Reads the files directly within a directory.
    /// </summary>
    /// <param name="directory">The directory to read.</param>
    /// <returns>The listing, or a failure result carrying the reason.</returns>
    FileListing TopLevel(string directory);

    /// <summary>
    ///     Reads every file beneath a directory, at any depth.
    /// </summary>
    /// <param name="root">The directory to walk.</param>
    /// <returns>The listing, or a failure result carrying the reason.</returns>
    FileListing Recursive(string root);
}

/// <summary>
///     The production <see cref="IDirectoryEnumerator"/>, reading the real file system.
/// </summary>
/// <remarks>
///     Converts an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> into a
///     <see cref="FileListing"/> carrying the path and the reason, so the verifier keeps its
///     report-rather-than-throw contract without ever losing the fact that a read failed. Stateless
///     and thread-safe.
/// </remarks>
internal sealed class FileSystemDirectoryEnumerator : IDirectoryEnumerator
{
    /// <summary>The shared instance used by the public verification entry point.</summary>
    /// <remarks>Stateless, so one instance serves every caller.</remarks>
    internal static readonly FileSystemDirectoryEnumerator Instance = new();

    /// <inheritdoc/>
    public FileListing TopLevel(string directory)
    {
        try
        {
            return new FileListing(Directory.GetFiles(directory), directory, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The read did not happen; say so rather than returning an empty listing
            return new FileListing([], directory, exception.Message);
        }
    }

    /// <inheritdoc/>
    public FileListing Recursive(string root)
    {
        try
        {
            return new FileListing(ArtifactInventory.EnumerateFiles(root), root, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A partially readable tree is not a verified tree; report the failure
            return new FileListing([], root, exception.Message);
        }
    }
}
