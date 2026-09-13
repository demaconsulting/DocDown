namespace DocDown.Core;

/// <summary>
///     A flattened, report-friendly view of a registered extractor's identity, declared and
///     effective capabilities, and current availability.
/// </summary>
/// <param name="Id">The extractor's stable, unique identifier.</param>
/// <param name="DisplayName">A human-readable name for the extractor.</param>
/// <param name="SupportedFormats">The document formats the extractor can process.</param>
/// <param name="Capabilities">The content aspects the extractor declares it can produce.</param>
/// <param name="EffectiveCapabilities">The content aspects actually usable in this environment.</param>
/// <param name="Priority">The extractor's ranking priority; higher is preferred on ties.</param>
/// <param name="IsAvailable"><see langword="true"/> when the extractor can run here.</param>
/// <param name="UnavailableReason">Why the extractor is unavailable, or <see langword="null"/> when available.</param>
/// <remarks>
///     Exposed by the engine's status query so operators can diagnose a deployment — for
///     example seeing that a backend is present but unavailable and why — without triggering an
///     extraction. Combining descriptor and availability into one flat record keeps that
///     diagnostic surface simple. Instances are immutable and thread-safe.
/// </remarks>
public sealed record BackendStatus(
    string Id, string DisplayName, IReadOnlyList<DocumentFormat> SupportedFormats,
    ExtractorCapabilities Capabilities, ExtractorCapabilities EffectiveCapabilities,
    int Priority, bool IsAvailable, string? UnavailableReason);
