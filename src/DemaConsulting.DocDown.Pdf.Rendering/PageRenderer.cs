using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using PDFtoImage;

namespace DocDown.Pdf.Rendering;

/// <summary>
///     The single native-interop seam of this package: it rasterizes one PDF page to a PNG through
///     PDFtoImage, whose native stack does the raster and the PNG encode, and answers a cheap,
///     non-throwing question about whether the native stack can load at all in this environment.
/// </summary>
/// <remarks>
///     <para>
///         This is the only type in DocDown that touches a native binary, and it is deliberately
///         thin: every PDFtoImage call lives here so the rest of the package — and
///         the rest of DocDown — stays fully managed and can be reasoned about without the native
///         stack in view. DocDown calls no native package directly; PDFium and SkiaSharp arrive
///         transitively underneath PDFtoImage and no DocDown type names either of them.
///     </para>
///     <para>
///         <strong>PDFium is not thread-safe.</strong> PDFtoImage documents that it serializes every
///         call into PDFium behind a lock, so only one document can be rasterized at a time in a
///         process. This seam mirrors that with its own process-wide <see cref="RenderGate"/>: two
///         concurrent extractions that both render pages take turns rather than corrupting the
///         native renderer's shared state. The lock is the price of using a native library that was
///         never designed for concurrency, and it is paid here rather than left as a latent hazard.
///     </para>
///     <para>
///         The availability probe loads the PDFium native for the current runtime identifier through
///         the PDFtoImage assembly's own native-resolution path (which honors the
///         <c>runtimes/&lt;rid&gt;/native</c> assets in the package graph and the <c>deps.json</c>
///         mapping). It returns a reason rather than throwing when the binary is missing or the
///         wrong bitness, because the extractor's <c>ProbeAvailability</c> must be cheap and must not
///         throw. The result is cached for the process because native availability cannot change
///         while the process runs. Instances hold no per-render state and are safe to reuse.
///     </para>
/// </remarks>
internal static class PageRenderer
{
    /// <summary>
    ///     The process-wide gate that serializes every native rasterization call.
    /// </summary>
    /// <remarks>
    ///     Static because PDFium's thread-unsafety is a property of the process-global native
    ///     renderer, not of any one <see cref="PageRenderer"/> instance; a per-instance lock would
    ///     let two instances race into PDFium at once. Mirrors PDFtoImage's own internal
    ///     serialization so concurrent extractions rasterize one at a time rather than corrupting
    ///     shared native state.
    /// </remarks>
    private static readonly object RenderGate = new();

    /// <summary>
    ///     The gate guarding the one-time native-availability probe.
    /// </summary>
    /// <remarks>Separate from <see cref="RenderGate"/> so probing never blocks behind an in-flight render.</remarks>
    private static readonly object ProbeGate = new();

    /// <summary>Whether the native-availability probe has run yet in this process.</summary>
    /// <remarks>Guarded by <see cref="ProbeGate"/>; the probe runs at most once per process.</remarks>
    private static bool _probed;

    /// <summary>The cached native-availability result, valid once <see cref="_probed"/> is set.</summary>
    /// <remarks>Cached because a native binary that is present (or absent) cannot change while the process runs.</remarks>
    private static NativeProbeResult _probeResult = NativeProbeResult.Unavailable("not probed");

    /// <summary>
    ///     Probes, cheaply and without throwing, whether the PDFium native stack can load here.
    /// </summary>
    /// <returns>
    ///     An available result when the native binary loads for the current runtime identifier, or
    ///     an unavailable result carrying a human-readable reason when it does not.
    /// </returns>
    /// <remarks>
    ///     Loads the native once and caches the outcome, so an engine that never renders pays the
    ///     load cost at most once and a plain (non-rendering) extraction pays it not at all. Does no
    ///     rasterization — a functional check belongs to the self-test and to a real extraction, not
    ///     to a hot probe.
    /// </remarks>
    public static NativeProbeResult ProbeAvailability()
    {
        lock (ProbeGate)
        {
            if (_probed)
            {
                return _probeResult;
            }

            _probeResult = DetectNativeAvailability();
            _probed = true;
            return _probeResult;
        }
    }

    /// <summary>
    ///     Rasterizes one page of a PDF to PNG bytes at a given DPI.
    /// </summary>
    /// <param name="pdf">The full bytes of the source PDF.</param>
    /// <param name="pageIndexZeroBased">The zero-based index of the page to render.</param>
    /// <param name="dpi">The dots-per-inch to render at; higher values trade file size for fidelity.</param>
    /// <returns>The PNG-encoded bytes of the rendered page.</returns>
    /// <remarks>
    ///     Runs the whole native call chain under <see cref="RenderGate"/> because PDFium is not
    ///     thread-safe. Any native or memory fault surfaces here as a thrown exception; the caller
    ///     isolates it per page and converts it into a plain note, so a single unrenderable page
    ///     never aborts a whole extraction and never reaches the library's caller as an exception.
    /// </remarks>
    public static byte[] Render(byte[] pdf, int pageIndexZeroBased, int dpi)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        // Serialize: PDFium is not thread-safe, so exactly one rasterization runs at a time process-wide
        lock (RenderGate)
        {
            return RenderCore(pdf, pageIndexZeroBased, dpi);
        }
    }

    /// <summary>
    ///     Reads the number of pages in a PDF.
    /// </summary>
    /// <param name="pdf">The full bytes of the source PDF.</param>
    /// <returns>The page count reported by the rasterizer.</returns>
    /// <remarks>
    ///     Kept here rather than in the caller so this type stays the package's single native-interop
    ///     seam: the count comes from the same component that will rasterize the pages, and it runs
    ///     under <see cref="RenderGate"/> like every other native call, because the rasterizer is not
    ///     thread-safe. Any native or memory fault surfaces here as a thrown exception; the caller
    ///     isolates it and reports a note rather than failing the extraction.
    /// </remarks>
    public static int GetPageCount(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        // Serialize: the native stack is not thread-safe, so exactly one call runs at a time process-wide
        lock (RenderGate)
        {
#pragma warning disable CA1416 // PDFtoImage supports every platform DocDown targets (Windows, Linux, macOS)
            return Conversion.GetPageCount(pdf);
#pragma warning restore CA1416
        }
    }

    /// <summary>
    ///     Performs the native rasterization and PNG encode for one page.
    /// </summary>
    /// <param name="pdf">The full bytes of the source PDF.</param>
    /// <param name="pageIndexZeroBased">The zero-based index of the page to render.</param>
    /// <param name="dpi">The dots-per-inch to render at.</param>
    /// <returns>The PNG-encoded bytes of the rendered page.</returns>
    /// <remarks>
    ///     The <c>CA1416</c> suppression is deliberate: PDFtoImage annotates its API as supported on
    ///     every platform DocDown targets (Windows, Linux, and macOS, plus mobile and browser), so
    ///     the platform-guard the analyzer wants would guard against nothing. Callers hold
    ///     <see cref="RenderGate"/>, so this method is never entered concurrently.
    /// </remarks>
    private static byte[] RenderCore(byte[] pdf, int pageIndexZeroBased, int dpi)
    {
#pragma warning disable CA1416 // PDFtoImage supports every platform DocDown targets (Windows, Linux, macOS)
        var options = new RenderOptions(Dpi: dpi);
        using var buffer = new MemoryStream();
        Conversion.SavePng(buffer, pdf, page: pageIndexZeroBased, options: options);
        return buffer.ToArray();
#pragma warning restore CA1416
    }

    /// <summary>
    ///     Attempts to load the PDFium native for the current runtime identifier, without throwing.
    /// </summary>
    /// <returns>
    ///     An available result when the native binary loads, or an unavailable result naming the
    ///     runtime identifier and the reason it could not load.
    /// </returns>
    /// <remarks>
    ///     Resolves the native through the PDFtoImage assembly so the package graph's
    ///     <c>runtimes/&lt;rid&gt;/native</c> asset is honored. Excluded from coverage because it is a
    ///     thin native-load seam whose only branch that runs in a supported environment is the
    ///     success path; the failure path exists solely to convert an environment defect into an
    ///     honest reason. The loaded handle is intentionally not freed: keeping the native resident
    ///     avoids reloading it on the first real render, and the probe runs at most once per process.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "Thin native-load seam; the success path is exercised functionally by the render tests, and the failure path only runs where the native binary is genuinely absent.")]
    private static NativeProbeResult DetectNativeAvailability()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        try
        {
            if (NativeLibrary.TryLoad("pdfium", typeof(Conversion).Assembly, null, out _))
            {
                return NativeProbeResult.Available;
            }

            return NativeProbeResult.Unavailable(
                $"the PDFium native binary for runtime '{rid}' could not be loaded");
        }
#pragma warning disable CA1031 // The probe must convert any native-load fault into an honest reason rather than throwing
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return NativeProbeResult.Unavailable(
                $"the PDFium native binary for runtime '{rid}' could not be loaded ({exception.Message})");
        }
    }
}

/// <summary>
///     The outcome of the cheap native-availability probe: whether the native rasterization stack
///     can load here, and if not, why.
/// </summary>
/// <param name="IsAvailable">
///     <see langword="true"/> when the PDFium native loaded for the current runtime identifier;
///     otherwise <see langword="false"/>.
/// </param>
/// <param name="Reason">
///     A human-readable reason the native stack is unavailable, or <see langword="null"/> when it
///     is available.
/// </param>
/// <remarks>
///     A tiny value carried from <see cref="PageRenderer.ProbeAvailability"/> up to the extractor,
///     which turns it into a Core <c>ExtractorAvailability</c>. Kept internal because it is an
///     implementation detail of this package, not part of any consumer-facing API. Immutable and
///     thread-safe.
/// </remarks>
internal sealed record NativeProbeResult(bool IsAvailable, string? Reason)
{
    /// <summary>A shared available result, since the available case carries no reason.</summary>
    /// <remarks>Reused so the hot path allocates nothing when the native stack is present.</remarks>
    public static readonly NativeProbeResult Available = new(true, null);

    /// <summary>
    ///     Creates an unavailable result with a reason.
    /// </summary>
    /// <param name="reason">The human-readable reason the native stack could not load. Must not be null or empty.</param>
    /// <returns>An unavailable <see cref="NativeProbeResult"/> carrying the reason.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or empty.</exception>
    /// <remarks>Requires a non-empty reason so an unavailable probe is never reported without an explanation.</remarks>
    public static NativeProbeResult Unavailable(string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        return new NativeProbeResult(false, reason);
    }
}
