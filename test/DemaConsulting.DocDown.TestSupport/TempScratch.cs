using System.Text;

namespace DemaConsulting.DocDown.TestSupport;

/// <summary>
///     A disposable, uniquely named temporary folder for one test, cleaned up on dispose.
/// </summary>
/// <remarks>
///     Each instance creates a fresh directory under the system temp path with a unique name, so
///     tests never share a fixed scratch location and are safe to run in parallel. Disposal
///     deletes the folder recursively on a best-effort basis: a cleanup failure (for example a
///     transient file lock) is swallowed so a test is never failed by teardown noise. Not
///     thread-safe; each test should own its own instance.
/// </remarks>
public sealed class TempScratch : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TempScratch"/> class, creating the folder.
    /// </summary>
    /// <remarks>
    ///     The folder name combines a fixed prefix with a GUID so concurrent tests cannot collide.
    ///     The directory is created eagerly so <see cref="Path"/> is immediately usable.
    /// </remarks>
    public TempScratch()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "docdown-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    ///     Gets the absolute path of the temporary folder.
    /// </summary>
    /// <remarks>The folder exists for the lifetime of the instance and is deleted on dispose.</remarks>
    public string Path { get; }

    /// <summary>
    ///     Creates a UTF-8 text file (no BOM) under the temporary folder and returns its path.
    /// </summary>
    /// <param name="relativeName">The file name or relative path within the folder. Must not be null or empty.</param>
    /// <param name="content">The text content to write.</param>
    /// <returns>The absolute path of the file that was written.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativeName"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Provided so a test can materialize an input document in one line; any intermediate
    ///     directories in <paramref name="relativeName"/> are created first.
    /// </remarks>
    public string CreateFile(string relativeName, string content)
    {
        // Validate inputs so a malformed relative name is rejected at the call site
        ArgumentException.ThrowIfNullOrEmpty(relativeName);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = System.IO.Path.Combine(Path, relativeName);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write UTF-8 without a BOM so an input file has no leading byte-order marker
        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return fullPath;
    }

    /// <summary>
    ///     Deletes the temporary folder and all its contents on a best-effort basis.
    /// </summary>
    /// <remarks>
    ///     Swallows any exception raised during deletion so a transient lock or an already-removed
    ///     folder never turns test teardown into a spurious failure.
    /// </remarks>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup: a transient lock must not fail the test
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup: a read-only or in-use file must not fail the test
        }
    }
}
