using DocDown.Core;
using DocDown.Visio.Com;

namespace DemaConsulting.DocDown.Visio.Tests.Com;

/// <summary>
///     Unit tests for <see cref="VisioComAvailability"/>, verifying the cheap, non-throwing,
///     never-launches-Visio probe on whatever platform the test runs.
/// </summary>
public class VisioComAvailabilityTests
{
    /// <summary>
    ///     Proves the probe never instructs an installation, regardless of platform.
    /// </summary>
    [Fact]
    public void VisioComAvailability_Probe_NeverInstructsInstallation()
    {
        var result = VisioComAvailability.Probe(ExtractorCapabilities.Text | ExtractorCapabilities.RenderedPages);

        Assert.DoesNotContain("install", result.UnavailableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the backend is unavailable off Windows, with a reason naming the operating system.
    /// </summary>
    [Fact]
    public void VisioComAvailability_Probe_OffWindows_IsUnavailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = VisioComAvailability.Probe(ExtractorCapabilities.RenderedPages);

        Assert.False(result.IsAvailable);
        Assert.Contains("Windows", result.UnavailableReason!, StringComparison.Ordinal);
    }
}
