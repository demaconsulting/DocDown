using System.Globalization;

namespace DocDown.Core;

/// <summary>
///     A backend-neutral snapshot of an Open Packaging Conventions core-property bag, decoupling the
///     shared metadata mapper from the concrete property type each backend exposes.
/// </summary>
/// <param name="Creator">The document creator (author), or <see langword="null"/> when absent.</param>
/// <param name="LastModifiedBy">The last-modifying user, or <see langword="null"/> when absent.</param>
/// <param name="Created">The creation timestamp, or <see langword="null"/> when absent.</param>
/// <param name="Modified">The last-modification timestamp, or <see langword="null"/> when absent.</param>
/// <param name="Revision">The revision label, or <see langword="null"/> when absent.</param>
/// <param name="Title">The document title, or <see langword="null"/> when absent.</param>
/// <param name="Subject">The subject, or <see langword="null"/> when absent.</param>
/// <param name="Keywords">The keywords, or <see langword="null"/> when absent.</param>
/// <param name="Category">The category, or <see langword="null"/> when absent.</param>
/// <param name="ContentStatus">The content status, or <see langword="null"/> when absent.</param>
/// <param name="Description">The description, or <see langword="null"/> when absent.</param>
/// <param name="LastPrinted">The last-printed timestamp, or <see langword="null"/> when absent.</param>
/// <param name="Version">The version label, or <see langword="null"/> when absent.</param>
/// <param name="Language">The language, or <see langword="null"/> when absent.</param>
/// <param name="Identifier">The identifier, or <see langword="null"/> when absent.</param>
/// <remarks>
///     The Open XML SDK exposes core properties as <c>IPackageProperties</c> while
///     <c>System.IO.Packaging</c> exposes them as <c>PackageProperties</c>; the two are unrelated
///     types with the same members, so neither Core nor the mapper can accept both directly. Each
///     backend copies its property bag into this record — a trivial field-for-field copy — and the
///     single set of honesty rules (omit-empty, interesting-absence, provenance, date normalization)
///     then lives in exactly one place. Immutable and thread-safe.
/// </remarks>
public sealed record OpcCoreProperties(
    string? Creator,
    string? LastModifiedBy,
    DateTime? Created,
    DateTime? Modified,
    string? Revision,
    string? Title,
    string? Subject,
    string? Keywords,
    string? Category,
    string? ContentStatus,
    string? Description,
    DateTime? LastPrinted,
    string? Version,
    string? Language,
    string? Identifier);

/// <summary>
///     Maps an Open Packaging Conventions core-property snapshot to a <see cref="DocumentMetadata"/>,
///     applying the omit-empty, interesting-absence, and provenance rules once for every OPC backend.
/// </summary>
/// <remarks>
///     <para>
///         Word, Excel, PowerPoint (Open XML SDK), and Visio (<c>System.IO.Packaging</c>) all carry
///         the same OPC core properties but expose them through different, unrelated types; each
///         backend copies its bag into an <see cref="OpcCoreProperties"/> and hands it here, so the
///         rules that make <c>metadata.json</c> honest live in exactly one place.
///     </para>
///     <para>
///         The rules, each tracing to a request requirement: a blank value is never emitted as a
///         field (omit-empty); the interesting fields a document commonly leaves blank
///         (<c>title</c>, <c>subject</c>, <c>keywords</c>, <c>category</c>, <c>contentStatus</c>) are
///         recorded as absent so the artifact itself can state the document supplied no value; dates
///         are normalized to ISO-8601 UTC; and every value carries
///         <see cref="MetadataProvenance.OpcCoreProperties"/>. Nothing is invented — an absent value
///         stays absent. Stateless, pure, and thread-safe.
///     </para>
/// </remarks>
public static class OpcMetadataMapper
{
    /// <summary>
    ///     The interesting fields recorded as absent when blank, in the order they are reported.
    /// </summary>
    /// <remarks>
    ///     These are the fields a reader expects a document might carry but which are commonly left
    ///     blank; naming them in the artifact turns silence into an explicit "the document supplied
    ///     no value" rather than an omission the reader cannot distinguish from a field the source
    ///     never exposes.
    /// </remarks>
    private static readonly string[] InterestingFieldOrder =
        ["title", "subject", "keywords", "category", "contentStatus"];

    /// <summary>
    ///     Maps a package's core properties to a <see cref="DocumentMetadata"/>.
    /// </summary>
    /// <param name="properties">The core-property bag to map. Must not be null.</param>
    /// <returns>
    ///     The document metadata: only populated fields (verbatim, provenance
    ///     <see cref="MetadataProvenance.OpcCoreProperties"/>, dates in ISO-8601 UTC) and the
    ///     interesting fields that were blank. An empty result is a genuine "the document supplied
    ///     nothing readable" statement.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="properties"/> is null.</exception>
    /// <remarks>
    ///     Field order is fixed so the serialized artifact is deterministic regardless of the order
    ///     the property bag enumerates. Pure and side-effect free.
    /// </remarks>
    public static DocumentMetadata From(OpcCoreProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var fields = new List<DocumentMetadataField>();

        // Populated text fields in canonical order; blanks are dropped by AddText (omit-empty)
        AddText(fields, "creator", properties.Creator);
        AddText(fields, "lastModifiedBy", properties.LastModifiedBy);
        AddDate(fields, "created", properties.Created);
        AddDate(fields, "modified", properties.Modified);
        AddText(fields, "revision", properties.Revision);
        AddText(fields, "title", properties.Title);
        AddText(fields, "subject", properties.Subject);
        AddText(fields, "keywords", properties.Keywords);
        AddText(fields, "category", properties.Category);
        AddText(fields, "contentStatus", properties.ContentStatus);
        AddText(fields, "description", properties.Description);
        AddDate(fields, "lastPrinted", properties.LastPrinted);
        AddText(fields, "version", properties.Version);
        AddText(fields, "language", properties.Language);
        AddText(fields, "identifier", properties.Identifier);

        // An interesting field is absent only when it produced no populated field above
        var present = new HashSet<string>(fields.Select(field => field.Name), StringComparer.Ordinal);
        var absent = InterestingFieldOrder.Where(name => !present.Contains(name)).ToList();

        return new DocumentMetadata(fields, absent);
    }

    /// <summary>
    ///     Adds a text field when its value carries information, dropping blanks.
    /// </summary>
    /// <param name="fields">The field list to append to.</param>
    /// <param name="name">The field's stable camelCase name.</param>
    /// <param name="value">The raw property value, which may be null or blank.</param>
    /// <remarks>
    ///     A blank value is an absence, not a field, so it is never emitted; the trimmed value is
    ///     stored verbatim at full length. Side effect: appends to <paramref name="fields"/>.
    /// </remarks>
    private static void AddText(List<DocumentMetadataField> fields, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        fields.Add(new DocumentMetadataField(name, value.Trim(), MetadataProvenance.OpcCoreProperties));
    }

    /// <summary>
    ///     Adds a date field, normalized to ISO-8601 UTC, when a value is present.
    /// </summary>
    /// <param name="fields">The field list to append to.</param>
    /// <param name="name">The field's stable camelCase name.</param>
    /// <param name="value">The raw date value, or <see langword="null"/> when the property is absent.</param>
    /// <remarks>
    ///     OPC core-property dates are stored as UTC; a value that arrives with an unspecified kind is
    ///     therefore treated as UTC rather than shifted by the local offset, and any other kind is
    ///     converted to UTC. The result is written as <c>yyyy-MM-ddTHH:mm:ssZ</c> so the artifact is
    ///     platform-independent. Side effect: appends to <paramref name="fields"/>.
    /// </remarks>
    private static void AddDate(List<DocumentMetadataField> fields, string name, DateTime? value)
    {
        if (value is not { } date)
        {
            return;
        }

        var utc = date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
        var text = utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        fields.Add(new DocumentMetadataField(name, text, MetadataProvenance.OpcCoreProperties));
    }
}
