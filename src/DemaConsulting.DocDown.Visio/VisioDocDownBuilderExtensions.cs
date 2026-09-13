using DocDown.Core;
using DocDown.Visio.Com;
using DocDown.Visio.OpenXml;

namespace DocDown.Visio;

/// <summary>
///     The registration seam that adds Visio extraction to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so a
///         host's dependency graph is exactly what its code says it is. This class is that explicit
///         seam for the Visio package: <see cref="AddVisio"/> registers both the managed Open
///         Packaging backend — the guaranteed content path that always recovers page names, shape
///         text, and the directed connector topology with no Visio installation present — and the COM
///         automation backend that additionally renders each page when Microsoft Visio is available.
///     </para>
///     <para>
///         Both backends carry no native asset: the managed backend reads the package with
///         <see cref="System.IO.Packaging"/>, and the COM backend reaches Visio through late-bound
///         IDispatch and self-disables where Visio is absent. One call therefore registers the
///         complete Visio extraction stack with no platform-specific dependency. All members are static and
///         thread-safe; the builder they mutate is not.
///     </para>
/// </remarks>
public static class VisioDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the managed Open Packaging and COM automation Visio backends with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers factories rather than instances so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>. The managed backend (priority 10) serves every
    ///     extraction that does not request rendering; the COM backend (priority 0) is chosen only
    ///     when page rendering is requested and Microsoft Visio is available, while the managed backend
    ///     still supplies the page names, shape text, connector topology, and embedded images. Side
    ///     effect: mutates <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using DocDown.Core;
    ///     using DocDown.Visio;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddVisio() // .vsdx, .vsdm - shape text and connections; page images need Visio
    ///         .Build();
    ///
    ///     Console.WriteLine(engine.Extractors.Count); // 2 — the Open Packaging and COM backends
    ///     </code>
    /// </example>
    public static DocDownBuilder AddVisio(this DocDownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddExtractor(static () => new VisioOpenXmlExtractor())
            .AddExtractor(static () => new VisioComExtractor());
    }
}
