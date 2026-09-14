using System.Text.Json;
using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Output;

/// <summary>
///     Unit tests for <see cref="MetadataWriter"/>, proving the <c>metadata.json</c> shape: the
///     always-present schema and document object, per-field provenance, omit-empty, the interesting
///     absence list and its note, the never-bare-<c>{}</c> sparse form, and that the file is written
///     for every run.
/// </summary>
public class MetadataWriterTests
{
    /// <summary>Gets the ambient test cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a null metadata (a backend that reported nothing) renders the sparse form: an empty
    ///     document object explained by a note, never an unexplained empty object.
    /// </summary>
    [Fact]
    public void MetadataWriter_Render_NullMetadata_ProducesSparseFormWithNote()
    {
        using var document = JsonDocument.Parse(MetadataWriter.Render(null));
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Empty(root.GetProperty("document").EnumerateObject());
        Assert.False(root.TryGetProperty("absent", out _));
        var note = Assert.Single(root.GetProperty("notes").EnumerateArray());
        Assert.Equal("The document supplied no metadata that this backend could read.", note.GetString());
    }

    /// <summary>
    ///     Proves an empty (no fields, no interesting absences) metadata also renders the sparse form.
    /// </summary>
    [Fact]
    public void MetadataWriter_Render_EmptyMetadata_ProducesSparseForm()
    {
        using var document = JsonDocument.Parse(MetadataWriter.Render(new DocumentMetadata([], [])));
        var root = document.RootElement;

        Assert.Empty(root.GetProperty("document").EnumerateObject());
        Assert.Equal(
            "The document supplied no metadata that this backend could read.",
            root.GetProperty("notes").EnumerateArray().Single().GetString());
    }

    /// <summary>
    ///     Proves each populated field is emitted as a <c>{ value, source }</c> object with the
    ///     provenance projected to its camelCase string, never as a bare scalar.
    /// </summary>
    [Fact]
    public void MetadataWriter_Render_PopulatedFields_EmitsValueAndSourceObjects()
    {
        var metadata = new DocumentMetadata(
        [
            new DocumentMetadataField("creator", "Ada Lovelace", MetadataProvenance.OpcCoreProperties),
            new DocumentMetadataField("producer", "PdfLib", MetadataProvenance.PdfDocumentInformation)
        ], []);

        using var document = JsonDocument.Parse(MetadataWriter.Render(metadata));
        var doc = document.RootElement.GetProperty("document");

        Assert.Equal("Ada Lovelace", doc.GetProperty("creator").GetProperty("value").GetString());
        Assert.Equal("opcCoreProperties", doc.GetProperty("creator").GetProperty("source").GetString());
        Assert.Equal("pdfDocumentInformation", doc.GetProperty("producer").GetProperty("source").GetString());
    }

    /// <summary>
    ///     Proves the interesting absences are emitted as an <c>absent</c> array and summarized in a
    ///     note, so the artifact itself records where the document supplied no value.
    /// </summary>
    [Fact]
    public void MetadataWriter_Render_AbsentInteresting_EmitsAbsentArrayAndNote()
    {
        var metadata = new DocumentMetadata(
            [new DocumentMetadataField("creator", "A", MetadataProvenance.OpcCoreProperties)],
            ["title", "subject"]);

        using var document = JsonDocument.Parse(MetadataWriter.Render(metadata));
        var root = document.RootElement;

        var absent = root.GetProperty("absent").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Equal(["title", "subject"], absent);
        Assert.Equal(
            "The document supplied no value for: title, subject.",
            root.GetProperty("notes").EnumerateArray().Single().GetString());
    }

    /// <summary>
    ///     Proves a fully populated metadata with no interesting absence carries no note and no absent
    ///     array — there is nothing sparse to explain.
    /// </summary>
    [Fact]
    public void MetadataWriter_Render_FullyPopulatedNoAbsence_OmitsNotesAndAbsent()
    {
        var metadata = new DocumentMetadata(
            [new DocumentMetadataField("creator", "A", MetadataProvenance.OpcCoreProperties)], []);

        using var document = JsonDocument.Parse(MetadataWriter.Render(metadata));
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("absent", out _));
        Assert.False(root.TryGetProperty("notes", out _));
        Assert.Equal("A", root.GetProperty("document").GetProperty("creator").GetProperty("value").GetString());
    }

    /// <summary>
    ///     Proves <see cref="MetadataWriter.WriteAsync"/> always writes <c>metadata.json</c> to the
    ///     scratch folder, using the sink's reported metadata.
    /// </summary>
    [Fact]
    public async Task MetadataWriter_WriteAsync_ReportedMetadata_WritesFile()
    {
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, new ExtractionOptions());
        sink.ReportDocumentMetadata(new DocumentMetadata(
            [new DocumentMetadataField("creator", "Author", MetadataProvenance.OpcCoreProperties)], []));

        await MetadataWriter.WriteAsync(folder, sink, Ct);

        var path = Path.Combine(folder.AbsolutePath, "metadata.json");
        Assert.True(File.Exists(path));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, Ct));
        Assert.Equal("Author", document.RootElement.GetProperty("document").GetProperty("creator").GetProperty("value").GetString());
    }

    /// <summary>
    ///     Proves <see cref="MetadataWriter.WriteAsync"/> writes the sparse form when the sink reported
    ///     no metadata, so the file is always present and always explained.
    /// </summary>
    [Fact]
    public async Task MetadataWriter_WriteAsync_NoReportedMetadata_WritesSparseFile()
    {
        using var temp = new TempScratch();
        var folder = ScratchFolder.Prepare(Path.Combine(temp.Path, "out"), ScratchFolderMode.CleanIfDocDownFolder);
        var sink = new ExtractionSink(folder, new ExtractionOptions());

        await MetadataWriter.WriteAsync(folder, sink, Ct);

        var path = Path.Combine(folder.AbsolutePath, "metadata.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, Ct));
        Assert.Empty(document.RootElement.GetProperty("document").EnumerateObject());
        Assert.Single(document.RootElement.GetProperty("notes").EnumerateArray());
    }
}
