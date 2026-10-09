using DemaConsulting.DocDown.Core;
using DemaConsulting.DocDown.Visio;

namespace DemaConsulting.DocDown.Office.Tests.Visio;

/// <summary>
///     Unit tests for <see cref="VisioDocDownBuilderExtensions"/>.
/// </summary>
public class VisioDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="VisioDocDownBuilderExtensions.AddVisio"/> registers only the managed
    ///     Open Packaging backend.
    /// </summary>
    [Fact]
    public void AddVisio_RegistersOpenXmlBackend()
    {
        var engine = new DocDownBuilder().AddVisio().Build();

        Assert.Single(engine.Extractors);
        Assert.Equal("visio-openxml", engine.Extractors.Single().Id);
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
