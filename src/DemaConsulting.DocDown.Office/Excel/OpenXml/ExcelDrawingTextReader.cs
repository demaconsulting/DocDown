using System.Text;
using DocumentFormat.OpenXml.Packaging;
using D = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace DocDown.Excel.OpenXml;

/// <summary>
///     Reads the text carried by the drawing shapes floating over a worksheet — callouts, labels, and
///     annotations that live in the drawing layer and in no cell.
/// </summary>
/// <remarks>
///     <para>
///         A worksheet's drawing holds more than pictures. Engineers annotate a pasted photograph or
///         schematic with text boxes and callout shapes: a part number beside a component, a numbered
///         label keyed to a legend, a warning about a worst-case condition. That text exists only in
///         the drawing part, so a backend that reads cells and pictures alone drops it while reporting
///         a complete extraction — the same failure mode as a dropped chart, in a quieter place.
///     </para>
///     <para>
///         Shapes are read in drawing order, including those nested inside groups, and a shape with no
///         text contributes nothing. Read-only over the package; stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ExcelDrawingTextReader
{
    /// <summary>
    ///     Collects the text of every shape in a worksheet's drawing, in drawing order.
    /// </summary>
    /// <param name="worksheetPart">The worksheet part whose drawing is walked, or <see langword="null"/> when the sheet has no part.</param>
    /// <returns>The non-empty shape texts in drawing order; empty when the sheet carries no annotated shape.</returns>
    /// <remarks>
    ///     Group membership is flattened because a grouped callout is read no differently by a person
    ///     looking at the sheet, and preserving the grouping would add structure a reader cannot act on.
    ///     Read-only I/O over the package.
    /// </remarks>
    public static IReadOnlyList<string> Collect(WorksheetPart? worksheetPart)
    {
        var drawing = worksheetPart?.DrawingsPart?.WorksheetDrawing;
        if (drawing is null)
        {
            return [];
        }

        var texts = new List<string>();
        foreach (var shape in drawing.Descendants<Xdr.Shape>())
        {
            var text = TextOf(shape);
            if (text.Length > 0)
            {
                texts.Add(text);
            }
        }

        return texts;
    }

    /// <summary>
    ///     Reads one shape's text, joining its paragraphs into a single line.
    /// </summary>
    /// <param name="shape">The drawing shape to read.</param>
    /// <returns>The shape's text, or an empty string when it carries none.</returns>
    /// <remarks>
    ///     Runs within a paragraph concatenate so a label split across formatting runs is not torn
    ///     apart, and paragraphs join with a space so a multi-line callout stays one readable annotation
    ///     rather than fragments a reader must reassemble. Pure.
    /// </remarks>
    private static string TextOf(Xdr.Shape shape)
    {
        var body = shape.TextBody;
        if (body is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var paragraph in body.Elements<D.Paragraph>())
        {
            var line = string.Concat(paragraph.Descendants<D.Text>().Select(run => run.Text)).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line);
        }

        return builder.ToString();
    }
}
