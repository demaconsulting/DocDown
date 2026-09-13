using DemaConsulting.DocDown.Pdf.Rendering.Tests.TestData;
using DocDown.Pdf.Rendering;

namespace DemaConsulting.DocDown.Pdf.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PageRenderer"/>, the native-interop seam: that it rasterizes a page
///     to a valid PNG, that its availability probe reports the native stack honestly without
///     throwing, and that concurrent renders all succeed behind the process-wide lock.
/// </summary>
/// <remarks>
///     These tests exercise the real PDFium raster and SkiaSharp PNG encode, so they are the
///     transitive verification evidence for the PDFium and SkiaSharp OTS items. Scenarios that need
///     the native stack skip when it is unavailable, because absence of the native deployment is an
///     environmental fact rather than a defect in the managed seam.
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
        SkipWhenRendererUnavailable();
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
    ///     Proves the availability probe reports the native stack honestly, without throwing.
    /// </summary>
    [Fact]
    public void PageRenderer_ProbeAvailability_AnyEnvironment_ReflectsUsabilityWithoutThrowing()
    {
        // Act: the probe must not throw
        var probe = PageRenderer.ProbeAvailability();

        // Assert: available results carry no reason, unavailable results carry one
        if (probe.IsAvailable)
        {
            Assert.Null(probe.Reason);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(probe.Reason));
        }
    }

    /// <summary>
    ///     Proves concurrent renders all succeed, exercising the process-wide serialization lock.
    /// </summary>
    /// <remarks>
    ///     PDFium is not thread-safe; <see cref="PageRenderer"/> serializes every call behind a
    ///     single lock. Driving several renders in parallel and asserting each returns a valid PNG is
    ///     the observable proof that the lock keeps concurrent callers from corrupting shared native
    ///     state.
    /// </remarks>
    [Fact]
    public void PageRenderer_Render_ConcurrentCalls_AllProduceValidPng()
    {
        // Arrange: one document rendered from several threads at once
        SkipWhenRendererUnavailable();
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
    ///     Proves an unavailable probe result carries the reason it was constructed with.
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

    /// <summary>
    ///     Skips the calling test when the native PDF renderer is unavailable in this environment.
    /// </summary>
    /// <remarks>
    ///     These scenarios prove real rasterization behavior, which is meaningful only when the
    ///     PDFium-backed deployment can load.
    /// </remarks>
    private static void SkipWhenRendererUnavailable()
    {
        var probe = PageRenderer.ProbeAvailability();
        Assert.SkipWhen(
            !probe.IsAvailable,
            $"PDF page rendering is unavailable in this environment: {probe.Reason}.");
    }
}
