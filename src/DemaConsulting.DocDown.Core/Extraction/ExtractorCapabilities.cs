namespace DocDown.Core;

/// <summary>
///     The set of content aspects a document extractor can produce, expressed as combinable flags.
/// </summary>
/// <remarks>
///     Modeled as a <see cref="FlagsAttribute"/> enum because an extractor typically provides
///     several aspects at once and selection negotiates over the union of what the caller
///     requires and what a backend offers. Callers must never assume a backend provides an
///     aspect it did not declare.
/// </remarks>
[Flags]
public enum ExtractorCapabilities
{
    /// <summary>
    ///     No capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Extraction of textual content.
    /// </summary>
    Text = 1,

    /// <summary>
    ///     Extraction of images embedded in the document.
    /// </summary>
    EmbeddedImages = 2,

    /// <summary>
    ///     Rendering of document pages to raster images.
    /// </summary>
    RenderedPages = 4,

    /// <summary>
    ///     Extraction of document-level metadata such as title and author.
    /// </summary>
    DocumentMetadata = 8,

    /// <summary>
    ///     Extraction of document structure such as sections, sheets, or slides.
    /// </summary>
    DocumentStructure = 16
}

/// <summary>
///     Helper operations over <see cref="ExtractorCapabilities"/> shared by selection and
///     manifest serialization.
/// </summary>
/// <remarks>
///     Centralized here (in the same file as the enum it serves) so the canonical flag ordering
///     and camelCase naming exist in exactly one place; both <c>ExtractorSelector</c> and
///     <c>ManifestWriter</c> depend on identical output, and duplicating the mapping would risk
///     divergence. All members are pure and thread-safe.
/// </remarks>
public static class ExtractorCapabilitiesExtensions
{
    /// <summary>
    ///     The individual capability flags paired with their camelCase names, in declaration order.
    /// </summary>
    /// <remarks>
    ///     Declared once as an ordered table so <see cref="ToCamelCaseNames"/> and
    ///     <see cref="CountFlags"/> iterate the same fixed sequence; <see cref="ExtractorCapabilities.None"/>
    ///     is deliberately excluded because it names the absence of a capability rather than one.
    /// </remarks>
    private static readonly (ExtractorCapabilities Flag, string Name)[] OrderedFlags =
    [
        (ExtractorCapabilities.Text, "text"),
        (ExtractorCapabilities.EmbeddedImages, "embeddedImages"),
        (ExtractorCapabilities.RenderedPages, "renderedPages"),
        (ExtractorCapabilities.DocumentMetadata, "documentMetadata"),
        (ExtractorCapabilities.DocumentStructure, "documentStructure")
    ];

    /// <summary>
    ///     Projects the set flags to their camelCase names in canonical declaration order.
    /// </summary>
    /// <param name="value">The capability set to project.</param>
    /// <returns>
    ///     A read-only list of camelCase capability names for each flag present in
    ///     <paramref name="value"/>, ordered by declaration; empty when no flags are set.
    /// </returns>
    /// <remarks>
    ///     Used to serialize capability sets to JSON and to render them in summaries. Returns an
    ///     <see cref="IReadOnlyList{T}"/> so callers cannot mutate the shared ordering. Pure and
    ///     thread-safe.
    /// </remarks>
    public static IReadOnlyList<string> ToCamelCaseNames(this ExtractorCapabilities value)
    {
        // Emit names in the fixed declaration order so serialized output is deterministic
        var names = new List<string>(OrderedFlags.Length);
        foreach (var (flag, name) in OrderedFlags)
        {
            if (value.HasFlag(flag))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    ///     Counts how many individual capability flags are set.
    /// </summary>
    /// <param name="value">The capability set to inspect.</param>
    /// <returns>The number of distinct capability flags present in <paramref name="value"/>.</returns>
    /// <remarks>
    ///     Selection ranking compares the number of satisfied capabilities, so this population
    ///     count is the tie-agnostic fidelity measure. Counting over
    ///     <see cref="OrderedFlags"/> ignores any undefined bits. Pure and thread-safe.
    /// </remarks>
    public static int CountFlags(this ExtractorCapabilities value)
    {
        // Count only the named capability flags so undefined bits never inflate the total
        var count = 0;
        foreach (var (flag, _) in OrderedFlags)
        {
            if (value.HasFlag(flag))
            {
                count++;
            }
        }

        return count;
    }
}
