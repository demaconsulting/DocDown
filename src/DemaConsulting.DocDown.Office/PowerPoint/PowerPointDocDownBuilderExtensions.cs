using DocDown.Core;
using DocDown.PowerPoint.OpenXml;

namespace DocDown.PowerPoint;

/// <summary>
///     The registration seam that adds PowerPoint extraction to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so a
///         host's dependency graph is exactly what its code says it is. This class is that explicit
///         seam for the PowerPoint package: <see cref="AddPowerPoint"/> registers the managed Open
///         XML backend — the guaranteed content path that always extracts slide text, titles,
///         speaker notes, and slide order.
///     </para>
///     <para>
///         The managed backend carries no native asset: it is pure Open XML, so one call registers
///         the complete PowerPoint capability with no platform-specific dependency. Rendered slide
///         images are a separate, opt-in concern provided by the
///         <c>DocDown.PowerPoint.Rendering</c> package. All members are static and thread-safe; the
///         builder they mutate is not.
///     </para>
/// </remarks>
public static class PowerPointDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the managed Open XML PowerPoint backend with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers a factory rather than an instance so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>. The managed backend (priority 10) serves every
    ///     extraction and never renders slides itself; when page rendering is requested and no
    ///     rendering-capable backend is registered alongside it, Core itself records a
    ///     plain-language note explaining that pages were not rendered, since this backend neither
    ///     claims to provide rendered pages nor reports one. Side effect: mutates
    ///     <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using DocDown.Core;
    ///     using DocDown.PowerPoint;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddPowerPoint() // .pptx - slide text and notes; slide images need DocDown.PowerPoint.Rendering
    ///         .Build();
    ///
    ///     Console.WriteLine(engine.Extractors.Count); // 1 — the Open XML backend
    ///     </code>
    /// </example>
    public static DocDownBuilder AddPowerPoint(this DocDownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddExtractor(static () => new PowerPointOpenXmlExtractor());
    }
}
