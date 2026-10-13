using System.Reflection;
using DemaConsulting.DocDown.Core;

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
            () => SelfTestProbe.Load(assembly, "DemaConsulting.DocDown.NoSuchProbe.bin"));

        Assert.Contains("DemaConsulting.DocDown.NoSuchProbe.bin", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves an embedded resource is loaded as its exact bytes.
    /// </summary>
    [Fact]
    public void Load_PresentResource_ReturnsResourceBytes()
    {
        // Arrange: an assembly whose manifest carries one known resource
        byte[] expected = [0x50, 0x4B, 0x03, 0x04, 0x00, 0xFF];
        var assembly = new StubAssembly("probe.bin", expected);

        // Act: load the resource by name
        var loaded = SelfTestProbe.Load(assembly, "probe.bin");

        // Assert: the bytes are returned unchanged
        Assert.Equal(expected, loaded);
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

    /// <summary>
    ///     An assembly stand-in whose manifest holds exactly one in-memory resource.
    /// </summary>
    private sealed class StubAssembly : Assembly
    {
        /// <summary>The name of the single resource.</summary>
        private readonly string _resourceName;

        /// <summary>The bytes of the single resource.</summary>
        private readonly byte[] _bytes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="StubAssembly"/> class.
        /// </summary>
        /// <param name="resourceName">The name of the resource the manifest carries.</param>
        /// <param name="bytes">The resource content.</param>
        public StubAssembly(string resourceName, byte[] bytes)
        {
            _resourceName = resourceName;
            _bytes = bytes;
        }

        /// <inheritdoc />
        public override string[] GetManifestResourceNames() => [_resourceName];

        /// <inheritdoc />
        public override Stream? GetManifestResourceStream(string name) =>
            string.Equals(name, _resourceName, StringComparison.Ordinal) ? new MemoryStream(_bytes) : null;
    }
}
