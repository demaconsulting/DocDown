using System.Text;
using System.Text.Json;

namespace DocDown.Core;

/// <summary>
///     Renders <c>metadata.json</c>, the root artifact carrying what a document asserts about
///     itself: the populated self-reported fields with per-field provenance, the interesting fields
///     the document left blank, and a note whenever there is sparseness to explain.
/// </summary>
/// <remarks>
///     <para>
///         <c>metadata.json</c> is the third machine-readable root artifact, distinct in purpose from
///         its siblings: <c>summary.txt</c> orients a reader, <c>manifest.json</c> is the
///         machine-readable inventory of what was written, and <c>metadata.json</c> is read on
///         demand for
///         the document's own claims. It is <strong>always written</strong>, on both the success and
///         failure paths, so a reader can always find it; when the backend supplied nothing it says
///         so in a note rather than emitting an unexplained empty object.
///     </para>
///     <para>
///         The JSON is produced with a hand-rolled <see cref="Utf8JsonWriter"/> rather than the
///         source-generated manifest context, because the two artifacts have opposite policies: the
///         manifest serializes nulls explicitly, whereas metadata.json omits blank values entirely.
///         Writing it by hand also gives full control over key order and the "never a bare
///         <c>{}</c>" rule, and stays trimming/AOT/single-file safe with no reflection. The writer
///         holds no state and performs filesystem I/O through the scratch-folder gate; it is intended
///         for the single extraction flow, not concurrent invocation against one folder.
///     </para>
/// </remarks>
public static class MetadataWriter
{
    /// <summary>The fixed relative name of the metadata file.</summary>
    /// <remarks>Part of the invariant output contract; never varies.</remarks>
    private const string MetadataFileName = "metadata.json";

    /// <summary>The schema version of the <c>metadata.json</c> artifact itself.</summary>
    /// <remarks>
    ///     Independent of the manifest schema version: metadata.json is a separate artifact with its
    ///     own shape, so it carries its own first version rather than tracking the manifest's.
    /// </remarks>
    private const string MetadataSchemaVersion = "1.0";

    /// <summary>The note emitted when the backend could read no self-reported metadata at all.</summary>
    private const string NothingReadableNote = "The document supplied no metadata that this backend could read.";

    /// <summary>
    ///     Writes <c>metadata.json</c> to the scratch folder from the sink's reported metadata.
    /// </summary>
    /// <param name="folder">The scratch folder to write into. Must not be null.</param>
    /// <param name="sink">The sink holding the reported document metadata. Must not be null.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="folder"/> or <paramref name="sink"/> is null.</exception>
    /// <remarks>
    ///     Always writes a file: a backend that reported nothing yields the sparse form (an empty
    ///     <c>document</c> object explained by a note), never an unexplained empty object. Performs
    ///     filesystem I/O.
    /// </remarks>
    public static async ValueTask WriteAsync(ScratchFolder folder, ExtractionSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(sink);

        var json = Render(sink.DocumentMetadata);
        await folder.WriteTextAsync(MetadataFileName, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Renders the metadata document to its JSON text.
    /// </summary>
    /// <param name="metadata">The reported metadata, or <see langword="null"/> when none was reported.</param>
    /// <returns>The indented JSON text, terminated with a single trailing newline.</returns>
    /// <remarks>
    ///     Separated from the write so the exact bytes can be asserted without touching the
    ///     filesystem. Applies every schema rule — omit-empty fields, an <c>absent</c> array only
    ///     when non-empty, and a <c>notes</c> entry whenever there is sparseness to explain. Pure.
    /// </remarks>
    internal static string Render(DocumentMetadata? metadata)
    {
        var fields = metadata?.Fields ?? [];
        var absent = metadata?.AbsentInteresting ?? [];

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", MetadataSchemaVersion);

            // The document object holds only populated fields, each as { value, source }
            writer.WriteStartObject("document");
            foreach (var field in fields)
            {
                writer.WriteStartObject(field.Name);
                writer.WriteString("value", field.Value);
                writer.WriteString("source", ProvenanceString(field.Source));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            // Interesting fields the document left blank are named so their absence is explicit
            if (absent.Count > 0)
            {
                writer.WriteStartArray("absent");
                foreach (var name in absent)
                {
                    writer.WriteStringValue(name);
                }

                writer.WriteEndArray();
            }

            // Never a bare {}: any sparseness carries a note that states it in words
            var note = ResolveNote(fields.Count, absent);
            if (note is not null)
            {
                writer.WriteStartArray("notes");
                writer.WriteStringValue(note);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>
    ///     Resolves the single note, if any, that explains the document's sparseness.
    /// </summary>
    /// <param name="fieldCount">The number of populated fields.</param>
    /// <param name="absent">The interesting fields that were blank.</param>
    /// <returns>The note text, or <see langword="null"/> when nothing needs explaining.</returns>
    /// <remarks>
    ///     When nothing at all was read, the note states that; when some interesting fields were
    ///     blank, the note names them so the file itself records the absence; when the document was
    ///     fully populated with no interesting field missing, no note is needed. Pure.
    /// </remarks>
    private static string? ResolveNote(int fieldCount, IReadOnlyList<string> absent)
    {
        if (fieldCount == 0 && absent.Count == 0)
        {
            return NothingReadableNote;
        }

        return absent.Count > 0
            ? "The document supplied no value for: " + string.Join(", ", absent) + "."
            : null;
    }

    /// <summary>
    ///     Projects a provenance enum to its fixed camelCase JSON string.
    /// </summary>
    /// <param name="provenance">The provenance to project.</param>
    /// <returns>The stable camelCase source string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provenance is not a known member.</exception>
    /// <remarks>Kept as an explicit switch so an unmapped future member is a compile-visible gap, not a silent default. Pure.</remarks>
    private static string ProvenanceString(MetadataProvenance provenance) => provenance switch
    {
        MetadataProvenance.OpcCoreProperties => "opcCoreProperties",
        MetadataProvenance.PdfDocumentInformation => "pdfDocumentInformation",
        MetadataProvenance.BackendHeuristic => "backendHeuristic",
        _ => throw new ArgumentOutOfRangeException(nameof(provenance), provenance, "Unknown metadata provenance.")
    };
}
