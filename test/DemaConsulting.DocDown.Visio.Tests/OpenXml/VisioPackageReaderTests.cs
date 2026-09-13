using DemaConsulting.DocDown.Visio.Tests.TestData;
using DocDown.Visio.OpenXml;

namespace DemaConsulting.DocDown.Visio.Tests.OpenXml;

/// <summary>
///     Unit tests for <see cref="VisioPackageReader"/>, exercising page names, shape text, and — the
///     headline capability — the resolved directed connector topology against drawings synthesized at
///     test time.
/// </summary>
public class VisioPackageReaderTests
{
    /// <summary>
    ///     Proves the reader surfaces the page name, because engineers navigate and cite drawings by
    ///     page name.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_WashSystem_SurfacesPageName()
    {
        using var stream = new MemoryStream(VsdxFixtures.WashSystem());

        var model = VisioPackageReader.Read(stream);

        var page = Assert.Single(model.Pages);
        Assert.Equal("Wash System", page.Name);
    }

    /// <summary>
    ///     Proves the reader extracts the text of every shape that carries text.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_WashSystem_ExtractsShapeText()
    {
        using var stream = new MemoryStream(VsdxFixtures.WashSystem());

        var model = VisioPackageReader.Read(stream);

        var texts = model.Pages[0].Shapes.Select(shape => shape.Text).ToList();
        Assert.Contains("Inlet Tank", texts);
        Assert.Contains("Transfer Pump", texts);
        Assert.Contains("Outlet Valve", texts);
    }

    /// <summary>
    ///     Proves a Wingdings-font run's <c>0xE0</c> is recovered as the arrow the drawing draws,
    ///     while an identical character in an ordinary run is left exactly as authored.
    /// </summary>
    /// <remarks>
    ///     Visio stores a Wingdings arrow as the byte <c>0xE0</c>, which decodes to the Latin letter
    ///     <c>à</c>, so a valve-positioning table reads <c>IN à OUT</c>. The only evidence that the
    ///     glyph is an arrow is the run's font, so the mapping must be gated on it: the second shape
    ///     here carries the same character as ordinary text, standing in for the French a blind
    ///     replacement would corrupt.
    /// </remarks>
    [Fact]
    public void VisioPackageReader_Read_SymbolFontRun_RecoversArrowWithoutCorruptingText()
    {
        // Arrange: a drawing with one Wingdings arrow run and one unstyled ordinary run
        using var stream = new MemoryStream(VsdxFixtures.SymbolFontArrows());

        // Act: read the drawing
        var model = VisioPackageReader.Read(stream);

        // Assert: the symbol-font code point becomes its documented Unicode arrow; the text stands
        var texts = model.Pages[0].Shapes.Select(shape => shape.Text).ToList();
        Assert.Contains("IN \u2794 OUT", texts);
        Assert.Contains("Valve \u00e0 5 bar", texts);
    }

    /// <summary>
    ///     Proves the reader resolves connector records into real directed edges between shapes, with
    ///     the direction the <c>FromCell</c> of <c>BeginX</c>/<c>EndX</c> encodes.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_WashSystem_ResolvesDirectedTopology()
    {
        using var stream = new MemoryStream(VsdxFixtures.WashSystem());

        var model = VisioPackageReader.Read(stream);

        var page = model.Pages[0];
        Assert.Equal(2, page.Connections.Count);
        Assert.Contains(page.Connections, connection => connection.FromId == "1" && connection.ToId == "2");
        Assert.Contains(page.Connections, connection => connection.FromId == "2" && connection.ToId == "3");
    }

    /// <summary>
    ///     Proves the reader returns pages in document order with their names.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_TwoPages_ReturnsPagesInOrder()
    {
        using var stream = new MemoryStream(VsdxFixtures.TwoPages());

        var model = VisioPackageReader.Read(stream);

        Assert.Equal(2, model.Pages.Count);
        Assert.Equal("Schematic", model.Pages[0].Name);
        Assert.Equal("Legend", model.Pages[1].Name);
    }

    /// <summary>
    ///     Proves an empty page yields no shapes and no connections rather than an error.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_EmptyPage_YieldsNoShapesOrConnections()
    {
        using var stream = new MemoryStream(VsdxFixtures.EmptyPage());

        var model = VisioPackageReader.Read(stream);

        var page = Assert.Single(model.Pages);
        Assert.Empty(page.Shapes);
        Assert.Empty(page.Connections);
    }

    /// <summary>
    ///     Proves a connector whose <c>EndX</c> record appears before its <c>BeginX</c> record still
    ///     resolves to exactly one edge — the root-cause regression for the over-reported topology.
    /// </summary>
    /// <remarks>
    ///     Before the fix, the two ordering guards both admitted the connector when its End record
    ///     came first, adding it to the ordering list twice and emitting a duplicate edge. This
    ///     fixture is the only one that exercises the End-before-Begin path, so it is the test that
    ///     would have failed while the code was wrong.
    /// </remarks>
    [Fact]
    public void VisioPackageReader_Read_ConnectorWithEndRecordBeforeBegin_YieldsSingleEdge()
    {
        using var stream = new MemoryStream(VsdxFixtures.EndRecordBeforeBegin());

        var model = VisioPackageReader.Read(stream);

        var page = Assert.Single(model.Pages);
        Assert.Equal(2, page.Connections.Count);
        Assert.Contains(page.Connections, connection => connection.FromId == "1" && connection.ToId == "2");
        Assert.Contains(page.Connections, connection => connection.FromId == "2" && connection.ToId == "3");
    }

    /// <summary>
    ///     Proves two distinct connector sheets joining the same ordered pair each contribute their
    ///     own edge, because de-duplication is per connector sheet and not per (from, to) pair.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_ParallelConnectors_YieldEdgePerConnector()
    {
        using var stream = new MemoryStream(VsdxFixtures.ParallelConnectors());

        var model = VisioPackageReader.Read(stream);

        var page = Assert.Single(model.Pages);
        Assert.Equal(2, page.Connections.Count);
        Assert.All(page.Connections, connection =>
        {
            Assert.Equal("1", connection.FromId);
            Assert.Equal("2", connection.ToId);
        });
    }

    /// <summary>
    ///     Proves pages that reuse the same shape ids report only their own edges, because each page's
    ///     connectors are resolved against its own part.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_MultiplePagesReusingShapeIds_ReportOnlyOwnEdges()
    {
        using var stream = new MemoryStream(VsdxFixtures.PagesReusingShapeIds());

        var model = VisioPackageReader.Read(stream);

        Assert.Equal(2, model.Pages.Count);

        var first = model.Pages[0];
        var edge = Assert.Single(first.Connections);
        Assert.Equal("1", edge.FromId);
        Assert.Equal("2", edge.ToId);

        var second = model.Pages[1];
        Assert.Equal(2, second.Connections.Count);
        Assert.Contains(second.Connections, connection => connection.FromId == "2" && connection.ToId == "3");
        Assert.Contains(second.Connections, connection => connection.FromId == "3" && connection.ToId == "1");
        Assert.DoesNotContain(second.Connections, connection => connection.FromId == "1" && connection.ToId == "2");
    }

    /// <summary>
    ///     Proves the reader resolves a shape's <c>Master</c> attribute to the master's name, so a
    ///     text-less shape still carries the drawing's own classification of it.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_MasterTypedSchematic_ResolvesMasterNames()
    {
        using var stream = new MemoryStream(VsdxFixtures.MasterTypedSchematic());

        var model = VisioPackageReader.Read(stream);

        var shapes = model.Pages[0].Shapes.ToDictionary(shape => shape.Id, StringComparer.Ordinal);
        Assert.Equal("Tank", shapes["1"].MasterName);
        Assert.Equal("Positive displacement", shapes["2"].MasterName);
        Assert.Equal("Dynamic connector", shapes["3"].MasterName);
    }

    /// <summary>
    ///     Proves a master that declares only <c>NameU</c> is still resolved, because the invariant
    ///     name is present even where a localized one is not.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_MasterWithOnlyNameU_FallsBackToNameU()
    {
        using var stream = new MemoryStream(VsdxFixtures.MasterTypedSchematic());

        var model = VisioPackageReader.Read(stream);

        var shape = Assert.Single(model.Pages[0].Shapes, candidate => candidate.Id == "5");
        Assert.Equal("Cyclone 1", shape.MasterName);
    }

    /// <summary>
    ///     Proves a shape that names no master carries no master name, rather than borrowing one.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_ShapeWithNoMaster_CarriesNoMasterName()
    {
        using var stream = new MemoryStream(VsdxFixtures.MasterTypedSchematic());

        var model = VisioPackageReader.Read(stream);

        var shape = Assert.Single(model.Pages[0].Shapes, candidate => candidate.Id == "4");
        Assert.Null(shape.MasterName);
    }

    /// <summary>
    ///     Proves a shape naming a master the drawing does not declare carries no master name, so a
    ///     dangling reference never becomes an invented type.
    /// </summary>
    [Fact]
    public void VisioPackageReader_Read_DanglingMasterReference_CarriesNoMasterName()
    {
        using var stream = new MemoryStream(VsdxFixtures.DanglingMasterReference());

        var model = VisioPackageReader.Read(stream);

        var shape = Assert.Single(model.Pages[0].Shapes, candidate => candidate.Id == "2");
        Assert.Null(shape.MasterName);
    }

    /// <summary>
    ///     Proves a drawing that declares no masters part at all is read without fault, with every
    ///     shape simply carrying no master name.
    /// </summary>
    /// <remarks>A masters part is optional in a real drawing, so its absence must be an ordinary case rather than a failure.</remarks>
    [Fact]
    public void VisioPackageReader_Read_DrawingWithoutMastersPart_CarriesNoMasterNames()
    {
        using var stream = new MemoryStream(VsdxFixtures.WashSystem());

        var model = VisioPackageReader.Read(stream);

        Assert.All(model.Pages[0].Shapes, shape => Assert.Null(shape.MasterName));
    }
}
