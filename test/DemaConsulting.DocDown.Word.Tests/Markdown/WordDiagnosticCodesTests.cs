using System.Reflection;
using DocDown.Word.Markdown;

namespace DemaConsulting.DocDown.Word.Tests.Markdown;

/// <summary>
///     Pins the Word diagnostic-code table so the published codes are a stable contract.
/// </summary>
/// <remarks>
///     A consumer branches on these codes, so a renumbering is a breaking change. This test makes any
///     drift a build failure rather than a silent surprise.
/// </remarks>
public class WordDiagnosticCodesTests
{
    /// <summary>
    ///     Proves the diagnostic-code table matches its pinned contract exactly, in names and values.
    /// </summary>
    [Fact]
    public void WordDiagnosticCodes_Table_MatchesPinnedContract()
    {
        // Arrange: the intended contract — contiguous WORD codes with stable names
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NoTextContent"] = "WORD0001",
            ["PasswordProtected"] = "WORD0002",
            ["EmptyTableSkipped"] = "WORD0003",
            ["TableHeaderAssumed"] = "WORD0004",
            ["MergedCellsFlattened"] = "WORD0005",
            ["VectorImageWrittenAsIs"] = "WORD0006",
            ["ForcePngNotHonored"] = "WORD0007",
            ["TrackedChangesAccepted"] = "WORD0008",
            ["HeaderFooterPageNumberingOnly"] = "WORD0009"
        };

        // Act: reflect over every public constant the table exposes
        var actual = typeof(WordDiagnosticCodes)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);

        // Assert: the table is exactly the pinned contract, no more and no less
        Assert.Equal(expected, actual);
    }
}
