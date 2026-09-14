using DocDown.Core;
using DocDown.Excel;
using DocDown.Office;

namespace DemaConsulting.DocDown.Office.Tests;

/// <summary>
///     Unit tests for <see cref="OfficeDocDownBuilderExtensions"/>.
/// </summary>
public class OfficeDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="OfficeDocDownBuilderExtensions.AddOffice"/> registers every backend the
    ///     package ships, and only those.
    /// </summary>
    /// <remarks>
    ///     The exact-set assertion is the point. One call standing in for four is only safe if it
    ///     registers the same backends those four did; asserting the whole set catches a backend
    ///     silently dropped from the chain as readily as one silently added.
    /// </remarks>
    [Fact]
    public void AddOffice_OnBuilder_RegistersEveryOfficeBackend()
    {
        var engine = new DocDownBuilder().AddOffice().Build();

        string[] expected =
        [
            "excel-openxml",
            "powerpoint-com",
            "powerpoint-openxml",
            "visio-com",
            "visio-openxml",
            "word-openxml"
        ];

        Assert.Equal(expected, engine.Extractors.Select(e => e.Id).Order().ToArray());
    }

    /// <summary>
    ///     Proves the registration method returns the same builder for chaining.
    /// </summary>
    [Fact]
    public void AddOffice_ReturnsSameBuilderForChaining()
    {
        var builder = new DocDownBuilder();

        Assert.Same(builder, builder.AddOffice());
    }

    /// <summary>
    ///     Proves a null builder is rejected rather than silently ignored.
    /// </summary>
    [Fact]
    public void AddOffice_NullBuilder_Throws()
    {
        DocDownBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddOffice());
    }

    /// <summary>
    ///     Proves the per-format calls still register one format's backends on their own.
    /// </summary>
    /// <remarks>
    ///     The granularity is the stated reason the per-format methods survived the merge into a
    ///     single package, so it is worth a test: a service that only reads spreadsheets registers one
    ///     backend, not six.
    /// </remarks>
    [Fact]
    public void AddExcel_Alone_RegistersOnlyTheExcelBackend()
    {
        var engine = new DocDownBuilder().AddExcel().Build();

        Assert.Single(engine.Extractors);
        Assert.Equal("excel-openxml", engine.Extractors[0].Id);
    }
}
