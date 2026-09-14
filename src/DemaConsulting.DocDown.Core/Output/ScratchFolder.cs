using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DocDown.Core;

/// <summary>
///     Owns the absolute output directory for one extraction and is the single gate through which
///     every Core-side path is allocated, validated, and contained.
/// </summary>
/// <remarks>
///     <para>
///         This type is a security control, not a convenience wrapper. Image names, part names,
///         sheet and slide titles, and attachment file names all originate in <em>untrusted
///         document content</em>, so every allocated path is routed through <see cref="Combine"/>,
///         which enforces directory containment, rejects reserved device names, bounds the total
///         path length, and refuses separator or colon injection. Containment alone is deliberately
///         treated as necessary but not sufficient: the reserved-name and length checks catch
///         attacks that a pure containment check would let through.
///     </para>
///     <para>
///         The reserved-device-name and length checks run on <em>all</em> platforms — including
///         Linux — so that output produced on one operating system stays portable to Windows, where
///         those names and lengths are hard errors. The <c>{ordinal:D4}-</c> prefix Core applies to
///         file names makes a reserved-name collision impossible in practice today, but the check is
///         kept explicit and unconditional so a future naming change cannot silently remove the
///         control.
///     </para>
///     <para>
///         <see cref="Prepare(string,ScratchFolderMode)"/> performs filesystem I/O (creating and, per mode, cleaning the
///         folder); the static helpers (<see cref="SafePathCombine"/>, <see cref="Slugify"/>,
///         <see cref="IsReservedDeviceName"/>, <see cref="ValidateTotalPathLength"/>) are pure
///         string operations that perform no I/O. Refusals are surfaced as
///         <see cref="ScratchFolderException"/> so the engine can convert them into a structured
///         failure rather than leaking an opaque <see cref="PathTooLongException"/> or
///         <see cref="IOException"/>. An instance is immutable after
///         <see cref="Prepare(string,ScratchFolderMode)"/> returns and its members are safe for concurrent
///         reads; the folder-mutating work happens only during preparation.
///     </para>
/// </remarks>
public sealed class ScratchFolder
{
    /// <summary>
    ///     The maximum length, in characters, of a single slugged path component.
    /// </summary>
    /// <remarks>
    ///     Bounds the slug so a pathological title cannot produce an unwieldy file name; exposed as
    ///     a constant so callers and tests can reference the same limit.
    /// </remarks>
    public const int MaxComponentLength = 40;

    /// <summary>
    ///     The maximum length, in characters, of any allocated absolute path.
    /// </summary>
    /// <remarks>
    ///     Per-component truncation does not bound the <em>total</em> path length, so this explicit
    ///     ceiling is enforced separately. 240 leaves headroom under the classic Windows
    ///     <c>MAX_PATH</c> of 260 for the file name and separators.
    /// </remarks>
    public const int MaxTotalPathLength = 240;

    /// <summary>
    ///     The maximum size, in bytes, of a <c>manifest.json</c> the reuse guard will parse.
    /// </summary>
    /// <remarks>
    ///     Bounds the read so a huge or hostile file cannot be used to exhaust memory while probing
    ///     a folder's <c>manifest.json</c>. 8 MiB is far beyond any manifest a real extraction
    ///     produces, so a legitimate folder is never rejected on size.
    /// </remarks>
    private const long MaxManifestProbeBytes = 8L * 1024 * 1024;

    /// <summary>
    ///     The exact tool name a DocDown manifest carries.
    /// </summary>
    /// <remarks>Matched with an ordinal comparison and in full — never as a substring — so a manifest that merely mentions DocDown is refused.</remarks>
    private const string DocDownToolName = "DocDown";

    /// <summary>
    ///     The package-name prefix every DocDown manifest carries.
    /// </summary>
    /// <remarks>A prefix rather than an exact match so a manifest written by any DocDown package is recognized.</remarks>
    private const string DocDownPackagePrefix = "DemaConsulting.DocDown";

    /// <summary>
    ///     The manifest schema version this library recognizes as its own output.
    /// </summary>
    /// <remarks>
    ///     Pinned deliberately: a folder written by a future, unrecognized schema is not provably
    ///     ours to delete, so it is refused rather than cleaned.
    /// </remarks>
    private const string SupportedManifestSchema = "3.0";

    /// <summary>
    ///     The set of reserved device-name stems, compared case-insensitively.
    /// </summary>
    /// <remarks>
    ///     Held as a case-insensitive lookup so the reserved-name check is a constant-time membership
    ///     test. Includes <c>CON</c>, <c>PRN</c>, <c>AUX</c>, <c>NUL</c>, and <c>COM0</c>-<c>COM9</c>
    ///     and <c>LPT0</c>-<c>LPT9</c>. Membership order is irrelevant, so a hash set is safe here
    ///     (it is never enumerated for output).
    /// </remarks>
    private static readonly HashSet<string> ReservedNames = BuildReservedNames();

    /// <summary>
    ///     The absolute, normalized path of the prepared scratch folder.
    /// </summary>
    /// <remarks>Captured once in the constructor so the instance is immutable after preparation.</remarks>
    private readonly string _absolutePath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolder"/> class.
    /// </summary>
    /// <param name="absolutePath">The absolute, already-validated folder path.</param>
    /// <remarks>
    ///     Private so instances can only be produced by <see cref="Prepare(string,ScratchFolderMode)"/>, which guarantees the
    ///     path has been normalized, length-checked, and created on disk.
    /// </remarks>
    private ScratchFolder(string absolutePath) => _absolutePath = absolutePath;

    /// <summary>
    ///     Gets the absolute path of the prepared scratch folder.
    /// </summary>
    /// <remarks>
    ///     This is the single most important value in the product: it is the location reported at
    ///     the top of <c>summary.txt</c> and in the manifest so a consumer knows exactly where the
    ///     output lives.
    /// </remarks>
    public string AbsolutePath => _absolutePath;

    /// <summary>
    ///     Prepares the scratch folder according to the requested mode, creating it on disk.
    /// </summary>
    /// <param name="requestedPath">The caller-requested folder path. Must not be null or empty.</param>
    /// <param name="mode">The preparation policy governing whether existing contents may be removed.</param>
    /// <returns>A prepared <see cref="ScratchFolder"/> whose <see cref="AbsolutePath"/> exists on disk.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="requestedPath"/> is null or empty.</exception>
    /// <exception cref="ScratchFolderException">
    ///     Thrown when the folder cannot be prepared safely: a folder under
    ///     <see cref="ScratchFolderMode.CleanIfDocDownFolder"/> that is not provably this run's
    ///     own prior output, an over-length path, or an underlying I/O error while creating or
    ///     cleaning the folder.
    /// </exception>
    /// <remarks>
    ///     Performs filesystem I/O: it may create the folder and, depending on
    ///     <paramref name="mode"/>, delete files within it. The <c>CleanIfDocDownFolder</c> guard
    ///     never empties a folder: it deletes only the specific files a manifest written for
    ///     <em>this</em> folder inventories, and refuses outright if anything else is present. That
    ///     is what prevents an accidental <c>--scratch</c> pointed at a documents folder from
    ///     destroying unrelated files. <see cref="ScratchFolderMode.Overwrite"/> is the explicit
    ///     opt-in for unconditional clearing.
    /// </remarks>
    public static ScratchFolder Prepare(string requestedPath, ScratchFolderMode mode)
    {
        // A scratch folder must have an identity before any policy can be applied
        ArgumentException.ThrowIfNullOrEmpty(requestedPath);

        return Prepare(requestedPath, mode, FileSystemFileStateReader.Instance);
    }

    /// <summary>
    ///     Prepares the scratch folder through a supplied file-state boundary.
    /// </summary>
    /// <param name="requestedPath">The caller-requested folder path. Must not be null or empty.</param>
    /// <param name="mode">The preparation policy governing whether existing contents may be removed.</param>
    /// <param name="reader">The boundary through which a file's state is read before it is deleted.</param>
    /// <returns>A prepared <see cref="ScratchFolder"/> whose <see cref="AbsolutePath"/> exists on disk.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="requestedPath"/> is null or empty.</exception>
    /// <exception cref="ScratchFolderException">Thrown for every refusal described on <see cref="Prepare(string,ScratchFolderMode)"/>.</exception>
    /// <remarks>
    ///     The implementation behind the public <see cref="Prepare(string,ScratchFolderMode)"/> entry
    ///     point. It is <see langword="internal"/> solely so tests can supply a reader that mutates
    ///     the folder between the inventory scan and the deletion, which is the only deterministic
    ///     way to exercise the changed-during-preparation refusal: winning a genuine race would
    ///     produce a flaky test that proves nothing on the runs it loses. The production path always
    ///     uses <see cref="FileSystemFileStateReader"/>. Performs filesystem I/O.
    /// </remarks>
    internal static ScratchFolder Prepare(string requestedPath, ScratchFolderMode mode, IFileStateReader reader)
    {
        // A scratch folder must have an identity before any policy can be applied
        ArgumentException.ThrowIfNullOrEmpty(requestedPath);

        try
        {
            // Normalize to an absolute path and bound its length before touching the filesystem
            var absolute = Path.GetFullPath(requestedPath);
            ValidateTotalPathLength(absolute);

            // Resolve the effective folder per the requested policy, then guarantee it exists
            var resolved = ApplyMode(absolute, mode, reader);
            Directory.CreateDirectory(resolved);
            return new ScratchFolder(resolved);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Convert low-level failures into a structured refusal the engine can classify
            throw new ScratchFolderException("scratchFolderIoError", $"The scratch folder could not be prepared: {exception.Message}");
        }
    }

    /// <summary>
    ///     Combines a relative, forward-slash path onto the scratch folder with full containment and
    ///     component validation.
    /// </summary>
    /// <param name="relativePath">
    ///     The relative path to allocate (for example <c>images/0001-logo.png</c>). Must not be null
    ///     or empty and must use forward slashes.
    /// </param>
    /// <returns>The absolute, contained path corresponding to <paramref name="relativePath"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativePath"/> is null or empty.</exception>
    /// <exception cref="ScratchFolderException">
    ///     Thrown when the path escapes the scratch folder, contains a reserved device name, contains
    ///     a colon, backslash, or NUL, or produces an over-length absolute path.
    /// </exception>
    /// <remarks>
    ///     Every Core-side allocation flows through this method, so it is where the untrusted-content
    ///     defenses live. It validates each path component individually (reserved names, injection
    ///     characters) before delegating to <see cref="SafePathCombine"/> for the containment check
    ///     and finally bounding the total length. It performs no I/O — creation is the caller's job
    ///     via <see cref="EnsureSubfolder"/> or a subsequent write.
    /// </remarks>
    public string Combine(string relativePath)
    {
        // A relative path is required; an empty allocation is a caller error
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        // Reject a rooted or backslash-bearing input up front; allocations are forward-slash relative
        if (relativePath.Contains('\\', StringComparison.Ordinal))
        {
            throw new ScratchFolderException("invalidPathComponent", $"The relative path must not contain a backslash: '{relativePath}'.");
        }

        // Validate each component so reserved names and injection characters are caught before combining
        foreach (var component in relativePath.Split('/'))
        {
            ValidateComponent(component, relativePath);
        }

        // Containment check: the resolved path must remain inside the scratch folder
        var combined = SafePathCombine(_absolutePath, relativePath);
        var absolute = Path.GetFullPath(combined);

        // The full allocated path must fit within the total-length ceiling
        ValidateTotalPathLength(absolute);
        return absolute;
    }

    /// <summary>
    ///     Ensures a relative subfolder exists under the scratch folder and returns its absolute path.
    /// </summary>
    /// <param name="relativeFolder">The relative folder to create (for example <c>images</c>).</param>
    /// <returns>The absolute path of the created (or already-existing) subfolder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativeFolder"/> is null or empty.</exception>
    /// <exception cref="ScratchFolderException">Thrown when the folder fails the containment or component checks, or cannot be created.</exception>
    /// <remarks>
    ///     Validates through <see cref="Combine"/> before creating, so the same untrusted-content
    ///     defenses apply to folders as to files. Performs filesystem I/O by creating the directory.
    /// </remarks>
    public string EnsureSubfolder(string relativeFolder)
    {
        // Reuse the containment and component validation, then materialize the directory
        var absolute = Combine(relativeFolder);
        try
        {
            Directory.CreateDirectory(absolute);
            return absolute;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Surface a structured refusal rather than an opaque I/O error
            throw new ScratchFolderException("subfolderCreateFailed", $"The subfolder '{relativeFolder}' could not be created: {exception.Message}");
        }
    }

    /// <summary>
    ///     Writes text to a relative path with deterministic, cross-platform encoding.
    /// </summary>
    /// <param name="relativePath">The relative, forward-slash path to write (for example <c>content.md</c>).</param>
    /// <param name="text">The text content to write.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ScratchFolderException">Thrown when the path fails the containment or component checks.</exception>
    /// <remarks>
    ///     Centralizes text output for every writer so the determinism rules live in one place: line
    ///     endings are normalized to <c>\n</c> and the bytes are UTF-8 <em>without</em> a byte-order
    ///     mark, which is what makes <c>summary.txt</c> and <c>manifest.json</c> byte-identical across
    ///     platforms for the same content. Resolving through <see cref="Combine"/> applies the
    ///     containment and component defenses before any byte reaches disk. Performs filesystem I/O;
    ///     this method is <see langword="internal"/> because only the Output writers use it.
    /// </remarks>
    internal async ValueTask WriteTextAsync(string relativePath, string text, CancellationToken cancellationToken)
    {
        // Content is required; an accidental null must not silently write an empty file
        ArgumentNullException.ThrowIfNull(text);

        // Validate and contain the path, then ensure the parent folder exists
        var absolute = Combine(relativePath);
        var directory = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Normalize to \n and encode as UTF-8 without a BOM so output is byte-identical everywhere
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(normalized);
        await File.WriteAllBytesAsync(absolute, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Safely combines two paths, guaranteeing the result stays within the base directory.
    /// </summary>
    /// <param name="basePath">The base directory path. Must not be null.</param>
    /// <param name="relativePath">The relative path to combine. Must not be null.</param>
    /// <returns>The combined path, still rooted within <paramref name="basePath"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basePath"/> or <paramref name="relativePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ScratchFolderException">Thrown when the resolved path escapes the base directory.</exception>
    /// <remarks>
    ///     Ported from the reference <c>PathHelpers.SafePathCombine</c> but throwing
    ///     <see cref="ScratchFolderException"/> instead of <see cref="ArgumentException"/> so escape
    ///     attempts map onto the scratch-folder failure taxonomy. Uses
    ///     <see cref="Path.GetRelativePath(string,string)"/> to detect traversal in a way that
    ///     handles rooting, separators, and platform case-sensitivity natively. Pure and thread-safe;
    ///     performs no I/O.
    /// </remarks>
    public static string SafePathCombine(string basePath, string relativePath)
    {
        // Validate inputs so the containment check operates on real paths
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePath);

        // Preserve the caller's relative/absolute style while resolving to compare
        var combinedPath = Path.Combine(basePath, relativePath);

        // Resolve both sides and derive the relationship; a traversal or rooted result means escape
        var absoluteBase = Path.GetFullPath(basePath);
        var absoluteCombined = Path.GetFullPath(combinedPath);
        var checkRelative = Path.GetRelativePath(absoluteBase, absoluteCombined);

        if (string.Equals(checkRelative, "..", StringComparison.Ordinal)
            || checkRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || checkRelative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(checkRelative))
        {
            throw new ScratchFolderException("pathEscapesScratchFolder", $"The path component escapes the scratch folder: '{relativePath}'.");
        }

        return combinedPath;
    }

    /// <summary>
    ///     Determines whether a file-name component is a reserved device name on any platform.
    /// </summary>
    /// <param name="fileName">The single path component to test.</param>
    /// <returns><see langword="true"/> when the component is a reserved device name; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     Checks the raw component, the component with its extension stripped, and the component with
    ///     trailing dots and spaces stripped (in both stripped forms), because Windows treats
    ///     <c>CON</c>, <c>con.txt</c>, and <c>NUL.</c> all as the device. Enforced on every platform
    ///     so Linux-produced output stays portable to Windows. Pure and thread-safe.
    /// </remarks>
    public static bool IsReservedDeviceName(string fileName)
    {
        // An empty component names no device
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        // Derive the candidate stems Windows would collapse to a device name
        var trimmed = fileName.TrimEnd('.', ' ');
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var withoutExtensionTrimmed = withoutExtension.TrimEnd('.', ' ');

        // Any candidate matching the reserved set means the whole component is unsafe
        return ReservedNames.Contains(fileName)
            || ReservedNames.Contains(trimmed)
            || ReservedNames.Contains(withoutExtension)
            || ReservedNames.Contains(withoutExtensionTrimmed);
    }

    /// <summary>
    ///     Produces a filesystem-safe slug from an arbitrary (possibly untrusted) title.
    /// </summary>
    /// <param name="value">The source value to slugify, or <see langword="null"/>.</param>
    /// <param name="maxLength">The maximum slug length. Defaults to <see cref="MaxComponentLength"/>.</param>
    /// <returns>
    ///     A lowercase ASCII slug of at most <paramref name="maxLength"/> characters, or
    ///     <see cref="string.Empty"/> when the input reduces to nothing (the caller then substitutes
    ///     the content kind).
    /// </returns>
    /// <remarks>
    ///     Normalizes with NFKD and drops non-spacing marks so accented characters fold to their base
    ///     letters; lowercases with the invariant culture for a stable, locale-independent result;
    ///     maps every non-<c>[a-z0-9]</c> character to a hyphen; collapses hyphen runs; trims; then
    ///     truncates and trims again so a truncation never leaves a trailing hyphen. Returning empty
    ///     rather than a placeholder keeps naming decisions with the caller. Pure and thread-safe.
    /// </remarks>
    public static string Slugify(string? value, int maxLength = MaxComponentLength)
    {
        // Nothing to slug: let the caller substitute the kind
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Fold accents to base letters by decomposing and dropping the combining marks
        var decomposed = value.Normalize(NormalizationForm.FormKD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                stripped.Append(ch);
            }
        }

        // Lowercase invariantly, then map to the [a-z0-9-] alphabet collapsing hyphen runs as we go
        var lowered = stripped.ToString().ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        var lastWasHyphen = false;
        foreach (var ch in lowered)
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        // Trim, bound the length, then trim once more so truncation never leaves a dangling hyphen
        var slug = builder.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength];
        }

        return slug.TrimEnd('-');
    }

    /// <summary>
    ///     Validates that an absolute path does not exceed <see cref="MaxTotalPathLength"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path to measure.</param>
    /// <exception cref="ScratchFolderException">Thrown when the path exceeds the total-length ceiling.</exception>
    /// <remarks>
    ///     Enforced as a distinct check because per-component truncation bounds only individual names,
    ///     not the accumulated path. Refusing here yields a classifiable
    ///     <see cref="ScratchFolderException"/> instead of an opaque
    ///     <see cref="PathTooLongException"/> surfacing later. Pure and thread-safe.
    /// </remarks>
    public static void ValidateTotalPathLength(string absolutePath)
    {
        // Bound the full path so a deep base plus a long name cannot exceed the platform ceiling
        if (!string.IsNullOrEmpty(absolutePath) && absolutePath.Length > MaxTotalPathLength)
        {
            throw new ScratchFolderException("pathTooLong", $"The allocated path exceeds the {MaxTotalPathLength}-character limit: '{absolutePath}'.");
        }
    }

    /// <summary>
    ///     Validates a single relative-path component against the untrusted-content defenses.
    /// </summary>
    /// <param name="component">The single component to validate.</param>
    /// <param name="relativePath">The full relative path, used only for a clearer error message.</param>
    /// <exception cref="ScratchFolderException">
    ///     Thrown when the component is empty, a reserved device name, or contains a colon, backslash,
    ///     or NUL character.
    /// </exception>
    /// <remarks>
    ///     Defense in depth: even though <see cref="Slugify"/> already removes dangerous characters,
    ///     this re-checks each component so a future caller that bypasses slugging cannot inject a
    ///     separator, drive-letter colon, reserved name, or NUL. Pure and thread-safe.
    /// </remarks>
    private static void ValidateComponent(string component, string relativePath)
    {
        // An empty component (for example a double slash) is not a valid allocation
        if (component.Length == 0)
        {
            throw new ScratchFolderException("invalidPathComponent", $"The path contains an empty component: '{relativePath}'.");
        }

        // A colon, backslash, or NUL indicates drive-letter, separator, or injection abuse
        if (component.Contains(':', StringComparison.Ordinal)
            || component.Contains('\\', StringComparison.Ordinal)
            || component.Contains('\0', StringComparison.Ordinal))
        {
            throw new ScratchFolderException("invalidPathComponent", $"The path component contains an illegal character: '{component}'.");
        }

        // Reserved device names are refused on every platform to keep output Windows-portable
        if (IsReservedDeviceName(component))
        {
            throw new ScratchFolderException("reservedDeviceName", $"The path component is a reserved device name: '{component}'.");
        }
    }

    /// <summary>
    ///     Resolves the effective folder path for a preparation mode, cleaning existing contents when
    ///     the policy allows.
    /// </summary>
    /// <param name="absolute">The absolute requested folder path.</param>
    /// <param name="mode">The preparation policy.</param>
    /// <param name="reader">The file-state boundary the reuse guard re-reads each target through.</param>
    /// <returns>The absolute folder path to create and use.</returns>
    /// <exception cref="ScratchFolderException">Thrown when the policy refuses the existing folder.</exception>
    /// <remarks>
    ///     Split from <see cref="Prepare(string,ScratchFolderMode)"/> so the per-mode policy is
    ///     expressed in one focused switch. Performs filesystem inspection and deletion of existing
    ///     contents. Only <see cref="ScratchFolderMode.CleanIfDocDownFolder"/> is given
    ///     <paramref name="reader"/>: it is the only mode that deletes files it has separately
    ///     proved it may delete, so it is the only one with a check to re-confirm.
    /// </remarks>
    private static string ApplyMode(string absolute, ScratchFolderMode mode, IFileStateReader reader) => mode switch
    {
        ScratchFolderMode.CleanIfDocDownFolder => PrepareCleanIfDocDown(absolute, reader),
        ScratchFolderMode.Overwrite => PrepareOverwrite(absolute),
        _ => throw new ScratchFolderException("unknownScratchFolderMode", $"The scratch-folder mode '{mode}' is not supported.")
    };

    /// <summary>
    ///     Resolves the folder for <see cref="ScratchFolderMode.CleanIfDocDownFolder"/> by deleting
    ///     exactly the files a manifest written for this folder inventories, and nothing else.
    /// </summary>
    /// <param name="absolute">The absolute folder path.</param>
    /// <param name="reader">The boundary through which each target's state is re-read before it is deleted.</param>
    /// <returns>The folder path to use.</returns>
    /// <exception cref="ScratchFolderException">
    ///     Thrown when the folder is non-empty and reuse cannot be proved: no DocDown manifest
    ///     (<c>scratchFolderNotDocDown</c>), a manifest written for a different folder
    ///     (<c>scratchFolderPathMismatch</c>), a file the manifest does not account for
    ///     (<c>scratchFolderUnaccountedContent</c>), a manifest listing a path outside the
    ///     folder (<c>scratchFolderManifestPathEscapes</c>), or a target that no longer matches
    ///     the state recorded when the folder was inventoried
    ///     (<c>scratchFolderChangedDuringPreparation</c>).
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This is the default mode, so its failure cost is the caller's data. It therefore
    ///         does not classify the folder and then empty it; it deletes only what it can prove
    ///         this library wrote <em>here</em>. Five things must hold, in order: the folder holds
    ///         a structurally valid DocDown manifest (<see cref="LoadDocDownManifest"/>); that
    ///         manifest's <c>scratchFolder</c> names this very folder (<see cref="PathBinds"/>);
    ///         every file present is accounted for by that manifest
    ///         (<see cref="ArtifactInventory"/>); every inventoried path resolves inside the folder
    ///         (<see cref="SafePathCombine"/>); and each target still matches, at the moment it is
    ///         about to be deleted, the state it had when the folder was inventoried. Only then is
    ///         each inventoried file deleted individually. No code path here empties a directory.
    ///     </para>
    ///     <para>
    ///         The fifth check exists because the inventory is taken at one instant and acted on at
    ///         another. Without it, a file created at an inventoried path after the scan — and
    ///         <c>summary.txt</c>, <c>manifest.json</c> and <c>content.md</c> are inventoried at
    ///         fixed, guessable names in every run — would be deleted on the strength of a check
    ///         that never saw it. The snapshot is built from the <em>same</em> directory listing
    ///         step 3 already performs, because taking it afterwards would simply reopen the
    ///         interval it exists to shorten, and each target is re-read immediately before its own
    ///         deletion rather than all targets up front, for the same reason.
    ///     </para>
    ///     <para>
    ///         This interval is <strong>narrowed, not eliminated</strong>, and saying otherwise
    ///         would be the false confidence this type exists to avoid. Three limits remain.
    ///         (1) A gap survives between reading a file's state and the delete operation itself;
    ///         closing it needs operating-system-level locking, which this library deliberately
    ///         does not take. (2) A modification that changes neither the byte length nor the
    ///         recorded last-write time — possible where timestamp resolution is coarse — is not
    ///         detected. (3) The refusal is <em>not atomic</em>: a mismatch on the n-th target
    ///         aborts after targets 1..n-1 have already been deleted. That is accepted rather than
    ///         hidden, because every file deleted before the abort is an inventoried DocDown
    ///         artifact, so the blast radius stays bounded by the inventory and no file of the
    ///         caller's can be lost this way; the refusal message states it.
    ///     </para>
    ///     <para>
    ///         The binding check is what closes the copied-manifest hole: a genuine
    ///         <c>manifest.json</c> carried into a folder of the caller's own documents proves the
    ///         file is a DocDown manifest, not that the folder is DocDown output. Anything
    ///         unproved is refused with a <see cref="ScratchFolderException"/>, which the engine
    ///         surfaces as <c>DD0501</c> <c>ScratchFolderRefused</c>, and every refusal message
    ///         names <see cref="ScratchFolderMode.Overwrite"/> as the deliberate escape hatch.
    ///         Refusing costs the caller one flag; a false positive costs them their files.
    ///     </para>
    ///     <para>
    ///         Empty directories left behind — typically <c>images/</c> or <c>pages/</c> — are
    ///         deliberately kept. Removing them would mean deleting something no manifest
    ///         inventories, which is the exact behavior this guard exists to eliminate, and an
    ///         empty directory holds no data. The next run re-creates the layout idempotently.
    ///     </para>
    /// </remarks>
    private static string PrepareCleanIfDocDown(string absolute, IFileStateReader reader)
    {
        // A fresh or empty folder needs no cleaning
        if (!Directory.Exists(absolute) || IsEmpty(absolute))
        {
            return absolute;
        }

        // Step 1: the folder must hold a structurally valid DocDown manifest
        var manifest = LoadDocDownManifest(absolute)
            ?? throw new ScratchFolderException("scratchFolderNotDocDown",
                $"The scratch folder is not empty and is not a recognizable DocDown folder: '{absolute}'. Use ScratchFolderMode.Overwrite to replace its contents deliberately.");

        // Step 2: that manifest must have been written for THIS folder, not merely be a DocDown manifest
        if (!PathBinds(manifest.ScratchFolder, absolute))
        {
            throw new ScratchFolderException("scratchFolderPathMismatch",
                $"The manifest in '{absolute}' was written for '{manifest.ScratchFolder}', so this folder is not provably DocDown output. Use ScratchFolderMode.Overwrite to replace its contents deliberately.");
        }

        // Step 3: everything present must be accounted for; an extra file means the folder is not only ours.
        // The one listing this step already performs is also what the snapshot is built from, so recording
        // the folder's state adds no second walk and opens no interval of its own
        var accounted = ArtifactInventory.AccountedRelativePaths(manifest);
        var listing = ArtifactInventory.EnumerateFiles(absolute);
        var unaccounted = ArtifactInventory.UnaccountedFiles(absolute, accounted, listing);
        if (unaccounted.Count > 0)
        {
            throw new ScratchFolderException("scratchFolderUnaccountedContent",
                $"The scratch folder '{absolute}' contains {unaccounted.Count.ToString(CultureInfo.InvariantCulture)} file(s) the manifest does not account for, starting with '{unaccounted[0]}'. Use ScratchFolderMode.Overwrite to replace its contents deliberately.");
        }

        var snapshot = SnapshotInventory(absolute, accounted, listing, reader);

        // Step 4: resolve every inventoried path through the containment gate BEFORE deleting anything,
        // so a hand-authored manifest can never reach a file outside the folder
        var targets = new List<(string Relative, string Absolute)>(accounted.Count);
        foreach (var relative in accounted)
        {
            try
            {
                targets.Add((relative, SafePathCombine(absolute, relative)));
            }
            catch (ScratchFolderException exception)
            {
                throw new ScratchFolderException("scratchFolderManifestPathEscapes",
                    $"The manifest in '{absolute}' lists a path that escapes the scratch folder: {exception.Message} Use ScratchFolderMode.Overwrite to replace its contents deliberately.");
            }
        }

        // Step 5: delete exactly the proved-ours files, re-reading each target immediately before its own
        // deletion and refusing on any divergence from the inventoried state — including a path that was
        // absent at scan time and exists now. Directories are never removed because none are inventoried
        foreach (var (relative, target) in targets)
        {
            var current = reader.Read(target);
            if (!current.Equals(snapshot[relative]))
            {
                throw new ScratchFolderException("scratchFolderChangedDuringPreparation",
                    $"The scratch folder '{absolute}' changed while it was being prepared: '{relative}' is not the file that was inventoried. Deletion stopped there, so inventoried DocDown artifacts already deleted are not restored, but nothing outside the manifest inventory was deleted. Use ScratchFolderMode.Overwrite to replace its contents deliberately.");
            }

            // An inventoried path the manifest lists but the folder never held is simply nothing to delete
            if (current.Exists)
            {
                File.Delete(target);
            }
        }

        return absolute;
    }

    /// <summary>
    ///     Records the state of every inventoried path at the instant the folder was scanned.
    /// </summary>
    /// <param name="absolute">The absolute folder path being prepared.</param>
    /// <param name="accounted">The manifest's accounted relative paths.</param>
    /// <param name="listing">The absolute paths step 3 enumerated; the only directory walk performed.</param>
    /// <param name="reader">The boundary each present file's state is read through.</param>
    /// <returns>A map from accounted relative path to the state observed for it during the scan.</returns>
    /// <remarks>
    ///     Built from the listing the unaccounted-content check already holds rather than from a
    ///     fresh walk, so the recorded state describes the same folder contents that check approved.
    ///     An accounted path absent from that listing is recorded as
    ///     <see cref="FileState.Missing"/>, which is what makes "did not exist at scan time, exists
    ///     now" a divergence rather than a silently deleted surprise — and that case matters most,
    ///     because <c>summary.txt</c>, <c>manifest.json</c> and <c>content.md</c> are inventoried
    ///     under fixed names anyone can predict. Read-only I/O.
    /// </remarks>
    private static Dictionary<string, FileState> SnapshotInventory(
        string absolute,
        HashSet<string> accounted,
        IReadOnlyList<string> listing,
        IFileStateReader reader)
    {
        // Every accounted path starts as absent; the listing then promotes the ones actually present
        var snapshot = new Dictionary<string, FileState>(accounted.Count, StringComparer.Ordinal);
        foreach (var relative in accounted)
        {
            snapshot[relative] = FileState.Missing;
        }

        // Only files the scan actually saw get a real reading, so no extra enumeration is performed
        foreach (var file in listing)
        {
            var relative = ArtifactInventory.ToRelativePath(absolute, file);
            if (snapshot.ContainsKey(relative))
            {
                snapshot[relative] = reader.Read(file);
            }
        }

        return snapshot;
    }

    /// <summary>
    ///     Determines whether a manifest's recorded scratch folder is this very folder.
    /// </summary>
    /// <param name="manifestScratchFolder">The <c>scratchFolder</c> value read from the manifest.</param>
    /// <param name="absolute">The absolute path of the folder being prepared.</param>
    /// <returns><see langword="true"/> only when both sides name the same folder; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     <para>
    ///         Both sides are reduced to the same canonical form —
    ///         <see cref="Path.GetFullPath(string)"/> to resolve <c>.</c>, <c>..</c> and separator
    ///         style, then <see cref="Path.TrimEndingDirectorySeparator(string)"/> so a trailing
    ///         separator cannot cause a false mismatch — and compared with
    ///         <see cref="StringComparison.OrdinalIgnoreCase"/> on Windows and macOS, whose default
    ///         file systems are case-insensitive, and <see cref="StringComparison.Ordinal"/>
    ///         elsewhere. Weakening the Linux comparison would accept a folder the library never
    ///         wrote.
    ///     </para>
    ///     <para>
    ///         Symbolic links, junctions and short (8.3) names are deliberately <em>not</em>
    ///         resolved. <see cref="File.ResolveLinkTarget(string,bool)"/> resolves only the final
    ///         component, so it cannot produce a true canonical path and would merely lend false
    ///         confidence; and resolving would make a destructive comparison <em>more</em>
    ///         permissive, which is the wrong direction. A manifest is written with exactly the
    ///         <see cref="Path.GetFullPath(string)"/> form that a re-run of the same requested path
    ///         reproduces, so a plain normalized comparison is precise for legitimate reuse, and an
    ///         alias-induced mismatch produces a refusal — the fail-safe direction, with
    ///         <see cref="ScratchFolderMode.Overwrite"/> named in the message.
    ///     </para>
    ///     <para>
    ///         A null, empty, or malformed recorded path is not a match: the manifest then proves
    ///         nothing about this folder. Pure apart from path normalization; performs no I/O.
    ///     </para>
    /// </remarks>
    private static bool PathBinds(string? manifestScratchFolder, string absolute)
    {
        // A manifest that records no folder binds to nothing
        if (string.IsNullOrEmpty(manifestScratchFolder))
        {
            return false;
        }

        try
        {
            var recorded = Path.TrimEndingDirectorySeparator(Path.GetFullPath(manifestScratchFolder));
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(absolute));

            // Match the host file system's case sensitivity: tolerant where the platform is, strict where it is not
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return string.Equals(recorded, current, comparison);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            // A path that cannot even be normalized cannot bind; refuse rather than guess
            return false;
        }
    }

    /// <summary>
    ///     Resolves the folder for <see cref="ScratchFolderMode.Overwrite"/>, deleting any contents.
    /// </summary>
    /// <param name="absolute">The absolute folder path.</param>
    /// <returns>The folder path to use.</returns>
    /// <remarks>Deletes unconditionally, so it is the caller's explicit opt-in to clobbering the folder.</remarks>
    private static string PrepareOverwrite(string absolute)
    {
        // The caller has opted into unconditional replacement of any existing contents
        if (Directory.Exists(absolute))
        {
            DeleteContents(absolute);
        }

        return absolute;
    }

    /// <summary>
    ///     Determines whether a folder contains no files or subfolders.
    /// </summary>
    /// <param name="absolute">The folder to inspect.</param>
    /// <returns><see langword="true"/> when the folder has no entries; otherwise <see langword="false"/>.</returns>
    /// <remarks>Uses a lazy enumerator so emptiness is decided without materializing the whole listing.</remarks>
    private static bool IsEmpty(string absolute) => !Directory.EnumerateFileSystemEntries(absolute).Any();

    /// <summary>
    ///     Loads the folder's <c>manifest.json</c> when, and only when, it is structurally a DocDown manifest.
    /// </summary>
    /// <param name="absolute">The folder to inspect.</param>
    /// <returns>
    ///     The parsed manifest when the folder contains a <c>manifest.json</c> that deserializes
    ///     into a well-formed DocDown manifest carrying the DocDown schema identifier and tool
    ///     identity; otherwise <see langword="null"/>.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         This is the first of the reuse guard's four steps. It establishes only that the file
    ///         <em>is a DocDown manifest</em> — never that the folder is <em>this run's</em> output,
    ///         which <see cref="PathBinds"/> and the inventory checks in
    ///         <see cref="PrepareCleanIfDocDown"/> decide. Returning the parsed manifest rather than
    ///         a boolean means the later steps reason about the very object these conditions
    ///         validated: one bounded read, one parse, no possibility of the structural check and
    ///         the binding check disagreeing about the file's content.
    ///     </para>
    ///     <para>
    ///         Proof is positive and structural, not a text search. All six conditions must hold:
    ///         (1) <c>manifest.json</c> exists and is no larger than
    ///         <see cref="MaxManifestProbeBytes"/>; (2) it deserializes successfully through the
    ///         source-generated <see cref="DocDownJsonContext"/> — a truncated or corrupt file
    ///         throws and is refused; (3) <c>schemaVersion</c> equals
    ///         <see cref="SupportedManifestSchema"/> with an ordinal comparison; (4) <c>tool.name</c> equals
    ///         <see cref="DocDownToolName"/> <em>exactly</em>, never as a substring;
    ///         (5) <c>tool.package</c> starts with <see cref="DocDownPackagePrefix"/>; and
    ///         (6) an <c>artifacts</c> ledger is present, which every real DocDown run writes.
    ///         An alien-but-valid JSON document deserializes with null members and fails at (3)
    ///         or (4).
    ///     </para>
    ///     <para>
    ///         Every DocDown run — including a failed one — writes the full layout with a
    ///         <c>manifest.json</c>, so legitimate reuse is unaffected. A run that crashed before
    ///         the manifest was written is refused, which is the safe direction. Any exception,
    ///         any doubt, returns <see langword="null"/>. Read-only I/O.
    ///     </para>
    /// </remarks>
    private static ExtractionManifest? LoadDocDownManifest(string absolute)
    {
        // Only a structurally valid DocDown manifest can start the reuse guard; it proves the file, not the folder
        var manifestPath = Path.Combine(absolute, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            // Bound the read so a huge or hostile file cannot be used to exhaust memory
            var info = new FileInfo(manifestPath);
            if (info.Length > MaxManifestProbeBytes)
            {
                return null;
            }

            // A corrupt, truncated, or non-JSON file throws here and is refused
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(json, DocDownJsonContext.Default.ExtractionManifest);

            // An alien JSON document deserializes with null members; require the DocDown identity exactly
            var isDocDown = manifest is not null
                && string.Equals(manifest.SchemaVersion, SupportedManifestSchema, StringComparison.Ordinal)
                && manifest.Tool is not null
                && string.Equals(manifest.Tool.Name, DocDownToolName, StringComparison.Ordinal)
                && manifest.Tool.Package is not null
                && manifest.Tool.Package.StartsWith(DocDownPackagePrefix, StringComparison.Ordinal)
                && manifest.Source is not null;

            return isDocDown ? manifest : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // If the manifest cannot be read or parsed, there is nothing to build proof on; the reuse guard refuses
            return null;
        }
    }

    /// <summary>
    ///     Deletes every file and subfolder inside a folder, leaving the folder itself in place.
    /// </summary>
    /// <param name="absolute">The folder whose contents are removed.</param>
    /// <remarks>
    ///     Reached only from <see cref="PrepareOverwrite"/>, which is the caller's explicit opt-in
    ///     to clobbering the folder; the default reuse mode never calls it, because unconditional
    ///     emptying is precisely the shape of operation that mode must not perform. Removes
    ///     contents rather than the folder so the folder's own permissions and handle stay valid
    ///     for the immediate re-creation. Performs destructive filesystem I/O.
    /// </remarks>
    private static void DeleteContents(string absolute)
    {
        // Remove files first, then subtrees, so the directory is emptied without deleting itself
        foreach (var file in Directory.EnumerateFiles(absolute))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(absolute))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Builds the case-insensitive reserved-device-name lookup.
    /// </summary>
    /// <returns>A set containing every reserved device-name stem.</returns>
    /// <remarks>
    ///     Constructs the set once at type initialization. Includes the numbered <c>COM</c> and
    ///     <c>LPT</c> devices from <c>0</c> through <c>9</c> per the specification. Uses an
    ///     ordinal-ignore-case comparer so case never affects membership.
    /// </remarks>
    private static HashSet<string> BuildReservedNames()
    {
        // Seed with the standalone device names
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL" };

        // Add the numbered serial and parallel port devices
        for (var i = 0; i <= 9; i++)
        {
            var digit = i.ToString(CultureInfo.InvariantCulture);
            names.Add("COM" + digit);
            names.Add("LPT" + digit);
        }

        return names;
    }
}

/// <summary>
///     The state of one inventoried file at a single instant: whether it was there, how large it
///     was, and when it was last written.
/// </summary>
/// <param name="Exists">Whether a file was present at the path when the reading was taken.</param>
/// <param name="Length">The file's length in bytes, or zero when it was absent.</param>
/// <param name="LastWriteTimeUtc">The file's last-write time in UTC, or the default value when it was absent.</param>
/// <remarks>
///     The reuse guard inventories a folder at one instant and deletes from it at another. This
///     type is what lets those two instants be compared: it is recorded during the inventory scan
///     and re-read immediately before each deletion, and any difference — including a path that was
///     absent and is now present — means the folder is no longer the one that was approved. All
///     three fields are compared together because none is sufficient alone: length misses an
///     equal-sized rewrite, the timestamp misses a change finer than the file system's resolution,
///     and existence misses both. Immutable and thread-safe.
/// </remarks>
internal readonly record struct FileState(bool Exists, long Length, DateTime LastWriteTimeUtc)
{
    /// <summary>The reading that describes a path holding no file.</summary>
    /// <remarks>
    ///     Named rather than written out at each site so every "absent" reading is byte-for-byte
    ///     the same value, which is what makes equality with a later reading meaningful.
    /// </remarks>
    internal static readonly FileState Missing = new(false, 0L, default);
}

/// <summary>
///     The boundary through which <see cref="ScratchFolder"/> reads a file's state.
/// </summary>
/// <remarks>
///     Exists so the changed-while-preparing refusal can be exercised deterministically: the only
///     alternative is to win a real race between the inventory scan and the deletion, which no test
///     can do reliably, and a test that fails to win the race would report success while proving
///     nothing. It is not a general abstraction over the file system and has exactly one production
///     implementation, <see cref="FileSystemFileStateReader"/>; nothing outside this file depends
///     on it except the tests that supply a reader which mutates the folder between readings.
/// </remarks>
internal interface IFileStateReader
{
    /// <summary>
    ///     Reads the current state of a single file.
    /// </summary>
    /// <param name="path">The absolute path to read.</param>
    /// <returns>The file's state, or <see cref="FileState.Missing"/> when no file is there.</returns>
    FileState Read(string path);
}

/// <summary>
///     The production <see cref="IFileStateReader"/>, reading the real file system.
/// </summary>
/// <remarks>
///     Takes a fresh reading on every call — <see cref="FileInfo"/> caches its values once queried,
///     so a reused instance would answer the second reading with the first one's data and defeat
///     the comparison entirely. An absent file is reported as <see cref="FileState.Missing"/>
///     rather than as an error, because "nothing to delete here" is an ordinary outcome for an
///     inventoried path. Stateless and thread-safe.
/// </remarks>
internal sealed class FileSystemFileStateReader : IFileStateReader
{
    /// <summary>The shared instance used by the public preparation entry point.</summary>
    /// <remarks>Stateless, so one instance serves every caller.</remarks>
    internal static readonly FileSystemFileStateReader Instance = new();

    /// <inheritdoc/>
    public FileState Read(string path)
    {
        // A new FileInfo each time: an existing one would answer from its cached snapshot
        var info = new FileInfo(path);
        return info.Exists ? new FileState(true, info.Length, info.LastWriteTimeUtc) : FileState.Missing;
    }
}

/// <summary>
///     The exception raised when the scratch output folder cannot be prepared and is refused.
/// </summary>
/// <remarks>
///     A dedicated exception type lets the engine catch scratch-folder refusals specifically and
///     convert them into a prose extraction failure, rather than surfacing an opaque
///     <see cref="System.IO.IOException"/> or <see cref="System.IO.PathTooLongException"/>. The
///     <see cref="Reason"/> property carries a short, stable cause the engine can present to the
///     caller. This type is immutable after construction and therefore thread-safe.
/// </remarks>
public sealed class ScratchFolderException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class.
    /// </summary>
    /// <remarks>
    ///     Provided to satisfy the standard exception constructor pattern; <see cref="Reason"/> is
    ///     an empty string because no cause was supplied.
    /// </remarks>
    public ScratchFolderException()
        : base()
    {
        Reason = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class with a
    ///     message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <remarks>
    ///     Part of the standard exception constructor pattern; <see cref="Reason"/> is an empty
    ///     string because no distinct short cause was supplied.
    /// </remarks>
    public ScratchFolderException(string message)
        : base(message)
    {
        Reason = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    /// <remarks>
    ///     Part of the standard exception constructor pattern; used to wrap a lower-level I/O
    ///     error. <see cref="Reason"/> is an empty string because no distinct short cause was
    ///     supplied.
    /// </remarks>
    public ScratchFolderException(string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = string.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScratchFolderException"/> class with a
    ///     short stable reason and a detailed message.
    /// </summary>
    /// <param name="reason">A short, stable cause the engine surfaces to the caller.</param>
    /// <param name="message">The detailed message that describes the error.</param>
    /// <remarks>
    ///     This is the constructor Core uses when refusing a folder, so the refusal always carries
    ///     both a concise <see cref="Reason"/> for programmatic use and a fuller message for
    ///     display. The two <see langword="string"/> parameters are ordered
    ///     (<paramref name="reason"/>, then <paramref name="message"/>) to read naturally at the
    ///     refusal site.
    /// </remarks>
    public ScratchFolderException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    ///     Gets the short, stable reason the scratch folder was refused.
    /// </summary>
    /// <remarks>
    ///     Exposed separately from <see cref="Exception.Message"/> so the engine can map the cause
    ///     into a structured failure without parsing the display message. Never
    ///     <see langword="null"/>; empty when no distinct reason was supplied.
    /// </remarks>
    public string Reason { get; }
}
