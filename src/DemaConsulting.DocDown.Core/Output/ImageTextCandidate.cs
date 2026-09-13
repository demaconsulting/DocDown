namespace DocDown.Core;

/// <summary>
///     One candidate piece of text describing an image, paired with where it came from.
/// </summary>
/// <param name="Text">
///     The candidate text, or <see langword="null"/> when the document offered nothing for this
///     source. Empty or whitespace-only text is treated the same as <see langword="null"/> by the
///     selector.
/// </param>
/// <param name="Source">The origin of the candidate, which determines its ranking and confidence.</param>
/// <remarks>
///     A backend produces one of these per source it can consult (an author description, a caption,
///     an object name, a heading, the media name) and hands the set to
///     <see cref="ImageTextSelector.Select"/>. Carrying the source alongside the text is what lets
///     the policy rank candidates and lets the manifest record provenance rather than an anonymous
///     string. Immutable and thread-safe.
/// </remarks>
public sealed record ImageTextCandidate(string? Text, ImageTextSource Source);
