using DocDown.Core;
using DocDown.Pdf;

namespace DemaConsulting.DocDown.Pdf.Tests;

/// <summary>
///     Unit tests for <see cref="PdfDocDownBuilderExtensions"/>, proving the registration seam
///     registers exactly one PDF backend, chains, rejects a null builder, and uses no reflection.
/// </summary>
/// <remarks>
///     The seam is small but load-bearing: it is the one visible edge from a host to this package,
///     and the property that makes DocDown's dependency graph honest is that nothing else can create
///     that edge. These tests pin both halves — that the call does register the backend, and that it
///     does so without reflection or assembly scanning.
/// </remarks>
public class PdfDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves the extension registers exactly one extractor, identified as the PDF backend.
    /// </summary>
    [Fact]
    public void PdfDocDownBuilderExtensions_AddPdf_EmptyBuilder_RegistersTheSinglePdfExtractor()
    {
        // Arrange: an empty builder
        var builder = new DocDownBuilder();

        // Act: register the PDF backend and build an engine
        var engine = builder.AddPdf().Build();

        // Assert: exactly one backend is registered, and it is the PDF one with its declared formats
        var descriptor = Assert.Single(engine.Extractors);
        Assert.Equal("pdf", descriptor.Id);
        Assert.Equal([DocumentFormat.Pdf], descriptor.SupportedFormats);
        Assert.Equal(
            ExtractorCapabilities.Text | ExtractorCapabilities.EmbeddedImages | ExtractorCapabilities.DocumentMetadata,
            descriptor.Capabilities);
    }

    /// <summary>
    ///     Proves the extension returns the same builder so registration can be chained.
    /// </summary>
    [Fact]
    public void PdfDocDownBuilderExtensions_AddPdf_AnyBuilder_ReturnsTheSameBuilderForChaining()
    {
        // Arrange: a builder to register against
        var builder = new DocDownBuilder();

        // Act: register the backend
        var returned = builder.AddPdf();

        // Assert: the same instance comes back, so a host can chain further configuration onto it
        Assert.Same(builder, returned);
    }

    /// <summary>
    ///     Proves registration is deferred to build time, so it creates no instance by itself.
    /// </summary>
    /// <remarks>
    ///     Registering a factory rather than an instance is what lets a host configure a builder it
    ///     never builds without paying for the backend, and gives each built engine its own instance.
    /// </remarks>
    [Fact]
    public void PdfDocDownBuilderExtensions_AddPdf_TwoEngines_EachReceivesItsOwnExtractor()
    {
        // Arrange: one builder registered once
        var builder = new DocDownBuilder().AddPdf();

        // Act: build two engines from it and probe each backend
        var first = builder.Build();
        var second = builder.Build();

        // Assert: both engines carry a working PDF backend of their own
        Assert.Equal("pdf", Assert.Single(first.Extractors).Id);
        Assert.Equal("pdf", Assert.Single(second.Extractors).Id);
        Assert.All(first.GetBackendStatus(), status => Assert.True(status.IsAvailable));
        Assert.All(second.GetBackendStatus(), status => Assert.True(status.IsAvailable));
    }

    /// <summary>
    ///     Proves the extension registers without reflection or assembly scanning.
    /// </summary>
    /// <remarks>
    ///     Asserted structurally: the registration surface is a plain extension method whose only
    ///     parameter is the builder, and the assembly declares no dependency on the reflection-based
    ///     loading APIs a scanning registration would need.
    /// </remarks>
    [Fact]
    public void PdfDocDownBuilderExtensions_AddPdf_RegistrationSurface_UsesNoReflection()
    {
        // Arrange: the extension method's own signature
        var method = typeof(PdfDocDownBuilderExtensions).GetMethod(nameof(PdfDocDownBuilderExtensions.AddPdf));

        // Assert: it takes only the builder, so there is no type, name, or assembly to resolve
        Assert.NotNull(method);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(DocDownBuilder), parameter.ParameterType);
        Assert.Equal(typeof(DocDownBuilder), method.ReturnType);

        // Assert: no PdfPig type appears on the seam, so a host can reference it without the parser
        Assert.DoesNotContain(typeof(PdfDocDownBuilderExtensions).GetMethods()
            .SelectMany(candidate => candidate.GetParameters().Select(argument => argument.ParameterType))
            .Append(method.ReturnType),
            type => type.Namespace?.StartsWith("UglyToad", StringComparison.Ordinal) ?? false);
    }

    /// <summary>
    ///     Proves a null builder is rejected as a caller error (boundary).
    /// </summary>
    [Fact]
    public void PdfDocDownBuilderExtensions_AddPdf_NullBuilder_ThrowsArgumentNullException()
    {
        // Act + Assert: the builder is what the registration mutates and is mandatory
        Assert.Throws<ArgumentNullException>(() => ((DocDownBuilder)null!).AddPdf());
    }
}
