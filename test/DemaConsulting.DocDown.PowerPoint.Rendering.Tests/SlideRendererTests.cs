using DemaConsulting.DocDown.PowerPoint.Rendering;
using DemaConsulting.DocDown.PowerPoint.Rendering.Tests.TestData;

namespace DemaConsulting.DocDown.PowerPoint.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="SlideRenderer"/>, the package's rasterization seam: that it
///     rasterizes a slide to a valid PNG, that the slide count matches the source presentation,
///     and that concurrent renders all succeed with no process-wide lock.
/// </summary>
/// <remarks>
///     These tests exercise the real rasterization and PNG encode through
///     CanvasNet.Pptx/CanvasNet/CanvasNet.Charts, so they are the transitive verification evidence
///     for the CanvasNet.Pptx, CanvasNet, and CanvasNet.Charts OTS items. Rendering is always
///     available in every environment, since CanvasNet.Pptx is a fully-managed library with no
///     native stack that could be absent.
/// </remarks>
public class SlideRendererTests
{
    /// <summary>
    ///     Proves a single slide rasterizes to a valid PNG of plausible dimensions.
    /// </summary>
    [Fact]
    public void SlideRenderer_Render_SingleSlide_ReturnsValidPng()
    {
        // Arrange: the embedded two-slide probe presentation
        var pptx = PptxRenderingFixtures.Probe();

        // Act: rasterize slide 0 at 96 DPI
        var png = SlideRenderer.Render(pptx, 0, 96);

        // Assert: the bytes are a valid PNG with positive dimensions
        Assert.True(Png.HasSignature(png), "the rendered bytes do not carry the PNG signature");
        var (width, height) = Png.ReadDimensions(png);
        Assert.True(width > 0 && height > 0, $"implausible dimensions {width}x{height}");
    }

    /// <summary>
    ///     Proves a higher DPI produces a larger rendered slide.
    /// </summary>
    [Fact]
    public void SlideRenderer_Render_HigherDpi_ProducesLargerImage()
    {
        // Arrange: the embedded probe presentation rendered at two DPIs
        var pptx = PptxRenderingFixtures.Probe();

        // Act
        var low = Png.ReadDimensions(SlideRenderer.Render(pptx, 0, 96));
        var high = Png.ReadDimensions(SlideRenderer.Render(pptx, 0, 192));

        // Assert: doubling the DPI doubles the pixel dimensions
        Assert.True(high.Width > low.Width, "doubling the DPI should widen the rendered slide");
        Assert.True(high.Height > low.Height, "doubling the DPI should heighten the rendered slide");
    }

    /// <summary>
    ///     Proves concurrent renders all succeed with no process-wide lock.
    /// </summary>
    /// <remarks>
    ///     CanvasNet.Pptx is a fully-managed library with no shared mutable per-call state: each
    ///     call opens and disposes its own document instance. Driving several renders in parallel
    ///     and asserting each returns a valid PNG is the observable evidence that independent calls
    ///     do not interfere with one another.
    /// </remarks>
    [Fact]
    public void SlideRenderer_Render_ConcurrentCalls_AllProduceValidPng()
    {
        // Arrange: one presentation rendered from several threads at once
        var pptx = PptxRenderingFixtures.Probe();

        // Act: render in parallel
        var results = new byte[8][];
        Parallel.For(0, results.Length, index => results[index] = SlideRenderer.Render(pptx, 0, 96));

        // Assert: every parallel render produced a valid PNG
        Assert.All(results, png => Assert.True(Png.HasSignature(png)));
    }

    /// <summary>
    ///     Proves the renderer rejects a null presentation.
    /// </summary>
    [Fact]
    public void SlideRenderer_Render_NullPptx_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SlideRenderer.Render(null!, 0, 96));
    }

    /// <summary>
    ///     Proves the slide-count reader rejects a null presentation.
    /// </summary>
    [Fact]
    public void SlideRenderer_GetSlideCount_NullPptx_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SlideRenderer.GetSlideCount(null!));
    }

    /// <summary>
    ///     Proves the slide count matches the number of slides in the source presentation.
    /// </summary>
    [Fact]
    public void SlideRenderer_GetSlideCount_ProbePresentation_ReturnsSlideCount()
    {
        // Arrange: the embedded two-slide probe presentation
        var pptx = PptxRenderingFixtures.Probe();

        // Act
        var count = SlideRenderer.GetSlideCount(pptx);

        // Assert
        Assert.Equal(2, count);
    }
}
