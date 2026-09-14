using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;

namespace DocDown.PowerPoint.Com;

/// <summary>
///     The real PowerPoint COM automation adapter: the single unit that talks to Microsoft
///     PowerPoint over late-bound IDispatch. It activates a PowerPoint session, opens the deck
///     read-only under a watchdog, exports every slide to a PNG at the requested resolution, and
///     tears the session down deterministically on every path.
/// </summary>
/// <remarks>
///     <para>
///         This class is the whole untestable COM boundary and is deliberately mechanical: it holds
///         no extraction or note policy. Which slides render, how failures become notes, and how the
///         content is delegated all live in the cross-platform-tested
///         <see cref="PowerPointComExtractor"/>; the low-level IDispatch plumbing, single-instance
///         activation, watchdog, and forced process termination live in
///         <see cref="PowerPointComDispatch"/>. Its correctness in a deployed environment is proven
///         by the release-time self-test cases, not by CI.
///     </para>
///     <para>
///         A whole session — activate, open, export every slide, close, quit, release — runs inside
///         one watchdog-guarded call on a dedicated single-threaded-apartment thread, so no COM
///         object ever outlives the timeout window and any modal-dialog hang is bounded by
///         force-terminating the owned PowerPoint process, leaving no orphan. Windows-only, for CA1416.
///     </para>
/// </remarks>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage(Justification = "Interop seam: every statement runs inside Microsoft PowerPoint over COM, which no CI runner has. The Code Coverage Policy reserves this attribute for these seams so reported coverage stays an honest measure of what the suite verifies; it is exercised functionally by the COM render self-test where Microsoft PowerPoint is installed.")]
internal sealed class PowerPointAutomation : IPowerPointAutomation
{
    /// <summary>The maximum time a single open-and-render session may take before it is treated as a hang.</summary>
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(5);

    /// <summary>The <c>ppAlertsNone</c> constant that suppresses every PowerPoint alert dialog.</summary>
    private const int PpAlertsNone = 1;

    /// <summary>The <c>msoAutomationSecurityForceDisable</c> constant that disables macros and add-ins on open.</summary>
    private const int MsoAutomationSecurityForceDisable = 3;

    /// <summary>The <c>msoTrue</c> tri-state value.</summary>
    private const int MsoTrue = -1;

    /// <summary>The <c>msoFalse</c> tri-state value.</summary>
    private const int MsoFalse = 0;

    /// <summary>Points per inch, used to convert a slide's point dimensions to pixels at a chosen DPI.</summary>
    private const double PointsPerInch = 72.0;

    /// <summary>Gets the process id of the PowerPoint host the most recent render owned, or zero when none was isolated.</summary>
    /// <remarks>Exposed so the release-time process-release self-test can assert the started process has exited.</remarks>
    internal int LastOwnedProcessId { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<PowerPointRenderedSlide> Render(string path, int dpi)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = Path.GetFullPath(path);

        IReadOnlyList<PowerPointRenderedSlide> slides = [];
        var ownedProcessId = new int[1];
        try
        {
            PowerPointComDispatch.RunSession(
                () => slides = RunOneShotSession(fullPath, dpi, ownedProcessId), ownedProcessId, RenderTimeout);
        }
        finally
        {
            LastOwnedProcessId = ownedProcessId[0];
        }

        return slides;
    }

    /// <inheritdoc />
    /// <remarks>Every PowerPoint object is created and released inside <see cref="Render"/>, so disposal has nothing left to release.</remarks>
    public void Dispose()
    {
        // The session is fully torn down before Render returns; nothing is retained to release here.
    }

    /// <summary>Activates PowerPoint, opens the deck read-only, exports every slide, and tears the session down.</summary>
    /// <param name="path">The absolute path to open.</param>
    /// <param name="dpi">The target resolution in dots per inch.</param>
    /// <param name="ownedProcessId">A one-element holder written with the owned process id so the watchdog can terminate it.</param>
    /// <returns>The rendered slides, in presentation order.</returns>
    private static IReadOnlyList<PowerPointRenderedSlide> RunOneShotSession(string path, int dpi, int[] ownedProcessId)
    {
        object? application = null;
        object? presentations = null;
        object? presentation = null;
        try
        {
            var (app, processId) = PowerPointComDispatch.Activate();
            application = app;
            ownedProcessId[0] = processId;

            PowerPointComDispatch.Set(application, "DisplayAlerts", PpAlertsNone);
            PowerPointComDispatch.Set(application, "AutomationSecurity", MsoAutomationSecurityForceDisable);

            presentations = PowerPointComDispatch.Get(application, "Presentations")!;
            presentation = OpenReadOnly(presentations, path);

            var slides = ExportSlides(presentation, dpi);

            PowerPointComDispatch.Invoke(presentation, "Close");
            return slides;
        }
        finally
        {
            QuitAndRelease(application, presentations, presentation);
        }
    }

    /// <summary>Opens the deck read-only, without a window, and without adding it to the recent-file list.</summary>
    /// <param name="presentations">The PowerPoint Presentations collection.</param>
    /// <param name="path">The absolute path to open.</param>
    /// <returns>The opened presentation object.</returns>
    private static object OpenReadOnly(object presentations, string path)
    {
        object[] args = [path, MsoTrue, MsoFalse, MsoFalse];
        string[] names = ["FileName", "ReadOnly", "Untitled", "WithWindow"];
        return PowerPointComDispatch.Invoke(presentations, "Open", args)
            ?? OpenNamedFallback(presentations, args, names);
    }

    /// <summary>Opens the deck using named arguments when the positional open returns nothing.</summary>
    /// <param name="presentations">The PowerPoint Presentations collection.</param>
    /// <param name="args">The positional argument values.</param>
    /// <param name="names">The parameter names.</param>
    /// <returns>The opened presentation object.</returns>
    private static object OpenNamedFallback(object presentations, object[] args, string[] names) =>
        presentations.GetType().InvokeMember(
            "Open", System.Reflection.BindingFlags.InvokeMethod, null, presentations, args, null,
            CultureInfo.InvariantCulture, names)
        ?? throw new OpenXml.PowerPointExtractionException(
            "Microsoft PowerPoint returned no presentation when opening the deck.");

    /// <summary>Exports every slide to a PNG at the requested resolution, isolating each slide's faults.</summary>
    /// <param name="presentation">The opened presentation.</param>
    /// <param name="dpi">The target resolution in dots per inch.</param>
    /// <returns>One entry per slide, each carrying PNG bytes or a per-slide failure reason.</returns>
    private static IReadOnlyList<PowerPointRenderedSlide> ExportSlides(object presentation, int dpi)
    {
        var pageSetup = PowerPointComDispatch.Get(presentation, "PageSetup")!;
        var widthPx = ToPixels(PowerPointComDispatch.AsDouble(PowerPointComDispatch.Get(pageSetup, "SlideWidth")), dpi);
        var heightPx = ToPixels(PowerPointComDispatch.AsDouble(PowerPointComDispatch.Get(pageSetup, "SlideHeight")), dpi);
        PowerPointComDispatch.Release(pageSetup);

        var slidesCollection = PowerPointComDispatch.Get(presentation, "Slides")!;
        var count = PowerPointComDispatch.Count(slidesCollection);
        var results = new List<PowerPointRenderedSlide>(count);
        var scratch = Path.Combine(Path.GetTempPath(), "docdown-pptx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            for (var number = 1; number <= count; number++)
            {
                results.Add(ExportSlide(slidesCollection, number, widthPx, heightPx, scratch));
            }
        }
        finally
        {
            PowerPointComDispatch.Release(slidesCollection);
            TryDeleteDirectory(scratch);
        }

        return results;
    }

    /// <summary>Exports one slide to a PNG, returning either its bytes or a failure reason.</summary>
    /// <param name="slidesCollection">The Slides collection.</param>
    /// <param name="number">The 1-based slide number.</param>
    /// <param name="widthPx">The export width in pixels.</param>
    /// <param name="heightPx">The export height in pixels.</param>
    /// <param name="scratch">The scratch folder to export into.</param>
    /// <returns>The rendered slide record.</returns>
    private static PowerPointRenderedSlide ExportSlide(
        object slidesCollection, int number, int widthPx, int heightPx, string scratch)
    {
        object? slide = null;
        try
        {
            slide = PowerPointComDispatch.Item(slidesCollection, number);
            var pngPath = Path.Combine(scratch, "slide" + number.ToString(CultureInfo.InvariantCulture) + ".png");
            PowerPointComDispatch.Invoke(slide, "Export", pngPath, "PNG", widthPx, heightPx);
            var bytes = File.ReadAllBytes(pngPath);
            return new PowerPointRenderedSlide(number, bytes, null);
        }
#pragma warning disable CA1031 // Per-slide fault isolation: any export fault becomes note data, never an exception
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new PowerPointRenderedSlide(number, null, exception.Message);
        }
        finally
        {
            PowerPointComDispatch.Release(slide);
        }
    }

    /// <summary>Converts a dimension in points to pixels at the given resolution, clamped to at least one pixel.</summary>
    /// <param name="points">The dimension in points.</param>
    /// <param name="dpi">The resolution in dots per inch.</param>
    /// <returns>The dimension in pixels.</returns>
    private static int ToPixels(double points, int dpi)
    {
        var pixels = (int)Math.Round(points / PointsPerInch * dpi, MidpointRounding.AwayFromZero);
        return pixels < 1 ? 1 : pixels;
    }

    /// <summary>Quits the application and releases every runtime callable wrapper in reverse acquisition order.</summary>
    /// <param name="application">The application object, or null.</param>
    /// <param name="presentations">The Presentations collection, or null.</param>
    /// <param name="presentation">The opened presentation, or null.</param>
    private static void QuitAndRelease(object? application, object? presentations, object? presentation)
    {
        try
        {
            if (application is not null)
            {
                PowerPointComDispatch.Invoke(application, "Quit");
            }
        }
#pragma warning disable CA1031 // Teardown records no fault: the process is released regardless of a late Quit failure
        catch (Exception)
#pragma warning restore CA1031
        {
            // The presentation may already be closing; releasing the wrappers below still frees the process
        }

        PowerPointComDispatch.Release(presentation);
        PowerPointComDispatch.Release(presentations);
        PowerPointComDispatch.Release(application);
        PowerPointComDispatch.CollectComObjects();
    }

    /// <summary>Deletes a scratch directory, ignoring any fault.</summary>
    /// <param name="path">The directory to delete.</param>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
#pragma warning disable CA1031 // Best-effort cleanup of a temporary folder must never mask the render outcome
        catch (Exception)
#pragma warning restore CA1031
        {
            // A leftover temp folder is harmless; the OS reclaims it
        }
    }
}
