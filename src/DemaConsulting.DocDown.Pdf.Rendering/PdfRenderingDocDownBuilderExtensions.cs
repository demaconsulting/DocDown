using DocDown.Core;

namespace DocDown.Pdf.Rendering;

/// <summary>
///     The registration seam that adds optional PDF page rendering to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so
///         a host's dependency graph is exactly what its code says it is. This class is that explicit
///         seam for the rendering package — one call, one visible edge from the host to this
///         assembly and, transitively, to the native rasterization stack it carries. A host that does
///         not call <see cref="AddPdfRendering"/> never loads a native binary.
///     </para>
///     <para>
///         Deliberately free of any PDFtoImage, PDFium, or SkiaSharp type, so a host can reference
///         the registration surface without the native rasterizer's types entering its compilation.
///         All members are static and thread-safe; the builder they mutate is not.
///     </para>
/// </remarks>
public static class PdfRenderingDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the PDF page-rendering extractor with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers a factory rather than an instance so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>: a host that configures a builder but never builds an
    ///     engine pays nothing, and each built engine gets its own extractor instance. Register this
    ///     alongside the managed PDF backend (<c>AddPdf().AddPdfRendering()</c>): the rendering
    ///     backend is chosen only when page rendering is requested, and the managed backend serves
    ///     every other extraction. Returning the builder keeps the call chainable. Side effect:
    ///     mutates <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using System.Threading;
    ///     using DocDown.Core;
    ///     using DocDown.Pdf;
    ///     using DocDown.Pdf.Rendering;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddPdf()          // .pdf  - text, embedded images, metadata
    ///         .AddPdfRendering() // .pdf  - page images (adds native binaries)
    ///         .Build();
    ///
    ///     var result = await engine.ExtractAsync(
    ///         "manuals/sample-manual.pdf",
    ///         "scratch/sample-manual",
    ///         new ExtractionOptions { RenderPages = true },
    ///         CancellationToken.None);
    ///
    ///     Console.WriteLine(result.PagePaths.Count); // rendered page images under pages/
    ///     </code>
    /// </example>
    public static DocDownBuilder AddPdfRendering(this DocDownBuilder builder)
    {
        // Reject a null builder at the point of the call so the error names this extension method
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExtractor(static () => new PdfPageRenderingExtractor());
    }
}
