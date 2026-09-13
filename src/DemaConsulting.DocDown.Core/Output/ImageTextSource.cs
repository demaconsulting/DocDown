namespace DocDown.Core;

/// <summary>
///     Where a piece of candidate text describing an image came from, in decreasing order of how
///     directly it describes the picture.
/// </summary>
/// <remarks>
///     <para>
///         A backend gathers whatever text a document offers about an image — an author's alt-text
///         description, a caption, an embedded object name, a nearby heading — and the shared policy
///         (<see cref="ImageTextSelector"/>) ranks the candidates by this source so the most
///         directly descriptive one wins. The enumeration order <em>is</em> the preference order, so
///         a lower member never loses to a higher one when both are present.
///     </para>
///     <para>
///         Modeling the origin as a closed vocabulary keeps the choice auditable: the manifest can
///         record not just the chosen text but exactly where it came from, so a reader can tell an
///         authored description apart from a filename or a heading rather than trusting an opaque
///         string. Each format backend maps its own structures onto these members; the ranking and
///         the honesty rules live in one place regardless of format.
///     </para>
/// </remarks>
public enum ImageTextSource
{
    /// <summary>The author's alt-text description (for example <c>wp:docPr/@descr</c> in Word).</summary>
    /// <remarks>The most direct description a document can carry: text written specifically to describe the picture.</remarks>
    Description,

    /// <summary>The author's title for the drawing (for example <c>wp:docPr/@title</c> in Word).</summary>
    /// <remarks>Authored specifically for the picture, though a title is typically terser than a description.</remarks>
    Title,

    /// <summary>The text of an adjacent caption (for example a <c>Caption</c>-styled paragraph in Word).</summary>
    /// <remarks>Written to describe the figure, including any sequence numbering read structurally from the caption's fields.</remarks>
    Caption,

    /// <summary>An embedded object name (for example <c>pic:cNvPr/@name</c> in Word), often the original file name.</summary>
    /// <remarks>Frequently a meaningful file name, but discarded when it is an auto-generated placeholder such as <c>Picture 1</c>.</remarks>
    PictureName,

    /// <summary>The nearest preceding heading in the document body.</summary>
    /// <remarks>
    ///     Context, not a description: a heading names the section the picture sits under, which is a
    ///     useful naming hint but must never be asserted as the picture's alt text — a heading such
    ///     as <c>References</c> would imply a description of the image that does not exist.
    /// </remarks>
    Heading,

    /// <summary>The media part's own name within the package (the last resort).</summary>
    /// <remarks>A stable fallback that carries no description; used only to keep file naming deterministic when nothing better exists.</remarks>
    Uri
}
