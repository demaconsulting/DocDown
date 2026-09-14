using DocDown.Core;
using DocDown.PowerPoint;

namespace DemaConsulting.DocDown.Office.Tests.PowerPoint;

/// <summary>
///     Unit tests for <see cref="PowerPointDocDownBuilderExtensions"/>.
/// </summary>
public class PowerPointDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="PowerPointDocDownBuilderExtensions.AddPowerPoint"/> registers both the
    ///     managed and COM backends.
    /// </summary>
    [Fact]
    public void AddPowerPoint_RegistersOpenXmlAndComBackends()
    {
        var engine = new DocDownBuilder().AddPowerPoint().Build();

        Assert.Equal(2, engine.Extractors.Count);
        Assert.Contains(engine.Extractors, extractor => extractor.Id == "powerpoint-openxml");
        Assert.Contains(engine.Extractors, extractor => extractor.Id == "powerpoint-com");
    }

    /// <summary>
    ///     Proves the managed backend outranks the COM backend for a default extraction so the
    ///     deterministic backend is the default. The COM identifier sorts before the managed one, so
    ///     equal priority would silently pick COM — the explicit priorities prevent that.
    /// </summary>
    [Fact]
    public void AddPowerPoint_ManagedBackend_HasHigherPriorityThanCom()
    {
        var engine = new DocDownBuilder().AddPowerPoint().Build();

        var managed = engine.Extractors.Single(extractor => extractor.Id == "powerpoint-openxml");
        var com = engine.Extractors.Single(extractor => extractor.Id == "powerpoint-com");
        Assert.True(managed.Priority > com.Priority);
        Assert.True(string.CompareOrdinal("powerpoint-com", "powerpoint-openxml") < 0);
    }

    /// <summary>
    ///     Proves <see cref="PowerPointDocDownBuilderExtensions.AddPowerPoint"/> returns the same
    ///     builder for chaining.
    /// </summary>
    [Fact]
    public void AddPowerPoint_ReturnsSameBuilder()
    {
        var builder = new DocDownBuilder();

        Assert.Same(builder, builder.AddPowerPoint());
    }

    /// <summary>
    ///     Proves <see cref="PowerPointDocDownBuilderExtensions.AddPowerPoint"/> rejects a null builder.
    /// </summary>
    [Fact]
    public void AddPowerPoint_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((DocDownBuilder)null!).AddPowerPoint());
    }
}
