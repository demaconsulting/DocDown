using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="DocDownBuilder"/>, exercising instance and factory registration,
///     default-option configuration, duplicate-identifier rejection, the build-time snapshot, and
///     null-argument guards.
/// </summary>
public class DocDownBuilderTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a registered extractor instance appears on the built engine.
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_Instance_AppearsOnBuiltEngine()
    {
        // Arrange: a builder with one explicitly registered instance
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("alpha", [DocumentFormat.Text]));

        // Act: build the engine
        var engine = builder.Build();

        // Assert: the registered descriptor appears on the engine
        Assert.Equal("alpha", Assert.Single(engine.Extractors).Id);
    }

    /// <summary>
    ///     Proves a registered factory is materialized exactly once at build time.
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_Factory_IsMaterializedOnceAtBuild()
    {
        // Arrange: a factory that counts how many times it is invoked
        var invocations = 0;
        var builder = new DocDownBuilder().AddExtractor(() =>
        {
            invocations++;
            return StubExtractor.Available("beta", [DocumentFormat.Text]);
        });

        // Act: build the engine
        var beforeBuild = invocations;
        var engine = builder.Build();

        // Assert: registration deferred construction and Build ran the factory once
        Assert.Equal(0, beforeBuild);
        Assert.Equal(1, invocations);
        Assert.Equal("beta", Assert.Single(engine.Extractors).Id);
    }

    /// <summary>
    ///     Proves configured defaults flow into the engine and are visible in extraction results.
    /// </summary>
    [Fact]
    public async Task DocDownBuilder_ConfigureDefaults_RenderPages_RecordsRenderNoteByDefault()
    {
        // Arrange: defaults request page rendering a text backend cannot provide
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("text", [DocumentFormat.Text]))
            .ConfigureDefaults(options =>
            {
                options.RenderPages = true;
                options.IncludeEmbeddedImages = false;
                options.TimestampUtc = DateTimeOffset.UnixEpoch;
            })
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");

        // Act: run with no per-call options so the configured defaults apply
        var result = await engine.ExtractAsync(input, Path.Combine(temp.Path, "out"), null, Ct);

        // Assert: the render request and image suppression defaults reached the run
        Assert.Equal(ExtractionOutcome.Produced, result.Outcome);
        Assert.Contains(
            result.Notes,
            note => note.Message.Contains("Page rendering was requested", StringComparison.Ordinal));
        Assert.Contains(
            result.Notes,
            note => note.Message.Contains("Embedded image extraction was disabled by the caller", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves duplicate extractor identifiers are rejected at build time.
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_DuplicateIds_ThrowsArgumentException()
    {
        // Arrange: a builder with two backends sharing one identifier
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("dup", [DocumentFormat.Text]))
            .AddExtractor(StubExtractor.Available("dup", [DocumentFormat.Pdf]));

        // Act / Assert: identifier uniqueness is a hard invariant
        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    /// <summary>
    ///     Proves a factory that returns null is rejected at build time.
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_FactoryReturnsNull_ThrowsArgumentException()
    {
        // Arrange: a builder whose factory yields no extractor
        var builder = new DocDownBuilder().AddExtractor(() => null!);

        // Act / Assert: a null-producing factory violates the registration contract
        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    /// <summary>
    ///     Proves mutating the builder after Build does not alter the already-built engine.
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_MutatedAfterBuild_DoesNotAffectSnapshot()
    {
        // Arrange: a builder with one backend, built into an engine
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("first", [DocumentFormat.Text]));
        var engine = builder.Build();

        // Act: mutate the builder after the engine snapshot was taken
        builder.AddExtractor(StubExtractor.Available("second", [DocumentFormat.Pdf]));

        // Assert: the built engine still reflects only the earlier snapshot
        Assert.Equal("first", Assert.Single(engine.Extractors).Id);
    }

    /// <summary>
    ///     Proves the builder remains reusable and produces independent engines.
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_CalledTwiceWithMoreRegistrations_ProducesIndependentEngines()
    {
        // Arrange: a builder with one backend
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("first", [DocumentFormat.Text]));

        // Act: build once, register another backend, then build again
        var firstEngine = builder.Build();
        builder.AddExtractor(StubExtractor.Available("second", [DocumentFormat.Pdf]));
        var secondEngine = builder.Build();

        // Assert: each engine reflects the registrations present at its own build time
        Assert.Single(firstEngine.Extractors);
        Assert.Equal(2, secondEngine.Extractors.Count);
    }

    /// <summary>
    ///     Proves registering a null instance is rejected at the call site.
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder
        var builder = new DocDownBuilder();

        // Act / Assert: a null extractor is a caller error
        Assert.Throws<ArgumentNullException>(() => builder.AddExtractor((IDocumentExtractor)null!));
    }

    /// <summary>
    ///     Proves registering a null factory is rejected at the call site.
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder
        var builder = new DocDownBuilder();

        // Act / Assert: a null factory cannot produce an extractor
        Assert.Throws<ArgumentNullException>(() => builder.AddExtractor((Func<IDocumentExtractor>)null!));
    }

    /// <summary>
    ///     Proves a null configuration action is rejected at the call site.
    /// </summary>
    [Fact]
    public void DocDownBuilder_ConfigureDefaults_NullAction_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder
        var builder = new DocDownBuilder();

        // Act / Assert: a null configuration action has nothing to apply
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureDefaults(null!));
    }
}
