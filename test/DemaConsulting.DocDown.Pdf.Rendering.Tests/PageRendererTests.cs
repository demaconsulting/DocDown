using DemaConsulting.DocDown.Pdf.Rendering;
using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PageRenderer"/>, the package's rasterization seam: that it
///     rasterizes a page to a valid PNG, that the page count matches the source document, and
///     that concurrent renders all succeed with no process-wide lock.
/// </summary>
/// <remarks>
///     These tests exercise the real rasterization and PNG encode through CanvasNet.Pdf/CanvasNet,
///     so they are the transitive verification evidence for the CanvasNet.Pdf and CanvasNet OTS
///     items. Rendering is always available in every environment, since CanvasNet.Pdf is a
///     fully-managed library with no native stack that could be absent.
/// </remarks>
public class PageRendererTests
{
    /// <summary>
    ///     Proves a single page rasterizes to a valid PNG of plausible dimensions.
    /// </summary>
    [Fact]
    public void PageRenderer_Render_SinglePage_ReturnsValidPng()
    {
        // Arrange: a generated one-page document
        var pdf = RenderingFixtures.SimpleText();

        // Act: rasterize page 0 at 96 DPI
        var png = PageRenderer.Render(pdf, 0, 96);

        // Assert: the bytes are a valid PNG with positive dimensions
        Assert.True(Png.HasSignature(png), "the rendered bytes do not carry the PNG signature");
        var (width, height) = Png.ReadDimensions(png);
        Assert.True(width > 0 && height > 0, $"implausible dimensions {width}x{height}");
        Assert.True(height > width, "an A4 page should render taller than it is wide");
    }

    /// <summary>
    ///     Proves concurrent renders all succeed with no process-wide lock.
    /// </summary>
    /// <remarks>
    ///     CanvasNet.Pdf is a fully-managed library with no shared mutable per-call state: each call
    ///     opens and disposes its own document instance. Driving several renders in parallel and
    ///     asserting each returns a valid PNG is the observable evidence that independent calls do
    ///     not interfere with one another.
    /// </remarks>
    [Fact]
    public void PageRenderer_Render_ConcurrentCalls_AllProduceValidPng()
    {
        // Arrange: one document rendered from several threads at once
        var pdf = RenderingFixtures.SimpleText();

        // Act: render in parallel
        var results = new byte[8][];
        Parallel.For(0, results.Length, index => results[index] = PageRenderer.Render(pdf, 0, 96));

        // Assert: every parallel render produced a valid PNG
        Assert.All(results, png => Assert.True(Png.HasSignature(png)));
    }

    /// <summary>
    ///     Proves the renderer rejects a null document.
    /// </summary>
    [Fact]
    public void PageRenderer_Render_NullPdf_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PageRenderer.Render(null!, 0, 96));
    }

    /// <summary>
    ///     Proves the page-count reader rejects a null document.
    /// </summary>
    [Fact]
    public void PageRenderer_GetPageCount_NullPdf_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PageRenderer.GetPageCount(null!));
    }

    /// <summary>
    ///     Proves the page count matches the number of pages in the source document.
    /// </summary>
    [Fact]
    public void PageRenderer_GetPageCount_MultiPageDocument_ReturnsPageCount()
    {
        // Arrange: a generated five-page document
        var pdf = RenderingFixtures.MultiPage(5);

        // Act
        var count = PageRenderer.GetPageCount(pdf);

        // Assert
        Assert.Equal(5, count);
    }
}
