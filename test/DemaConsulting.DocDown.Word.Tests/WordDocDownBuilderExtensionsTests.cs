using DocDown.Core;
using DocDown.Word;

namespace DemaConsulting.DocDown.Word.Tests;

/// <summary>
///     Unit tests for <see cref="WordDocDownBuilderExtensions"/>.
/// </summary>
public class WordDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves <see cref="WordDocDownBuilderExtensions.AddWord"/> registers the managed Open XML
    ///     backend, and that it is the only backend the package registers.
    /// </summary>
    /// <remarks>
    ///     The absence assertion is as load-bearing as the presence one: this package ships exactly
    ///     one backend, so a second registered extractor would mean a backend re-entered the package
    ///     without anyone deciding it should.
    /// </remarks>
    [Fact]
    public void AddWord_OnBuilder_RegistersOpenXmlBackend()
    {
        var engine = new DocDownBuilder().AddWord().Build();

        Assert.Contains(engine.Extractors, extractor => extractor.Id == "word-openxml");
        Assert.Single(engine.Extractors);
    }

    /// <summary>
    ///     Proves the registration method returns the same builder for chaining.
    /// </summary>
    [Fact]
    public void AddWord_ReturnsSameBuilderForChaining()
    {
        var builder = new DocDownBuilder();

        Assert.Same(builder, builder.AddWord());
    }

    /// <summary>
    ///     Proves the registration method rejects a null builder naming the extension method.
    /// </summary>
    [Fact]
    public void AddWord_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((DocDownBuilder)null!).AddWord());
    }
}
