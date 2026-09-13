using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     Read-only assertions that every inline resource link in a completed extraction folder
///     resolves to a real file on disk when read relative to its own containing document.
/// </summary>
/// <remarks>
///     <para>
///         String assertions on emitted markdown cannot catch a link that dangles on disk, because
///         a link string can be well-formed yet point at a path that does not exist from the file it
///         lives in. These helpers close that gap by doing what a Markdown renderer or an LLM agent
///         would do: they extract each <c>![alt](target)</c> and <c>[text](target)</c> link, resolve
///         the target against the directory of the file that contains it, and assert the target
///         exists. This is exactly the class of check that would have caught the dangling
///         <c>parts/images/…</c> defect.
///     </para>
///     <para>
///         Only local resource targets are checked: external (<c>http(s):</c>, <c>mailto:</c>) and
///         in-document anchor (<c>#…</c>) targets are skipped because they name no file on disk. The
///         helpers perform read-only filesystem I/O, hold no state, and raise
///         <see cref="ContractAssertionException"/> so no test-framework dependency is introduced.
///     </para>
/// </remarks>
public static class MarkdownImageLinks
{
    /// <summary>
    ///     Matches a Markdown inline link and captures its target, for both the image and plain forms.
    /// </summary>
    /// <remarks>
    ///     Group <c>target</c> captures everything between <c>](</c> and the closing <c>)</c>; a title
    ///     suffix is not expected in DocDown output, so the simple form suffices for these fixtures.
    ///     A match timeout is supplied so the analyzer's bound holds here as it does on the production
    ///     rewriter; the pattern has no nested quantifier and runs only over generated test output.
    /// </remarks>
    private static readonly Regex LinkPattern = new(
        @"!?\[[^\]]*\]\((?<target>[^)]+)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    /// <summary>
    ///     Asserts that every inline resource link in <c>content.md</c> and every <c>parts/*.md</c>
    ///     under the scratch root resolves to an existing file from its own containing document.
    /// </summary>
    /// <param name="scratchRoot">The absolute path of the extraction folder. Must not be null or empty.</param>
    /// <returns>The number of local resource links that were checked and found to resolve.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scratchRoot"/> is null or empty.</exception>
    /// <exception cref="ContractAssertionException">
    ///     Thrown when any local resource link resolves to a path that does not exist, with a message
    ///     naming every dangling link and its containing file.
    /// </exception>
    /// <remarks>
    ///     Walks the content entry point and each split part file so both layouts are covered by a
    ///     single call. Returns the resolved count so a caller can additionally assert that a document
    ///     it knows carries links was actually exercised (guarding against a vacuous pass).
    /// </remarks>
    public static int AssertAllImageLinksResolveOnDisk(string scratchRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(scratchRoot);

        // Gather the content entry point and every split part file; either may carry resource links
        var documents = new List<string>();
        var contentPath = Path.Combine(scratchRoot, "content.md");
        if (File.Exists(contentPath))
        {
            documents.Add(contentPath);
        }

        var partsFolder = Path.Combine(scratchRoot, "parts");
        if (Directory.Exists(partsFolder))
        {
            documents.AddRange(Directory.GetFiles(partsFolder, "*.md", SearchOption.AllDirectories));
        }

        // Resolve each local link from its own directory, collecting every dangling target
        var resolved = 0;
        var failures = new List<string>();
        foreach (var document in documents)
        {
            resolved += CheckDocument(document, failures);
        }

        if (failures.Count > 0)
        {
            var message = new StringBuilder()
                .Append(failures.Count)
                .Append(" markdown link(s) do not resolve on disk:\n")
                .AppendJoin('\n', failures)
                .ToString();
            throw new ContractAssertionException(message);
        }

        return resolved;
    }

    /// <summary>
    ///     Checks every local resource link in one markdown document, recording any that dangle.
    /// </summary>
    /// <param name="documentPath">The absolute path of the markdown document to check.</param>
    /// <param name="failures">The running list of human-readable failure descriptions to append to.</param>
    /// <returns>The number of local resource links in this document that resolved successfully.</returns>
    /// <remarks>
    ///     Resolves each target against the document's own directory, mirroring how a renderer reads a
    ///     relative link, and skips targets that name no local file (external URLs and anchors).
    /// </remarks>
    private static int CheckDocument(string documentPath, List<string> failures)
    {
        var directory = Path.GetDirectoryName(documentPath)!;
        var text = File.ReadAllText(documentPath);
        var resolved = 0;

        foreach (Match match in LinkPattern.Matches(text))
        {
            var target = match.Groups["target"].Value;

            // Skip anything that does not name a local file: external URLs and in-document anchors
            if (IsExternalOrAnchor(target))
            {
                continue;
            }

            // Resolve the target relative to the containing document, exactly as a renderer would
            var candidate = Path.GetFullPath(Path.Combine(directory, target.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(candidate))
            {
                resolved++;
            }
            else
            {
                failures.Add($"  {documentPath}: '{target}' -> missing '{candidate}'");
            }
        }

        return resolved;
    }

    /// <summary>
    ///     Determines whether a link target names no local file and so must be skipped.
    /// </summary>
    /// <param name="target">The raw link target extracted from the markdown.</param>
    /// <returns><see langword="true"/> for external URLs and in-document anchors; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     External schemes and anchors are not on-disk resources, so resolving them would be
    ///     meaningless; every other target is treated as a filesystem path to verify.
    /// </remarks>
    private static bool IsExternalOrAnchor(string target) =>
        target.StartsWith('#')
        || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
}
