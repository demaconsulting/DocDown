namespace DocDown.Core;

/// <summary>
///     A fluent builder that collects extractor registrations and default options and produces a
///     configured <see cref="DocDownEngine"/>.
/// </summary>
/// <remarks>
///     <para>
///         The builder is a mutable configuration stage; the engine it produces is not. Every
///         registration method returns <c>this</c> so a caller can chain configuration, and
///         <see cref="Build"/> takes a snapshot: it copies the registration list and
///         <strong>clones</strong> the configured default options, so mutating the builder after
///         <see cref="Build"/> — adding more extractors or reconfiguring defaults — cannot leak into an
///         already-built engine.
///     </para>
///     <para>
///         Extractors may be registered either as ready instances or as factories; the factory form
///         defers instance creation to <see cref="Build"/>, where <see cref="ExtractorRegistry"/>
///         materializes each factory exactly once and rejects duplicate identifiers. Duplicate
///         identifiers therefore surface as an <see cref="ArgumentException"/> at build time, because
///         an identifier is both the caller-override key and the manifest key and must be unique.
///     </para>
///     <para>
///         A builder instance is not thread-safe and is intended to be configured from a single
///         thread; the engine it builds is safe for concurrent use.
///     </para>
/// </remarks>
/// <example>
///     The minimum viable extraction: register the one backend the application needs, build the
///     engine, extract, and branch on the outcome.
///     <code language="csharp">
///     using System;
///     using System.Threading;
///     using DocDown.Core;
///     using DocDown.Word;
///
///     // Register only the backends this application needs; nothing else is discovered.
///     var engine = new DocDownBuilder()
///         .AddWord() // .docx - text, tables, images; no page images
///         .Build();
///
///     var result = await engine.ExtractAsync(
///         documentPath: "contracts/sample-agreement.docx",
///         scratchFolder: "scratch/sample-agreement",
///         options: null,
///         cancellationToken: CancellationToken.None);
///
///     if (result.Outcome == ExtractionOutcome.Unreadable)
///     {
///         // The document or the scratch folder could not be read; the explanation says why.
///         Console.Error.WriteLine(result.Failure?.Explanation);
///     }
///     else
///     {
///         // Produced: the invariant output layout was written under the scratch folder.
///         Console.WriteLine(result.SummaryPath);   // absolute path to summary.txt
///         Console.WriteLine(result.ManifestPath);  // absolute path to manifest.json
///
///         // Notes can appear on a produced extraction; each is a short factual message.
///         foreach (var note in result.Notes)
///         {
///             Console.WriteLine($"Note: {note.Message}");
///         }
///     }
///     </code>
///     When a host handles several formats, register the full menu and delete the lines it does not
///     need — each line states what it buys:
///     <code language="csharp">
///     using System;
///     using DocDown.Core;
///     using DocDown.Excel;
///     using DocDown.Pdf;
///     using DocDown.Pdf.Rendering;
///     using DocDown.PowerPoint;
///     using DocDown.Visio;
///     using DocDown.Word;
///
///     var engine = new DocDownBuilder()
///         .AddPdf()          // .pdf  - text, embedded images, metadata
///         .AddPdfRendering() // .pdf  - page images (adds native binaries)
///         .AddWord()         // .docx - text, tables, images; no page images
///         .AddExcel()        // .xlsx - cells, formulas, charts; workbooks are never rendered
///         .AddPowerPoint()   // .pptx - slide text and notes; slide images need PowerPoint
///         .AddVisio()        // .vsdx, .vsdm - shape text and connections; page images need Visio
///         .Build();
///
///     Console.WriteLine(engine.Extractors.Count); // 8 — AddPowerPoint and AddVisio register two each
///     </code>
///     Register only the formats you need — each call is one visible edge to one package. Slide and
///     page images are produced by driving Microsoft Office over COM, so they are Windows-only and
///     require that application to be installed. Without it the managed backend still extracts the
///     text, and <c>summary.txt</c> records that pages were not rendered.
/// </example>
public sealed class DocDownBuilder
{
    /// <summary>
    ///     Creates an empty builder with no registered extractors and default extraction options.
    /// </summary>
    /// <remarks>
    ///     This is the normal entry point to the library: construct a builder, call the
    ///     <c>Add*</c> extension method of each backend package you need (<c>AddPdf</c>,
    ///     <c>AddWord</c>, and so on), optionally call <see cref="ConfigureDefaults"/>, then call
    ///     <see cref="Build"/> to obtain a <see cref="DocDownEngine"/>. A builder with no
    ///     registrations still builds, but the resulting engine can extract nothing.
    /// </remarks>
    public DocDownBuilder()
    {
    }

    /// <summary>The registered extractor factories in registration order.</summary>
    /// <remarks>Instances are wrapped as factories so both registration forms share one materialization path.</remarks>
    private readonly List<Func<IDocumentExtractor>> _factories = [];

    /// <summary>The mutable default options configured through <see cref="ConfigureDefaults"/>.</summary>
    /// <remarks>Cloned at <see cref="Build"/> time so later reconfiguration cannot affect a built engine.</remarks>
    private readonly ExtractionOptions _defaults = new();

    /// <summary>
    ///     Registers an already-constructed extractor instance.
    /// </summary>
    /// <param name="extractor">The extractor to register. Must not be null.</param>
    /// <returns>This builder, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extractor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Wraps the instance in a factory so instance and factory registrations flow through the same
    ///     materialization and duplicate-detection path in <see cref="ExtractorRegistry"/>. Registration
    ///     order is preserved for reporting and self-test aggregation.
    /// </remarks>
    public DocDownBuilder AddExtractor(IDocumentExtractor extractor)
    {
        // Reject a null extractor at the point of registration so the error names the offending call
        ArgumentNullException.ThrowIfNull(extractor);
        _factories.Add(() => extractor);
        return this;
    }

    /// <summary>
    ///     Registers an extractor factory whose instance is created when the engine is built.
    /// </summary>
    /// <param name="factory">The factory that produces the extractor. Must not be null.</param>
    /// <returns>This builder, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Deferring construction to <see cref="Build"/> lets a backend delay any expensive setup until
    ///     an engine is actually assembled; the factory is invoked exactly once during
    ///     <see cref="Build"/>. Registration order is preserved.
    /// </remarks>
    public DocDownBuilder AddExtractor(Func<IDocumentExtractor> factory)
    {
        // The factory itself must be non-null; the instance it returns is validated at build time
        ArgumentNullException.ThrowIfNull(factory);
        _factories.Add(factory);
        return this;
    }

    /// <summary>
    ///     Applies a configuration action to the engine's default extraction options.
    /// </summary>
    /// <param name="configure">The action that mutates the default options. Must not be null.</param>
    /// <returns>This builder, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Mutates the builder's own defaults instance; the accumulated configuration is cloned into
    ///     the engine at <see cref="Build"/> time. Calling this more than once composes the changes in
    ///     call order.
    /// </remarks>
    public DocDownBuilder ConfigureDefaults(Action<ExtractionOptions> configure)
    {
        // A null configuration action has nothing to apply and is a caller error
        ArgumentNullException.ThrowIfNull(configure);
        configure(_defaults);
        return this;
    }

    /// <summary>
    ///     Builds a configured <see cref="DocDownEngine"/> from the current registrations and defaults.
    /// </summary>
    /// <returns>A new engine bound to an immutable snapshot of the registrations and a private copy of the defaults.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when two registered extractors share an identifier, when a factory returns
    ///     <see langword="null"/>, or when an extractor has a null or empty identifier.
    /// </exception>
    /// <remarks>
    ///     Snapshots the registrations into a fresh list and materializes them through
    ///     <see cref="ExtractorRegistry"/> — where duplicate identifiers are caught — then clones the
    ///     configured defaults so later builder mutation cannot alter the engine. The builder remains
    ///     usable after <see cref="Build"/> and can produce further, independent engines.
    /// </remarks>
    public DocDownEngine Build()
    {
        // Copy the registration list so later AddExtractor calls cannot mutate the built engine's registry
        var registry = new ExtractorRegistry(_factories.ToList());

        // Clone the defaults so later ConfigureDefaults calls cannot leak into the built engine
        return new DocDownEngine(registry, _defaults.Clone());
    }
}
