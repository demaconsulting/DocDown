using System.Reflection;
using DocDown.PowerPoint.Markdown;

namespace DemaConsulting.DocDown.PowerPoint.Tests.Markdown;

/// <summary>
///     Table-pinning tests for <see cref="PowerPointDiagnosticCodes"/>. The diagnostic-code set is a
///     published output contract the docstring calls contiguous and stable, so this test freezes the
///     exact identities and the contiguous numbering and fails on any silent drift.
/// </summary>
public class PowerPointDiagnosticCodesTests
{
    /// <summary>
    ///     Proves each diagnostic constant carries exactly its contracted code.
    /// </summary>
    [Fact]
    public void PowerPointDiagnosticCodes_Constants_MatchContract()
    {
        Assert.Equal("PPTX0001", PowerPointDiagnosticCodes.NoSlides);
        Assert.Equal("PPTX0002", PowerPointDiagnosticCodes.NoSpeakerNotes);
        Assert.Equal("PPTX0003", PowerPointDiagnosticCodes.VectorImageWrittenAsIs);
        Assert.Equal("PPTX0004", PowerPointDiagnosticCodes.SlideRenderFailed);
        Assert.Equal("PPTX0005", PowerPointDiagnosticCodes.ChartsNotExtracted);
    }

    /// <summary>
    ///     Proves the declared code set is exactly these four codes — no more, no fewer — so a
    ///     removed or newly-added constant is caught, and that the numbering is contiguous from
    ///     <c>PPTX0001</c> with no holes.
    /// </summary>
    [Fact]
    public void PowerPointDiagnosticCodes_Set_IsExactAndContiguous()
    {
        var codes = typeof(PowerPointDiagnosticCodes)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["PPTX0001", "PPTX0002", "PPTX0003", "PPTX0004", "PPTX0005"], codes);
    }
}
