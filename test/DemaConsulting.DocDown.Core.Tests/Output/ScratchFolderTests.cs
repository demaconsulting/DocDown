using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Adversarial unit tests for <see cref="ScratchFolder"/>, the library's path-safety control.
/// </summary>
/// <remarks>
///     These tests prove containment, reserved-name rejection, path-length refusal, manifest-bound
///     safe reuse, and the two surviving preparation modes.
/// </remarks>
public class ScratchFolderTests
{
    /// <summary>
    ///     Proves a traversal, absolute, UNC, or rooted allocation is refused as an escape.
    /// </summary>
    /// <param name="hostilePath">The untrusted relative path an attacker might supply.</param>
    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("a/../../b")]
    [InlineData("....//")]
    [InlineData("../../etc/shadow")]
    [InlineData("images/../../escape")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("\\foo")]
    [InlineData("\\\\server\\share\\x")]
    public void ScratchFolder_Combine_TraversalOrRootedPath_IsRefused(string hostilePath)
    {
        // Arrange: a prepared scratch folder
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);

        // Act / Assert: every escape attempt is refused
        Assert.Throws<ScratchFolderException>(() => folder.Combine(hostilePath));
    }

    /// <summary>
    ///     Proves a legitimate relative allocation stays contained under the scratch folder.
    /// </summary>
    [Fact]
    public void ScratchFolder_Combine_LegitimateRelativePath_StaysContained()
    {
        // Arrange: a prepared scratch folder
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);

        // Act: combine a safe relative resource path
        var absolute = folder.Combine("images/0001-logo.png");

        // Assert: the result is rooted inside the scratch folder
        Assert.StartsWith(folder.AbsolutePath, absolute, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a colon or NUL injected into a component is refused.
    /// </summary>
    /// <param name="hostileComponent">The untrusted component carrying an injection character.</param>
    [Theory]
    [InlineData("images/pa:th.png")]
    [InlineData("images/na\0me.png")]
    public void ScratchFolder_Combine_InjectionCharacterInComponent_IsRefused(string hostileComponent)
    {
        // Arrange: a prepared scratch folder
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);

        // Act / Assert: injection characters are refused
        Assert.Throws<ScratchFolderException>(() => folder.Combine(hostileComponent));
    }

    /// <summary>
    ///     Proves reserved device names are detected on every platform.
    /// </summary>
    /// <param name="reserved">A component that resolves to a reserved device name.</param>
    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("con.txt")]
    [InlineData("com9.png")]
    [InlineData("NUL.")]
    [InlineData("LPT1 ")]
    [InlineData("aux.tar")]
    public void ScratchFolder_IsReservedDeviceName_ReservedComponent_ReturnsTrue(string reserved)
    {
        // Act / Assert: the device name is detected regardless of disguise
        Assert.True(ScratchFolder.IsReservedDeviceName(reserved));
    }

    /// <summary>
    ///     Proves ordinary names that merely resemble device names are not falsely rejected.
    /// </summary>
    /// <param name="ordinary">A safe component that must not be treated as reserved.</param>
    [Theory]
    [InlineData("console")]
    [InlineData("com")]
    [InlineData("com10")]
    [InlineData("lpt")]
    [InlineData("readme.txt")]
    [InlineData("communication")]
    public void ScratchFolder_IsReservedDeviceName_OrdinaryComponent_ReturnsFalse(string ordinary)
    {
        // Act / Assert: safe names remain allowed
        Assert.False(ScratchFolder.IsReservedDeviceName(ordinary));
    }

    /// <summary>
    ///     Proves combining a reserved device-name component is refused.
    /// </summary>
    [Fact]
    public void ScratchFolder_Combine_ReservedDeviceNameComponent_IsRefused()
    {
        // Arrange: a prepared scratch folder
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);

        // Act / Assert: an allocation naming a device is refused
        Assert.Throws<ScratchFolderException>(() => folder.Combine("images/CON.png"));
    }

    /// <summary>
    ///     Proves the ordinal prefix neutralizes reserved names, guarding the naming scheme.
    /// </summary>
    /// <param name="reservedStem">A reserved device-name stem that the ordinal prefix must defuse.</param>
    [Theory]
    [InlineData("con")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("lpt1")]
    [InlineData("prn")]
    [InlineData("aux")]
    public void ScratchFolder_IsReservedDeviceName_OrdinalPrefixedName_IsNotReserved(string reservedStem)
    {
        // Arrange: the ordinal-prefixed forms Core allocates
        var prefixedFile = "0001-" + reservedStem + ".png";
        var prefixedStem = "0001-" + reservedStem;

        // Act / Assert: the ordinal prefix defuses the reserved stem
        Assert.False(ScratchFolder.IsReservedDeviceName(prefixedFile));
        Assert.False(ScratchFolder.IsReservedDeviceName(prefixedStem));
    }

    /// <summary>
    ///     Proves an over-length absolute path is refused with a structured exception.
    /// </summary>
    [Fact]
    public void ScratchFolder_ValidateTotalPathLength_OverLimit_ThrowsScratchFolderException()
    {
        // Arrange: an absolute path beyond the total-length ceiling
        var overLong = "C:\\" + new string('x', ScratchFolder.MaxTotalPathLength + 10);

        // Act / Assert: the length ceiling is enforced as a scratch refusal
        var exception = Assert.Throws<ScratchFolderException>(() => ScratchFolder.ValidateTotalPathLength(overLong));
        Assert.Equal("pathTooLong", exception.Reason);
    }

    /// <summary>
    ///     Proves a deep allocation whose absolute path exceeds the ceiling fails with a scratch refusal.
    /// </summary>
    [Fact]
    public void ScratchFolder_Combine_OverLongTotalPath_ThrowsScratchFolderException()
    {
        // Arrange: a prepared scratch folder and a component that pushes the total path over the limit
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var longComponent = "images/" + new string('a', ScratchFolder.MaxTotalPathLength + 20) + ".png";

        // Act / Assert: the resulting path is refused as over-length
        var exception = Assert.Throws<ScratchFolderException>(() => folder.Combine(longComponent));
        Assert.Equal("pathTooLong", exception.Reason);
    }

    /// <summary>
    ///     Proves trailing dots and spaces are stripped by slugging.
    /// </summary>
    [Fact]
    public void ScratchFolder_Slugify_TrailingDotsAndSpaces_AreRemoved()
    {
        // Act / Assert: the unsafe trailing characters are removed
        Assert.Equal("report", ScratchFolder.Slugify("report...   "));
    }

    /// <summary>
    ///     Proves accented and punctuated input folds to a clean ASCII slug.
    /// </summary>
    [Fact]
    public void ScratchFolder_Slugify_AccentedAndPunctuated_ProducesCleanAsciiSlug()
    {
        // Act / Assert: accents fold away and punctuation collapses to hyphens
        Assert.Equal("hello-world", ScratchFolder.Slugify("Héllo,  World!!"));
    }

    /// <summary>
    ///     Proves the default clean-if-DocDown mode refuses a folder of unrelated files.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_CleanIfDocDownOnUserFolder_RefusesAndPreservesContents()
    {
        // Arrange: a non-empty folder that is not recognizable DocDown output
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(target);
        var userFile = Path.Combine(target, "thesis.md");
        File.WriteAllText(userFile, "irreplaceable work");

        // Act / Assert: the safe default refuses to clean it
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderNotDocDown", exception.Reason);
        Assert.True(File.Exists(userFile));
    }

    /// <summary>
    ///     Proves a legitimate clean re-run deletes only the inventoried files and keeps empty directories.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_CleanIfDocDownOnDocDownFolder_CleansInventoriedFiles()
    {
        // Arrange: a folder whose manifest proves it is prior DocDown output for this very folder
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(target, "images"));
        File.WriteAllText(Path.Combine(target, "summary.txt"), "summary");
        File.WriteAllText(Path.Combine(target, "metadata.json"), "{}\n");
        File.WriteAllText(Path.Combine(target, "content.md"), "stale output");
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));

        // Act: prepare over the proven DocDown folder
        var folder = ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder);

        // Assert: the inventoried files were deleted while the empty directory remains
        Assert.False(File.Exists(Path.Combine(folder.AbsolutePath, "summary.txt")));
        Assert.False(File.Exists(Path.Combine(folder.AbsolutePath, "manifest.json")));
        Assert.False(File.Exists(Path.Combine(folder.AbsolutePath, "metadata.json")));
        Assert.False(File.Exists(Path.Combine(folder.AbsolutePath, "content.md")));
        Assert.True(Directory.Exists(Path.Combine(folder.AbsolutePath, "images")));
    }

    /// <summary>
    ///     Proves a genuine manifest copied among the caller's files is refused because it names another folder.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_CopiedGenuineManifestAmongUserFiles_RefusesAndPreservesContents()
    {
        // Arrange: a documents folder containing a real manifest from some other run
        using var temp = new TempScratch();
        var otherRun = Path.Combine(temp.Path, "some-other-run");
        var target = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(target);
        var manifestPath = Path.Combine(target, "manifest.json");
        var userFile = Path.Combine(target, "thesis.md");
        File.WriteAllText(manifestPath, DocDownManifest(otherRun));
        File.WriteAllText(userFile, "irreplaceable work");

        // Act / Assert: the manifest proves the file, not this folder
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderPathMismatch", exception.Reason);
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(userFile));
    }

    /// <summary>
    ///     Proves a folder with an unaccounted file beside a genuine manifest is refused.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_UnaccountedFileBesideManifest_RefusesAndPreservesContents()
    {
        // Arrange: a genuine DocDown folder plus one extra file not listed by the manifest
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "summary.txt"), "summary");
        File.WriteAllText(Path.Combine(target, "metadata.json"), "{}\n");
        File.WriteAllText(Path.Combine(target, "content.md"), "stale output");
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        var rogue = Path.Combine(target, "rogue.txt");
        File.WriteAllText(rogue, "keep me");

        // Act / Assert: the unaccounted file prevents safe deletion
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderUnaccountedContent", exception.Reason);
        Assert.True(File.Exists(rogue));
    }

    /// <summary>
    ///     Proves a manifest that lists a path escaping the folder is refused.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_EscapingManifestPath_Refuses()
    {
        // Arrange: a manifest whose images list contains an escaping relative path
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "summary.txt"), "summary");
        File.WriteAllText(Path.Combine(target, "metadata.json"), "{}\n");
        File.WriteAllText(Path.Combine(target, "content.md"), "stale output");
        File.WriteAllText(
            Path.Combine(target, "manifest.json"),
            DocDownManifest(target, images: ["../escape.txt"]));

        // Act / Assert: the escaping manifest path is refused before any deletion occurs
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderManifestPathEscapes", exception.Reason);
    }

    /// <summary>
    ///     Proves a change detected between inventory and deletion aborts the preparation.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_FolderChangesDuringPreparation_Refuses()
    {
        // Arrange: a genuine DocDown folder and a reader that mutates summary.txt on its second read
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        var summaryPath = Path.Combine(target, "summary.txt");
        File.WriteAllText(summaryPath, "summary");
        File.WriteAllText(Path.Combine(target, "metadata.json"), "{}\n");
        File.WriteAllText(Path.Combine(target, "content.md"), "stale output");
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        var reader = new MutatingFileStateReader(summaryPath);

        // Act / Assert: the changed file is detected and preparation refuses
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder, reader));
        Assert.Equal("scratchFolderChangedDuringPreparation", exception.Reason);
    }

    /// <summary>
    ///     Proves overwrite mode clears any existing contents unconditionally.
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_OverwriteOnPopulatedFolder_ClearsContents()
    {
        // Arrange: a populated target folder
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "replace-me");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "user.txt"), "replace me");
        Directory.CreateDirectory(Path.Combine(target, "nested"));
        File.WriteAllText(Path.Combine(target, "nested", "deep.txt"), "replace me too");

        // Act: prepare under overwrite mode
        var folder = ScratchFolder.Prepare(target, ScratchFolderMode.Overwrite);

        // Assert: the contents were cleared and the folder itself remains ready for reuse
        Assert.Empty(Directory.GetFileSystemEntries(folder.AbsolutePath));
    }

    /// <summary>
    ///     Builds a minimal well-formed DocDown manifest bound to a given scratch folder.
    /// </summary>
    /// <param name="scratchFolder">The absolute folder the manifest records as its own.</param>
    /// <param name="images">The image paths to account for.</param>
    /// <param name="pages">The page paths to account for.</param>
    /// <param name="parts">The part paths to account for.</param>
    /// <returns>The manifest JSON body.</returns>
    private static string DocDownManifest(
        string scratchFolder,
        IReadOnlyList<string>? images = null,
        IReadOnlyList<string>? pages = null,
        IReadOnlyList<string>? parts = null)
    {
        var manifest = new
        {
            schemaVersion = "2.0",
            tool = new { name = "DocDown", package = "DemaConsulting.DocDown.Core" },
            scratchFolder,
            extractedAtUtc = "2024-01-02T03:04:05Z",
            status = "produced",
            source = new
            {
                path = (string?)null,
                fileName = "source.txt",
                sizeBytes = 5,
                sha256 = "0000",
                format = "text",
                mediaType = "text/plain",
                detectionBasis = "extension"
            },
            extractor = new { id = "text", displayName = "Text (stub)", package = "DemaConsulting.DocDown.TestSupport", priority = 0 },
            environment = new { operatingSystem = "TestOS", processArchitecture = "X64", runtimeVersion = "test-runtime", runtimeIdentifier = "test-rid", facts = Array.Empty<object>() },
            document = new { title = "Doc", author = (string?)null, pageCount = 1, partCount = (int?)null },
            contentFeatures = Array.Empty<object>(),
            images = (images ?? []).Select(path => new { path, mediaType = "image/png", widthPx = 1, heightPx = 1, sizeBytes = 1, sha256 = "11", sourcePage = 1, sourcePages = new[] { 1 }, referencedByTemplate = false, sourceRef = (string?)null, transform = "passthrough", references = 1, description = (string?)null, descriptionSource = (string?)null }).ToArray(),
            pages = (pages ?? []).Select(path => new { path, pageNumber = 1, sizeBytes = 1, sha256 = "22" }).ToArray(),
            parts = (parts ?? []).Select(path => new { path, kind = "section", ordinal = 1, title = "Part", characterCount = 1 }).ToArray(),
            notes = Array.Empty<string>(),
            requestedOptions = new
            {
                renderPages = false,
                pages = (string?)null,
                includeEmbeddedImages = true,
                imageOutput = "preserve",
                maxImageDimensionPx = (int?)null,
                maxImageBytes = (long?)null,
                pageRenderDpi = 150,
                contentSplit = "auto",
                scratchFolder = "cleanIfDocDownFolder"
            },
            failure = (object?)null
        };

        return JsonSerializer.Serialize(manifest);
    }

    /// <summary>
    ///     A file-state reader that mutates one file on its second read.
    /// </summary>
    /// <remarks>
    ///     This deterministically simulates a folder changing between the inventory pass and the
    ///     per-file deletion check.
    /// </remarks>
    private sealed class MutatingFileStateReader : IFileStateReader
    {
        /// <summary>The production reader used for the actual file-state snapshots.</summary>
        private readonly IFileStateReader _inner = FileSystemFileStateReader.Instance;

        /// <summary>The path to mutate on the second read.</summary>
        private readonly string _pathToMutate;

        /// <summary>The number of reads observed for the target path.</summary>
        private int _reads;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MutatingFileStateReader"/> class.
        /// </summary>
        /// <param name="pathToMutate">The file to rewrite on its second read.</param>
        public MutatingFileStateReader(string pathToMutate) => _pathToMutate = pathToMutate;

        /// <inheritdoc />
        public FileState Read(string path)
        {
            if (string.Equals(path, _pathToMutate, StringComparison.Ordinal))
            {
                _reads++;
                if (_reads == 2)
                {
                    File.WriteAllText(path, "mutated");
                }
            }

            return _inner.Read(path);
        }
    }
}
