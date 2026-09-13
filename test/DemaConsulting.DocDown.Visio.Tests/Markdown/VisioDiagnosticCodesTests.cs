using System.Reflection;
using DocDown.Visio.Markdown;

namespace DemaConsulting.DocDown.Visio.Tests.Markdown;

/// <summary>
///     Table-pinning tests for <see cref="VisioDiagnosticCodes"/>. The diagnostic-code set is a
///     published output contract the docstring calls contiguous and stable, so this test freezes the
///     exact identities and the contiguous numbering and fails on any silent drift.
/// </summary>
public class VisioDiagnosticCodesTests
{
    /// <summary>
    ///     Proves each diagnostic constant carries exactly its contracted code.
    /// </summary>
    [Fact]
    public void VisioDiagnosticCodes_Constants_MatchContract()
    {
        Assert.Equal("VISIO0001", VisioDiagnosticCodes.NoPages);
        Assert.Equal("VISIO0002", VisioDiagnosticCodes.EmptyPage);
        Assert.Equal("VISIO0003", VisioDiagnosticCodes.VectorImageWrittenAsIs);
        Assert.Equal("VISIO0004", VisioDiagnosticCodes.PageRenderFailed);
        Assert.Equal("VISIO0005", VisioDiagnosticCodes.TopologyLabelConvention);
        Assert.Equal("VISIO0006", VisioDiagnosticCodes.TopologyEndpointCoverage);
    }

    /// <summary>
    ///     Proves the declared code set is exactly these six codes — no more, no fewer — so a
    ///     removed or newly-added constant is caught, and that the numbering is contiguous from
    ///     <c>VISIO0001</c> with no holes.
    /// </summary>
    [Fact]
    public void VisioDiagnosticCodes_Set_IsExactAndContiguous()
    {
        var codes = typeof(VisioDiagnosticCodes)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["VISIO0001", "VISIO0002", "VISIO0003", "VISIO0004", "VISIO0005", "VISIO0006"], codes);
    }
}
