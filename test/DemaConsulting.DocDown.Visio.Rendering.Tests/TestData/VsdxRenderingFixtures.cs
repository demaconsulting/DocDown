using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Visio.Rendering;

namespace DemaConsulting.DocDown.Visio.Rendering.Tests.TestData;

/// <summary>
///     Supplies every Visio drawing the rendering test suite needs.
/// </summary>
/// <remarks>
///     <para>
///         Unlike the PDF rendering suite — which synthesizes every fixture at test time through
///         PdfPig's writer — this suite reads the same real, embedded <c>probe.vsdx</c> the
///         production backend's own self-test reads. CanvasNet.Vsdx renders the full theme and
///         shape geometry a genuine drawing carries, and a drawing a test synthesized with only the
///         handful of parts the Open Packaging reader needs would prove only that the rasterizer
///         agrees with a hand-built document, not that it renders what Visio actually produces. No
///         second binary is committed for this: the bytes are read from the production assembly's
///         own embedded resource through <see cref="SelfTestProbe"/>.
///     </para>
///     <para>
///         The probe carries a single page holding two labeled shapes joined by a glued dynamic
///         connector — simple rectangle-and-line geometry that avoids the spline/bezier/ellipse
///         constructs CanvasNet.Vsdx does not yet rasterize (see
///         docs/design/docdown-visio-rendering.md's Design Constraints). All members are static and
///         pure apart from their allocations, and are safe for concurrent use.
///     </para>
/// </remarks>
public static class VsdxRenderingFixtures
{
    /// <summary>The fully qualified embedded resource name of the production package's probe drawing.</summary>
    private const string ProbeResourceName = "DemaConsulting.DocDown.Visio.Rendering.Resources.probe.vsdx";

    /// <summary>
    ///     Loads the bytes of the embedded single-page probe drawing.
    /// </summary>
    /// <returns>The bytes of the probe drawing.</returns>
    /// <remarks>The anchor fixture for the single-page render, determinism, and PNG-validity scenarios.</remarks>
    public static byte[] Probe() => SelfTestProbe.Load(typeof(VisioPageRenderingExtractor).Assembly, ProbeResourceName);
}
