using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace DemaConsulting.DocDown.Office.Tests.PowerPoint.TestData;

/// <summary>
///     Builds every deck the test suite needs, at test time, from the Open XML SDK.
/// </summary>
/// <remarks>
///     No binary <c>.pptx</c> is committed to this repository. Each fixture is synthesized here when
///     the test that needs it runs, which keeps the repository text-only and makes the exact content
///     of every fixture readable in the same place it is asserted against. All members are static and
///     pure apart from their allocations.
/// </remarks>
public static class PptxFixtures
{
    /// <summary>
    ///     Builds arbitrary bytes for a legacy <c>.ppt</c> fixture whose content is never read.
    /// </summary>
    /// <returns>Placeholder bytes.</returns>
    /// <remarks>Used where selection fails before any extractor opens the deck.</remarks>
    public static byte[] LegacyPptBytes() => "This is a placeholder for a legacy binary PowerPoint deck."u8.ToArray();

    /// <summary>
    ///     Builds a two-slide deck where both slides carry a title, body text, and speaker notes.
    /// </summary>
    /// <returns>The deck bytes.</returns>
    public static byte[] DeckWithNotes() => Build(presentationPart =>
    {
        AddSlide(presentationPart, 256U, "Overview", ["First bullet", "Second bullet"], "Say this out loud on slide one.");
        AddSlide(presentationPart, 257U, "Details", ["Detail line"], "Remember the caveat on slide two.");
    });

    /// <summary>
    ///     Builds a two-slide deck with titles and body text but no speaker notes on any slide.
    /// </summary>
    /// <returns>The deck bytes.</returns>
    public static byte[] DeckWithoutNotes() => Build(presentationPart =>
    {
        AddSlide(presentationPart, 256U, "Alpha", ["Alpha body"], notes: null);
        AddSlide(presentationPart, 257U, "Beta", ["Beta body"], notes: null);
    });

    /// <summary>
    ///     Builds a single-slide deck carrying one embedded PNG picture with an authored description,
    ///     so the image reader and the sink-write pipeline can be exercised end to end.
    /// </summary>
    /// <returns>The deck bytes.</returns>
    public static byte[] DeckWithImage() => Build(presentationPart =>
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();

        var imagePart = slidePart.AddImagePart(ImagePartType.Png);
        using (var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            stream.Write([1, 2, 3, 4, 5, 6, 7, 8], 0, 8);
        }

        var relationshipId = slidePart.GetIdOfPart(imagePart);

        var shapeTree = new P.ShapeTree();
        shapeTree.AppendChild(GroupShapeProperties());
        shapeTree.AppendChild(TextShape(2U, "Title", P.PlaceholderValues.Title, ["Illustrated"]));
        shapeTree.AppendChild(PictureShape(3U, "Logo", "Company logo", relationshipId));

        var commonSlideData = new P.CommonSlideData();
        commonSlideData.AppendChild(shapeTree);
        var slide = new P.Slide();
        slide.AppendChild(commonSlideData);
        slidePart.Slide = slide;

        var slideIdList = presentationPart.Presentation!.SlideIdList!;
        slideIdList.AppendChild(new P.SlideId { Id = 256U, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
    });

    /// <summary>
    ///     Builds a single-slide deck carrying one embedded EMF vector metafile picture with an
    ///     authored description, so the vector-passthrough caveat can be exercised end to end.
    /// </summary>
    /// <returns>The deck bytes.</returns>
    public static byte[] DeckWithVectorImage() => Build(presentationPart =>
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();

        var imagePart = slidePart.AddImagePart(ImagePartType.Emf);
        using (var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
        {
            stream.Write([0x01, 0x00, 0x00, 0x00, 0x45, 0x4D, 0x46, 0x20], 0, 8);
        }

        var relationshipId = slidePart.GetIdOfPart(imagePart);

        var shapeTree = new P.ShapeTree();
        shapeTree.AppendChild(GroupShapeProperties());
        shapeTree.AppendChild(TextShape(2U, "Title", P.PlaceholderValues.Title, ["Schematic"]));
        shapeTree.AppendChild(PictureShape(3U, "Schematic", "Wiring schematic", relationshipId));

        var commonSlideData = new P.CommonSlideData();
        commonSlideData.AppendChild(shapeTree);
        var slide = new P.Slide();
        slide.AppendChild(commonSlideData);
        slidePart.Slide = slide;

        var slideIdList = presentationPart.Presentation!.SlideIdList!;
        slideIdList.AppendChild(new P.SlideId { Id = 256U, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
    });

    /// <summary>
    ///     Builds a picture shape referencing an embedded image part, with an authored description.
    /// </summary>
    /// <param name="id">The shape's non-visual drawing id.</param>
    /// <param name="name">The picture object name.</param>
    /// <param name="description">The authored description (alt text).</param>
    /// <param name="relationshipId">The relationship id of the embedded image part.</param>
    /// <returns>The picture element.</returns>
    private static P.Picture PictureShape(uint id, string name, string description, string relationshipId)
    {
        var nonVisualPictureProperties = new P.NonVisualPictureProperties(
            new P.NonVisualDrawingProperties { Id = id, Name = name, Description = description },
            new P.NonVisualPictureDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties());

        var blipFill = new P.BlipFill();
        blipFill.AppendChild(new D.Blip { Embed = relationshipId });
        var stretch = new D.Stretch();
        stretch.AppendChild(new D.FillRectangle());
        blipFill.AppendChild(stretch);

        var picture = new P.Picture();
        picture.AppendChild(nonVisualPictureProperties);
        picture.AppendChild(blipFill);
        picture.AppendChild(new P.ShapeProperties());
        return picture;
    }

    /// <summary>
    ///     Builds a deck and returns its bytes, applying the supplied population action to the
    ///     presentation part.
    /// </summary>
    /// <param name="populate">The action that adds slides to the presentation part.</param>
    /// <returns>The deck bytes.</returns>
    private static byte[] Build(Action<PresentationPart> populate)
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();
            presentationPart.Presentation.AppendChild(new P.SlideIdList());
            populate(presentationPart);
            presentationPart.Presentation.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Adds a slide carrying a title, body lines, and optional speaker notes, and registers it in
    ///     the slide-id list.
    /// </summary>
    /// <param name="presentationPart">The presentation part.</param>
    /// <param name="slideId">The unique slide identifier (>= 256).</param>
    /// <param name="title">The slide title.</param>
    /// <param name="body">The body text lines.</param>
    /// <param name="notes">The speaker notes, or <see langword="null"/> for none.</param>
    private static void AddSlide(
        PresentationPart presentationPart, uint slideId, string title, IReadOnlyList<string> body, string? notes)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();

        var shapeTree = new P.ShapeTree();
        shapeTree.AppendChild(GroupShapeProperties());
        shapeTree.AppendChild(TextShape(2U, "Title", P.PlaceholderValues.Title, [title]));
        shapeTree.AppendChild(TextShape(3U, "Body", P.PlaceholderValues.Body, body));

        var commonSlideData = new P.CommonSlideData();
        commonSlideData.AppendChild(shapeTree);
        var slide = new P.Slide();
        slide.AppendChild(commonSlideData);
        slidePart.Slide = slide;

        if (notes is not null)
        {
            AddNotes(slidePart, notes);
        }

        var slideIdList = presentationPart.Presentation!.SlideIdList!;
        slideIdList.AppendChild(new P.SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
    }

    /// <summary>
    ///     Attaches a notes slide carrying the given narration in a body placeholder.
    /// </summary>
    /// <param name="slidePart">The slide part to attach notes to.</param>
    /// <param name="notes">The narration text.</param>
    private static void AddNotes(SlidePart slidePart, string notes)
    {
        var notesPart = slidePart.AddNewPart<NotesSlidePart>();

        var shapeTree = new P.ShapeTree();
        shapeTree.AppendChild(GroupShapeProperties());
        shapeTree.AppendChild(TextShape(2U, "Notes Placeholder", P.PlaceholderValues.Body, [notes]));

        var commonSlideData = new P.CommonSlideData();
        commonSlideData.AppendChild(shapeTree);
        var notesSlide = new P.NotesSlide();
        notesSlide.AppendChild(commonSlideData);
        notesPart.NotesSlide = notesSlide;
    }

    /// <summary>
    ///     Builds the mandatory group-shape properties every shape tree begins with.
    /// </summary>
    /// <returns>The non-visual group-shape properties element.</returns>
    private static P.NonVisualGroupShapeProperties GroupShapeProperties()
    {
        var properties = new P.NonVisualGroupShapeProperties();
        properties.AppendChild(new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty });
        properties.AppendChild(new P.NonVisualGroupShapeDrawingProperties());
        properties.AppendChild(new P.ApplicationNonVisualDrawingProperties());
        return properties;
    }

    /// <summary>
    ///     Builds a text shape of the given placeholder type carrying the given paragraphs.
    /// </summary>
    /// <param name="id">The shape's non-visual drawing id.</param>
    /// <param name="name">The shape's name.</param>
    /// <param name="placeholder">The placeholder type (title or body).</param>
    /// <param name="paragraphs">The paragraph texts.</param>
    /// <returns>The shape element.</returns>
    private static P.Shape TextShape(uint id, string name, P.PlaceholderValues placeholder, IReadOnlyList<string> paragraphs)
    {
        var applicationProperties = new P.ApplicationNonVisualDrawingProperties();
        applicationProperties.AppendChild(new P.PlaceholderShape { Type = placeholder });

        var nonVisualShapeProperties = new P.NonVisualShapeProperties();
        nonVisualShapeProperties.AppendChild(new P.NonVisualDrawingProperties { Id = id, Name = name });
        nonVisualShapeProperties.AppendChild(new P.NonVisualShapeDrawingProperties());
        nonVisualShapeProperties.AppendChild(applicationProperties);

        var textBody = new P.TextBody();
        textBody.AppendChild(new D.BodyProperties());
        textBody.AppendChild(new D.ListStyle());
        foreach (var paragraph in paragraphs)
        {
            var run = new D.Run();
            run.AppendChild(new D.Text(paragraph));
            var element = new D.Paragraph();
            element.AppendChild(run);
            textBody.AppendChild(element);
        }

        var shape = new P.Shape();
        shape.AppendChild(nonVisualShapeProperties);
        shape.AppendChild(new P.ShapeProperties());
        shape.AppendChild(textBody);
        return shape;
    }
}
