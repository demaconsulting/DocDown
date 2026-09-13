namespace DocDown.Word.Markdown;

/// <summary>
///     The kind of a block in a <see cref="WordDocumentModel"/>.
/// </summary>
/// <remarks>
///     The block vocabulary is the pivot that keeps the entire markdown mapping — the part where
///     Word must beat PDF —
///     testable from a hand-built model with no document and no Open XML SDK. Kept
///     deliberately small: the consumer is a language model, so only the structure that carries
///     meaning is modeled.
/// </remarks>
internal enum WordBlockKind
{
    /// <summary>A heading, whose level is carried by <see cref="WordBlock.HeadingLevel"/>.</summary>
    Heading,

    /// <summary>A plain paragraph of inline runs.</summary>
    Paragraph,

    /// <summary>A list item, whose level and ordering are carried by <see cref="WordBlock.List"/>.</summary>
    ListItem,

    /// <summary>A table, carried by <see cref="WordBlock.Table"/>.</summary>
    Table,

    /// <summary>An embedded image, carried by <see cref="WordBlock.Image"/>.</summary>
    Image,

    /// <summary>An explicit page break, rendered as a thematic break.</summary>
    PageBreak,

    /// <summary>
    ///     A document-control subsection (a surviving header or footer), whose origin label is
    ///     carried by <see cref="WordBlock.Label"/>.
    /// </summary>
    DocumentControl
}
