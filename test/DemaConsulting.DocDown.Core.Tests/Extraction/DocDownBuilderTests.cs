using DemaConsulting.DocDown.TestSupport;
using DocDown.Core;

namespace DemaConsulting.DocDown.Core.Tests.Extraction;

/// <summary>
///     Unit tests for <see cref="DocDownBuilder"/>, exercising instance and factory registration,
///     default-option configuration, duplicate-identifier rejection, the build-time snapshot, and
///     null-argument guards.
/// </summary>
/// <remarks>
///     These tests bind to <see cref="DocDownBuilder"/> and its documented product,
///     <see cref="DocDownEngine"/>. Each is named for the unit requirement it evidences: register
///     an instance, register a factory, configure defaults, reject duplicate ids, take an immutable
///     snapshot at build time, and reject null arguments.
/// </remarks>
public class DocDownBuilderTests
{
    /// <summary>Gets the ambient test cancellation token so async calls stay responsive to cancellation.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Proves a registered extractor instance appears on the built engine (RegisterInstance).
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_Instance_AppearsOnBuiltEngine()
    {
        // Arrange: a builder with one explicitly registered instance
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("alpha", [DocumentFormat.Text], ExtractorCapabilities.Text));

        // Act: build the engine
        var engine = builder.Build();

        // Assert: the registered instance is the sole descriptor on the engine
        var descriptor = Assert.Single(engine.Extractors);
        Assert.Equal("alpha", descriptor.Id);
    }

    /// <summary>
    ///     Proves a registered factory is materialized exactly once at build time (RegisterFactory).
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_Factory_IsMaterializedOnceAtBuild()
    {
        // Arrange: a factory that counts how many times it is invoked
        var invocations = 0;
        var builder = new DocDownBuilder().AddExtractor(() =>
        {
            invocations++;
            return StubExtractor.Available("beta", [DocumentFormat.Text], ExtractorCapabilities.Text);
        });

        // Act: the factory should not run until Build, and then exactly once
        var beforeBuild = invocations;
        var engine = builder.Build();

        // Assert: registration deferred construction, and Build materialized the factory a single time
        Assert.Equal(0, beforeBuild);
        Assert.Equal(1, invocations);
        Assert.Equal("beta", Assert.Single(engine.Extractors).Id);
    }

    /// <summary>
    ///     Proves configured defaults flow into the engine and govern extraction (ConfigureDefaults).
    /// </summary>
    [Fact]
    public async Task DocDownBuilder_ConfigureDefaults_RenderPages_ProducesPageGapByDefault()
    {
        // Arrange: a text-only backend and defaults that request page rendering it cannot satisfy
        using var temp = new TempScratch();
        var engine = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("text", [DocumentFormat.Text], ExtractorCapabilities.Text))
            .ConfigureDefaults(options =>
            {
                options.RenderPages = true;
                options.IncludeEmbeddedImages = false;
                options.TimestampUtc = DateTimeOffset.UnixEpoch;
            })
            .Build();
        var input = temp.CreateFile("document.txt", "hello world");
        var scratch = Path.Combine(temp.Path, "out");

        // Act: run with no per-call options so the configured defaults apply
        var result = await engine.ExtractAsync(input, scratch, null, Ct);

        // Assert: the configured render request surfaced as a pages gap, proving defaults took effect
        Assert.Contains(result.Gaps, gap => gap.Kind == GapKind.Pages);
    }

    /// <summary>
    ///     Proves two extractors sharing an identifier are rejected at build time (RejectDuplicateIds).
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_DuplicateIds_ThrowsArgumentException()
    {
        // Arrange: a builder with two backends that share the same identifier
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("dup", [DocumentFormat.Text], ExtractorCapabilities.Text))
            .AddExtractor(StubExtractor.Available("dup", [DocumentFormat.Pdf], ExtractorCapabilities.Text));

        // Act + Assert: the identifier is the override and manifest key, so a collision fails the build
        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    /// <summary>
    ///     Proves a factory that returns null is rejected at build time (RejectDuplicateIds boundary).
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_FactoryReturnsNull_ThrowsArgumentException()
    {
        // Arrange: a builder whose factory yields no extractor
        var builder = new DocDownBuilder().AddExtractor(() => null!);

        // Act + Assert: a null-producing factory violates the registration contract
        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    /// <summary>
    ///     Proves mutating the builder after Build does not alter the already-built engine (ImmutableSnapshot).
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_MutatedAfterBuild_DoesNotAffectSnapshot()
    {
        // Arrange: a builder with one backend, built into an engine
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("first", [DocumentFormat.Text], ExtractorCapabilities.Text));
        var engine = builder.Build();

        // Act: add another backend to the builder after the snapshot was taken
        builder.AddExtractor(StubExtractor.Available("second", [DocumentFormat.Pdf], ExtractorCapabilities.Text));

        // Assert: the built engine still carries only the snapshot's single backend
        Assert.Equal("first", Assert.Single(engine.Extractors).Id);
    }

    /// <summary>
    ///     Proves the builder remains reusable and produces independent engines (ImmutableSnapshot).
    /// </summary>
    [Fact]
    public void DocDownBuilder_Build_CalledTwiceWithMoreRegistrations_ProducesIndependentEngines()
    {
        // Arrange: a builder with one backend
        var builder = new DocDownBuilder()
            .AddExtractor(StubExtractor.Available("first", [DocumentFormat.Text], ExtractorCapabilities.Text));

        // Act: build once, register another backend, then build again
        var firstEngine = builder.Build();
        builder.AddExtractor(StubExtractor.Available("second", [DocumentFormat.Pdf], ExtractorCapabilities.Text));
        var secondEngine = builder.Build();

        // Assert: each engine reflects the registrations present at its own build time
        Assert.Single(firstEngine.Extractors);
        Assert.Equal(2, secondEngine.Extractors.Count);
    }

    /// <summary>
    ///     Proves registering a null instance is rejected at the call site (RejectNullArguments).
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_NullInstance_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder
        var builder = new DocDownBuilder();

        // Act + Assert: a null extractor is a caller error named at the registration site
        Assert.Throws<ArgumentNullException>(() => builder.AddExtractor((IDocumentExtractor)null!));
    }

    /// <summary>
    ///     Proves registering a null factory is rejected at the call site (RejectNullArguments).
    /// </summary>
    [Fact]
    public void DocDownBuilder_AddExtractor_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder
        var builder = new DocDownBuilder();

        // Act + Assert: a null factory cannot produce an extractor and is rejected
        Assert.Throws<ArgumentNullException>(() => builder.AddExtractor((Func<IDocumentExtractor>)null!));
    }

    /// <summary>
    ///     Proves a null configuration action is rejected at the call site (RejectNullArguments).
    /// </summary>
    [Fact]
    public void DocDownBuilder_ConfigureDefaults_NullAction_ThrowsArgumentNullException()
    {
        // Arrange: a fresh builder
        var builder = new DocDownBuilder();

        // Act + Assert: a null configuration action has nothing to apply and is rejected
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureDefaults(null!));
    }
}
