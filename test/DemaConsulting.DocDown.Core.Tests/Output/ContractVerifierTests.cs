using System.Text.Json.Nodes;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="ContractVerifier"/>, proving that an honest extraction passes and
///     that every category of deliberately introduced dishonesty is reported rather than trusted or
///     thrown.
/// </summary>
/// <remarks>
///     These tests build a genuine extraction folder through the real writer chain (documented
///     dependencies of the verifier), then corrupt it — deleting a listed resource, adding an
///     unlisted file, tampering with bytes, editing the ledger, removing a gap's reason, renaming
///     the backend in the summary, and flipping the complete flag — and assert the expected
///     violation code. Each is named for the unit requirement it evidences: honest-extraction pass,
///     missing artifact, unlisted file, hash mismatch, ledger mismatch, unexplained absence, backend
///     mismatch, and report-rather-than-throw.
/// </remarks>
public class ContractVerifierTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves an honest extraction yields zero violations (HonestExtractionPasses).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_HonestExtraction_ReportsNoViolations()
    {
        // Arrange: a genuine extraction folder with text and one image
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);

        // Act: verify the untouched folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: an honest, internally consistent extraction has nothing to report
        Assert.Empty(violations);
    }

    /// <summary>
    ///     Proves a claimed-present <c>metadata.json</c> missing from disk is detected, the same
    ///     broken promise as any missing root artifact (DetectsMissingMetadata).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_MetadataJsonDeleted_DetectsMissingArtifact()
    {
        // Arrange: an honest folder from which the always-present metadata.json is deleted
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        File.Delete(Path.Combine(scratch, "metadata.json"));

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the ledger claims metadata.json present, so its absence is DD0715
        Assert.Contains(violations, violation => violation.Code == "DD0715");
    }

    /// <summary>
    ///     Proves a listed resource missing from disk is detected (DetectsMissingArtifact).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_ListedImageDeleted_DetectsMissingArtifact()
    {
        // Arrange: an honest folder from which the single listed image is deleted
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        File.Delete(Directory.GetFiles(Path.Combine(scratch, "images")).Single());

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the broken promise of a listed-but-absent resource is reported as DD0715
        Assert.Contains(violations, violation => violation.Code == "DD0715");
    }

    /// <summary>
    ///     Proves a file on disk not listed in the manifest is detected (DetectsUnlistedFile).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_UnlistedFileAdded_DetectsUnlistedFile()
    {
        // Arrange: an honest folder into which an extra, unlisted image is smuggled
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        await File.WriteAllTextAsync(Path.Combine(scratch, "images", "rogue.png"), "not listed", Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the silent, undocumented artifact is reported as DD0717
        Assert.Contains(violations, violation => violation.Code == "DD0717");
    }

    /// <summary>
    ///     Proves an unlisted file at the scratch root is detected (DetectsUnlistedFile).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_RootLevelUnlistedFile_ReportsUnlistedFile()
    {
        // Arrange: an honest folder that first verifies clean
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        Assert.Empty(ContractVerifier.Verify(scratch));

        // Arrange: exactly one divergence — a stray file dropped at the scratch root
        await File.WriteAllTextAsync(Path.Combine(scratch, "notes.md"), "smuggled in at the root", Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the root-level stray is reported, named by its relative path
        Assert.Contains(violations, violation => violation.Code == "DD0717");
        Assert.Contains(violations, violation => violation.Detail.Contains("notes.md", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves an unlisted file nested below a resource folder is detected (DetectsUnlistedFile).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_NestedUnlistedFileUnderImages_ReportsUnlistedFile()
    {
        // Arrange: an honest folder that first verifies clean
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        Assert.Empty(ContractVerifier.Verify(scratch));

        // Arrange: exactly one divergence — a file hidden one level below images/, invisible to a shallow walk
        var nested = Path.Combine(scratch, "images", "thumbs");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "hidden.png"), "nested and unlisted", Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the exhaustive walk sees it and reports the forward-slash relative path
        Assert.Contains(violations, violation => violation.Code == "DD0717");
        Assert.Contains(violations, violation => violation.Detail.Contains("images/thumbs/hidden.png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a manifest claiming a root artifact that is absent on disk is detected (DetectsMissingArtifact).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_ManifestClaimsSummaryPresentButAbsent_ReportsListedResourceMissing()
    {
        // Arrange: an honest folder that first verifies clean
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        Assert.Empty(ContractVerifier.Verify(scratch));

        // Arrange: exactly one divergence — the manifest still claims summary.txt is present, but it is gone
        File.Delete(Path.Combine(scratch, "summary.txt"));

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the forward direction catches the claim that disk cannot back
        Assert.Contains(violations, violation => violation.Code == "DD0715");
        Assert.Contains(violations, violation => violation.Detail.Contains("summary.txt", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a backend named only outside the Backend block does not satisfy the check (DetectsBackendMismatch).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_SummaryMentionsBackendOutsideTheBackendBlock_ReportsBackendMismatch()
    {
        // Arrange: an honest folder whose manifest records the 'text' backend
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        Assert.Empty(ContractVerifier.Verify(scratch));

        // Arrange: a summary whose authoritative Selected line names a different backend, while the
        // narrative below merely mentions the real one — exactly the incidental-substring spoof
        await File.WriteAllTextAsync(Path.Combine(scratch, "summary.txt"),
            "DocDown Extraction Summary\n\nBackend\n"
            + "  Selected  : other - Other Backend      [selected automatically]\n"
            + "  Why       : candidate 'text' (Text (stub)) was considered and rejected.\n", Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: only the exact Selected line counts, so the mismatch is reported as DD0721
        Assert.Contains(violations, violation => violation.Code == "DD0721");
    }

    /// <summary>
    ///     Proves tampered resource bytes are detected via a SHA-256 mismatch (DetectsHashMismatch).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_TamperedImageBytes_DetectsHashMismatch()
    {
        // Arrange: an honest folder whose listed image bytes are overwritten
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        var imagePath = Directory.GetFiles(Path.Combine(scratch, "images")).Single();
        await File.WriteAllBytesAsync(imagePath, [0xDE, 0xAD, 0xBE, 0xEF, 0x00], Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the size or hash mismatch against the recorded digest is reported as DD0716
        Assert.Contains(violations, violation => violation.Code == "DD0716");
    }

    /// <summary>
    ///     Proves a ledger obtained count that contradicts the filesystem is detected (DetectsLedgerMismatch).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_LedgerCountContradictsDisk_DetectsLedgerMismatch()
    {
        // Arrange: an honest folder whose images ledger claims more obtained than exist on disk
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        await MutateManifestAsync(scratch, root => root["artifacts"]!["images"]!["obtained"] = 99);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the ledger-versus-filesystem contradiction is reported as DD0718
        Assert.Contains(violations, violation => violation.Code == "DD0718");
    }

    /// <summary>
    ///     Proves a partial artifact with no matching gap is detected (DetectsUnexplainedAbsence).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_PartialArtifactWithoutGap_DetectsUnexplainedAbsence()
    {
        // Arrange: an honest clean folder whose content is edited to claim partial without any gap
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        await MutateManifestAsync(scratch, root => root["artifacts"]!["content"]!["status"] = "partial");

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the unexplained partial absence is reported as DD0719
        Assert.Contains(violations, violation => violation.Code == "DD0719");
    }

    /// <summary>
    ///     Proves the summary failing to name the manifest's backend is detected (DetectsBackendMismatch).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_SummaryOmitsBackend_DetectsBackendMismatch()
    {
        // Arrange: an honest folder whose summary is replaced with text naming no backend
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        await File.WriteAllTextAsync(Path.Combine(scratch, "summary.txt"), "DocDown Extraction Summary\nno backend named here\n", Ct);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the two documents disagreeing about the backend is reported as DD0721
        Assert.Contains(violations, violation => violation.Code == "DD0721");
    }

    /// <summary>
    ///     Proves a gap with a blanked reason is detected (DetectsUnexplainedAbsence boundary).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_GapReasonBlanked_DetectsEmptyReason()
    {
        // Arrange: an honest gapped folder whose first gap reason is blanked out
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteSilentImages);
        await MutateManifestAsync(scratch, root => root["gaps"]![0]!["reason"] = "");

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: a reasonless gap defeats the gap mechanism and is reported as DD0720
        Assert.Contains(violations, violation => violation.Code == "DD0720");
    }

    /// <summary>
    ///     Proves a flipped complete flag inconsistent with the gap list is detected (DetectsLedgerMismatch boundary).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_CompleteFlagFlipped_DetectsCompleteMismatch()
    {
        // Arrange: an honest clean folder whose complete flag is flipped to false with no gaps
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        await MutateManifestAsync(scratch, root => root["complete"] = false);

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the complete flag disagreeing with gaps.length == 0 is reported as DD0722
        Assert.Contains(violations, violation => violation.Code == "DD0722");
    }

    /// <summary>
    ///     Proves an unsupported schema version is detected (DetectsLedgerMismatch boundary).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_UnsupportedSchemaVersion_DetectsSchemaViolation()
    {
        // Arrange: an honest folder whose manifest declares a future schema version
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        await MutateManifestAsync(scratch, root => root["schemaVersion"] = "2.0");

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: an unexpected schema version is reported as DD0712
        Assert.Contains(violations, violation => violation.Code == "DD0712");
    }

    /// <summary>
    ///     Proves a missing mandatory field is detected (DetectsUnexplainedAbsence boundary).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_MandatoryFieldRemoved_DetectsMissingField()
    {
        // Arrange: an honest folder whose mandatory tool block is removed
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        await MutateManifestAsync(scratch, root => root.AsObject().Remove("tool"));

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the omitted mandatory block is reported as DD0713
        Assert.Contains(violations, violation => violation.Code == "DD0713");
    }

    /// <summary>
    ///     Proves a missing summary file is detected (DetectsMissingArtifact boundary).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_SummaryDeleted_DetectsMissingSummary()
    {
        // Arrange: an honest folder whose summary.txt is deleted
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteText);
        File.Delete(Path.Combine(scratch, "summary.txt"));

        // Act: verify the tampered folder
        var violations = ContractVerifier.Verify(scratch);

        // Assert: the missing invariant summary file is reported as DD0714
        Assert.Contains(violations, violation => violation.Code == "DD0714");
    }

    /// <summary>
    ///     Proves the verifier reports rather than throws for an absent folder and an unparsable manifest (ReportsRatherThanThrows).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_AbsentAndUnparsable_ReportsRatherThanThrows()
    {
        // Arrange: a completely absent folder and, separately, an unparsable manifest
        using var temp = new TempScratch();
        var absent = Path.Combine(temp.Path, "does-not-exist");
        var unparsable = Path.Combine(temp.Path, "unparsable");
        Directory.CreateDirectory(unparsable);
        await File.WriteAllTextAsync(Path.Combine(unparsable, "manifest.json"), "{ this is not valid json", Ct);

        // Act: verify both without any exception escaping
        var absentViolations = ContractVerifier.Verify(absent);
        var unparsableViolations = ContractVerifier.Verify(unparsable);

        // Assert: both are reported as data — a missing manifest and an unparsable one
        Assert.Contains(absentViolations, violation => violation.Code == "DD0710");
        Assert.Contains(unparsableViolations, violation => violation.Code == "DD0711");
    }

    /// <summary>
    ///     Proves an unreadable subtree is reported as incomplete verification (ReportsIncompleteVerification).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_UnreadableSubtree_ReportsVerificationIncomplete()
    {
        // Arrange: an honest folder whose recursive walk is blocked, as an unreadable subtree would block it
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);

        // Act: verify through an enumerator that cannot read the tree
        var violations = ContractVerifier.Verify(scratch, new BlockedDirectoryEnumerator(scratch));

        // Assert: the failure is stated explicitly, naming both the folder and the reason
        Assert.Contains(violations, violation => violation.Code == "DD0723");
        Assert.Contains(violations, violation => violation.Detail.Contains(scratch, StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Detail.Contains(BlockedDirectoryEnumerator.Reason, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves a blocked read is never reported as a clean pass (ReportsIncompleteVerification).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_UnreadableSubtree_IsNotACleanPass()
    {
        // Arrange: an otherwise honest folder that verifies clean when it can actually be read
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        Assert.Empty(ContractVerifier.Verify(scratch));

        // Act: verify the identical folder with the walk blocked
        var violations = ContractVerifier.Verify(scratch, new BlockedDirectoryEnumerator(scratch));

        // Assert: this is the executable form of the product principle — "I could not check" must
        // never be indistinguishable from "I checked and it was fine"
        Assert.NotEmpty(violations);
    }

    /// <summary>
    ///     Proves a blocked folder read reports incomplete rather than a phantom count (ReportsIncompleteVerification).
    /// </summary>
    [Fact]
    public async Task ContractVerifier_Verify_UnreadableResourceFolder_ReportsIncompleteWithoutFalseLedgerMismatch()
    {
        // Arrange: an honest folder with one listed image, whose images/ folder cannot be counted
        using var temp = new TempScratch();
        var scratch = await BuildHonestFolderAsync(temp, WriteTextAndImage);
        var images = Path.Combine(scratch, "images");

        // Act: verify with only the images/ read blocked, so the rest of the tree still reconciles
        var violations = ContractVerifier.Verify(scratch, new BlockedDirectoryEnumerator(images));

        // Assert: the unknown count is reported as unknown
        Assert.Contains(violations, violation => violation.Code == "DD0723");

        // Assert: and is never substituted with zero, which would contradict an honest ledger
        Assert.DoesNotContain(violations, violation => violation.Code == "DD0718");
    }

    /// <summary>
    ///     A directory enumerator that refuses to read one path and delegates everything else to the real file system.
    /// </summary>
    /// <param name="blockedPath">The single directory whose read fails.</param>
    /// <remarks>
    ///     The enumeration boundary is injected because an unreadable directory cannot be simulated
    ///     portably: <c>chmod 000</c> has no effect when the test process runs as root, which is
    ///     common in containers, and the Windows equivalent needs access-control edits that are not
    ///     available in every runner. Blocking exactly one path and delegating the rest keeps each
    ///     scenario to a single divergence from a genuine, otherwise honest folder.
    /// </remarks>
    private sealed class BlockedDirectoryEnumerator(string blockedPath) : IDirectoryEnumerator
    {
        /// <summary>The reason the blocked read reports, standing in for a real access failure.</summary>
        internal const string Reason = "Access to the path is denied.";

        /// <summary>The real file system, used for every path that is not blocked.</summary>
        private readonly IDirectoryEnumerator _real = FileSystemDirectoryEnumerator.Instance;

        /// <inheritdoc/>
        public FileListing TopLevel(string directory) =>
            IsBlocked(directory) ? new FileListing([], directory, Reason) : _real.TopLevel(directory);

        /// <inheritdoc/>
        public FileListing Recursive(string root) =>
            IsBlocked(root) ? new FileListing([], root, Reason) : _real.Recursive(root);

        /// <summary>Determines whether a path is the blocked one.</summary>
        /// <param name="path">The path being read.</param>
        /// <returns><see langword="true"/> when the read must fail.</returns>
        private bool IsBlocked(string path) =>
            string.Equals(Path.GetFullPath(path), Path.GetFullPath(blockedPath), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Builds a genuine extraction folder through the real writer chain for a write action.
    /// </summary>
    /// <param name="temp">The owning temporary folder.</param>
    /// <param name="write">The action that writes through the sink.</param>
    /// <returns>The absolute path of the honest extraction folder.</returns>
    /// <remarks>Writes summary and manifest so the verifier's cross-checks apply to an authentic run.</remarks>
    private static async Task<string> BuildHonestFolderAsync(TempScratch temp, Func<IExtractionSink, ValueTask> write)
    {
        var options = new ExtractionOptions { TimestampUtc = DateTimeOffset.UnixEpoch };
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, options);
        await write(sink);

        var descriptor = new ExtractorDescriptor("text", "Text (stub)", [DocumentFormat.Text], ExtractorCapabilities.Text, 0);
        var trace = new[] { new CandidateVerdict("text", "Text (stub)", 0, CandidateOutcome.Selected, "selected for the test") };
        var selection = new SelectionResult(descriptor, SelectionMode.Automatic, ExtractorCapabilities.Text, ExtractorCapabilities.Text, trace, null);
        var environment = new ExtractionEnvironment("TestOS", "X64", "test-runtime", "test-rid", []);
        var source = DocumentSource.FromFile(temp.CreateFile("source.txt", "hello"));
        var detection = new FormatDetection(DocumentFormat.Text, DetectionBasis.Extension, 0.5);
        var report = new ExtractionReport(
            ExtractionOutcome.Succeeded, source, "0000", detection, selection, environment, options, DateTimeOffset.UnixEpoch, null);

        var content = await ContentWriter.WriteAsync(sink, options.ContentSplit, "Document", Ct);
        var reconciliation = ManifestWriter.Reconcile(sink, report, content);
        await ManifestWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        await MetadataWriter.WriteAsync(folder, sink, Ct);
        await SummaryWriter.WriteAsync(folder, sink, report, content, reconciliation, Ct);
        return folder.AbsolutePath;
    }

    /// <summary>
    ///     Applies a mutation to the manifest JSON and writes it back to disk.
    /// </summary>
    /// <param name="scratch">The extraction folder whose manifest is mutated.</param>
    /// <param name="mutate">The mutation to apply to the parsed manifest root.</param>
    /// <returns>A task that completes when the mutated manifest has been written.</returns>
    /// <remarks>Editing the parsed node tree lets a test corrupt one field precisely without hand-writing JSON.</remarks>
    private static async Task MutateManifestAsync(string scratch, Action<JsonNode> mutate)
    {
        var path = Path.Combine(scratch, "manifest.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path, Ct))!;
        mutate(root);
        await File.WriteAllTextAsync(path, root.ToJsonString(), Ct);
    }

    /// <summary>
    ///     Writes a small text document through the sink.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the text is written.</returns>
    /// <remarks>The clean write used by the ledger, backend, and complete-flag corruption scenarios.</remarks>
    private static ValueTask WriteText(IExtractionSink sink) =>
        sink.WriteContentAsync("# Document\n\nContract verifier content.\n", CancellationToken.None);

    /// <summary>
    ///     Writes text and a single image for an honest, verifiable extraction.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Produces a hashed image resource for the missing-artifact and hash-mismatch scenarios.</remarks>
    private static async ValueTask WriteTextAndImage(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nHonest content.\n", CancellationToken.None);
        using var png = new MemoryStream([10, 20, 30, 40, 50]);
        await sink.AddImageAsync(png, new ImageHint("figure", "image/png"), CancellationToken.None);
    }

    /// <summary>
    ///     Writes text and claims images were found without writing or explaining them.
    /// </summary>
    /// <param name="sink">The sink to write through.</param>
    /// <returns>A task that completes when the writes finish.</returns>
    /// <remarks>Produces a genuine synthesized gap for the empty-reason corruption scenario.</remarks>
    private static async ValueTask WriteSilentImages(IExtractionSink sink)
    {
        await sink.WriteContentAsync("# Document\n\nText present, images missing.\n", CancellationToken.None);
        sink.ReportFound(GapKind.Images, 2);
    }
}
