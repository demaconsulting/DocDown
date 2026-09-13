using System.Text;
using DocDown.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
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

            foreach (var (slidePart, slideOrdinal) in slideParts)
            {
                var imageRefs = imageCollection.SlideImageRefs.TryGetValue(slideOrdinal, out var refs)
                    ? refs
                    : [];
                slides.Add(ReadSlide(slidePart, slideOrdinal, imageRefs));
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
                document.PackageProperties.Identifier)));
        }
    }

    /// <summary>
    ///     Reads one slide into the model: its title, body text lines, and speaker notes.
    /// </summary>
    /// <param name="slidePart">The slide part.</param>
    /// <param name="ordinal">The slide's 1-based ordinal.</param>
    /// <param name="imageRefs">The images this slide references, in reading order, for inline linking.</param>
    /// <returns>The slide model.</returns>
    /// <remarks>
    ///     The title comes from the title placeholder; every other shape's paragraphs become body
    ///     lines. The notes come from the notes slide's body placeholder. Read-only over the part.
    /// </remarks>
    private static PowerPointSlideModel ReadSlide(
        SlidePart slidePart, int ordinal, IReadOnlyList<PowerPointSlideImageRef> imageRefs)
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
        return new PowerPointSlideModel(ordinal, title, lines, notes, imageRefs);
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
