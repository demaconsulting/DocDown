using System.Text;
using DocDown.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using O21 = DocumentFormat.OpenXml.Office2021.PowerPoint.Comment;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocDown.PowerPoint.OpenXml;

/// <summary>
///     Reads a presentation package into the backend-neutral <see cref="PowerPointDeckModel"/>,
///     preserving slide order and extracting each slide's title, body text, and — crucially — its
///     speaker notes, which never appear in any render.
/// </summary>
/// <remarks>
///     The reader is the one place that touches the Open XML object model. It walks slides in
///     presentation order (never document-part order), separates the title placeholder from the body
///     shapes, and reads the notes slide's body placeholder so the narration a slide only gestures at
///     is recovered from the file itself. It performs no filesystem I/O; the caller hands it a
///     seekable stream. Stateless and safe to reuse.
/// </remarks>
internal static class PowerPointOpenXmlReader
{
    /// <summary>
    ///     Reads a deck from a seekable stream into the model.
    /// </summary>
    /// <param name="stream">A readable, seekable stream over the <c>.pptx</c> bytes.</param>
    /// <returns>The deck model with its slides, titles, text, and notes in presentation order.</returns>
    /// <exception cref="PowerPointExtractionException">Thrown when the package cannot be opened or carries no presentation part.</exception>
    /// <remarks>Read-only over the stream. Any Open XML fault is wrapped so Core sees a structured failure.</remarks>
    public static PowerPointDeckModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        PresentationDocument document;
        try
        {
            document = PresentationDocument.Open(stream, isEditable: false);
        }
        catch (OpenXmlPackageException exception)
        {
            throw new PowerPointExtractionException(
                "The presentation could not be opened; it may be encrypted, malformed, or not a valid Open XML presentation.",
                exception);
        }
        catch (FileFormatException exception)
        {
            throw new PowerPointExtractionException(
                "The presentation could not be opened; it may be encrypted, malformed, or not a valid Open XML presentation.",
                exception);
        }

        using (document)
        {
            var presentationPart = document.PresentationPart
                ?? throw new PowerPointExtractionException("The presentation contains no presentation part and cannot be read.");

            var slides = new List<PowerPointSlideModel>();
            var slideOrdinals = new Dictionary<SlidePart, int>();
            var slideParts = new List<(SlidePart Part, int Ordinal)>();
            var slideIds = presentationPart.Presentation?.SlideIdList?.Elements<P.SlideId>() ?? [];
            var ordinal = 1;
            foreach (var slideId in slideIds)
            {
                if (slideId.RelationshipId?.Value is { } relationshipId
                    && presentationPart.GetPartById(relationshipId) is SlidePart slidePart)
                {
                    slideOrdinals[slidePart] = ordinal;
                    slideParts.Add((slidePart, ordinal));
                    ordinal++;
                }
            }

            var imageCollection = PowerPointOpenXmlImageReader.Collect(presentationPart, slideOrdinals);
            var authorNames = LoadCommentAuthors(presentationPart);

            var modernCommentCount = 0;
            var modernCommentSlideCount = 0;
            foreach (var (slidePart, slideOrdinal) in slideParts)
            {
                var imageRefs = imageCollection.SlideImageRefs.TryGetValue(slideOrdinal, out var refs)
                    ? refs
                    : [];
                slides.Add(ReadSlide(slidePart, slideOrdinal, imageRefs, authorNames));

                var modernOnThisSlide = CountModernComments(slidePart);
                if (modernOnThisSlide > 0)
                {
                    modernCommentCount += modernOnThisSlide;
                    modernCommentSlideCount++;
                }
            }

            var images = imageCollection.Images;

            return new PowerPointDeckModel(slides, images, OpcMetadataMapper.From(new OpcCoreProperties(
                document.PackageProperties.Creator,
                document.PackageProperties.LastModifiedBy,
                document.PackageProperties.Created,
                document.PackageProperties.Modified,
                document.PackageProperties.Revision,
                document.PackageProperties.Title,
                document.PackageProperties.Subject,
                document.PackageProperties.Keywords,
                document.PackageProperties.Category,
                document.PackageProperties.ContentStatus,
                document.PackageProperties.Description,
                document.PackageProperties.LastPrinted,
                document.PackageProperties.Version,
                document.PackageProperties.Language,
                document.PackageProperties.Identifier)),
                modernCommentCount,
                modernCommentSlideCount);
        }
    }

    /// <summary>
    ///     Counts the modern persona-based comments a slide carries, without reading any of them.
    /// </summary>
    /// <param name="slidePart">The slide part to inspect.</param>
    /// <returns>The number of modern comments attached to the slide; zero when it carries none.</returns>
    /// <remarks>
    ///     <para>
    ///         Counting is deliberately all this does. A deck whose comments are all modern would
    ///         otherwise be indistinguishable in the output from a deck nobody ever commented on,
    ///         which is the one thing this repository will not do: an absence caused by a boundary of
    ///         the extractor must be reported, not silently rendered as an absence in the document.
    ///         The emitter turns a non-zero count into a plain note.
    ///     </para>
    ///     <para>
    ///         The count comes from the SDK's own typed accessors — <see cref="SlidePart.commentParts"/>
    ///         and <see cref="PowerPointCommentPart.CommentList"/> over
    ///         <c>DocumentFormat.OpenXml.Office2021.PowerPoint.Comment</c> — so nothing here reaches
    ///         into the package by raw relationship or guesses at a grammar. Read-only over the part.
    ///     </para>
    /// </remarks>
    private static int CountModernComments(SlidePart slidePart) =>
        slidePart.commentParts.Sum(part => part.CommentList?.Elements<O21.Comment>().Count() ?? 0);

    /// <summary>
    ///     Reads one slide into the model: its title, body text lines, speaker notes, and the
    ///     reviewer comments attached to it.
    /// </summary>
    /// <param name="slidePart">The slide part.</param>
    /// <param name="ordinal">The slide's 1-based ordinal.</param>
    /// <param name="imageRefs">The images this slide references, in reading order, for inline linking.</param>
    /// <param name="authorNames">The presentation's comment-author names, keyed by author identifier.</param>
    /// <returns>The slide model.</returns>
    /// <remarks>
    ///     The title comes from the title placeholder; every other shape's paragraphs become body
    ///     lines. The notes come from the notes slide's body placeholder. Read-only over the part.
    /// </remarks>
    private static PowerPointSlideModel ReadSlide(
        SlidePart slidePart, int ordinal, IReadOnlyList<PowerPointSlideImageRef> imageRefs,
        IReadOnlyDictionary<uint, string> authorNames)
    {
        string? title = null;
        var lines = new List<string>();

        var shapeTree = slidePart.Slide?.CommonSlideData?.ShapeTree;
        if (shapeTree is not null)
        {
            foreach (var shape in shapeTree.Elements<P.Shape>())
            {
                var paragraphs = ReadShapeParagraphs(shape);
                if (paragraphs.Count == 0)
                {
                    continue;
                }

                if (title is null && IsTitle(shape))
                {
                    title = string.Join(' ', paragraphs);
                }
                else
                {
                    lines.AddRange(paragraphs);
                }
            }
        }

        var notes = ReadNotes(slidePart);
        var comments = ReadComments(slidePart, authorNames);
        return new PowerPointSlideModel(ordinal, title, lines, notes, imageRefs, comments);
    }

    /// <summary>
    ///     Loads the presentation's comment-author list, which a slide comment names only by index.
    /// </summary>
    /// <param name="presentationPart">The presentation part.</param>
    /// <returns>The author names, keyed by the identifier a comment cites, or an empty map when the deck declares none.</returns>
    /// <remarks>
    ///     A slide comment carries only an <c>authorId</c>; the names live once in the presentation's
    ///     comment-authors part, so reading them up front lets every comment resolve its author
    ///     without re-walking the package. An identifier the list does not name yields no attribution
    ///     rather than a guess. Read-only over the part.
    /// </remarks>
    private static IReadOnlyDictionary<uint, string> LoadCommentAuthors(PresentationPart presentationPart)
    {
        var names = new Dictionary<uint, string>();
        var list = presentationPart.CommentAuthorsPart?.CommentAuthorList;
        if (list is null)
        {
            return names;
        }

        foreach (var author in list.Elements<P.CommentAuthor>())
        {
            if (author.Id?.Value is { } id && author.Name?.Value is { Length: > 0 } name)
            {
                names[id] = name;
            }
        }

        return names;
    }

    /// <summary>
    ///     Reads the reviewer comments attached to a slide, from its legacy slide-comments part.
    /// </summary>
    /// <param name="slidePart">The slide part.</param>
    /// <param name="authorNames">The presentation's comment-author names, keyed by author identifier.</param>
    /// <returns>The slide's comments in the order the part declares them, or an empty list when it has none.</returns>
    /// <remarks>
    ///     <para>
    ///         Only the legacy comments part (<c>ppt/comments/comment<em>n</em>.xml</c>, exposed as
    ///         <see cref="SlidePart.SlideCommentsPart"/>) is read, and its author identifiers are
    ///         resolved through the presentation's comment-authors part.
    ///     </para>
    ///     <para>
    ///         <strong>Stated scope limitation.</strong> The modern persona-based, cloud-synced
    ///         comments that current PowerPoint writes are deliberately <em>not</em> read. This is a
    ///         scope choice, not a tooling limit: <c>DocumentFormat.OpenXml 3.5.1</c> does expose the
    ///         part and its grammar through <see cref="SlidePart.commentParts"/>,
    ///         <see cref="PowerPointCommentPart.CommentList"/>, and the
    ///         <c>DocumentFormat.OpenXml.Office2021.PowerPoint.Comment</c> namespace. What is not
    ///         read is the modern author model: a modern comment names its author through a separate
    ///         persona and author list rather than through the presentation's comment-authors part,
    ///         and that resolution could not be validated here against a genuine
    ///         PowerPoint-authored deck, so reading the comments would mean attributing them on
    ///         unverified reasoning.
    ///     </para>
    ///     <para>
    ///         So that the boundary is visible to whoever holds the output rather than only in this
    ///         repository's documents, <see cref="CountModernComments"/> counts them and the emitter
    ///         states their presence as an extraction note: a deck whose comments are all modern
    ///         reports none here, but says so plainly instead of reading as a deck nobody commented on.
    ///     </para>
    ///     <para>
    ///         A comment with no text is dropped, because it says nothing. Read-only over the part.
    ///     </para>
    /// </remarks>
    private static IReadOnlyList<PowerPointCommentModel> ReadComments(
        SlidePart slidePart, IReadOnlyDictionary<uint, string> authorNames)
    {
        var comments = new List<PowerPointCommentModel>();
        var list = slidePart.SlideCommentsPart?.CommentList;
        if (list is null)
        {
            return comments;
        }

        foreach (var comment in list.Elements<P.Comment>())
        {
            var text = comment.Text?.InnerText;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            string? author = null;
            if (comment.AuthorId?.Value is { } authorId && authorNames.TryGetValue(authorId, out var name))
            {
                author = name;
            }

            comments.Add(new PowerPointCommentModel(author, text));
        }

        return comments;
    }

    /// <summary>
    ///     Reads the speaker notes attached to a slide, from the notes slide's body placeholder.
    /// </summary>
    /// <param name="slidePart">The slide part.</param>
    /// <returns>The notes text, or <see langword="null"/> when the slide has no notes.</returns>
    /// <remarks>
    ///     Only the body placeholder is read, so the slide-number and date furniture on the notes
    ///     slide do not contaminate the narration. Read-only over the notes part.
    /// </remarks>
    private static string? ReadNotes(SlidePart slidePart)
    {
        var notesSlide = slidePart.NotesSlidePart?.NotesSlide;
        var shapeTree = notesSlide?.CommonSlideData?.ShapeTree;
        if (shapeTree is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var shape in shapeTree.Elements<P.Shape>())
        {
            if (!IsBody(shape))
            {
                continue;
            }

            foreach (var paragraph in ReadShapeParagraphs(shape))
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(paragraph);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    ///     Reads a shape's paragraphs, one string per paragraph, dropping blank paragraphs.
    /// </summary>
    /// <param name="shape">The shape to read.</param>
    /// <returns>The shape's non-blank paragraph texts in reading order.</returns>
    /// <remarks>Concatenates the text runs of each paragraph so a run-split line reads as one line. Pure.</remarks>
    private static List<string> ReadShapeParagraphs(P.Shape shape)
    {
        var paragraphs = new List<string>();
        var textBody = shape.TextBody;
        if (textBody is null)
        {
            return paragraphs;
        }

        foreach (var paragraph in textBody.Elements<D.Paragraph>())
        {
            var builder = new StringBuilder();
            foreach (var text in paragraph.Descendants<D.Text>())
            {
                builder.Append(text.Text);
            }

            var line = builder.ToString().Trim();
            if (line.Length > 0)
            {
                paragraphs.Add(line);
            }
        }

        return paragraphs;
    }

    /// <summary>
    ///     Determines whether a shape is the slide's title placeholder.
    /// </summary>
    /// <param name="shape">The shape to test.</param>
    /// <returns><see langword="true"/> when the shape is a title or centered-title placeholder.</returns>
    /// <remarks>Pure.</remarks>
    private static bool IsTitle(P.Shape shape)
    {
        var type = PlaceholderType(shape);
        return type is not null
            && (type == P.PlaceholderValues.Title || type == P.PlaceholderValues.CenteredTitle);
    }

    /// <summary>
    ///     Determines whether a shape is a body placeholder (used for notes narration).
    /// </summary>
    /// <param name="shape">The shape to test.</param>
    /// <returns><see langword="true"/> when the shape is a body placeholder.</returns>
    /// <remarks>Pure.</remarks>
    private static bool IsBody(P.Shape shape) => PlaceholderType(shape) == P.PlaceholderValues.Body;

    /// <summary>
    ///     Reads a shape's placeholder type, or <see langword="null"/> when it is not a placeholder.
    /// </summary>
    /// <param name="shape">The shape to inspect.</param>
    /// <returns>The placeholder type value, or <see langword="null"/>.</returns>
    /// <remarks>Pure.</remarks>
    private static P.PlaceholderValues? PlaceholderType(P.Shape shape)
    {
        var placeholder = shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .GetFirstChild<P.PlaceholderShape>();
        return placeholder?.Type?.Value;
    }
}

/// <summary>
///     The exception the PowerPoint reader raises for a deck it cannot open or interpret, so Core
///     can convert it into a structured failure rather than letting a raw fault reach the caller.
/// </summary>
/// <remarks>
///     Raised for a missing presentation part, or a package the Open XML SDK cannot open (an
///     encrypted or malformed <c>.pptx</c>). Core catches it and writes a structured failure with
///     the full output layout still present.
/// </remarks>
public sealed class PowerPointExtractionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointExtractionException"/> class.
    /// </summary>
    public PowerPointExtractionException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointExtractionException"/> class with a message.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    public PowerPointExtractionException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PowerPointExtractionException"/> class with a
    ///     message and an inner exception.
    /// </summary>
    /// <param name="message">The message describing what could not be read.</param>
    /// <param name="innerException">The underlying fault.</param>
    public PowerPointExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
///     The backend-neutral model of a whole deck: its slides in presentation order, each with its
///     title, body text, and speaker notes.
/// </summary>
/// <param name="Slides">The slides, in the order the presentation declares them.</param>
/// <param name="Images">
///     The embedded images resolved from across the deck — slides, notes, layouts, and masters —
///     deduplicated by package part, in slide-then-template order. Empty when the deck embeds no
///     images. Written through the sink, which deduplicates again by content.
/// </param>
/// <param name="Metadata">
///     What the deck asserts about itself, mapped from the OPC core properties, or
///     <see langword="null"/> when not captured (for example a hand-built test model). The
///     first-slide title used as the document title is a heuristic and is deliberately not part of
///     this authored metadata.
/// </param>
/// <param name="ModernCommentCount">
///     The number of modern persona-based, cloud-synced comments the deck carries that this reader
///     counts but does not read. Zero for a deck that carries none. The emitter turns a non-zero
///     count into a note, so a deck whose comments are all modern is distinguishable from a deck
///     nobody commented on.
/// </param>
/// <param name="ModernCommentSlideCount">
///     The number of slides carrying at least one such comment, so the note can say where they are
///     without naming every slide.
/// </param>
/// <remarks>
///     The reader populates this model from the Open XML package and hands it to the emitter, so
///     every decision about what reaches the output is made once against a model that can be built
///     by hand with no deck behind it. Immutable and thread-safe.
/// </remarks>
internal sealed record PowerPointDeckModel(
    IReadOnlyList<PowerPointSlideModel> Slides,
    IReadOnlyList<EmbeddedImage> Images,
    DocumentMetadata? Metadata = null,
    int ModernCommentCount = 0,
    int ModernCommentSlideCount = 0)
{
    /// <summary>
    ///     Initializes a deck model that embeds no images, for a hand-built model with no deck behind it.
    /// </summary>
    /// <param name="slides">The slides, in presentation order.</param>
    /// <param name="metadata">The self-reported metadata, or <see langword="null"/>.</param>
    /// <remarks>A convenience for tests and callers that do not exercise embedded images; images default to empty.</remarks>
    public PowerPointDeckModel(IReadOnlyList<PowerPointSlideModel> slides, DocumentMetadata? metadata = null)
        : this(slides, [], metadata)
    {
    }
}

/// <summary>
///     One slide: its 1-based ordinal, its title where one exists, its body text lines, its
///     speaker notes, and the images it references in reading order.
/// </summary>
/// <param name="Ordinal">The slide's 1-based position, because a deck's argument is sequential.</param>
/// <param name="Title">The slide title from the title placeholder, or <see langword="null"/> when none.</param>
/// <param name="TextLines">
///     The slide's body text as a sequence of lines (one per paragraph across the non-title shapes),
///     in reading order. Empty when the slide carries no body text.
/// </param>
/// <param name="Notes">
///     The speaker notes attached to the slide, or <see langword="null"/> when the slide has none.
///     Notes never appear in any render, so they are extracted from the file itself.
/// </param>
/// <param name="Images">
///     The images this slide references, in reading order and distinct by package part, so the
///     emitter can link each one inline at the point of occurrence — following Word's convention. A
///     part shown on several slides appears in each slide's list, recording every reference. Empty
///     when the slide shows no picture.
/// </param>
/// <param name="Comments">
///     The reviewer comments attached to the slide, in the order the deck declares them. Empty when
///     the slide carries none, and empty for a deck whose comments are all modern persona comments,
///     which this reader counts and reports as present-but-unread rather than reading. A comment is
///     commentary <em>about</em> the deck rather than part of it, so it travels to Core as a review
///     comment and never into <c>content.md</c>.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointSlideModel(
    int Ordinal, string? Title, IReadOnlyList<string> TextLines, string? Notes,
    IReadOnlyList<PowerPointSlideImageRef> Images, IReadOnlyList<PowerPointCommentModel> Comments)
{
    /// <summary>
    ///     Initializes a slide model that carries no reviewer comments, for a hand-built model.
    /// </summary>
    /// <param name="ordinal">The slide's 1-based position.</param>
    /// <param name="title">The slide title, or <see langword="null"/>.</param>
    /// <param name="textLines">The slide's body text lines.</param>
    /// <param name="notes">The speaker notes, or <see langword="null"/>.</param>
    /// <param name="images">The images this slide references.</param>
    /// <remarks>A convenience for tests that do not exercise reviewer comments; comments default to empty.</remarks>
    public PowerPointSlideModel(
        int ordinal, string? title, IReadOnlyList<string> textLines, string? notes,
        IReadOnlyList<PowerPointSlideImageRef> images)
        : this(ordinal, title, textLines, notes, images, [])
    {
    }

    /// <summary>
    ///     Initializes a slide model that references no images inline, for a hand-built model.
    /// </summary>
    /// <param name="ordinal">The slide's 1-based position.</param>
    /// <param name="title">The slide title, or <see langword="null"/>.</param>
    /// <param name="textLines">The slide's body text lines.</param>
    /// <param name="notes">The speaker notes, or <see langword="null"/>.</param>
    /// <remarks>A convenience for tests that do not exercise inline image links; images default to empty.</remarks>
    public PowerPointSlideModel(int ordinal, string? title, IReadOnlyList<string> textLines, string? notes)
        : this(ordinal, title, textLines, notes, [], [])
    {
    }
}

/// <summary>
///     One reviewer comment attached to a slide: who wrote it and what it says.
/// </summary>
/// <param name="Author">
///     The comment's author as the deck's comment-author list names them, or
///     <see langword="null"/> when the deck names nobody for it, so no attribution is invented.
/// </param>
/// <param name="Text">The comment's text, taken whole so a long remark is never clipped.</param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointCommentModel(string? Author, string Text);

/// <summary>
///     One image occurrence on a slide: the package-part reference that keys the written-path map,
///     and the alt text to show, if any.
/// </summary>
/// <param name="SourceRef">The image part URI within the package; the key into the sink's written-path map.</param>
/// <param name="AltText">
///     The image's descriptive alt text when a genuinely descriptive source was chosen; otherwise
///     <see langword="null"/> so a neutral placeholder is used rather than implying a description.
/// </param>
/// <remarks>Immutable and thread-safe.</remarks>
internal sealed record PowerPointSlideImageRef(string SourceRef, string? AltText);
