using System.Text.RegularExpressions;

namespace DemaConsulting.DocDown.Core.Tests;

/// <summary>
///     Repository-level tests proving the API reference shipped inside every library package carries
///     a runnable usage example for each entry point a consumer starts from.
/// </summary>
/// <remarks>
///     The examples are authored as XML documentation comments in the library sources, and the
///     packaging pipeline turns those comments into the <c>api/</c> Markdown folder placed inside
///     each NuGet package. Asserting on the authored comment is therefore the earliest and most
///     direct place to prove the promise the README makes about offline example coverage: a member
///     whose comment carries no example cannot produce an API reference page that has one.
/// </remarks>
public class ApiExampleCoverageTests
{
    /// <summary>Matches the one-call backend registration method each format package exposes.</summary>
    private static readonly Regex RegistrationMethod = new(
        @"^\s*public\s+static\s+DocDownBuilder\s+(Add\w+)\s*\(\s*this\s+DocDownBuilder\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>Matches the builder type declaration consumers construct first.</summary>
    private static readonly Regex BuilderType = new(
        @"^\s*public\s+sealed\s+class\s+DocDownBuilder\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>Matches the extraction entry point the builder's engine exposes.</summary>
    private static readonly Regex ExtractAsyncMethod = new(
        @"^\s*public\s+(async\s+)?ValueTask<ExtractionResult>\s+ExtractAsync\s*\(",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Proves the builder, the extraction entry point, and every backend registration method
    ///     carry a runnable example in the API reference shipped with the package.
    /// </summary>
    [Fact]
    public void DocDownCore_ApiExamples_ConsumerEntryPoints_CarryRunnableExamples()
    {
        // Arrange: every checked-in library source file across all packages
        var sources = ProductionSources();
        Assert.NotEmpty(sources);

        // Act: collect each registration method and assert its documentation carries an example
        var registrations = new List<string>();
        foreach (var file in sources)
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var match = RegistrationMethod.Match(lines[index]);
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups[1].Value;
                registrations.Add(name);
                AssertCarriesExample(DocCommentAbove(lines, index), $"{Path.GetFileName(file)}: {name}()");
            }
        }

        // Assert: the full registration menu is covered, so the check cannot pass vacuously
        Assert.Equal(
            ["AddExcel", "AddOffice", "AddPdf", "AddPdfRendering", "AddPowerPoint", "AddVisio", "AddWord"],
            registrations.Order().ToArray());

        // Assert: the two Core entry points a consumer starts from carry examples too
        AssertMemberCarriesExample(
            Path.Combine("DemaConsulting.DocDown.Core", "Extraction", "DocDownBuilder.cs"),
            BuilderType,
            "DocDownBuilder");
        AssertMemberCarriesExample(
            Path.Combine("DemaConsulting.DocDown.Core", "Extraction", "DocDownEngine.cs"),
            ExtractAsyncMethod,
            "DocDownEngine.ExtractAsync");
    }

    /// <summary>
    ///     Asserts the first member matching a pattern in a library source file carries an example.
    /// </summary>
    /// <param name="relativePath">The source file path relative to the <c>src</c> folder.</param>
    /// <param name="declaration">The pattern identifying the member's declaration line.</param>
    /// <param name="member">The member name used in the failure message.</param>
    private static void AssertMemberCarriesExample(string relativePath, Regex declaration, string member)
    {
        var path = Path.Combine(RepositoryRoot(), "src", relativePath);
        Assert.True(File.Exists(path), $"'{relativePath}' was not found beneath src.");

        var lines = File.ReadAllLines(path);
        var index = Array.FindIndex(lines, line => declaration.IsMatch(line));
        Assert.True(index >= 0, $"No declaration of '{member}' was found in '{relativePath}'.");
        AssertCarriesExample(DocCommentAbove(lines, index), member);
    }

    /// <summary>
    ///     Asserts a documentation comment carries a complete, runnable C# example.
    /// </summary>
    /// <param name="documentation">The documentation comment text.</param>
    /// <param name="member">The member the comment documents, used in the failure message.</param>
    private static void AssertCarriesExample(string documentation, string member)
    {
        Assert.True(
            documentation.Contains("<example>", StringComparison.Ordinal),
            $"'{member}' carries no <example> element, so its API reference page ships without one.");
        Assert.True(
            documentation.Contains("<code language=\"csharp\">", StringComparison.Ordinal),
            $"'{member}' has an example with no C# code block, so no runnable sample ships with it.");
        Assert.True(
            documentation.Contains("new DocDownBuilder()", StringComparison.Ordinal),
            $"'{member}' has an example that does not build an engine, so it is not runnable as shown.");
    }

    /// <summary>
    ///     Reads the documentation comment immediately above a declaration line.
    /// </summary>
    /// <param name="lines">The source file's lines.</param>
    /// <param name="declarationIndex">The index of the declaration line.</param>
    /// <returns>The documentation comment text, or an empty string when the member has none.</returns>
    /// <remarks>
    ///     Walks upward over attributes and the contiguous run of <c>///</c> lines, which is exactly
    ///     the block the compiler attributes to the member. Pure.
    /// </remarks>
    private static string DocCommentAbove(string[] lines, int declarationIndex)
    {
        var collected = new List<string>();
        for (var index = declarationIndex - 1; index >= 0; index--)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith('['))
            {
                continue;
            }

            if (!trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                break;
            }

            collected.Add(trimmed);
        }

        collected.Reverse();
        return string.Join('\n', collected);
    }

    /// <summary>
    ///     Enumerates the checked-in C# sources of every library package.
    /// </summary>
    /// <returns>The absolute paths of the library sources, excluding build output.</returns>
    private static List<string> ProductionSources() =>
        [.. Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

    /// <summary>
    ///     Walks up from the test output to the repository root.
    /// </summary>
    /// <returns>The absolute path of the folder holding the solution file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no ancestor holds the solution file.</exception>
    /// <remarks>Anchored on the solution file so the walk cannot stop at a coincidentally named folder. Read-only I/O.</remarks>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(ApiExampleCoverageTests).Assembly.Location)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DocDown.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from the test output folder.");
    }
}
