using DocDown.Core;
using DocDown.Tool.Cli;

namespace DemaConsulting.DocDown.Tool.Tests.Cli;

/// <summary>
///     Unit tests for the <c>Context</c> unit: argument parsing and validation, option mapping, and
///     the output routing that <c>--silent</c> and <c>--log</c> control.
/// </summary>
public class ContextTests
{
    /// <summary>Proves an unrecognized argument is rejected.</summary>
    [Fact]
    public void Context_Create_UnknownArgument_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--not-a-flag"]));
    }

    /// <summary>Proves a log file receives written lines and is flushed immediately.</summary>
    [Fact]
    public void Context_Create_LogFile_WritesLinesWithAutoFlush()
    {
        var logFile = Path.Combine(Path.GetTempPath(), $"docdown-ctx-{Guid.NewGuid():N}.log");
        try
        {
            using (var context = Context.Create(["--log", logFile]))
            {
                context.WriteLine("hello world");

                // AutoFlush means the line is on disk before Dispose; read with a shared handle
                // because the writer still holds the file open.
                using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                Assert.Contains("hello world", reader.ReadToEnd(), StringComparison.Ordinal);
            }
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>Proves an error in silent mode still drives the exit code to 1.</summary>
    [Fact]
    public void Context_WriteError_SilentMode_StillSetsExitCodeOne()
    {
        using var context = Context.Create(["--silent"]);

        context.WriteError("something failed");

        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>Proves that with no error reported the exit code is 0.</summary>
    [Fact]
    public void Context_ExitCode_NoErrors_ReturnsZero()
    {
        using var context = Context.Create(["--silent"]);

        context.WriteLine("all good");

        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>Proves a heading depth outside the supported range is rejected.</summary>
    [Fact]
    public void Context_Create_DepthOutOfRange_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "7"]));
    }

    /// <summary>Proves a malformed page range is rejected.</summary>
    [Fact]
    public void Context_Create_MalformedPageRange_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--page-range", "3"]));
    }

    /// <summary>Proves a removed option is rejected as unsupported rather than silently ignored.</summary>
    [Fact]
    public void Context_Create_RemovedImagesOption_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--images", "png"]));
    }

    /// <summary>Proves the removed split option is rejected as unsupported.</summary>
    [Fact]
    public void Context_Create_RemovedSplitOption_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--split", "part"]));
    }

    /// <summary>Proves writing in silent mode suppresses the console but still writes the log.</summary>
    [Fact]
    public void Context_WriteLine_SilentMode_SuppressesConsoleButWritesLog()
    {
        var logFile = Path.Combine(Path.GetTempPath(), $"docdown-ctx-{Guid.NewGuid():N}.log");
        var originalOut = Console.Out;
        using var capturedOut = new StringWriter();
        Console.SetOut(capturedOut);
        try
        {
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                context.WriteLine("silent line");
            }

            Assert.DoesNotContain("silent line", capturedOut.ToString(), StringComparison.Ordinal);
            Assert.Contains("silent line", File.ReadAllText(logFile), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Proves the supported extraction flags project onto <see cref="ExtractionOptions"/>.
    /// </summary>
    [Fact]
    public void Context_BuildExtractionOptions_SupportedFlags_MapOntoOptions()
    {
        using var context = Context.Create(
        [
            "--input", "doc.pdf",
            "--scratch", "out",
            "--pages",
            "--page-range", "2-5",
            "--dpi", "300",
            "--max-image-dim", "1024",
            "--max-image-bytes", "2048",
            "--overwrite", "overwrite"
        ]);

        var options = context.BuildExtractionOptions();

        Assert.True(options.RenderPages);
        Assert.Equal(new PageRange(2, 5), options.Pages);
        Assert.Equal(300, options.PageRenderDpi);
        Assert.Equal(1024, options.MaxImageDimensionPx);
        Assert.Equal(2048L, options.MaxImageBytes);
        Assert.Equal(ScratchFolderMode.Overwrite, options.ScratchFolder);
    }

    /// <summary>
    ///     Proves <c>--no-images</c> and the supported scratch-policy tokens map to their enum values.
    /// </summary>
    [Theory]
    [InlineData("clean", ScratchFolderMode.CleanIfDocDownFolder)]
    [InlineData("overwrite", ScratchFolderMode.Overwrite)]
    public void Context_BuildExtractionOptions_SupportedScratchPolicyToken_MapsToMode(string token, ScratchFolderMode expected)
    {
        using var context = Context.Create(["--no-images", "--overwrite", token]);

        var options = context.BuildExtractionOptions();

        Assert.False(options.IncludeEmbeddedImages);
        Assert.Equal(expected, options.ScratchFolder);
    }

    /// <summary>
    ///     Proves removed scratch-policy tokens are rejected during argument parsing.
    /// </summary>
    /// <param name="token">The removed token that should no longer parse.</param>
    [Theory]
    [InlineData("require-empty")]
    [InlineData("unique")]
    public void Context_Create_RemovedScratchPolicyToken_ThrowsArgumentException(string token)
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--overwrite", token]));
    }
}
