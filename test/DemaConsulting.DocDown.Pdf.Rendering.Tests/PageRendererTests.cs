using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DocDown.Pdf.Rendering;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PageRenderer"/>, the native-interop seam: that it rasterizes a page
///     to a valid PNG, that its availability probe reports the deployed native stack without
///     throwing, and that concurrent renders (which its process-wide lock serializes) all succeed.
/// </summary>
/// <remarks>
///     These tests exercise the real PDFium raster and SkiaSharp PNG encode, so they are the
///     transitive verification evidence for the PDFium and SkiaSharp OTS items. They run only where
///     the native stack is deployed, which is every environment the test host runs in.
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

        // Assert: the bytes are a valid PNG with positive dimensions (real PDFium raster + Skia encode)
        Assert.True(Png.HasSignature(png), "the rendered bytes do not carry the PNG signature");
        var (width, height) = Png.ReadDimensions(png);
        Assert.True(width > 0 && height > 0, $"implausible dimensions {width}x{height}");
        Assert.True(height > width, "an A4 page should render taller than it is wide");
    }

    /// <summary>
    ///     Proves the availability probe reports the deployed native stack as usable, without throwing.
    /// </summary>
    [Fact]
    public void PageRenderer_ProbeAvailability_DeployedNativeStack_ReportsAvailableWithoutThrowing()
    {
        // Act: the probe must not throw
        var probe = PageRenderer.ProbeAvailability();

        // Assert: available here, and available results carry no reason
        Assert.True(probe.IsAvailable, probe.Reason);
        Assert.Null(probe.Reason);
    }

    /// <summary>
    ///     Proves concurrent renders all succeed, exercising the process-wide serialization lock.
    /// </summary>
    /// <remarks>
    ///     PDFium is not thread-safe; <see cref="PageRenderer"/> serializes every call behind a
    ///     single lock. Driving several renders in parallel and asserting each returns a valid PNG is
    ///     the observable proof that the lock keeps concurrent callers from corrupting shared native
    ///     state — without the lock this test would flake or crash rather than pass cleanly.
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
    ///     Proves an unavailable probe result carries the reason it was constructed with, which is
    ///     what lets the extractor report an honest, displayable cause when the native stack is
    ///     missing (a case that cannot be reproduced where the stack is deployed).
    /// </summary>
    [Fact]
    public void NativeProbeResult_Unavailable_CarriesReason()
    {
        // Act
        var result = NativeProbeResult.Unavailable("the native binary could not be loaded");

        // Assert
        Assert.False(result.IsAvailable);
        Assert.Equal("the native binary could not be loaded", result.Reason);
    }

    /// <summary>
    ///     Proves an unavailable probe result refuses an empty reason, so a shortfall is never silent.
    /// </summary>
    [Fact]
    public void NativeProbeResult_Unavailable_EmptyReason_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => NativeProbeResult.Unavailable(string.Empty));
    }
}
