using DocDown.Core;
using DocDown.PowerPoint.Com;
using DocDown.PowerPoint.OpenXml;

namespace DocDown.PowerPoint;

/// <summary>
///     The registration seam that adds PowerPoint extraction to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so a
///         host's dependency graph is exactly what its code says it is. This class is that explicit
///         seam for the PowerPoint package: <see cref="AddPowerPoint"/> registers both the managed
///         Open XML backend — the guaranteed content path that always extracts slide text, titles,
///         speaker notes, and slide order — and the COM automation backend that additionally renders
///         each slide when Microsoft PowerPoint is available.
///     </para>
///     <para>
///         Both backends carry no native asset: the managed backend is pure Open XML, and the COM
///         backend reaches PowerPoint through late-bound IDispatch and self-disables where PowerPoint
///         is absent. One call therefore registers the complete PowerPoint capability with no
///         platform-specific dependency. All members are static and thread-safe; the builder they
///         mutate is not.
///     </para>
/// </remarks>
public static class PowerPointDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the managed Open XML and COM automation PowerPoint backends with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers factories rather than instances so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>. The managed backend (priority 10) serves every
    ///     extraction that does not request rendering; the COM backend (priority 0) is chosen only
    ///     when page rendering is requested and Microsoft PowerPoint is available, and otherwise
    ///     records a plain-language note when a requested slide render cannot be completed. Side
    ///     effect: mutates <paramref name="builder"/>'s registration list.
    /// </remarks>
    public static DocDownBuilder AddPowerPoint(this DocDownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddExtractor(static () => new PowerPointOpenXmlExtractor())
            .AddExtractor(static () => new PowerPointComExtractor());
    }
}
