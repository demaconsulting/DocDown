using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Adversarial unit tests for <see cref="ScratchFolder"/>, the library's path-safety security
///     control (Correction C3). They prove containment, reserved-device-name rejection on every
///     platform, over-length refusal, deterministic slugging, the ordinal-prefix protection, and
///     all four preparation modes including the catastrophic-data-loss guard.
/// </summary>
/// <remarks>
///     These tests bind only to <see cref="ScratchFolder"/> and its refusal type
///     <see cref="ScratchFolderException"/>. Because scratch names originate in untrusted document
///     content, the tests deliberately supply hostile inputs — traversal sequences, absolute and
///     UNC paths, reserved device names, injection characters, Unicode-normalization collisions, and
///     over-length values — and assert the control refuses or neutralizes each. Each test is named
///     for the unit requirement it evidences: the four modes, path containment, reserved-name
///     rejection, over-length rejection, deterministic naming, and refusal reporting.
/// </remarks>
public class ScratchFolderTests
{
    /// <summary>
    ///     Builds a well-formed DocDown <c>manifest.json</c> body bound to a given scratch folder,
    ///     used as the positive reuse proof and as the base the adversarial fixtures mutate.
    /// </summary>
    /// <param name="scratchFolder">The absolute folder the manifest records as its own.</param>
    /// <returns>The manifest JSON body.</returns>
    /// <remarks>
    ///     Carries everything the reuse guard demands: the pinned <c>schemaVersion</c>, the exact
    ///     <c>tool.name</c> and the DocDown <c>tool.package</c> prefix, a real <c>artifacts</c>
    ///     ledger, and the recorded <c>scratchFolder</c>. The folder is a parameter because the
    ///     guard binds the manifest to the folder it names: a fixed literal would prove only that
    ///     the file is a DocDown manifest, which is precisely the property that is no longer
    ///     sufficient. Truncating it, renaming its tool, pointing it at another folder, or leaving
    ///     an unaccounted file beside it must each cause the guard to refuse — which is what the
    ///     adversarial tests below assert.
    /// </remarks>
    private static string DocDownManifest(string scratchFolder) =>
        "{\"schemaVersion\":\"1.2\"," +
        "\"tool\":{\"name\":\"DocDown\",\"package\":\"DemaConsulting.DocDown.Core\"}," +
        $"\"scratchFolder\":{JsonSerializer.Serialize(scratchFolder)},\"extractedAtUtc\":\"2024-01-02T03:04:05Z\"," +
        "\"status\":\"succeeded\",\"complete\":true," +
        "\"artifacts\":{" +
        "\"summary\":{\"path\":\"summary.txt\",\"status\":\"present\"}," +
        "\"manifest\":{\"path\":\"manifest.json\",\"status\":\"present\"}," +
        "\"metadata\":{\"path\":\"metadata.json\",\"status\":\"present\"}," +
        "\"content\":{\"path\":\"content.md\",\"status\":\"present\"}," +
        "\"images\":{\"path\":\"images/\",\"status\":\"absent\",\"obtained\":0}," +
        "\"pages\":{\"path\":\"pages/\",\"status\":\"absent\",\"obtained\":0}}}";

    /// <summary>
    ///     Proves a traversal, absolute, UNC, or rooted allocation is refused as an escape (PathContainment).
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

        // Act + Assert: every escape attempt is refused with a structured scratch-folder exception
        Assert.Throws<ScratchFolderException>(() => folder.Combine(hostilePath));
    }

    /// <summary>
    ///     Proves a legitimate relative allocation stays contained under the scratch folder (PathContainment).
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
    ///     Proves a colon or NUL injected into a component is refused (PathContainment).
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

        // Act + Assert: a colon or NUL is defense-in-depth rejected even after slugging would have removed it
        Assert.Throws<ScratchFolderException>(() => folder.Combine(hostileComponent));
    }

    /// <summary>
    ///     Proves reserved device names are detected on every platform, in all their disguises (RejectsReservedNames).
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
        // Act: test the component against the reserved-name control
        var isReserved = ScratchFolder.IsReservedDeviceName(reserved);

        // Assert: the device name is detected regardless of extension or trailing dots and spaces
        Assert.True(isReserved);
    }

    /// <summary>
    ///     Proves ordinary names that merely resemble device names are not falsely rejected (RejectsReservedNames).
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
        // Act: test the safe component against the reserved-name control
        var isReserved = ScratchFolder.IsReservedDeviceName(ordinary);

        // Assert: a name that only resembles a device is allowed
        Assert.False(isReserved);
    }

    /// <summary>
    ///     Proves combining a reserved device-name component is refused (RejectsReservedNames).
    /// </summary>
    [Fact]
    public void ScratchFolder_Combine_ReservedDeviceNameComponent_IsRefused()
    {
        // Arrange: a prepared scratch folder
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);

        // Act + Assert: an allocation naming a device is refused so output stays Windows-portable
        Assert.Throws<ScratchFolderException>(() => folder.Combine("images/CON.png"));
    }

    /// <summary>
    ///     Proves the <c>{ordinal:D4}-</c> prefix neutralizes reserved names, guarding the naming scheme (RejectsReservedNames).
    /// </summary>
    /// <param name="reservedStem">A reserved device-name stem that the ordinal prefix must defuse.</param>
    /// <remarks>
    ///     This is the deliberate belt-and-braces check: Core prefixes every allocated image and part
    ///     file name with four digits and a hyphen, which makes a reserved-name collision impossible
    ///     because <c>0001-CON</c> is not a device name. Asserting that property here means a future
    ///     change to the naming scheme that dropped the prefix would surface as a failing test rather
    ///     than silently removing this incidental reserved-name protection.
    /// </remarks>
    [Theory]
    [InlineData("con")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("lpt1")]
    [InlineData("prn")]
    [InlineData("aux")]
    public void ScratchFolder_IsReservedDeviceName_OrdinalPrefixedName_IsNotReserved(string reservedStem)
    {
        // Arrange: the ordinal-prefixed forms Core actually allocates for images and parts
        var prefixedFile = "0001-" + reservedStem + ".png";
        var prefixedStem = "0001-" + reservedStem;

        // Act + Assert: the four-digit-and-hyphen prefix makes the name safe even for a reserved stem
        Assert.False(ScratchFolder.IsReservedDeviceName(prefixedFile));
        Assert.False(ScratchFolder.IsReservedDeviceName(prefixedStem));
    }

    /// <summary>
    ///     Proves an over-length absolute path is refused with a structured exception (RejectsOverlongPaths).
    /// </summary>
    [Fact]
    public void ScratchFolder_ValidateTotalPathLength_OverLimit_ThrowsScratchFolderException()
    {
        // Arrange: an absolute path well beyond the total-length ceiling
        var overLong = "C:\\" + new string('x', ScratchFolder.MaxTotalPathLength + 10);

        // Act + Assert: the length ceiling is enforced with a classifiable refusal, never PathTooLongException
        var exception = Assert.Throws<ScratchFolderException>(() => ScratchFolder.ValidateTotalPathLength(overLong));
        Assert.IsNotType<PathTooLongException>(exception);
    }

    /// <summary>
    ///     Proves a deep allocation whose absolute path exceeds the ceiling fails with a scratch refusal (RejectsOverlongPaths).
    /// </summary>
    [Fact]
    public void ScratchFolder_Combine_OverLongTotalPath_ThrowsScratchFolderNotPathTooLong()
    {
        // Arrange: a prepared scratch folder and a component that pushes the total path over the limit
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var longComponent = "images/" + new string('a', ScratchFolder.MaxTotalPathLength + 20) + ".png";

        // Act + Assert: an over-length total path is refused as a scratch-folder exception, not an opaque PathTooLongException
        var exception = Assert.Throws<ScratchFolderException>(() => folder.Combine(longComponent));
        Assert.IsNotType<PathTooLongException>(exception);
    }

    /// <summary>
    ///     Proves an over-length title is truncated to the component-length limit (DeterministicNaming).
    /// </summary>
    [Fact]
    public void ScratchFolder_Slugify_OverLongTitle_TruncatesToComponentLimit()
    {
        // Arrange: a 300-character title of slug-safe characters
        var title = new string('a', 300);

        // Act: slug the title
        var slug = ScratchFolder.Slugify(title);

        // Assert: the slug is bounded to the 40-character component limit
        Assert.Equal(ScratchFolder.MaxComponentLength, slug.Length);
    }

    /// <summary>
    ///     Proves trailing dots and spaces are stripped by slugging (DeterministicNaming).
    /// </summary>
    [Fact]
    public void ScratchFolder_Slugify_TrailingDotsAndSpaces_AreRemoved()
    {
        // Act: slug a title padded with trailing dots and spaces
        var slug = ScratchFolder.Slugify("report...   ");

        // Assert: the unsafe trailing characters are gone, leaving a clean slug
        Assert.Equal("report", slug);
    }

    /// <summary>
    ///     Proves accented input folds to ASCII and non-alphanumeric runs collapse to single hyphens (DeterministicNaming).
    /// </summary>
    [Fact]
    public void ScratchFolder_Slugify_AccentedAndPunctuated_ProducesCleanAsciiSlug()
    {
        // Act: slug a title with accents, punctuation, and repeated separators
        var slug = ScratchFolder.Slugify("Héllo,  World!!");

        // Assert: the slug is lowercase ASCII with collapsed, trimmed hyphens
        Assert.Equal("hello-world", slug);
    }

    /// <summary>
    ///     Proves an input that reduces to nothing yields an empty slug for the caller to substitute (DeterministicNaming).
    /// </summary>
    [Fact]
    public void ScratchFolder_Slugify_OnlyPunctuation_ReturnsEmpty()
    {
        // Act: slug a value made only of separators and dots
        var slug = ScratchFolder.Slugify(" ... --- ");

        // Assert: nothing survives, so the slug is empty and naming falls to the caller's fallback
        Assert.Equal(string.Empty, slug);
    }

    /// <summary>
    ///     Proves NFC and NFD spellings of the same accented word slug identically (DeterministicNaming).
    /// </summary>
    /// <remarks>
    ///     The implementation guarantees the precomposed (NFC) <c>café</c> and the decomposed (NFD)
    ///     <c>cafe\u0301</c> slug to the <em>same</em> ASCII value, because slugging normalizes with
    ///     NFKD and drops the combining marks. That identical slug is what lets the sink resolve the
    ///     collision safely with a <c>-2</c> suffix rather than emitting two files whose names differ
    ///     only by invisible normalization.
    /// </remarks>
    [Fact]
    public void ScratchFolder_Slugify_UnicodeNormalizationForms_ProduceIdenticalSlug()
    {
        // Arrange: the same word spelled precomposed (NFC) and decomposed (NFD)
        const string composed = "caf\u00e9";
        const string decomposed = "cafe\u0301";

        // Act: slug both spellings
        var composedSlug = ScratchFolder.Slugify(composed);
        var decomposedSlug = ScratchFolder.Slugify(decomposed);

        // Assert: both fold to the identical safe ASCII slug
        Assert.Equal("cafe", composedSlug);
        Assert.Equal(composedSlug, decomposedSlug);
    }

    /// <summary>
    ///     Proves RequireEmpty accepts an absent or empty folder and creates it (ModeRequireEmpty).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_RequireEmptyOnAbsentFolder_CreatesFolder()
    {
        // Arrange: a target path that does not yet exist
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "fresh");

        // Act: prepare under the strict empty-only policy
        var folder = ScratchFolder.Prepare(target, ScratchFolderMode.RequireEmpty);

        // Assert: the folder now exists at the requested absolute path
        Assert.True(Directory.Exists(folder.AbsolutePath));
    }

    /// <summary>
    ///     Proves RequireEmpty refuses a non-empty folder without deleting anything (ModeRequireEmpty).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_RequireEmptyOnPopulatedFolder_RefusesAndPreservesContents()
    {
        // Arrange: a populated target folder
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "populated");
        Directory.CreateDirectory(target);
        var userFile = Path.Combine(target, "user.txt");
        File.WriteAllText(userFile, "keep me");

        // Act + Assert: the strict policy refuses rather than clobbering existing content
        Assert.Throws<ScratchFolderException>(() => ScratchFolder.Prepare(target, ScratchFolderMode.RequireEmpty));
        Assert.True(File.Exists(userFile));
    }

    /// <summary>
    ///     Proves CleanIfDocDownFolder refuses to clean a folder of unrelated files (ModeCleanIfDocDownFolder).
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

        // Act + Assert: the default mode's guard refuses, so the caller's data is never destroyed
        Assert.Throws<ScratchFolderException>(() => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.True(File.Exists(userFile));
    }

    /// <summary>
    ///     Proves a legitimate clean re-run still succeeds (ModeCleanIfDocDownFolder).
    /// </summary>
    /// <remarks>
    ///     <strong>This is a happy-path guard, not a discriminating test.</strong> It passes both
    ///     before and after the inventory-scoped reuse change, and is claimed as evidence only that
    ///     the tightened guard did not break legitimate reuse. The discriminating evidence for the
    ///     reuse rules is in the refusal tests that follow.
    /// </remarks>
    [Fact]
    public void ScratchFolder_Prepare_CleanIfDocDownOnDocDownFolder_CleansContents()
    {
        // Arrange: a folder whose manifest.json proves it is prior DocDown output of this very
        // folder, plus stale content and the empty images/ folder a previous run leaves behind
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(target, "images"));
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        var staleFile = Path.Combine(target, "content.md");
        File.WriteAllText(staleFile, "stale output");

        // Act: prepare over the provably-DocDown folder
        var folder = ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder);

        // Assert: the prior output was cleaned so the folder is empty and ready for a fresh run
        Assert.False(File.Exists(staleFile));
        Assert.False(File.Exists(Path.Combine(folder.AbsolutePath, "manifest.json")));

        // Assert: the leftover empty folder is tolerated, not deleted — nothing outside the inventory is removed
        Assert.True(Directory.Exists(Path.Combine(folder.AbsolutePath, "images")));
    }

    /// <summary>
    ///     Proves a genuine manifest copied among the caller's own files is refused (ReuseBoundToFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_CopiedGenuineManifestAmongUserFiles_RefusesAndPreservesContents()
    {
        // Arrange: the caller's documents folder into which a real DocDown manifest.json from some
        // other run has been copied — every structural condition holds, yet nothing here is ours
        using var temp = new TempScratch();
        var otherRun = Path.Combine(temp.Path, "some-other-run");
        var target = Path.Combine(temp.Path, "documents");
        Directory.CreateDirectory(target);
        var manifestPath = Path.Combine(target, "manifest.json");
        File.WriteAllText(manifestPath, DocDownManifest(otherRun));
        var userFile = Path.Combine(target, "thesis.md");
        File.WriteAllText(userFile, "irreplaceable work");
        var originalUserFile = File.ReadAllBytes(userFile);
        var originalManifest = File.ReadAllBytes(manifestPath);

        // Act + Assert: a valid manifest proves the file is ours, not that the folder is; the guard refuses
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderPathMismatch", exception.Reason);

        // Assert: every byte the caller had is still exactly where they left it
        Assert.Equal(originalUserFile, File.ReadAllBytes(userFile));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifestPath));
    }

    /// <summary>
    ///     Proves a manifest naming a different folder is refused even when it is alone (ReuseBoundToFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_ManifestNamingADifferentFolder_IsRefused()
    {
        // Arrange: a folder holding nothing but a well-formed manifest written for a sibling folder
        using var temp = new TempScratch();
        var sibling = Path.Combine(temp.Path, "sibling-run");
        var target = Path.Combine(temp.Path, "target-run");
        Directory.CreateDirectory(target);
        var manifestPath = Path.Combine(target, "manifest.json");
        File.WriteAllText(manifestPath, DocDownManifest(sibling));

        // Act + Assert: the binding is what makes reuse provable, so a mismatch is refused outright
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderPathMismatch", exception.Reason);

        // Assert: nothing was deleted on the way to the refusal
        Assert.True(File.Exists(manifestPath));
    }

    /// <summary>
    ///     Proves one unaccounted file in an otherwise genuine folder is refused (InventoryScopedDeletion).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_DocDownFolderWithOneUnlistedFile_RefusesAndPreservesContents()
    {
        // Arrange: a genuine, correctly bound prior run into which the caller has dropped one file
        // of their own — the folder is ours, but its contents are no longer only ours
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        File.WriteAllText(Path.Combine(target, "summary.txt"), "prior summary");
        File.WriteAllText(Path.Combine(target, "content.md"), "prior content");
        var userFile = Path.Combine(target, "thesis.md");
        File.WriteAllText(userFile, "irreplaceable work");
        var originalUserFile = File.ReadAllBytes(userFile);

        // Act + Assert: an unaccounted file means deletion cannot be proved safe, so nothing is deleted
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderUnaccountedContent", exception.Reason);

        // Assert: refusal is total — the run's own artifacts are preserved alongside the caller's file
        Assert.Equal(originalUserFile, File.ReadAllBytes(userFile));
        Assert.True(File.Exists(Path.Combine(target, "summary.txt")));
        Assert.True(File.Exists(Path.Combine(target, "content.md")));
        Assert.True(File.Exists(Path.Combine(target, "manifest.json")));
    }

    /// <summary>
    ///     Proves a manifest listing a path outside the folder is refused (InventoryScopedDeletion).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_ManifestListingAnEscapingPath_IsRefused()
    {
        // Arrange: a correctly bound manifest whose image list has been hand-authored to reach two
        // levels above the scratch folder, at a file that has nothing to do with DocDown
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "nested", "run");
        Directory.CreateDirectory(target);
        var victim = Path.Combine(temp.Path, "evil.png");
        File.WriteAllText(victim, "someone else's file");
        var originalVictim = File.ReadAllBytes(victim);
        var manifest = DocDownManifest(target).Replace(
            "\"status\":\"succeeded\"",
            "\"images\":[{\"path\":\"../../evil.png\",\"mediaType\":\"image/png\",\"sizeBytes\":1,\"sha256\":\"00\",\"transform\":\"passthrough\",\"references\":1}],\"status\":\"succeeded\"",
            StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(target, "manifest.json"), manifest);

        // Act + Assert: every inventoried path is routed through the containment gate before any
        // deletion happens, so an escaping entry refuses the whole operation
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderManifestPathEscapes", exception.Reason);

        // Assert: the file outside the scratch folder was never reachable
        Assert.Equal(originalVictim, File.ReadAllBytes(victim));
    }

    /// <summary>
    ///     Proves a refusal names the deliberate escape hatch (ReuseBoundToFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_RefusedReuse_NamesTheOverwriteEscapeHatch()
    {
        // Arrange: the unaccounted-content fixture — a genuine bound run plus one file of the caller's
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        File.WriteAllText(Path.Combine(target, "thesis.md"), "irreplaceable work");

        // Act: capture the refusal
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));

        // Assert: a refusal a caller cannot act on is a dead end, so the message names the way forward
        Assert.Contains("Overwrite", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a legitimate re-run is not refused over cosmetic path differences (ReuseBoundToFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_ManifestPathDifferingOnlyCosmetically_StillCleans()
    {
        // Arrange: a manifest recording the same folder written with a redundant '.' segment and a
        // trailing separator — the same folder by any reading, so refusing it would be a false alarm
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        var cosmetic = Path.Combine(temp.Path, ".", "prior-run") + Path.DirectorySeparatorChar;
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(cosmetic));
        var staleFile = Path.Combine(target, "content.md");
        File.WriteAllText(staleFile, "stale output");

        // Act: prepare over the folder
        var folder = ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder);

        // Assert: both sides normalize to the same folder, so reuse proceeds and the stale output is gone
        Assert.False(File.Exists(staleFile));
        Assert.Equal(target, folder.AbsolutePath);
    }

    /// <summary>
    ///     Proves a folder that merely contains the word docdown is refused (ModeCleanIfDocDownFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_FolderMentioningDocDownInAPlainFile_IsRefused()
    {
        // Arrange: an ordinary notes folder whose text mentions docdown, plus the user's own hand-written
        // manifest.json note index; both mentions are incidental, and a substring fingerprint looking for
        // "tool" and DocDown would have accepted this folder and deleted the notes
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "notes");
        Directory.CreateDirectory(target);
        var userFile = Path.Combine(target, "notes.txt");
        File.WriteAllText(userFile, "reminder: evaluate docdown for the \"tool\" selection review");
        var decoyManifest = Path.Combine(target, "manifest.json");
        File.WriteAllText(decoyManifest, "{\"note\":\"scratch index for the DocDown evaluation\",\"tool\":{\"name\":\"pencil\"}}");
        var originalNotes = File.ReadAllBytes(userFile);
        var originalDecoy = File.ReadAllBytes(decoyManifest);

        // Act + Assert: an incidental mention is not proof — the note index parses as JSON but carries no
        // schemaVersion and names another tool, so the structural guard refuses
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderNotDocDown", exception.Reason);

        // Assert: both of the user's files are exactly as they were left
        Assert.True(File.Exists(userFile));
        Assert.Equal(originalNotes, File.ReadAllBytes(userFile));
        Assert.True(File.Exists(decoyManifest));
        Assert.Equal(originalDecoy, File.ReadAllBytes(decoyManifest));
    }

    /// <summary>
    ///     Proves an unrelated file that merely shares the manifest name is refused (ModeCleanIfDocDownFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_UnrelatedFileNamedManifestJson_IsRefused()
    {
        // Arrange: a folder holding another product's manifest.json that mentions DocDown in passing
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "acme-export");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"),
            "{\"tool\":\"AcmeExporter\",\"docs\":\"DocDown-like\"}");
        var userFile = Path.Combine(target, "export.csv");
        File.WriteAllText(userFile, "id,value\n1,2\n");

        // Act + Assert: the name alone is not proof; the structure must match
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderNotDocDown", exception.Reason);

        // Assert: the user's data survived
        Assert.True(File.Exists(userFile));
        Assert.Equal("id,value\n1,2\n", File.ReadAllText(userFile));
    }

    /// <summary>
    ///     Proves a corrupt or truncated DocDown manifest is refused rather than trusted (ModeCleanIfDocDownFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_TruncatedManifest_IsRefused()
    {
        // Arrange: the first 70 bytes of a real DocDown manifest — cut part-way through the tool block, as
        // a crashed run would leave behind, so the prefix still names DocDown and only structural parsing
        // can refuse it
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "half-written");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target)[..70]);
        var userFile = Path.Combine(target, "keep-me.txt");
        File.WriteAllText(userFile, "not ours to delete");
        var originalUserFile = File.ReadAllBytes(userFile);

        // Act + Assert: unparsable is not provable, so the guard refuses rather than guessing
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderNotDocDown", exception.Reason);

        // Assert: nothing in the folder was touched
        Assert.True(File.Exists(userFile));
        Assert.Equal(originalUserFile, File.ReadAllBytes(userFile));
    }

    /// <summary>
    ///     Proves a schema-shaped manifest from a different tool is refused (ModeCleanIfDocDownFolder).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_ManifestFromADifferentTool_IsRefused()
    {
        // Arrange: a manifest with DocDown's exact shape but another tool's identity
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "other-tool-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"),
            DocDownManifest(target).Replace("\"name\":\"DocDown\"", "\"name\":\"OtherTool\"", StringComparison.Ordinal));
        var userFile = Path.Combine(target, "other-tool-output.bin");
        File.WriteAllText(userFile, "another tool's work product");

        // Act + Assert: a matching shape is not a matching identity, so the guard refuses
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder));
        Assert.Equal("scratchFolderNotDocDown", exception.Reason);

        // Assert: the other tool's output survived
        Assert.True(File.Exists(userFile));
        Assert.Equal("another tool's work product", File.ReadAllText(userFile));
    }

    /// <summary>
    ///     Proves Overwrite unconditionally clears any existing contents (ModeOverwrite).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_OverwriteOnPopulatedFolder_ClearsContents()
    {
        // Arrange: a populated folder the caller has opted to clobber
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "clobber");
        Directory.CreateDirectory(target);
        var userFile = Path.Combine(target, "anything.txt");
        File.WriteAllText(userFile, "will be removed");

        // Act: prepare with the unconditional overwrite policy
        ScratchFolder.Prepare(target, ScratchFolderMode.Overwrite);

        // Assert: the existing content was deleted as explicitly requested
        Assert.False(File.Exists(userFile));
    }

    /// <summary>
    ///     Proves CreateUnique leaves an existing folder untouched and allocates a new name (ModeCreateUnique).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_CreateUniqueOnExistingFolder_AllocatesDistinctFolder()
    {
        // Arrange: an existing folder with a file that must survive
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "run");
        Directory.CreateDirectory(target);
        var userFile = Path.Combine(target, "existing.txt");
        File.WriteAllText(userFile, "untouched");

        // Act: prepare, which must pick a fresh sibling name rather than reuse the occupied one
        var folder = ScratchFolder.Prepare(target, ScratchFolderMode.CreateUnique);

        // Assert: a distinct folder was created and the original content is preserved
        Assert.NotEqual(Path.GetFullPath(target), folder.AbsolutePath);
        Assert.True(Directory.Exists(folder.AbsolutePath));
        Assert.True(File.Exists(userFile));
    }

    /// <summary>
    ///     Proves a refusal carries a stable, non-empty reason for the engine to classify (RefusalReported).
    /// </summary>
    [Fact]
    public void ScratchFolder_Prepare_RefusedFolder_ReportsStableReason()
    {
        // Arrange: a non-empty folder prepared under the strict RequireEmpty policy
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "busy");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "occupied.txt"), "content");

        // Act: capture the refusal
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.RequireEmpty));

        // Assert: the refusal exposes a stable reason distinct from the display message
        Assert.Equal("scratchFolderNotEmpty", exception.Reason);
    }

    /// <summary>
    ///     Proves an over-length total path refusal reports the length reason (RefusalReported).
    /// </summary>
    [Fact]
    public void ScratchFolder_ValidateTotalPathLength_OverLimit_ReportsPathTooLongReason()
    {
        // Arrange: an absolute path beyond the ceiling
        var overLong = "C:\\" + new string('y', ScratchFolder.MaxTotalPathLength + 5);

        // Act: capture the refusal
        var exception = Assert.Throws<ScratchFolderException>(() => ScratchFolder.ValidateTotalPathLength(overLong));

        // Assert: the refusal names the length cause for programmatic classification
        Assert.Equal("pathTooLong", exception.Reason);
    }

    /// <summary>
    ///     Proves a file changed between the inventory scan and its deletion is refused, not deleted
    ///     (RevalidatedBeforeDeletion).
    /// </summary>
    /// <remarks>
    ///     The mutation is a genuine append to a real file on disk, performed through the injected
    ///     reader at the instant the production code re-reads that file. A reader that merely
    ///     returned invented values would assert something about a fiction rather than about the
    ///     folder, which is the property that matters here.
    /// </remarks>
    [Fact]
    public void ScratchFolder_Prepare_InventoriedFileChangedBetweenScanAndDelete_RefusesAndPreservesTheChange()
    {
        // Arrange: a genuine, correctly bound prior run, and a reader that really appends to
        // content.md between the scan that approved it and the deletion that would remove it
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        var contended = Path.Combine(target, "content.md");
        File.WriteAllText(contended, "stale output");
        var reader = new MutatingFileStateReader(contended, 2, () => File.AppendAllText(contended, "!"));

        // Act + Assert: the state no longer matches what was inventoried, so the guard refuses
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder, reader));
        Assert.Equal("scratchFolderChangedDuringPreparation", exception.Reason);

        // Assert: the concurrently written bytes are still on disk, and the caller is told the way forward
        Assert.True(File.Exists(contended));
        Assert.Equal("stale output!", File.ReadAllText(contended));
        Assert.Contains("Overwrite", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a file created at an inventoried path after the scan is refused, not deleted
    ///     (RevalidatedBeforeDeletion).
    /// </summary>
    /// <remarks>
    ///     This is the attack the check exists for, reproduced exactly: an inventoried path that
    ///     held nothing when the folder was approved holds a file by the time the deletion reaches
    ///     it. It also pins that absence-to-presence counts as a divergence, which a comparison of
    ///     length and last-write time alone would not catch.
    /// </remarks>
    [Fact]
    public void ScratchFolder_Prepare_InventoriedPathCreatedAfterTheScan_RefusesAndPreservesTheNewFile()
    {
        // Arrange: a bound manifest inventorying an image that is not on disk, and a reader that
        // really creates that file at the moment the production code reads its state before deleting
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        var manifest = DocDownManifest(target).Replace(
            "\"status\":\"succeeded\"",
            "\"images\":[{\"path\":\"images/0001-late.png\",\"mediaType\":\"image/png\",\"sizeBytes\":1,\"sha256\":\"00\",\"transform\":\"passthrough\",\"references\":1}],\"status\":\"succeeded\"",
            StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(target, "manifest.json"), manifest);
        var latecomer = Path.Combine(target, "images/0001-late.png");
        // The path is spelled exactly as the guard forms it from the manifest's forward-slash entry,
        // so the reader recognizes the very path the production code asks it about
        var reader = new MutatingFileStateReader(latecomer, 1, () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(latecomer)!);
            File.WriteAllText(latecomer, "arrived after the scan");
        });

        // Act + Assert: nothing was there when the folder was approved, so the file that is there
        // now was never covered by that approval and must not be deleted on the strength of it
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder, reader));
        Assert.Equal("scratchFolderChangedDuringPreparation", exception.Reason);

        // Assert: the newly created file survives with its bytes intact
        Assert.True(File.Exists(latecomer));
        Assert.Equal("arrived after the scan", File.ReadAllText(latecomer));
    }

    /// <summary>
    ///     Proves a rewrite that changes only the last-write time is refused (RevalidatedBeforeDeletion).
    /// </summary>
    /// <remarks>
    ///     Isolates the timestamp half of the comparison: the bytes and the length are identical, so
    ///     dropping the last-write-time comparison would leave this scenario silently deleted while
    ///     the other two scenarios still refused. That is what earns this test its place rather than
    ///     making it a restatement of the changed-content test.
    /// </remarks>
    [Fact]
    public void ScratchFolder_Prepare_InventoriedFileTouchedBetweenScanAndDelete_RefusesOnTimestampAlone()
    {
        // Arrange: a genuine, correctly bound prior run, and a reader that rewrites content.md with
        // byte-for-byte identical content and advances its real last-write time on disk
        using var temp = new TempScratch();
        var target = Path.Combine(temp.Path, "prior-run");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "manifest.json"), DocDownManifest(target));
        var contended = Path.Combine(target, "content.md");
        File.WriteAllText(contended, "stale output");
        var advanced = File.GetLastWriteTimeUtc(contended).AddMinutes(5);
        var reader = new MutatingFileStateReader(contended, 2, () =>
        {
            File.WriteAllText(contended, "stale output");
            File.SetLastWriteTimeUtc(contended, advanced);
        });

        // Act + Assert: the length is unchanged, so only the timestamp can reveal the rewrite
        var exception = Assert.Throws<ScratchFolderException>(
            () => ScratchFolder.Prepare(target, ScratchFolderMode.CleanIfDocDownFolder, reader));
        Assert.Equal("scratchFolderChangedDuringPreparation", exception.Reason);

        // Assert: the rewritten file survives, still with the advanced time that exposed the change
        Assert.True(File.Exists(contended));
        Assert.Equal("stale output", File.ReadAllText(contended));
        Assert.Equal(advanced, File.GetLastWriteTimeUtc(contended));
    }

    /// <summary>
    ///     An <see cref="IFileStateReader"/> that reads the real file system and performs one genuine
    ///     on-disk change at a chosen point in the reading sequence.
    /// </summary>
    /// <remarks>
    ///     The production code reads a target's state during the inventory scan and again
    ///     immediately before deleting it, and the interval between those two readings is what the
    ///     guard has to cover. No test can reliably win that race from another thread, so the change
    ///     is injected at the boundary instead — but it is a real write to a real file, and every
    ///     reading returned is the file system's own. The alternative, a reader that fabricated a
    ///     differing value, would prove only that the comparison compares values.
    /// </remarks>
    private sealed class MutatingFileStateReader : IFileStateReader
    {
        /// <summary>The absolute path whose readings are counted and which the change applies to.</summary>
        private readonly string _path;

        /// <summary>The one-based reading of <see cref="_path"/> to apply the change before.</summary>
        /// <remarks>
        ///     Two for a path the scan itself reads, one for a path the scan never sees because no
        ///     file is there — the created-after-the-scan case, where the deletion read is the first.
        /// </remarks>
        private readonly int _mutateBeforeRead;

        /// <summary>The real on-disk change to perform.</summary>
        private readonly Action _mutate;

        /// <summary>How many times <see cref="_path"/> has been read so far.</summary>
        private int _reads;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MutatingFileStateReader"/> class.
        /// </summary>
        /// <param name="path">The absolute path to watch and change.</param>
        /// <param name="mutateBeforeRead">The one-based reading of that path to change the file before.</param>
        /// <param name="mutate">The real on-disk change to perform.</param>
        internal MutatingFileStateReader(string path, int mutateBeforeRead, Action mutate)
        {
            _path = path;
            _mutateBeforeRead = mutateBeforeRead;
            _mutate = mutate;
        }

        /// <inheritdoc/>
        public FileState Read(string path)
        {
            // Change the file on disk just before the chosen reading of the watched path
            if (string.Equals(path, _path, StringComparison.Ordinal) && ++_reads == _mutateBeforeRead)
            {
                _mutate();
            }

            // Every value returned is the real file system's, taken after any change was applied
            return FileSystemFileStateReader.Instance.Read(path);
        }
    }
}
