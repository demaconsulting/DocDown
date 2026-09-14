using System.Reflection;
using DocDown.Core;
using DocDown.Excel;
using DocDown.Pdf;
using DocDown.Pdf.Rendering;
using DocDown.PowerPoint;
using DocDown.Tool.Cli;
using DocDown.Tool.SelfTest;
using DocDown.Visio;
using DocDown.Word;

namespace DocDown.Tool;

/// <summary>
///     Entry point for the <c>docdown</c> command-line tool: priority-ordered dispatch, the banner
///     and help text, the extraction and reporting logic, and the process exit code.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Main"/> treats <see cref="ArgumentException"/> and
///         <see cref="InvalidOperationException"/> as expected errors — their message goes to
///         standard error and the process returns 1 without a stack trace — and re-throws anything
///         else after writing it to standard error so the runtime can record it.
///     </para>
///     <para>
///         Extractors are registered explicitly through
///         <c>new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build()</c>, with no reflection
///         or assembly scanning, which is what keeps single-file publishing viable. The optional
///         rendering backend carries a native stack (PDFium/SkiaSharp), so a self-contained
///         single-file publish is runtime-identifier specific; trimming and AOT are left off
///         because that stack's compatibility with them is unverified.
///     </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    ///     Gets the tool version from the assembly informational version attribute.
    /// </summary>
    /// <remarks>Read via reflection on every access; callers that need it repeatedly should cache it.</remarks>
    public static string Version
    {
        get
        {
            var assembly = typeof(Program).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }
    }

    /// <summary>
    ///     Main entry point for the tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>0 when the output layout was produced, 1 when the document could not be read or an argument was bad.</returns>
    /// <exception cref="Exception">Re-thrown after writing to standard error for any unexpected error.</exception>
    public static int Main(string[] args)
    {
        try
        {
            using var context = Context.Create(args);
            Run(context);
            return context.ExitCode;
        }
        catch (ArgumentException ex)
        {
            // Expected argument fault: message only, no stack trace
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            // Expected operation fault (for example a log file that cannot be opened)
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            // Unexpected: write to stderr and re-throw so the runtime can record it
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Runs the tool logic for a parsed context.
    /// </summary>
    /// <param name="context">The parsed context.</param>
    /// <remarks>
    ///     Dispatch is priority-ordered and executes only the highest-priority match: version, then
    ///     the banner and help, then the <c>--list-backends</c> auxiliary command, then
    ///     <c>--validate</c>, then extraction.
    /// </remarks>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Priority 1: version
        if (context.Version)
        {
            context.WriteLine(Version);
            return;
        }

        PrintBanner(context);

        // Priority 2: help
        if (context.Help)
        {
            PrintHelp(context);
            return;
        }

        // Priority 3: list registered backends
        if (context.ListBackends)
        {
            RunListBackends(context);
            return;
        }

        // Priority 4: self-validation
        if (context.Validate)
        {
            Validation.Run(context, BuildEngine());
            return;
        }

        // Priority 5: extraction
        RunExtraction(context);
    }

    /// <summary>Prints the application banner.</summary>
    /// <param name="context">The context for output.</param>
    private static void PrintBanner(Context context)
    {
        context.WriteLine($"DocDown Tool version {Version}");
        context.WriteLine("Copyright (c) DEMA Consulting");
        context.WriteLine("");
    }

    /// <summary>Prints the usage and option list.</summary>
    /// <param name="context">The context for output.</param>
    private static void PrintHelp(Context context)
    {
        context.WriteLine("Usage: docdown [options]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  -v, --version              Display version information");
        context.WriteLine("  -?, -h, --help             Display this help message");
        context.WriteLine("  --silent                   Suppress console output");
        context.WriteLine("  --validate                 Run self-validation");
        context.WriteLine("  --results <file>           Write validation results to file (.trx or .xml)");
        context.WriteLine("  --depth <#>                Set heading depth for markdown output (default: 1)");
        context.WriteLine("  --log <file>               Write output to a log file");
        context.WriteLine("  --list-backends            List registered backends with availability and reason");
        context.WriteLine("");
        context.WriteLine("Extraction options:");
        context.WriteLine("  --input <file>             Document to extract (required for extraction)");
        context.WriteLine("  --scratch <dir>            Scratch folder to write the extraction into (required)");
        context.WriteLine("  --pages                    Request rendered page images");
        context.WriteLine("  --no-pages                 Do not render page images (default)");
        context.WriteLine("  --page-range <a-b>         Restrict extraction to a page range");
        context.WriteLine("  --dpi <#>                  Page render DPI (range 36-1200)");
        context.WriteLine("  --no-images                Do not extract embedded images");
        context.WriteLine("  --max-image-dim <#>        Skip images exceeding this pixel dimension");
        context.WriteLine("  --max-image-bytes <#>      Skip images exceeding this byte size");
        context.WriteLine("  --overwrite <clean|overwrite>  Scratch folder policy (default: clean)");
    }

    /// <summary>Lists the registered backends with their formats and availability.</summary>
    /// <param name="context">The context for output.</param>
    private static void RunListBackends(Context context)
    {
        var engine = BuildEngine();
        context.WriteLine("Registered backends:");
        foreach (var backend in engine.GetBackends())
        {
            var descriptor = backend.Descriptor;
            var availability = backend.Availability;
            var formats = string.Join(", ", descriptor.SupportedFormats.Select(f => f.Id));
            var status = availability.IsAvailable ? "available" : $"unavailable ({availability.UnavailableReason})";
            context.WriteLine($"  {descriptor.Id} - {descriptor.DisplayName}");
            context.WriteLine($"    formats: {formats}");
            context.WriteLine($"    status: {status}");
        }
    }

    /// <summary>Runs an extraction and reports its outcome.</summary>
    /// <param name="context">The context carrying the input, scratch folder, and option flags.</param>
    /// <exception cref="ArgumentException">Thrown when the required <c>--input</c> or <c>--scratch</c> is missing.</exception>
    private static void RunExtraction(Context context)
    {
        if (string.IsNullOrEmpty(context.Input))
        {
            throw new ArgumentException("--input is required for extraction");
        }

        if (string.IsNullOrEmpty(context.Scratch))
        {
            throw new ArgumentException("--scratch is required for extraction");
        }

        var engine = BuildEngine();
        var options = context.BuildExtractionOptions();

        // The engine returns adverse conditions as data; only argument faults and cancellation throw
        var result = engine.ExtractAsync(context.Input, context.Scratch, options).AsTask().GetAwaiter().GetResult();

        if (result.Outcome == ExtractionOutcome.Unreadable)
        {
            // Render the prose failure verbatim (headline plus explanation)
            var failure = result.Failure;
            context.WriteError(failure?.Explanation ?? "Extraction produced no output.");
            return;
        }

        // Output was produced: report any notes and print the absolute path to summary.txt
        context.WriteLine("Extraction produced the output layout.");
        if (result.Notes.Count > 0)
        {
            context.WriteLine($"{result.Notes.Count} note(s) recorded; see summary.txt for detail.");
        }

        context.WriteLine(Path.GetFullPath(result.SummaryPath));
    }

    /// <summary>Builds the engine with every backend package registered explicitly.</summary>
    /// <returns>A configured engine.</returns>
    /// <remarks>
    ///     Registration is explicit and reflection-free, which keeps single-file publish viable. The
    ///     managed PDF, Word, Visio, PowerPoint, and Excel backends serve every extraction; the
    ///     optional PDF rendering backend and the Visio and PowerPoint COM backends are chosen only
    ///     when page rendering is requested and their environment (a native stack, or Microsoft Visio
    ///     or PowerPoint) is available, degrading through the engine's own path otherwise.
    /// </remarks>
    private static DocDownEngine BuildEngine() =>
        new DocDownBuilder().AddPdf().AddPdfRendering().AddWord().AddVisio().AddPowerPoint().AddExcel().Build();
}
