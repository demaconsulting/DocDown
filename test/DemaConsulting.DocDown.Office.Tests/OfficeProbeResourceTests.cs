using DocDown.Core;
using DocDown.Word;

namespace DemaConsulting.DocDown.Office.Tests;

/// <summary>
///     Proves the package actually carries the probe documents its self-tests read.
/// </summary>
/// <remarks>
///     Each backend's self-test extracts an embedded document authored in the application whose
///     format it reads. If a probe were dropped from the package, every one of those self-tests would
///     fail at once and report the environment as broken when only the packaging was. Asserting the
///     resources are present, and are the file types their names claim, catches that at the point the
///     mistake is made.
/// </remarks>
public class OfficeProbeResourceTests
{
    /// <summary>The assembly that carries the probe documents.</summary>
    private static readonly System.Reflection.Assembly OfficeAssembly = typeof(WordDocDownBuilderExtensions).Assembly;

    /// <summary>
    ///     Proves every embedded probe loads and begins with the Zip signature an Open XML package has.
    /// </summary>
    /// <param name="resourceName">The fully qualified resource name to load.</param>
    [Theory]
    [InlineData("DemaConsulting.DocDown.Office.Resources.probe.docx")]
    [InlineData("DemaConsulting.DocDown.Office.Resources.probe.xlsx")]
    [InlineData("DemaConsulting.DocDown.Office.Resources.probe.pptx")]
    [InlineData("DemaConsulting.DocDown.Office.Resources.probe.vsdx")]
    public void Probe_EmbeddedResource_LoadsAsAnOpenPackage(string resourceName)
    {
        var bytes = SelfTestProbe.Load(OfficeAssembly, resourceName);

        Assert.True(bytes.Length > 0, $"'{resourceName}' loaded as an empty document.");

        // Every Open XML format is a Zip container, which starts with the local file header "PK\x03\x04"
        Assert.Equal([0x50, 0x4B, 0x03, 0x04], bytes.Take(4).ToArray());
    }
}
