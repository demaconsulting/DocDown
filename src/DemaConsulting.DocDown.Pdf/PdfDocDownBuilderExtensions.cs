using DocDown.Core;

namespace DocDown.Pdf;

/// <summary>
///     The registration seam that adds PDF extraction to a <see cref="DocDownBuilder"/>.
/// </summary>
/// <remarks>
///     <para>
///         DocDown registers backends explicitly rather than by reflection or assembly scanning, so
///         a host's dependency graph is exactly what its code says it is: nothing appears in an
///         engine because a package happened to be on disk. This class is that explicit seam for the
///         PDF package — one call, one visible edge from the host to this assembly.
///     </para>
///     <para>
///         Deliberately free of any PdfPig type, so a host can reference the registration surface
///         without the PDF parser's types entering its compilation. All members are static and
///         thread-safe; the builder they mutate is not.
///     </para>
/// </remarks>
public static class PdfDocDownBuilderExtensions
{
    /// <summary>
    ///     Registers the PDF extractor with the builder.
    /// </summary>
    /// <param name="builder">The builder to register with. Must not be null.</param>
    /// <returns>The same <paramref name="builder"/>, so registration can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Registers a factory rather than an instance so construction is deferred to
    ///     <see cref="DocDownBuilder.Build"/>: a host that configures a builder but never builds an
    ///     engine pays nothing, and each built engine gets its own extractor instance. Returning the
    ///     builder keeps the call chainable alongside other backends. Side effect: mutates
    ///     <paramref name="builder"/>'s registration list.
    /// </remarks>
    /// <example>
    ///     <code language="csharp">
    ///     using System;
    ///     using DocDown.Core;
    ///     using DocDown.Pdf;
    ///
    ///     var engine = new DocDownBuilder()
    ///         .AddPdf() // .pdf - text, embedded images, metadata
    ///         .Build();
    ///
    ///     Console.WriteLine(engine.Extractors.Count); // 1 — the managed PDF backend
    ///     </code>
    /// </example>
    public static DocDownBuilder AddPdf(this DocDownBuilder builder)
    {
        // Reject a null builder at the point of the call so the error names this extension method
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddExtractor(static () => new PdfDocumentExtractor());
    }
}
