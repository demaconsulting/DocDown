namespace DocDown.Core;

/// <summary>
///     The text a backend chose to describe an image, with its origin and how far it may be trusted.
/// </summary>
/// <param name="Text">The chosen, trimmed text. Never null or empty.</param>
/// <param name="Source">The origin the chosen text came from.</param>
/// <param name="Confidence">How confidently the text describes the image, which gates where it may be used.</param>
/// <remarks>
///     The result of <see cref="ImageTextSelector.Select"/>. A backend uses <see cref="Text"/> as a
///     naming hint regardless of confidence, but asserts it as alt text only when
///     <see cref="Confidence"/> is <see cref="ImageTextConfidence.Descriptive"/>; the manifest
///     records <see cref="Text"/> and <see cref="Source"/> for descriptive and contextual tiers so
///     provenance is preserved without inventing a description. Immutable and thread-safe.
/// </remarks>
public sealed record SelectedImageText(string Text, ImageTextSource Source, ImageTextConfidence Confidence);
