using DocDown.Core;

namespace DocDown.Tool.Cli;

/// <summary>
///     Owns the parsed command-line arguments and every channel the tool writes to: standard
///     output, standard error, and an optional <c>--log</c> file.
/// </summary>
/// <remarks>
///     <para>
///         The type follows the DEMA tool house pattern: a private constructor with a static
///         <see cref="Create"/> factory, a nested <see cref="ArgumentParser"/> that dispatches on a
///         <c>switch</c>, and <see cref="WriteLine"/>/<see cref="WriteError"/> routing that a
///         <c>--silent</c> flag suppresses on the console but never on the log. <see cref="WriteError"/>
///         sets the error flag unconditionally, so <see cref="ExitCode"/> reports failure even when
///         the console output was silenced.
///     </para>
///     <para>
///         Instances are not thread-safe and are intended to be used from a single thread for the
///         lifetime of one invocation. <see cref="Dispose"/> closes the log file.
///     </para>
/// </remarks>
internal sealed class Context : IDisposable
{
    /// <summary>Log file stream writer (if logging is enabled).</summary>
    private StreamWriter? _logWriter;

    /// <summary>Indicates whether any error has been reported.</summary>
    private bool _hasErrors;

    /// <summary>Gets a value indicating whether the version flag was specified.</summary>
    public bool Version { get; private init; }

    /// <summary>Gets a value indicating whether the help flag was specified.</summary>
    public bool Help { get; private init; }

    /// <summary>Gets a value indicating whether the silent flag was specified.</summary>
    public bool Silent { get; private init; }

    /// <summary>Gets a value indicating whether the validate flag was specified.</summary>
    public bool Validate { get; private init; }

    /// <summary>Gets the validation results file path, or <see langword="null"/> when not requested.</summary>
    public string? ResultsFile { get; private init; }

    /// <summary>Gets the heading depth for markdown output (default is 1).</summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>Gets a value indicating whether the list-backends command was requested.</summary>
    public bool ListBackends { get; private init; }

    /// <summary>Gets the folder to run the contract verifier against, or <see langword="null"/> when not requested.</summary>
    public string? VerifyFolder { get; private init; }

    /// <summary>Gets the document to extract, or <see langword="null"/> when none was specified.</summary>
    public string? Input { get; private init; }

    /// <summary>Gets the scratch folder to extract into, or <see langword="null"/> when none was specified.</summary>
    public string? Scratch { get; private init; }

    /// <summary>Gets a value indicating whether rendered page images were requested.</summary>
    public bool RenderPages { get; private init; }

    /// <summary>Gets a value indicating whether embedded image extraction is enabled (default true).</summary>
    public bool IncludeEmbeddedImages { get; private init; } = true;

    /// <summary>Gets a value indicating whether page rendering must fail rather than degrade.</summary>
    public bool RequirePages { get; private init; }

    /// <summary>Gets the requested page range, or <see langword="null"/> for the whole document.</summary>
    public PageRange? Pages { get; private init; }

    /// <summary>Gets the requested page-render DPI, or <see langword="null"/> to use the engine default.</summary>
    public int? Dpi { get; private init; }

    /// <summary>Gets the requested image output mode, or <see langword="null"/> to use the engine default.</summary>
    public ImageOutputMode? ImageOutput { get; private init; }

    /// <summary>Gets the maximum image dimension in pixels, or <see langword="null"/> for no limit.</summary>
    public int? MaxImageDimensionPx { get; private init; }

    /// <summary>Gets the maximum image size in bytes, or <see langword="null"/> for no limit.</summary>
    public long? MaxImageBytes { get; private init; }

    /// <summary>Gets the requested content-split mode, or <see langword="null"/> to use the engine default.</summary>
    public ContentSplitMode? ContentSplit { get; private init; }

    /// <summary>Gets the forced extractor identifier, or <see langword="null"/> for automatic selection.</summary>
    public string? Backend { get; private init; }

    /// <summary>Gets the requested scratch-folder policy, or <see langword="null"/> to use the engine default.</summary>
    public ScratchFolderMode? ScratchMode { get; private init; }

    /// <summary>Gets the proposed exit code for the application (0 for success, 1 for errors).</summary>
    public int ExitCode => _hasErrors ? 1 : 0;

    /// <summary>Private constructor - use <see cref="Create"/> instead.</summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a <see cref="Context"/> from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new, fully parsed <see cref="Context"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an argument is unrecognized or malformed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        var result = new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            Validate = parser.Validate,
            ResultsFile = parser.ResultsFile,
            HeadingDepth = parser.HeadingDepth,
            ListBackends = parser.ListBackends,
            VerifyFolder = parser.VerifyFolder,
            Input = parser.Input,
            Scratch = parser.Scratch,
            RenderPages = parser.RenderPages,
            IncludeEmbeddedImages = parser.IncludeEmbeddedImages,
            RequirePages = parser.RequirePages,
            Pages = parser.Pages,
            Dpi = parser.Dpi,
            ImageOutput = parser.ImageOutput,
            MaxImageDimensionPx = parser.MaxImageDimensionPx,
            MaxImageBytes = parser.MaxImageBytes,
            ContentSplit = parser.ContentSplit,
            Backend = parser.Backend,
            ScratchMode = parser.ScratchMode
        };

        // Open log file if specified
        if (parser.LogFile != null)
        {
            result.OpenLogFile(parser.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Projects the parsed extraction flags onto a fresh <see cref="ExtractionOptions"/>.
    /// </summary>
    /// <returns>An options instance carrying only the values the caller supplied on the command line.</returns>
    /// <remarks>
    ///     Only options the caller explicitly set are written; every other property is left at the
    ///     <see cref="ExtractionOptions"/> default. No option is invented that Core does not support.
    ///     <c>--require-pages</c> adds the <see cref="ExtractorCapabilities.RenderedPages"/> hard
    ///     requirement, which turns an unavailable page render from a degrade into a failure.
    /// </remarks>
    public ExtractionOptions BuildExtractionOptions()
    {
        var options = new ExtractionOptions
        {
            RenderPages = RenderPages,
            IncludeEmbeddedImages = IncludeEmbeddedImages
        };

        if (Pages.HasValue)
        {
            options.Pages = Pages.Value;
        }

        if (Dpi.HasValue)
        {
            options.PageRenderDpi = Dpi.Value;
        }

        if (ImageOutput.HasValue)
        {
            options.ImageOutput = ImageOutput.Value;
        }

        if (MaxImageDimensionPx.HasValue)
        {
            options.MaxImageDimensionPx = MaxImageDimensionPx.Value;
        }

        if (MaxImageBytes.HasValue)
        {
            options.MaxImageBytes = MaxImageBytes.Value;
        }

        if (ContentSplit.HasValue)
        {
            options.ContentSplit = ContentSplit.Value;
        }

        if (ScratchMode.HasValue)
        {
            options.ScratchFolder = ScratchMode.Value;
        }

        if (!string.IsNullOrEmpty(Backend))
        {
            options.PreferredExtractorId = Backend;
        }

        // A hard capability requirement so an unavailable renderer fails rather than degrades
        if (RequirePages)
        {
            options.RequireCapabilities = (options.RequireCapabilities ?? ExtractorCapabilities.None)
                | ExtractorCapabilities.RenderedPages;
        }

        return options;
    }

    /// <summary>Opens the log file for writing with immediate flushing.</summary>
    /// <param name="logFile">Log file path.</param>
    /// <exception cref="InvalidOperationException">Thrown when the log file cannot be opened.</exception>
    private void OpenLogFile(string logFile)
    {
        try
        {
            // AutoFlush so log entries reach disk even if the process terminates before Dispose
            _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Generic catch is justified: any file-system exception is wrapped with context here.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Writes a line to standard output and to the log file (when open).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>Standard-output writing is suppressed when <see cref="Silent"/> is set; the log is not.</remarks>
    public void WriteLine(string message)
    {
        if (!Silent)
        {
            Console.WriteLine(message);
        }

        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Writes an error message to standard error and to the log file (when open), and records
    ///     that an error occurred.
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     The error flag is set <strong>unconditionally</strong>, so <see cref="ExitCode"/> returns
    ///     1 even when <see cref="Silent"/> suppresses the console output. Standard-error writing is
    ///     suppressed when silent; the log is not.
    /// </remarks>
    public void WriteError(string message)
    {
        // Mark that we have encountered errors regardless of silence
        _hasErrors = true;

        if (!Silent)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        _logWriter?.WriteLine(message);
    }

    /// <summary>Closes the log file, if one is open.</summary>
    public void Dispose()
    {
        _logWriter?.Dispose();
        _logWriter = null;
    }

    /// <summary>
    ///     Parses command-line arguments into the fields the <see cref="Context"/> is built from.
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>Gets a value indicating whether the version flag was specified.</summary>
        public bool Version { get; private set; }

        /// <summary>Gets a value indicating whether the help flag was specified.</summary>
        public bool Help { get; private set; }

        /// <summary>Gets a value indicating whether the silent flag was specified.</summary>
        public bool Silent { get; private set; }

        /// <summary>Gets a value indicating whether the validate flag was specified.</summary>
        public bool Validate { get; private set; }

        /// <summary>Gets the log file path.</summary>
        public string? LogFile { get; private set; }

        /// <summary>Gets the validation results file path.</summary>
        public string? ResultsFile { get; private set; }

        /// <summary>Gets the heading depth for markdown output.</summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>Gets a value indicating whether the list-backends command was requested.</summary>
        public bool ListBackends { get; private set; }

        /// <summary>Gets the folder to verify.</summary>
        public string? VerifyFolder { get; private set; }

        /// <summary>Gets the document to extract.</summary>
        public string? Input { get; private set; }

        /// <summary>Gets the scratch folder to extract into.</summary>
        public string? Scratch { get; private set; }

        /// <summary>Gets a value indicating whether rendered pages were requested.</summary>
        public bool RenderPages { get; private set; }

        /// <summary>Gets a value indicating whether embedded image extraction is enabled.</summary>
        public bool IncludeEmbeddedImages { get; private set; } = true;

        /// <summary>Gets a value indicating whether page rendering must fail rather than degrade.</summary>
        public bool RequirePages { get; private set; }

        /// <summary>Gets the requested page range.</summary>
        public PageRange? Pages { get; private set; }

        /// <summary>Gets the requested page-render DPI.</summary>
        public int? Dpi { get; private set; }

        /// <summary>Gets the requested image output mode.</summary>
        public ImageOutputMode? ImageOutput { get; private set; }

        /// <summary>Gets the maximum image dimension in pixels.</summary>
        public int? MaxImageDimensionPx { get; private set; }

        /// <summary>Gets the maximum image size in bytes.</summary>
        public long? MaxImageBytes { get; private set; }

        /// <summary>Gets the requested content-split mode.</summary>
        public ContentSplitMode? ContentSplit { get; private set; }

        /// <summary>Gets the forced extractor identifier.</summary>
        public string? Backend { get; private set; }

        /// <summary>Gets the requested scratch-folder policy.</summary>
        public ScratchFolderMode? ScratchMode { get; private set; }

        /// <summary>Parses every argument in order.</summary>
        /// <param name="args">Command-line arguments.</param>
        public void ParseArguments(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>Parses a single argument and returns the index of the next one.</summary>
        /// <param name="arg">The argument to parse.</param>
        /// <param name="args">All arguments.</param>
        /// <param name="index">The index following <paramref name="arg"/>.</param>
        /// <returns>The updated index.</returns>
        /// <exception cref="ArgumentException">Thrown when the argument is unrecognized or malformed.</exception>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--log":
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    HeadingDepth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1, 6);
                    return index + 1;

                case "--list-backends":
                    ListBackends = true;
                    return index;

                case "--verify":
                    VerifyFolder = GetRequiredStringArgument(arg, args, index, "a folder argument");
                    return index + 1;

                case "--input":
                    Input = GetRequiredStringArgument(arg, args, index, "a document path argument");
                    return index + 1;

                case "--scratch":
                    Scratch = GetRequiredStringArgument(arg, args, index, "a folder argument");
                    return index + 1;

                case "--pages":
                    RenderPages = true;
                    return index;

                case "--no-pages":
                    RenderPages = false;
                    return index;

                case "--require-pages":
                    RequirePages = true;
                    return index;

                case "--no-images":
                    IncludeEmbeddedImages = false;
                    return index;

                case "--page-range":
                    Pages = ParsePageRange(arg, GetRequiredStringArgument(arg, args, index, "a page range argument (a-b)"));
                    return index + 1;

                case "--dpi":
                    Dpi = GetRequiredIntArgument(arg, args, index, "a DPI argument", 36, 1200);
                    return index + 1;

                case "--images":
                    ImageOutput = ParseImageOutput(arg, GetRequiredStringArgument(arg, args, index, "an image mode argument (preserve|png)"));
                    return index + 1;

                case "--max-image-dim":
                    MaxImageDimensionPx = GetRequiredIntArgument(arg, args, index, "a pixel dimension argument", 1, int.MaxValue);
                    return index + 1;

                case "--max-image-bytes":
                    MaxImageBytes = GetRequiredLongArgument(arg, args, index, "a byte-count argument", 1, long.MaxValue);
                    return index + 1;

                case "--split":
                    ContentSplit = ParseContentSplit(arg, GetRequiredStringArgument(arg, args, index, "a split mode argument (auto|single|part)"));
                    return index + 1;

                case "--backend":
                    Backend = GetRequiredStringArgument(arg, args, index, "an extractor identifier argument");
                    return index + 1;

                case "--overwrite":
                    ScratchMode = ParseScratchMode(arg, GetRequiredStringArgument(arg, args, index, "a scratch policy argument (require-empty|clean|overwrite|unique)"));
                    return index + 1;

                default:
                    throw new ArgumentException($"Unsupported argument '{arg}'", nameof(args));
            }
        }

        /// <summary>Gets a required string argument value.</summary>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            return args[index];
        }

        /// <summary>Gets a required integer argument in the inclusive range [min, max].</summary>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min, int max)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!int.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }

        /// <summary>Gets a required long argument in the inclusive range [min, max].</summary>
        private static long GetRequiredLongArgument(string arg, string[] args, int index, string description, long min, long max)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!long.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }

        /// <summary>Parses a <c>a-b</c> page range into a <see cref="PageRange"/>.</summary>
        private static PageRange ParsePageRange(string arg, string value)
        {
            var parts = value.Split('-');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var first)
                || !int.TryParse(parts[1], out var last)
                || first < 1
                || last < 1)
            {
                throw new ArgumentException($"{arg} requires a page range in the form 'a-b' with pages >= 1", nameof(arg));
            }

            return new PageRange(first, last);
        }

        /// <summary>Parses an image output token into an <see cref="ImageOutputMode"/>.</summary>
        private static ImageOutputMode ParseImageOutput(string arg, string value) => value switch
        {
            "preserve" => ImageOutputMode.Preserve,
            "png" => ImageOutputMode.ForcePng,
            _ => throw new ArgumentException($"{arg} requires 'preserve' or 'png'", nameof(arg))
        };

        /// <summary>Parses a content-split token into a <see cref="ContentSplitMode"/>.</summary>
        private static ContentSplitMode ParseContentSplit(string arg, string value) => value switch
        {
            "auto" => ContentSplitMode.Auto,
            "single" => ContentSplitMode.Single,
            "part" => ContentSplitMode.PerPart,
            _ => throw new ArgumentException($"{arg} requires 'auto', 'single', or 'part'", nameof(arg))
        };

        /// <summary>Parses a scratch-policy token into a <see cref="ScratchFolderMode"/>.</summary>
        private static ScratchFolderMode ParseScratchMode(string arg, string value) => value switch
        {
            "require-empty" => ScratchFolderMode.RequireEmpty,
            "clean" => ScratchFolderMode.CleanIfDocDownFolder,
            "overwrite" => ScratchFolderMode.Overwrite,
            "unique" => ScratchFolderMode.CreateUnique,
            _ => throw new ArgumentException($"{arg} requires 'require-empty', 'clean', 'overwrite', or 'unique'", nameof(arg))
        };
    }
}
