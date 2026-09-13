namespace DocDown.Core;

/// <summary>
///     Document-level metadata an extractor reports about the source document.
/// </summary>
/// <param name="Title">The document title, or <see langword="null"/> when unknown or unavailable.</param>
/// <param name="Author">The document author, or <see langword="null"/> when unknown or unavailable.</param>
/// <param name="PageCount">The number of pages, or <see langword="null"/> when not applicable or unknown.</param>
/// <param name="PartCount">
///     The number of logical parts (sheets, slides, sections), or <see langword="null"/> when
///     not applicable or unknown.
/// </param>
/// <remarks>
///     Every member is nullable because different formats expose different metadata and a value
///     that is genuinely unknown must be reported as such rather than guessed. Surfaced in the
///     manifest's <c>document</c> block. Instances are immutable and thread-safe.
/// </remarks>
public sealed record DocumentInfo(string? Title = null, string? Author = null,
    int? PageCount = null, int? PartCount = null);
