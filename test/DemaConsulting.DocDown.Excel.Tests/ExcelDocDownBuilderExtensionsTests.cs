using DocDown.Core;
using DocDown.Excel;

namespace DemaConsulting.DocDown.Excel.Tests;

/// <summary>
///     Unit tests for <see cref="ExcelDocDownBuilderExtensions"/>.
/// </summary>
public class ExcelDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="ExcelDocDownBuilderExtensions.AddExcel"/> registers exactly the single
    ///     managed Excel backend.
    /// </summary>
    [Fact]
    public void AddExcel_RegistersSingleOpenXmlBackend()
    {
        var engine = new DocDownBuilder().AddExcel().Build();

        var extractor = Assert.Single(engine.Extractors);
        Assert.Equal("excel-openxml", extractor.Id);
    }

    /// <summary>
    ///     Proves <see cref="ExcelDocDownBuilderExtensions.AddExcel"/> returns the same builder for chaining.
    /// </summary>
    [Fact]
    public void AddExcel_ReturnsSameBuilder()
    {
        var builder = new DocDownBuilder();

        Assert.Same(builder, builder.AddExcel());
    }

    /// <summary>
    ///     Proves <see cref="ExcelDocDownBuilderExtensions.AddExcel"/> rejects a null builder.
    /// </summary>
    [Fact]
    public void AddExcel_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((DocDownBuilder)null!).AddExcel());
    }
}
