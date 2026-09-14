using System.Reflection;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="SelfTestProbe" />.
/// </summary>
/// <remarks>
///     The probe loader is how every backend's self-test obtains its embedded document, so a silent
///     failure here would turn a real check into a misleading one. These tests use this test
///     assembly's own manifest rather than a backend's, so they assert the loader's behavior without
///     depending on which documents a particular package happens to embed.
/// </remarks>
public class SelfTestProbeTests
{
    /// <summary>
    ///     Proves a missing resource fails loudly, naming what was sought and what is available.
    /// </summary>
    /// <remarks>
    ///     A backend packaged without its probe cannot run its self-test at all. Reporting that as a
    ///     clear exception rather than an empty array is what stops it being mistaken for a document
    ///     that extracted to nothing.
    /// </remarks>
    [Fact]
    public void Load_MissingResource_ThrowsNamingTheResource()
    {
        var assembly = typeof(SelfTestProbeTests).Assembly;

        var exception = Assert.Throws<InvalidOperationException>(
            () => SelfTestProbe.Load(assembly, "DocDown.NoSuchProbe.bin"));

        Assert.Contains("DocDown.NoSuchProbe.bin", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves malformed arguments are rejected at the call site.
    /// </summary>
    [Fact]
    public void Load_MalformedArguments_Throw()
    {
        var assembly = typeof(SelfTestProbeTests).Assembly;

        Assert.Throws<ArgumentNullException>(() => SelfTestProbe.Load(null!, "probe"));
        Assert.Throws<ArgumentException>(() => SelfTestProbe.Load(assembly, string.Empty));
    }
}
