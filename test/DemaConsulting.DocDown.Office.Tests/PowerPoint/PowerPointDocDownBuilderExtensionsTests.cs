using DocDown.Core;
using DocDown.PowerPoint;

namespace DemaConsulting.DocDown.Office.Tests.PowerPoint;

/// <summary>
///     Unit tests for <see cref="PowerPointDocDownBuilderExtensions"/>.
/// </summary>
public class PowerPointDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="PowerPointDocDownBuilderExtensions.AddPowerPoint"/> registers only the
    ///     managed Open XML backend.
    /// </summary>
    [Fact]
    public void AddPowerPoint_RegistersOpenXmlBackend()
    {
        var engine = new DocDownBuilder().AddPowerPoint().Build();

        Assert.Single(engine.Extractors);
        Assert.Equal("powerpoint-openxml", engine.Extractors.Single().Id);
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
