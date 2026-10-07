using DocDown.Core;
using DocDown.PowerPoint.Rendering;

namespace DemaConsulting.DocDown.PowerPoint.Rendering.Tests.TestData;

/// <summary>
///     Supplies every PowerPoint presentation the rendering test suite needs.
/// </summary>
/// <remarks>
///     <para>
///         Unlike the PDF rendering suite — which synthesizes every fixture at test time through
///         PdfPig's writer — this suite reads the same real, embedded <c>probe.pptx</c> the
///         production backend's own self-test reads. CanvasNet.Pptx renders the full theme, layout,
///         and master inheritance chain a genuine deck carries, and a deck a test synthesized with
///         only the handful of parts the Open XML reader needs would prove only that the rasterizer
///         agrees with a hand-built document, not that it renders what PowerPoint actually produces.
///         No second binary is committed for this: the bytes are read from the production
///         assembly's own embedded resource through <see cref="SelfTestProbe"/>.
///     </para>
///     <para>
///         The probe carries two slides, each with a title and body text and no speaker notes. All
///         members are static and pure apart from their allocations, and are safe for concurrent use.
///     </para>
/// </remarks>
public static class PptxRenderingFixtures
{
    /// <summary>The fully qualified embedded resource name of the production package's probe deck.</summary>
    private const string ProbeResourceName = "DemaConsulting.DocDown.PowerPoint.Rendering.Resources.probe.pptx";

    /// <summary>
    ///     Loads the bytes of the embedded two-slide probe presentation.
    /// </summary>
    /// <returns>The bytes of the probe presentation.</returns>
    /// <remarks>The anchor fixture for the single-slide render, determinism, and PNG-validity scenarios.</remarks>
    public static byte[] Probe() => SelfTestProbe.Load(typeof(PowerPointPageRenderingExtractor).Assembly, ProbeResourceName);
}
