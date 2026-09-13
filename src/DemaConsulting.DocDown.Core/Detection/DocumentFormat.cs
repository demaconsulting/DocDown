namespace DocDown.Core;

/// <summary>
///     Identifies a document format by a short stable identifier and its IANA media type.
/// </summary>
/// <param name="Id">
///     The short, lowercase, stable identifier for the format (for example <c>pdf</c>).
///     Used in manifest output and as an extractor lookup key, so it must remain stable
///     across releases.
/// </param>
/// <param name="MediaType">
///     The IANA media type describing the format (for example <c>application/pdf</c>).
/// </param>
/// <remarks>
///     Modeled as a <see langword="readonly"/> <see langword="record struct"/> so that a
///     format value can be compared, copied, and used as a dictionary key cheaply and without
///     heap allocation. Instances are immutable and therefore inherently thread-safe.
///     <para>
///         Because this is a record struct, a <c>default(DocumentFormat)</c> value has
///         <see langword="null"/> <see cref="Id"/> and <see cref="MediaType"/> strings. The
///         members here are written to tolerate that state: <see cref="IsUnknown"/> treats a
///         <see langword="null"/> identifier as unknown so a defaulted value never masquerades
///         as a real, recognized format.
///     </para>
/// </remarks>
public readonly record struct DocumentFormat(string Id, string MediaType)
{
    /// <summary>
    ///     The identifier value used to represent an unrecognized format.
    /// </summary>
    /// <remarks>
    ///     Declared as a named constant so the sentinel is defined in exactly one place and the
    ///     null-safe comparison in <see cref="IsUnknown"/> cannot drift from the value produced
    ///     by <see cref="Unknown"/>.
    /// </remarks>
    private const string UnknownId = "unknown";

    /// <summary>
    ///     Gets the well-known Portable Document Format descriptor.
    /// </summary>
    /// <remarks>
    ///     Exposed as a static property (rather than a static field) so the value is produced on
    ///     demand and callers cannot accidentally hold or mutate shared boxed state.
    /// </remarks>
    public static DocumentFormat Pdf => new("pdf", "application/pdf");

    /// <summary>
    ///     Gets the well-known Office Open XML word-processing (<c>.docx</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Docx => new("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

    /// <summary>
    ///     Gets the well-known legacy binary Word (<c>.doc</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Names the legacy binary word-processing format so Core can detect it and report a
    ///     format-specific failure rather than an unrecognized-format one; Core does not extract
    ///     it. Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Doc => new("doc", "application/msword");

    /// <summary>
    ///     Gets the well-known Office Open XML spreadsheet (<c>.xlsx</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Xlsx => new("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    /// <summary>
    ///     Gets the well-known legacy binary Excel (<c>.xls</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Names the legacy binary spreadsheet format so Core can detect it and report a
    ///     format-specific failure rather than an unrecognized-format one; Core does not extract
    ///     it. Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Xls => new("xls", "application/vnd.ms-excel");

    /// <summary>
    ///     Gets the well-known Office Open XML presentation (<c>.pptx</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Pptx => new("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation");

    /// <summary>
    ///     Gets the well-known legacy binary PowerPoint (<c>.ppt</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Names the legacy binary presentation format so Core can detect it and report a
    ///     format-specific failure rather than an unrecognized-format one; Core does not extract
    ///     it. Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Ppt => new("ppt", "application/vnd.ms-powerpoint");

    /// <summary>
    ///     Gets the well-known Visio drawing (<c>.vsdx</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Vsdx => new("vsdx", "application/vnd.ms-visio.drawing");

    /// <summary>
    ///     Gets the well-known macro-enabled Visio drawing (<c>.vsdm</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     The macro-enabled sibling of <c>.vsdx</c>: the same modern Open Packaging Conventions
    ///     drawing with an embedded macro project, carrying its own IANA media type. DocDown treats it
    ///     as a drawing (its pages, shapes, and connectors are read exactly as a <c>.vsdx</c>), never
    ///     as a stencil or template. Exposed as a static property so each read yields an independent
    ///     immutable value.
    /// </remarks>
    public static DocumentFormat Vsdm => new("vsdm", "application/vnd.ms-visio.drawing.macroenabled.12");

    /// <summary>
    ///     Gets the well-known legacy binary Visio (<c>.vsd</c>) descriptor.
    /// </summary>
    /// <remarks>
    ///     Names the legacy binary Visio drawing format with its own IANA media type
    ///     (<c>application/vnd.visio</c>, distinct from the modern <c>.vsdx</c> type) so Core can
    ///     detect it and report a format-specific failure rather than an unrecognized-format one;
    ///     Core does not extract it. Exposed as a static property so each read yields an
    ///     independent immutable value.
    /// </remarks>
    public static DocumentFormat Vsd => new("vsd", "application/vnd.visio");

    /// <summary>
    ///     Gets the well-known HTML descriptor.
    /// </summary>
    /// <remarks>
    ///     Exposed as a static property so each read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Html => new("html", "text/html");

    /// <summary>
    ///     Gets the well-known plain-text descriptor.
    /// </summary>
    /// <remarks>
    ///     Plain text is the one format Core can both detect (from file extension) and exercise
    ///     end-to-end without an external backend package, which is why it is a first-class
    ///     well-known format. Exposed as a static property so each read yields an independent
    ///     immutable value.
    /// </remarks>
    public static DocumentFormat Text => new("text", "text/plain");

    /// <summary>
    ///     Gets the descriptor used when the format could not be recognized.
    /// </summary>
    /// <remarks>
    ///     Uses the generic <c>application/octet-stream</c> media type because an unrecognized
    ///     stream carries no more specific type guarantee. Exposed as a static property so each
    ///     read yields an independent immutable value.
    /// </remarks>
    public static DocumentFormat Unknown => new(UnknownId, "application/octet-stream");

    /// <summary>
    ///     Gets a value indicating whether this descriptor represents an unrecognized format.
    /// </summary>
    /// <remarks>
    ///     Written to be null-safe so that a <c>default(DocumentFormat)</c> value (whose
    ///     <see cref="Id"/> is <see langword="null"/>) is reported as unknown. This prevents a
    ///     defaulted value from being mistaken for a recognized format by downstream selection
    ///     logic.
    /// </remarks>
    public bool IsUnknown => Id is null || string.Equals(Id, UnknownId, StringComparison.Ordinal);

    /// <summary>
    ///     Creates a custom format descriptor for a format Core does not define natively.
    /// </summary>
    /// <param name="id">The short stable identifier for the format. Must not be null or empty.</param>
    /// <param name="mediaType">The IANA media type for the format. Must not be null or empty.</param>
    /// <returns>A <see cref="DocumentFormat"/> carrying the supplied identifier and media type.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="id"/> or <paramref name="mediaType"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     Validation happens here so that a malformed custom format is refused at the point of
    ///     creation rather than surfacing later as a confusing lookup miss. This method is pure
    ///     and thread-safe.
    /// </remarks>
    public static DocumentFormat Custom(string id, string mediaType)
    {
        // Reject empty inputs immediately so a custom format always has a usable identity
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(mediaType);
        return new DocumentFormat(id, mediaType);
    }

    /// <summary>
    ///     Returns a human-readable representation combining the identifier and media type.
    /// </summary>
    /// <returns>A string in the form <c>{Id} ({MediaType})</c>, for example <c>pdf (application/pdf)</c>.</returns>
    /// <remarks>
    ///     Overrides the record-generated <c>ToString</c> to produce a compact display form that
    ///     is embedded verbatim in summary and detection descriptions. Has no side effects.
    /// </remarks>
    public override string ToString() => $"{Id} ({MediaType})";
}
