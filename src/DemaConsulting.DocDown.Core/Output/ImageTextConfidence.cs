namespace DocDown.Core;

/// <summary>
///     How confidently a selected piece of image text actually describes the picture, which governs
///     where the text may honestly be used.
/// </summary>
/// <remarks>
///     <para>
///         The selection policy answers two different questions with one ranking: which candidate is
///         best, and how far that best candidate may be trusted. Confidence captures the second. It
///         is the mechanism that keeps a merely <em>contextual</em> hint (a nearby heading) or a
///         bare <em>fallback</em> (a media file name) from masquerading as an authored description.
///     </para>
///     <para>
///         Consumers gate on this: a filename slug may use any non-empty selection, but markdown
///         alt text is emitted only for a <see cref="Descriptive"/> selection so a heading is never
///         presented as if it described the image. The manifest records the selection and its source
///         for descriptive and contextual tiers alike, so provenance is preserved honestly without
///         inventing a description.
///     </para>
/// </remarks>
public enum ImageTextConfidence
{
    /// <summary>Text authored to describe the picture (description, title, caption, or a meaningful object name).</summary>
    /// <remarks>Safe to assert as the image's alt text because it was written about the image itself.</remarks>
    Descriptive,

    /// <summary>Text that locates the picture in the document but does not describe it (a nearby heading).</summary>
    /// <remarks>Usable as a naming hint and recorded in the manifest with its source, but never asserted as alt text.</remarks>
    Contextual,

    /// <summary>A last-resort identifier with no descriptive value (the media part name).</summary>
    /// <remarks>Keeps naming deterministic; carries no description, so it never becomes alt text or a manifest description.</remarks>
    Fallback
}
