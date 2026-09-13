using DocDown.Core;
using DocDown.PowerPoint.Com;

namespace DemaConsulting.DocDown.PowerPoint.Tests.Com;

/// <summary>
///     Unit tests for <see cref="PowerPointComAvailability"/>, verifying the cheap, non-throwing,
///     never-launches-PowerPoint probe on whatever platform the test runs.
/// </summary>
public class PowerPointComAvailabilityTests
{
    /// <summary>
    ///     Proves the probe never throws and never instructs an installation, regardless of platform.
    /// </summary>
    [Fact]
    public void PowerPointComAvailability_Probe_NeverThrowsAndNeverInstructsInstallation()
    {
        var result = PowerPointComAvailability.Probe(ExtractorCapabilities.Text | ExtractorCapabilities.RenderedPages);

        Assert.DoesNotContain("install", result.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the backend is unavailable off Windows, with a reason naming the operating system.
    /// </summary>
    [Fact]
    public void PowerPointComAvailability_Probe_OffWindows_IsUnavailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = PowerPointComAvailability.Probe(ExtractorCapabilities.RenderedPages);

        Assert.False(result.IsAvailable);
        Assert.Contains("Windows", result.UnavailableReason!, StringComparison.Ordinal);
    }
}
