namespace DocDown.Core;

/// <summary>
///     The category of content that a gap concerns.
/// </summary>
/// <remarks>
///     Classifying a gap by the kind of content affected lets consumers filter and reason about
///     missing content — for example distinguishing missing text from missing rendered pages —
///     without parsing the gap's prose reason.
/// </remarks>
public enum GapKind
{
    /// <summary>Textual content.</summary>
    Text,

    /// <summary>Embedded images.</summary>
    Images,

    /// <summary>Rendered pages.</summary>
    Pages,

    /// <summary>Document structure.</summary>
    Structure,

    /// <summary>Document metadata.</summary>
    Metadata,

    /// <summary>Logical content parts.</summary>
    Parts
}
