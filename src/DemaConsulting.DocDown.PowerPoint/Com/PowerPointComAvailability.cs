using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DocDown.Core;

namespace DocDown.PowerPoint.Com;

/// <summary>
///     Probes whether the PowerPoint COM automation backend can run in the current environment.
/// </summary>
/// <remarks>
///     <para>
///         The probe is cheap and side-effect free: it checks the operating system first and then, on
///         Windows, whether the <c>PowerPoint.Application</c> ProgID is registered. It opens no deck,
///         launches no application, and never throws — the obligations Core places on availability
///         probing. It is fully testable cross-platform because the non-Windows path is reached
///         wherever CI runs and returns an unavailable result naming the operating system.
///     </para>
///     <para>
///         The unavailable reasons are deliberately declarative and use the word "available", never
///         "install": they state a fact about the environment the reader cannot act on, matching the
///         library-wide convention that forbids instructing an installation.
///     </para>
/// </remarks>
internal static class PowerPointComAvailability
{
    /// <summary>
    ///     Probes availability, returning the declared capabilities when PowerPoint automation can run.
    /// </summary>
    /// <param name="declared">The capabilities the extractor declares.</param>
    /// <returns>An availability result: available with the declared capabilities, or unavailable with a declarative reason.</returns>
    /// <remarks>Cheap, side-effect free, and never throws. Pure apart from the registry read on Windows.</remarks>
    public static ExtractorAvailability Probe(ExtractorCapabilities declared)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ExtractorAvailability.Unavailable(
                "Microsoft PowerPoint COM automation is available only on Windows with Microsoft PowerPoint "
                + $"available; this process is running on {RuntimeInformation.OSDescription}.");
        }

        return IsPowerPointRegistered()
            ? ExtractorAvailability.Available(declared)
            : ExtractorAvailability.Unavailable(
                "Microsoft PowerPoint is not registered on this machine; the PowerPoint COM automation backend "
                + "is available only where Microsoft PowerPoint is present.");
    }

    /// <summary>
    ///     Reports whether the <c>PowerPoint.Application</c> ProgID resolves on this machine.
    /// </summary>
    /// <returns><see langword="true"/> when PowerPoint is registered; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     A registry lookup only — it does not activate PowerPoint. Any fault is treated as "not
    ///     registered" so the probe honors its no-throw obligation. Windows-only.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static bool IsPowerPointRegistered()
    {
        try
        {
            return Type.GetTypeFromProgID("PowerPoint.Application", throwOnError: false) is not null;
        }
#pragma warning disable CA1031 // A probe reports every fault as "unavailable" rather than throwing at its caller
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }
}
