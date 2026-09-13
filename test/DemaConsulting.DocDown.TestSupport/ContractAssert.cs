using System.Text;
using System.Text.Json;
using DocDown.Core;

namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     Framework-agnostic assertions over a completed extraction folder, raising
///     <see cref="ContractAssertionException"/> on failure so no test runner is referenced.
/// </summary>
/// <remarks>
///     These helpers turn the library's output guarantees into one-line checks: that the standard
///     layout is present with the manifest's mandatory blocks, and that two files are byte-for-byte
///     identical. Each helper performs read-only filesystem I/O and holds no state.
/// </remarks>
public static class ContractAssert
{
    /// <summary>
    ///     Asserts the standard layout is present: <c>summary.txt</c> and <c>manifest.json</c> exist
    ///     and the manifest carries its mandatory top-level blocks.
    /// </summary>
    /// <param name="scratchFolder">The absolute path of the extraction folder. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scratchFolder"/> is null or empty.</exception>
    /// <exception cref="ContractAssertionException">
    ///     Thrown when a required file is missing, the manifest is unparsable, or a mandatory block is
    ///     absent.
    /// </exception>
    /// <remarks>
    ///     Confirms the two always-present files exist and that the manifest carries the
    ///     <c>tool</c>, <c>status</c>, <c>source</c>, and <c>notes</c> blocks that describe the run.
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
            // Every mandatory top-level block must be present so the manifest is self-describing
            foreach (var block in new[] { "tool", "status", "source", "notes" })
            {
                if (!document.RootElement.TryGetProperty(block, out _))
                {
                    throw new ContractAssertionException(
                        $"manifest.json in '{scratchFolder}' is missing the mandatory '{block}' block.");
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
