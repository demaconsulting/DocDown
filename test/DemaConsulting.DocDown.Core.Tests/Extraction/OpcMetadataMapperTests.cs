using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="OpcMetadataMapper"/>, proving the shared OPC mapping rules:
///     omit-empty fields, the interesting-absence list, provenance tagging, and date normalization
///     to ISO-8601 UTC.
/// </summary>
public class OpcMetadataMapperTests
{
    /// <summary>
    ///     Proves populated values become fields with <see cref="MetadataProvenance.OpcCoreProperties"/>
    ///     provenance and that dates normalize to ISO-8601 UTC.
    /// </summary>
    [Fact]
    public void OpcMetadataMapper_From_PopulatedProperties_MapsFieldsWithProvenanceAndIsoDates()
    {
        var properties = Empty() with
        {
            Creator = "Ada Lovelace",
            LastModifiedBy = "Babbage, Charles",
            Created = new DateTime(2026, 3, 23, 9, 26, 0, DateTimeKind.Utc),
            Modified = new DateTime(2026, 7, 7, 14, 58, 0, DateTimeKind.Utc),
            Revision = "864"
        };

        var metadata = OpcMetadataMapper.From(properties);

        Assert.Equal("Ada Lovelace", Value(metadata, "creator"));
        Assert.Equal("Babbage, Charles", Value(metadata, "lastModifiedBy"));
        Assert.Equal("2026-03-23T09:26:00Z", Value(metadata, "created"));
        Assert.Equal("2026-07-07T14:58:00Z", Value(metadata, "modified"));
        Assert.Equal("864", Value(metadata, "revision"));
        Assert.All(metadata.Fields, field => Assert.Equal(MetadataProvenance.OpcCoreProperties, field.Source));
    }

    /// <summary>
    ///     Proves a blank value is never emitted as a field (omit-empty), while an interesting blank
    ///     field is recorded as absent.
    /// </summary>
    [Fact]
    public void OpcMetadataMapper_From_BlankValues_OmittedAsFieldsAndRecordedAbsent()
    {
        var properties = Empty() with { Creator = "A", Title = "  ", Subject = null };

        var metadata = OpcMetadataMapper.From(properties);

        Assert.DoesNotContain(metadata.Fields, field => field.Name == "title");
        Assert.DoesNotContain(metadata.Fields, field => field.Name == "subject");
        Assert.Equal(["title", "subject", "keywords", "category", "contentStatus"], metadata.AbsentInteresting);
    }

    /// <summary>
    ///     Proves an interesting field that is populated is not listed as absent.
    /// </summary>
    [Fact]
    public void OpcMetadataMapper_From_PopulatedInterestingField_NotRecordedAbsent()
    {
        var metadata = OpcMetadataMapper.From(Empty() with { Title = "Real Title" });

        Assert.Equal("Real Title", Value(metadata, "title"));
        Assert.DoesNotContain("title", metadata.AbsentInteresting);
    }

    /// <summary>
    ///     Proves a date with an unspecified kind is treated as UTC rather than shifted by the local
    ///     offset, keeping the artifact platform-independent.
    /// </summary>
    [Fact]
    public void OpcMetadataMapper_From_UnspecifiedKindDate_TreatedAsUtc()
    {
        var properties = Empty() with { Created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified) };

        var metadata = OpcMetadataMapper.From(properties);

        Assert.Equal("2026-01-02T03:04:05Z", Value(metadata, "created"));
    }

    /// <summary>
    ///     Proves an empty property bag maps to an empty field list, a genuine "nothing readable"
    ///     statement, while still recording the interesting fields as absent.
    /// </summary>
    [Fact]
    public void OpcMetadataMapper_From_EmptyProperties_ProducesNoFields()
    {
        var metadata = OpcMetadataMapper.From(Empty());

        Assert.Empty(metadata.Fields);
        Assert.Equal(["title", "subject", "keywords", "category", "contentStatus"], metadata.AbsentInteresting);
    }

    /// <summary>Builds an all-null core-property snapshot to specialize per test with a <c>with</c> expression.</summary>
    /// <returns>An <see cref="OpcCoreProperties"/> with every member null.</returns>
    /// <remarks>Keeps each test terse by naming only the properties it exercises.</remarks>
    private static OpcCoreProperties Empty() => new(
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>Reads a mapped field's value by name.</summary>
    /// <param name="metadata">The mapped metadata.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field value, or <see langword="null"/> when absent.</returns>
    private static string? Value(DocumentMetadata metadata, string name) =>
        metadata.Fields.FirstOrDefault(field => field.Name == name)?.Value;
}
