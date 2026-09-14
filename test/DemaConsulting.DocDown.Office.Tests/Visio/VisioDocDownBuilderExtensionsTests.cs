using DocDown.Core;
using DocDown.Visio;

namespace DemaConsulting.DocDown.Office.Tests.Visio;

/// <summary>
///     Unit tests for <see cref="VisioDocDownBuilderExtensions"/>.
/// </summary>
public class VisioDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="VisioDocDownBuilderExtensions.AddVisio"/> registers both the managed and
    ///     COM backends.
    /// </summary>
    [Fact]
    public void AddVisio_RegistersOpenXmlAndComBackends()
    {
        var engine = new DocDownBuilder().AddVisio().Build();

        Assert.Equal(2, engine.Extractors.Count);
        Assert.Contains(engine.Extractors, extractor => extractor.Id == "visio-openxml");
        Assert.Contains(engine.Extractors, extractor => extractor.Id == "visio-com");
    }

    /// <summary>
    ///     Proves the managed backend outranks the COM backend, and that the COM identifier sorts
    ///     before the managed one — so equal priority would silently pick COM, which the explicit
    ///     priorities prevent.
    /// </summary>
    [Fact]
    public void AddVisio_ManagedBackend_HasHigherPriorityThanCom()
    {
        var engine = new DocDownBuilder().AddVisio().Build();

        var managed = engine.Extractors.Single(extractor => extractor.Id == "visio-openxml");
        var com = engine.Extractors.Single(extractor => extractor.Id == "visio-com");
        Assert.True(managed.Priority > com.Priority);
        Assert.True(string.CompareOrdinal("visio-com", "visio-openxml") < 0);
    }

    /// <summary>
    ///     Proves <see cref="VisioDocDownBuilderExtensions.AddVisio"/> returns the same builder for chaining.
    /// </summary>
    [Fact]
    public void AddVisio_ReturnsSameBuilder()
    {
        var builder = new DocDownBuilder();

        Assert.Same(builder, builder.AddVisio());
    }

    /// <summary>
    ///     Proves <see cref="VisioDocDownBuilderExtensions.AddVisio"/> rejects a null builder.
    /// </summary>
    [Fact]
    public void AddVisio_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((DocDownBuilder)null!).AddVisio());
    }
}
