using System.Reflection;
using DocDown.PowerPoint.Markdown;

namespace DemaConsulting.DocDown.PowerPoint.Tests.Markdown;

/// <summary>
///     Table-pinning tests for <see cref="PowerPointDiagnosticCodes"/>. The diagnostic-code set is a
///     published output contract, so this test freezes the exact identities and fails on any silent
///     drift — including the accidental reuse of a retired code number.
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
        Assert.Equal("PPTX0003", PowerPointDiagnosticCodes.VectorImageWrittenAsIs);
        Assert.Equal("PPTX0004", PowerPointDiagnosticCodes.SlideRenderFailed);
        Assert.Equal("PPTX0005", PowerPointDiagnosticCodes.ChartsNotExtracted);
    }

    /// <summary>
    ///     Proves the declared code set is exactly these four codes — no more, no fewer — so a
    ///     removed or newly-added constant is caught.
    /// </summary>
    /// <remarks>
    ///     PPTX0002 is deliberately absent and permanently reserved: it announced that a deck carries
    ///     no speaker notes, a statement about the document rather than about the extraction, now
    ///     carried by the content outline as a counted zero. A published code number is never reused,
    ///     so the set is stable rather than contiguous and this test guards the hole.
    /// </remarks>
    [Fact]
    public void PowerPointDiagnosticCodes_Set_IsExactAndExcludesRetiredCode()
    {
        var codes = typeof(PowerPointDiagnosticCodes)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["PPTX0001", "PPTX0003", "PPTX0004", "PPTX0005"], codes);
        Assert.DoesNotContain("PPTX0002", codes);
    }
}
