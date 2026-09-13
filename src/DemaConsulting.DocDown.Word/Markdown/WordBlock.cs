namespace DocDown.Word.Markdown;

/// <summary>
///     A single block in the backend-neutral document model.
/// </summary>
/// <param name="Kind">The block's kind, which selects the meaningful payload members.</param>
/// <param name="Inlines">
///     The inline runs for a heading, paragraph, list item, or document-control subsection, or
///     <see langword="null"/> for a table, image, or page break.
/// </param>
/// <param name="HeadingLevel">The 1-based heading level for a <see cref="WordBlockKind.Heading"/> block.</param>
/// <param name="List">The list membership for a <see cref="WordBlockKind.ListItem"/> block, else <see langword="null"/>.</param>
/// <param name="Table">The table for a <see cref="WordBlockKind.Table"/> block, else <see langword="null"/>.</param>
/// <param name="Image">The image reference for a <see cref="WordBlockKind.Image"/> block, else <see langword="null"/>.</param>
/// <param name="Label">
///     An origin label for a <see cref="WordBlockKind.DocumentControl"/> subsection (for example
///     <c>Header</c> or <c>Footer (section 2, first page)</c>), else <see langword="null"/>.
/// </param>
/// <remarks>
///     A single record with kind-selected payloads keeps the block sequence uniform and cheap to
///     walk, which is what lets one writer render every kind and one reader per backend populate
///     it. Immutable and thread-safe.
/// </remarks>
internal sealed record WordBlock(
    WordBlockKind Kind,
    IReadOnlyList<WordInline>? Inlines = null,
    int HeadingLevel = 0,
    WordListInfo? List = null,
    WordTableModel? Table = null,
    WordImageRef? Image = null,
    string? Label = null);

/// <summary>
///     A reference to an embedded image, carrying the exact stored bytes and their provenance.
/// </summary>
/// <param name="Bytes">The complete image file bytes, exactly as the document stored them.</param>
/// <param name="MediaType">The image media type (for example <c>image/png</c>), from the part content type.</param>
/// <param name="PreferredName">
///     The base name Core slugs the file from — the selected image text of any usable source, or the
///     media part name, or <see langword="null"/> to let Core name it from its ordinal alone.
/// </param>
/// <param name="SourceRef">The part URI within the package, recorded as provenance, or <see langword="null"/>.</param>
/// <param name="AltText">
///     Descriptive alt text to assert in markdown, present only when a genuinely descriptive source
///     was chosen (an author description, title, caption, or meaningful object name); otherwise
///     <see langword="null"/> so a neutral placeholder is emitted instead of implying a description.
/// </param>
/// <param name="Description">
///     The chosen text recorded in the manifest as image metadata, present for descriptive and
///     contextual (heading) sources alike; <see langword="null"/> when only the media name was
///     available.
/// </param>
/// <param name="DescriptionSource">
///     The camelCase provenance of <paramref name="Description"/> (for example <c>description</c> or
///     <c>heading</c>), or <see langword="null"/> when there is no description.
/// </param>
/// <remarks>
///     An Open XML image part stores a complete image file byte-for-byte, so a reference always
///     describes bytes that can be written through unchanged as a passthrough. The naming, alt-text,
///     and manifest-description fields are populated from the shared image-text policy so a heading
///     is used to help name the file yet never asserted as if it described the picture. Immutable
///     and thread-safe.
/// </remarks>
internal sealed record WordImageRef(
    byte[] Bytes,
    string MediaType,
    string? PreferredName,
    string? SourceRef,
    string? AltText = null,
    string? Description = null,
    string? DescriptionSource = null);
