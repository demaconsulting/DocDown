namespace DocDown.Core;

/// <summary>
///     The single, shared definition of what a manifest accounts for: the exact set of relative
///     paths an extraction claims to have written into a scratch folder.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="ScratchFolder"/> needs this proposition before a destructive reuse ("is
///         everything here accounted for by this folder's manifest, so that I may delete exactly
///         those files, or is something present that we never wrote?"). Holding the definition in one
///         place keeps the accounted set and the safe-deletion set byte-for-byte the same, so the
///         cleaner never deletes a file nobody promised to write.
///     </para>
///     <para>
///         This is a leaf supporting type. It depends only on <see cref="ExtractionManifest"/> and
///         <c>System.IO</c> path arithmetic.
///     </para>
///     <para>
///         The inventory is deliberately <strong>file-only</strong>. Directories are never
///         inventoried, because a directory holds no data: an empty <c>images/</c> left behind by a
///         previous run is neither an unlisted artifact to report nor content to destroy. Keeping
///         the predicate file-only is what makes it byte-for-byte the same proposition as the
///         unlisted-file check, which enumerates files.
///     </para>
///     <para>
///         All members are pure except <see cref="EnumerateFiles"/>, which performs read-only
///         filesystem I/O. The type holds no state and is safe for concurrent use.
///     </para>
/// </remarks>
internal static class ArtifactInventory
{
    /// <summary>
    ///     The fixed root-level artifacts every extraction writes, whatever the outcome.
    /// </summary>
    /// <remarks>
    ///     These four are the only files permitted at the scratch root. They are not listed in the
    ///     manifest's resource arrays — a manifest cannot list itself by hash — so they are
    ///     accounted for here by the layout contract instead. Any other root-level file is
    ///     unaccounted.
    /// </remarks>
    internal static readonly string[] RootArtifacts = ["summary.txt", "manifest.json", "metadata.json", "content.md"];

    /// <summary>
    ///     Builds the set of every relative path a manifest accounts for.
    /// </summary>
    /// <param name="manifest">The parsed manifest whose claims define the inventory.</param>
    /// <returns>
    ///     The manifest's image, page, and part paths together with the four
    ///     <see cref="RootArtifacts"/>, held in manifest (forward-slash) form and matched exactly.
    /// </returns>
    /// <remarks>
    ///     Each resource array is null-tolerant: a manifest that omits <c>images</c>,
    ///     <c>pages</c>, or <c>parts</c> contributes nothing from that array rather than failing,
    ///     which matches how the deserializer materializes an absent array. Comparison is ordinal
    ///     because manifest paths are written by this library in a single normalized form, so a
    ///     case-insensitive match would accept a path the library never wrote. Pure.
    /// </remarks>
    internal static HashSet<string> AccountedRelativePaths(ExtractionManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var accounted = new HashSet<string>(StringComparer.Ordinal);

        // Every resource the manifest claims by path is accounted for
        foreach (var image in manifest.Images ?? [])
        {
            accounted.Add(image.Path);
        }

        foreach (var page in manifest.Pages ?? [])
        {
            accounted.Add(page.Path);
        }

        foreach (var part in manifest.Parts ?? [])
        {
            accounted.Add(part.Path);
        }

        // The four fixed root artifacts are accounted for by the layout contract
        foreach (var rootArtifact in RootArtifacts)
        {
            accounted.Add(rootArtifact);
        }

        return accounted;
    }

    /// <summary>
    ///     Selects the files that are present beneath a root but accounted for by nothing.
    /// </summary>
    /// <param name="root">The scratch folder the files live beneath.</param>
    /// <param name="accounted">The accounted-path set from <see cref="AccountedRelativePaths"/>.</param>
    /// <param name="files">The absolute paths of the files found beneath <paramref name="root"/>.</param>
    /// <returns>The relative, forward-slash paths of the unaccounted files, in enumeration order.</returns>
    /// <remarks>
    ///     Takes the file listing as a parameter rather than performing the walk, so the caller
    ///     owns how enumeration failures are surfaced — the verifier reports them, the scratch
    ///     folder refuses on them — while the accounting rule itself stays identical for both.
    ///     Pure.
    /// </remarks>
    internal static IReadOnlyList<string> UnaccountedFiles(string root, HashSet<string> accounted, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(accounted);
        ArgumentNullException.ThrowIfNull(files);

        var unaccounted = new List<string>();
        foreach (var file in files)
        {
            var relative = ToRelativePath(root, file);
            if (!accounted.Contains(relative))
            {
                unaccounted.Add(relative);
            }
        }

        return unaccounted;
    }

    /// <summary>
    ///     Enumerates every file beneath a root, at any depth.
    /// </summary>
    /// <param name="root">The folder to walk.</param>
    /// <returns>The absolute paths of every file in the tree, or an empty array when the root does not exist.</returns>
    /// <remarks>
    ///     Materializes the listing so callers observe a complete result or an exception, never a
    ///     partially consumed enumerator. Enumeration failures are deliberately <em>not</em>
    ///     caught here: an unreadable tree is a fact the caller must act on, and swallowing it
    ///     would be the very "I could not check" reported as "I checked" that this library exists
    ///     to prevent. Read-only I/O.
    /// </remarks>
    internal static string[] EnumerateFiles(string root) =>
        Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : [];

    /// <summary>
    ///     Converts an absolute path beneath a root to its manifest-style relative path.
    /// </summary>
    /// <param name="root">The scratch folder the path lives beneath.</param>
    /// <param name="diskPath">The absolute path of a file beneath it.</param>
    /// <returns>The relative path using forward slashes, matching how the manifest records paths.</returns>
    /// <remarks>
    ///     Normalizing the platform separator to a forward slash is what lets an on-disk listing be
    ///     compared against manifest paths identically on every operating system. Pure.
    /// </remarks>
    internal static string ToRelativePath(string root, string diskPath) =>
        Path.GetRelativePath(root, diskPath).Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
}
