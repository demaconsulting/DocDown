using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;

namespace DocDown.Visio.Com;

/// <summary>
///     The real Visio COM automation adapter: the single unit that talks to Microsoft Visio over
///     late-bound IDispatch. It activates an invisible Visio session, opens the drawing read-only
///     under a watchdog, exports every foreground page to a PNG, and tears the session down
///     deterministically on every path.
/// </summary>
/// <remarks>
///     <para>
///         This class is the whole untestable COM boundary and is deliberately mechanical: it holds
///         no extraction policy. Which pages render, how failures become notes, and how the
///         content and topology are delegated all live in the cross-platform-tested
///         <see cref="VisioComExtractor"/>; the low-level IDispatch plumbing, single-instance
///         activation, watchdog, and forced process termination live in <see cref="VisioComDispatch"/>.
///         Its correctness in a deployed environment is proven by the release-time self-test cases,
///         not by CI.
///     </para>
///     <para>
///         It activates the <c>Visio.InvisibleApp</c> ProgID (no window), opens with
///         <c>Documents.OpenEx(path, 386)</c> for read-only (the value <c>4</c> is
///         <c>visOpenDocked</c>, not read-only), drives resolution through the
///         <c>SetRasterExportResolution</c> method (the property access throws), and exports each page
///         with <c>Page.Export</c>, which writes a PNG directly with no PDF hop. The export crops to
///         the drawing extent, so pixel dimensions are not derived from page size and are simply read
///         back from the written image. A whole session runs inside one watchdog-guarded call on a
///         dedicated single-threaded-apartment thread, so no COM object outlives the timeout window
///         and any modal-dialog hang is bounded by force-terminating the owned Visio process, leaving
///         no orphan. Windows-only, for CA1416.
///     </para>
/// </remarks>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage(Justification = "Interop seam: every statement runs inside Microsoft Visio over COM, which no CI runner has. The Code Coverage Policy reserves this attribute for these seams so reported coverage stays an honest measure of what the suite verifies; it is exercised functionally by the COM render self-test where Microsoft Visio is installed.")]
internal sealed class VisioAutomation : IVisioAutomation
{
    /// <summary>The maximum time a single open-and-render session may take before it is treated as a hang.</summary>
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromMinutes(5);

    /// <summary>The <c>OpenEx</c> flags that open a document read-only without showing it.</summary>
    /// <remarks><c>386</c> is the read-only combination; the value <c>4</c> is <c>visOpenDocked</c>, not read-only.</remarks>
    private const int VisOpenReadOnlyFlags = 386;

    /// <summary>The <c>visRasterUseCustomResolution</c> constant for the export-resolution method.</summary>
    private const int VisRasterUseCustomResolution = 1;

    /// <summary>The <c>visRasterPixelsPerInch</c> units constant for the export-resolution method.</summary>
    private const int VisRasterPixelsPerInch = 2;

    /// <summary>The <c>IDOK</c> alert response that auto-dismisses any dialog Visio would otherwise show.</summary>
    private const int AlertResponseOk = 1;

    /// <summary>Gets the process id of the Visio host the most recent render owned, or zero when none was isolated.</summary>
    /// <remarks>Exposed so the release-time process-release self-test can assert the started process has exited.</remarks>
    internal int LastOwnedProcessId { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<VisioRenderedPage> Render(string path, int dpi)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = Path.GetFullPath(path);

        IReadOnlyList<VisioRenderedPage> pages = [];
        var ownedProcessId = new int[1];
        try
        {
            VisioComDispatch.RunSession(
                () => pages = RunOneShotSession(fullPath, dpi, ownedProcessId), ownedProcessId, RenderTimeout);
        }
        finally
        {
            LastOwnedProcessId = ownedProcessId[0];
        }

        return pages;
    }

    /// <inheritdoc />
    /// <remarks>Every Visio object is created and released inside <see cref="Render"/>, so disposal has nothing left to release.</remarks>
    public void Dispose()
    {
        // The session is fully torn down before Render returns; nothing is retained to release here.
    }

    /// <summary>Activates Visio, opens the drawing read-only, exports every foreground page, and tears the session down.</summary>
    /// <param name="path">The absolute path to open.</param>
    /// <param name="dpi">The target resolution in dots per inch.</param>
    /// <param name="ownedProcessId">A one-element holder written with the owned process id so the watchdog can terminate it.</param>
    /// <returns>The rendered pages, in document order.</returns>
    private static IReadOnlyList<VisioRenderedPage> RunOneShotSession(string path, int dpi, int[] ownedProcessId)
    {
        object? application = null;
        object? documents = null;
        object? document = null;
        try
        {
            var (app, processId) = VisioComDispatch.Activate();
            application = app;
            ownedProcessId[0] = processId;

            TrySuppressAlerts(application);

            documents = VisioComDispatch.Get(application, "Documents")!;
            document = VisioComDispatch.Invoke(documents, "OpenEx", path, VisOpenReadOnlyFlags)
                ?? throw new OpenXml.VisioExtractionException(
                    "Microsoft Visio returned no document when opening the drawing.");

            TrySetResolution(document, dpi);
            var pages = ExportPages(document);

            VisioComDispatch.Invoke(document, "Close");
            return pages;
        }
        finally
        {
            QuitAndRelease(application, documents, document);
        }
    }

    /// <summary>Configures Visio to auto-dismiss any dialog rather than block on it, ignoring a fault.</summary>
    /// <param name="application">The Visio application object.</param>
    private static void TrySuppressAlerts(object application)
    {
        try
        {
            VisioComDispatch.Set(application, "AlertResponse", AlertResponseOk);
        }
#pragma warning disable CA1031 // Alert suppression is best-effort; the watchdog is the real backstop against a hang
        catch (Exception)
#pragma warning restore CA1031
        {
            // Some Visio builds do not expose AlertResponse on the invisible app; the watchdog still bounds any hang
        }
    }

    /// <summary>Sets the raster export resolution through the method (the property access throws), ignoring a fault.</summary>
    /// <param name="document">The opened document.</param>
    /// <param name="dpi">The target resolution in dots per inch.</param>
    private static void TrySetResolution(object document, int dpi)
    {
        try
        {
            VisioComDispatch.Invoke(
                document, "SetRasterExportResolution",
                VisRasterUseCustomResolution, (double)dpi, (double)dpi, VisRasterPixelsPerInch);
        }
#pragma warning disable CA1031 // Resolution is a quality hint; the export still succeeds at the default resolution if this fails
        catch (Exception)
#pragma warning restore CA1031
        {
            // The export crops to the drawing extent regardless, so a resolution failure is non-fatal
        }
    }

    /// <summary>Exports every foreground page to a PNG, isolating each page's faults.</summary>
    /// <param name="document">The opened document.</param>
    /// <returns>One entry per foreground page, each carrying PNG bytes or a per-page failure reason.</returns>
    private static IReadOnlyList<VisioRenderedPage> ExportPages(object document)
    {
        var pagesCollection = VisioComDispatch.Get(document, "Pages")!;
        var count = VisioComDispatch.Count(pagesCollection);
        var results = new List<VisioRenderedPage>();
        var scratch = Path.Combine(Path.GetTempPath(), "docdown-vsdx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var pageNumber = 0;
            for (var index = 1; index <= count; index++)
            {
                var page = VisioComDispatch.Item(pagesCollection, index);
                try
                {
                    if (VisioComDispatch.AsBool(VisioComDispatch.Get(page, "Background")))
                    {
                        continue;
                    }

                    pageNumber++;
                    results.Add(ExportPage(page, pageNumber, scratch));
                }
                finally
                {
                    VisioComDispatch.Release(page);
                }
            }
        }
        finally
        {
            VisioComDispatch.Release(pagesCollection);
            TryDeleteDirectory(scratch);
        }

        return results;
    }

    /// <summary>Exports one page to a PNG, returning either its bytes or a failure reason.</summary>
    /// <param name="page">The page object.</param>
    /// <param name="pageNumber">The 1-based foreground-page number.</param>
    /// <param name="scratch">The scratch folder to export into.</param>
    /// <returns>The rendered page record.</returns>
    private static VisioRenderedPage ExportPage(object page, int pageNumber, string scratch)
    {
        try
        {
            var pngPath = Path.Combine(scratch, "page" + pageNumber.ToString(CultureInfo.InvariantCulture) + ".png");
            VisioComDispatch.Invoke(page, "Export", pngPath);
            var bytes = File.ReadAllBytes(pngPath);
            return new VisioRenderedPage(pageNumber, bytes, null);
        }
#pragma warning disable CA1031 // Per-page fault isolation: any export fault becomes a note-bearing result, never an exception
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new VisioRenderedPage(pageNumber, null, exception.Message);
        }
    }

    /// <summary>Quits the application and releases every runtime callable wrapper in reverse acquisition order.</summary>
    /// <param name="application">The application object, or null.</param>
    /// <param name="documents">The Documents collection, or null.</param>
    /// <param name="document">The opened document, or null.</param>
    private static void QuitAndRelease(object? application, object? documents, object? document)
    {
        try
        {
            if (application is not null)
            {
                VisioComDispatch.Invoke(application, "Quit");
            }
        }
#pragma warning disable CA1031 // Teardown records no fault: the process is released regardless of a late Quit failure
        catch (Exception)
#pragma warning restore CA1031
        {
            // The document may already be closing; releasing the wrappers below still frees the process
        }

        VisioComDispatch.Release(document);
        VisioComDispatch.Release(documents);
        VisioComDispatch.Release(application);
        VisioComDispatch.CollectComObjects();
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
