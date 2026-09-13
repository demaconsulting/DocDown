using System.Text;
using System.Text.Json;
using DocDown.Core;

namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     Framework-agnostic assertions over a completed extraction folder, raising
///     <see cref="ContractAssertionException"/> on failure so no test runner is referenced.
/// </summary>
/// <remarks>
///     These helpers turn the library's honesty guarantees into one-line checks: that an
///     extraction folder passes the <see cref="ContractVerifier"/> with no violations, that the
///     standard layout is present with every ledger slot resolved, and that two files are
///     byte-for-byte identical. Each helper performs read-only filesystem I/O and holds no state.
/// </remarks>
public static class ContractAssert
{
    /// <summary>The six ledger slots every manifest must resolve.</summary>
    /// <remarks>Mirrors <see cref="ArtifactLedger"/> so a missing slot is caught as a layout failure.</remarks>
    private static readonly string[] LedgerSlots = ["summary", "manifest", "metadata", "content", "images", "pages"];

    /// <summary>
    ///     Asserts the extraction folder passes the contract verifier with no violations.
    /// </summary>
    /// <param name="scratchFolder">The absolute path of the extraction folder. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scratchFolder"/> is null or empty.</exception>
    /// <exception cref="ContractAssertionException">Thrown when the verifier reports one or more violations.</exception>
    /// <remarks>
    ///     Wraps <see cref="ContractVerifier.Verify"/> and, on failure, builds a message naming
    ///     every violation code and detail so the failure is self-explanatory.
    /// </remarks>
    public static void NoViolations(string scratchFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(scratchFolder);

        var violations = ContractVerifier.Verify(scratchFolder);
        if (violations.Count == 0)
        {
            return;
        }

        // Build a richly formatted report naming each violation so the failure is actionable
        var message = new StringBuilder();
        message.Append(violations.Count)
            .Append(" contract violation(s) in '")
            .Append(scratchFolder)
            .Append("':");
        foreach (var violation in violations)
        {
            message.Append('\n').Append("  - ").Append(violation.Code).Append(": ").Append(violation.Detail);
        }

        throw new ContractAssertionException(message.ToString());
    }

    /// <summary>
    ///     Asserts the standard layout is present: <c>summary.txt</c> and <c>manifest.json</c> exist
    ///     and the manifest resolves all six ledger slots.
    /// </summary>
    /// <param name="scratchFolder">The absolute path of the extraction folder. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scratchFolder"/> is null or empty.</exception>
    /// <exception cref="ContractAssertionException">
    ///     Thrown when a required file is missing, the manifest is unparsable, or a ledger slot is
    ///     absent or unresolved.
    /// </exception>
    /// <remarks>
    ///     Confirms the two always-present files exist and that the manifest's <c>artifacts</c> block
    ///     carries a resolved entry (with a <c>path</c> and <c>status</c>) for each of the six slots.
    /// </remarks>
    public static void LayoutPresent(string scratchFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(scratchFolder);

        var summaryPath = Path.Combine(scratchFolder, "summary.txt");
        var manifestPath = Path.Combine(scratchFolder, "manifest.json");

        if (!File.Exists(summaryPath))
        {
            throw new ContractAssertionException($"summary.txt is missing from '{scratchFolder}'.");
        }

        if (!File.Exists(manifestPath))
        {
            throw new ContractAssertionException($"manifest.json is missing from '{scratchFolder}'.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        }
        catch (JsonException exception)
        {
            throw new ContractAssertionException($"manifest.json in '{scratchFolder}' is not valid JSON.", exception);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("artifacts", out var artifacts))
            {
                throw new ContractAssertionException($"manifest.json in '{scratchFolder}' has no 'artifacts' ledger.");
            }

            // Every ledger slot must be present and resolved to a path and status
            foreach (var slot in LedgerSlots)
            {
                if (!artifacts.TryGetProperty(slot, out var entry))
                {
                    throw new ContractAssertionException(
                        $"manifest.json ledger in '{scratchFolder}' is missing the '{slot}' slot.");
                }

                if (!entry.TryGetProperty("path", out _) || !entry.TryGetProperty("status", out _))
                {
                    throw new ContractAssertionException(
                        $"manifest.json ledger slot '{slot}' in '{scratchFolder}' is not resolved.");
                }
            }
        }
    }

    /// <summary>
    ///     Asserts two files are byte-for-byte identical.
    /// </summary>
    /// <param name="pathA">The path of the first file. Must not be null or empty.</param>
    /// <param name="pathB">The path of the second file. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when either path is null or empty.</exception>
    /// <exception cref="ContractAssertionException">Thrown when the files differ in length or content.</exception>
    /// <remarks>Reads both files fully and compares their bytes, so it detects any difference including line endings.</remarks>
    public static void FileEquals(string pathA, string pathB)
    {
        ArgumentException.ThrowIfNullOrEmpty(pathA);
        ArgumentException.ThrowIfNullOrEmpty(pathB);

        var bytesA = File.ReadAllBytes(pathA);
        var bytesB = File.ReadAllBytes(pathB);

        if (!bytesA.AsSpan().SequenceEqual(bytesB))
        {
            throw new ContractAssertionException(
                $"files differ: '{pathA}' ({bytesA.Length} bytes) is not byte-identical to '{pathB}' ({bytesB.Length} bytes).");
        }
    }
}
