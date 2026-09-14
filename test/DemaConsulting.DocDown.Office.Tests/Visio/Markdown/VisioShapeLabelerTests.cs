using DocDown.Visio.Markdown;
using DocDown.Visio.OpenXml;

namespace DemaConsulting.DocDown.Office.Tests.Visio.Markdown;

/// <summary>
///     Unit tests for <see cref="VisioShapeLabeler"/>, pinning the label precedence, the convention
///     that keeps a type visibly distinct from a name, and the refusal to name an endpoint after the
///     connector attached to it.
/// </summary>
public class VisioShapeLabelerTests
{
    /// <summary>
    ///     Builds the id-to-shape map the labeler consumes.
    /// </summary>
    /// <param name="shapes">The shapes to index.</param>
    /// <returns>The map from shape id to shape.</returns>
    private static Dictionary<string, VisioShapeModel> Index(params VisioShapeModel[] shapes) =>
        shapes.ToDictionary(shape => shape.Id, StringComparer.Ordinal);

    /// <summary>
    ///     Proves a shape's own text is used verbatim and reported as authored text, because text is
    ///     the only label anyone actually gave the shape.
    /// </summary>
    [Fact]
    public void VisioShapeLabeler_Label_ShapeWithText_UsesTextVerbatim()
    {
        var shapes = Index(new VisioShapeModel("1", "Transfer Pump", "Positive displacement"));

        var label = VisioShapeLabeler.Label("1", shapes);

        Assert.Equal("Transfer Pump", label.Display);
        Assert.Equal(VisioLabelSource.Text, label.Source);
    }

    /// <summary>
    ///     Proves a text-less shape is named by its master type, parenthesized and paired with the
    ///     shape id so it can never be read as a name someone authored.
    /// </summary>
    [Fact]
    public void VisioShapeLabeler_Label_TextLessShapeWithMaster_UsesParenthesizedTypeAndId()
    {
        var shapes = Index(new VisioShapeModel("5", null, "Positive displacement"));

        var label = VisioShapeLabeler.Label("5", shapes);

        Assert.Equal("(Positive displacement, shape 5)", label.Display);
        Assert.Equal(VisioLabelSource.Type, label.Source);
    }

    /// <summary>
    ///     Proves two text-less shapes sharing a master stay distinguishable, because the shape id
    ///     rides along with the type — otherwise a valve-to-valve edge would read as a self-loop.
    /// </summary>
    [Fact]
    public void VisioShapeLabeler_Label_TwoShapesOfSameType_RemainDistinguishable()
    {
        var shapes = Index(
            new VisioShapeModel("34", null, "3-way Plug Valve"),
            new VisioShapeModel("37", null, "3-way Plug Valve"));

        var first = VisioShapeLabeler.Label("34", shapes);
        var second = VisioShapeLabeler.Label("37", shapes);

        Assert.NotEqual(first.Display, second.Display);
    }

    /// <summary>
    ///     Proves a master describing the connector itself is refused as an endpoint label and the
    ///     honest shape-id fallback is kept, because naming an endpoint after the wire attached to it
    ///     would actively mislead.
    /// </summary>
    /// <param name="master">The connective master name under test.</param>
    [Theory]
    [InlineData("Dynamic connector")]
    [InlineData("Line-curve connector")]
    [InlineData("Straight Line")]
    [InlineData("Curved Line")]
    [InlineData("Freeform Line")]
    public void VisioShapeLabeler_Label_ConnectiveMaster_FallsBackToShapeId(string master)
    {
        var shapes = Index(new VisioShapeModel("96", null, master));

        var label = VisioShapeLabeler.Label("96", shapes);

        Assert.Equal("(shape 96)", label.Display);
        Assert.Equal(VisioLabelSource.Unresolved, label.Source);
    }

    /// <summary>
    ///     Proves a text-less shape with no master keeps the shape-id fallback rather than acquiring
    ///     an invented label.
    /// </summary>
    [Fact]
    public void VisioShapeLabeler_Label_TextLessShapeWithNoMaster_FallsBackToShapeId()
    {
        var shapes = Index(new VisioShapeModel("11", null));

        var label = VisioShapeLabeler.Label("11", shapes);

        Assert.Equal("(shape 11)", label.Display);
        Assert.Equal(VisioLabelSource.Unresolved, label.Source);
    }

    /// <summary>
    ///     Proves an endpoint naming a shape absent from the page degrades to the shape-id fallback
    ///     rather than throwing, so one malformed record cannot abort a whole extraction.
    /// </summary>
    [Fact]
    public void VisioShapeLabeler_Label_UnknownShapeId_FallsBackToShapeId()
    {
        var label = VisioShapeLabeler.Label("42", Index());

        Assert.Equal("(shape 42)", label.Display);
        Assert.Equal(VisioLabelSource.Unresolved, label.Source);
    }

    /// <summary>
    ///     Proves the published convention actually describes the three rendered forms, so a consumer
    ///     that reads only the convention can decode the output it accompanies.
    /// </summary>
    [Fact]
    public void VisioShapeLabeler_Convention_DescribesEveryRenderedForm()
    {
        Assert.Contains("(Type, shape N)", VisioShapeLabeler.Convention, StringComparison.Ordinal);
        Assert.Contains("(shape N)", VisioShapeLabeler.Convention, StringComparison.Ordinal);
        Assert.Contains("own text", VisioShapeLabeler.Convention, StringComparison.Ordinal);
    }
}
