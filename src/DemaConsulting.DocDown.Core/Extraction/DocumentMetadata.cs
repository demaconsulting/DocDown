namespace DocDown.Core;

/// <summary>
///     The origin of a self-reported metadata value, so an authored claim is never confused with a
///     backend-derived one.
/// </summary>
/// <remarks>
///     Provenance is the whole point of <c>metadata.json</c>: the artifact records <em>what the
///     document asserts about itself</em>, and a reader must be able to tell an OPC core property
///     apart from a PDF document-information entry apart from a value a backend merely inferred. The
///     set is closed and each member projects to a fixed camelCase string in the serialized form, so
///     the vocabulary stays stable across releases.
/// </remarks>
public enum MetadataProvenance
{
    /// <summary>
    ///     The value came from the Open Packaging Conventions core properties
    ///     (<c>docProps/core.xml</c>), shared by Word, Excel, PowerPoint, and Visio.
    /// </summary>
    OpcCoreProperties,

    /// <summary>The value came from a PDF's document information dictionary via PdfPig.</summary>
    PdfDocumentInformation,

    /// <summary>
    ///     The value was derived by a backend rather than read from an authored property (for
    ///     example a title taken from a slide). Kept distinct so derived values never blend with
    ///     authored ones.
    /// </summary>
    BackendHeuristic
}

/// <summary>
///     One populated metadata field: its stable name, its verbatim value, and where the value came
///     from.
/// </summary>
/// <param name="Name">
///     The field's stable camelCase name (for example <c>creator</c>, <c>modified</c>), used as the
///     JSON key and as the identity a summary or test refers to.
/// </param>
/// <param name="Value">
///     The value exactly as read (dates already normalized to ISO-8601 UTC by the mapper), preserved
///     at full length. Never blank: a blank value is an absence, not a field.
/// </param>
/// <param name="Source">The provenance of the value, so authored and derived claims stay separable.</param>
/// <remarks>
///     Only genuinely present values become fields; the omit-empty rule is applied by the mapper
///     before a field is created, so every field in a <see cref="DocumentMetadata"/> is real.
///     Immutable and thread-safe.
/// </remarks>
public sealed record DocumentMetadataField(string Name, string Value, MetadataProvenance Source);

/// <summary>
///     What a document asserts about itself: the populated metadata fields plus the interesting
///     fields the property bag exposed but left blank.
/// </summary>
/// <param name="Fields">
///     The populated fields, in a stable order, each with its provenance. Only real values appear;
///     blank values are never emitted as fields (the omit-empty rule).
/// </param>
/// <param name="AbsentInteresting">
///     The names of interesting fields the source exposes but which were blank, recorded so the
///     artifact itself can state "the document supplied no value" where a reader would expect one
///     (for example <c>title</c>). Distinct from fields the source does not expose at all, which are
///     simply not mentioned.
/// </param>
/// <remarks>
///     <para>
///         This record is the backend-neutral carrier between a reader (which maps a property bag to
///         it) and <c>MetadataWriter</c> (which serializes it to <c>metadata.json</c>). Separating
///         it from <see cref="DocumentInfo"/> keeps authored, provenance-tagged claims apart from the
///         orientation and integrity metadata (title, counts) the manifest and headings use, which is
///         exactly the "do not blend authored and derived" rule the artifact exists to honor.
///     </para>
///     <para>
///         An instance whose <see cref="Fields"/> and <see cref="AbsentInteresting"/> are both empty
///         is a genuine "this backend read nothing" statement, which the writer renders as the sparse
///         form rather than an unexplained empty object. Immutable and thread-safe.
///     </para>
/// </remarks>
public sealed record DocumentMetadata(
    IReadOnlyList<DocumentMetadataField> Fields,
    IReadOnlyList<string> AbsentInteresting);
