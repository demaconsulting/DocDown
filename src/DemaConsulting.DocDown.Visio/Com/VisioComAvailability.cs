using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DocDown.Core;

namespace DocDown.Visio.Com;

/// <summary>
///     Probes whether the Visio COM automation backend can run in the current environment.
/// </summary>
/// <remarks>
///     <para>
///         The probe is cheap and side-effect free: it checks the operating system first and then, on
///         Windows, whether the <c>Visio.Application</c> ProgID is registered. It opens no drawing,
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
internal static class VisioComAvailability
{
    /// <summary>
    ///     Probes availability, returning whether Visio automation can run and render pages here.
    /// </summary>
    /// <returns>
    ///     An availability result: available with rendered-page support, or unavailable with a
    ///     declarative reason.
    /// </returns>
    /// <remarks>Cheap, side-effect free, and never throws. Pure apart from the registry read on Windows.</remarks>
    public static ExtractorAvailability Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return ExtractorAvailability.Unavailable(
                "Microsoft Visio COM automation is available only on Windows with Microsoft Visio "
                + $"available; this process is running on {RuntimeInformation.OSDescription}.");
        }

        return IsVisioRegistered()
            ? ExtractorAvailability.Available(providesRenderedPages: true)
            : ExtractorAvailability.Unavailable(
                "Microsoft Visio is not registered on this machine; the Visio COM automation backend is "
                + "available only where Microsoft Visio is present.");
    }

    /// <summary>
    ///     Reports whether the <c>Visio.Application</c> ProgID resolves on this machine.
    /// </summary>
    /// <returns><see langword="true"/> when Visio is registered; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     A registry lookup only — it does not activate Visio. Any fault is treated as "not
    ///     registered" so the probe honors its no-throw obligation. Windows-only.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static bool IsVisioRegistered()
    {
        try
        {
            return Type.GetTypeFromProgID("Visio.Application", throwOnError: false) is not null;
        }
#pragma warning disable CA1031 // A probe reports every fault as "unavailable" rather than throwing at its caller
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }
}
