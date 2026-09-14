using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DocDown.Core;

namespace DocDown.Office.Com;

/// <summary>
///     Probes whether a Microsoft Office COM automation backend can run in the current environment.
/// </summary>
/// <remarks>
///     <para>
///         The probe is cheap and side-effect free: it checks the operating system first and then, on
///         Windows, whether the application's ProgID is registered. It opens no document, launches no
///         application, and never throws — the obligations Core places on availability probing. It is
///         fully testable cross-platform because the non-Windows path is reached wherever CI runs and
///         returns an unavailable result naming the operating system.
///     </para>
///     <para>
///         The unavailable reasons are deliberately declarative and use the word "available", never
///         "install": they state a fact about the environment the reader cannot act on, matching the
///         library-wide convention that forbids instructing an installation.
///     </para>
///     <para>
///         Shared by the PowerPoint and Visio backends. Their probes were identical apart from the
///         application name and ProgID, so those are arguments rather than two copies of the logic.
///     </para>
/// </remarks>
internal static class OfficeComAvailability
{
    /// <summary>
    ///     Probes availability, returning whether the named application's automation can run here.
    /// </summary>
    /// <param name="applicationName">
    ///     The application's display name as it should appear in a reason, for example
    ///     <c>Microsoft Visio</c>. Must not be null or empty.
    /// </param>
    /// <param name="progId">
    ///     The COM ProgID to look up, for example <c>Visio.Application</c>. Must not be null or empty.
    /// </param>
    /// <param name="providesRenderedPages">Whether this backend renders pages when it is available.</param>
    /// <returns>
    ///     An availability result: available, or unavailable with a declarative reason.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="applicationName"/> or <paramref name="progId"/> is null or empty.</exception>
    /// <remarks>Cheap, side-effect free, and never throws for environment reasons. Pure apart from the registry read on Windows.</remarks>
    public static ExtractorAvailability Probe(string applicationName, string progId, bool providesRenderedPages)
    {
        ArgumentException.ThrowIfNullOrEmpty(applicationName);
        ArgumentException.ThrowIfNullOrEmpty(progId);

        if (!OperatingSystem.IsWindows())
        {
            return ExtractorAvailability.Unavailable(
                $"{applicationName} COM automation is available only on Windows with {applicationName} "
                + $"available; this process is running on {RuntimeInformation.OSDescription}.");
        }

        return IsRegistered(progId)
            ? ExtractorAvailability.Available(providesRenderedPages)
            : ExtractorAvailability.Unavailable(
                $"{applicationName} is not registered on this machine; the {applicationName} COM "
                + $"automation backend is available only where {applicationName} is present.");
    }

    /// <summary>
    ///     Reports whether a ProgID resolves on this machine.
    /// </summary>
    /// <param name="progId">The ProgID to look up.</param>
    /// <returns><see langword="true"/> when the ProgID resolves; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     A registry lookup only — it does not activate the application. Any fault is treated as "not
    ///     registered" so the probe honors its no-throw obligation. Windows-only.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static bool IsRegistered(string progId)
    {
        try
        {
            return Type.GetTypeFromProgID(progId, throwOnError: false) is not null;
        }
#pragma warning disable CA1031 // A probe reports every fault as "unavailable" rather than throwing at its caller
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }
}
