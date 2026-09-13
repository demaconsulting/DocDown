namespace DocDown.Core;

/// <summary>
///     An immutable description of a registered extractor's identity captured once at registration
///     time.
/// </summary>
/// <param name="Id">The extractor's stable, unique identifier used for lookup.</param>
/// <param name="DisplayName">A human-readable name for the extractor shown in summaries.</param>
/// <param name="SupportedFormats">The document formats the extractor can process.</param>
/// <param name="Priority">
///     The extractor's ranking priority; higher values are preferred when the format and
///     page-rendering preference leave more than one candidate.
/// </param>
/// <param name="PageRenderingApplicable">
///     Whether rendering document pages to images is a meaningful request for the extractor's
///     formats. <see langword="true"/> for a paginated format (the default), <see langword="false"/>
///     for a non-paginated one such as a spreadsheet, so the engine can honor a page request against
///     a non-paginated format with silence rather than a note about an absent renderer.
/// </param>
/// <remarks>
///     A descriptor is a snapshot detached from the live <see cref="IDocumentExtractor"/>
///     instance so selection and reporting can reason about an extractor without invoking it or
///     depending on its lifetime. Instances are immutable and thread-safe; the
///     <see cref="SupportedFormats"/> list is captured at construction and not copied defensively,
///     so callers must pass a list they will not mutate.
/// </remarks>
public sealed record ExtractorDescriptor(
    string Id, string DisplayName, IReadOnlyList<DocumentFormat> SupportedFormats,
    int Priority, bool PageRenderingApplicable = true);
