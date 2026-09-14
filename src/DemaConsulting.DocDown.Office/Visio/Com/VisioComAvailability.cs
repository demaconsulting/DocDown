using DocDown.Core;
using DocDown.Office.Com;

namespace DocDown.Visio.Com;

/// <summary>
///     Probes whether the Microsoft Visio COM automation backend can run in the current environment.
/// </summary>
/// <remarks>
///     The probe itself lives in <see cref="OfficeComAvailability"/>, which PowerPoint and Visio
///     share: their probes differed only in the application name and ProgID. This type names the
///     backend's own entry point and supplies those two values.
/// </remarks>
internal static class VisioComAvailability
{
    /// <summary>
    ///     Probes availability, returning whether Microsoft Visio automation can run and render pages here.
    /// </summary>
    /// <returns>
    ///     An availability result: available with rendered-page support, or unavailable with a
    ///     declarative reason.
    /// </returns>
    /// <remarks>Cheap, side-effect free, and never throws. Pure apart from the registry read on Windows.</remarks>
    public static ExtractorAvailability Probe() =>
        OfficeComAvailability.Probe("Microsoft Visio", "Visio.Application", providesRenderedPages: true);
}
