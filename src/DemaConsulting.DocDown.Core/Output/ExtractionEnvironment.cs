namespace DocDown.Core;

/// <summary>
///     A description of the environment an extraction ran in, from runtime information plus
///     contributed facts.
/// </summary>
/// <param name="OperatingSystem">The operating system description (from <c>RuntimeInformation.OSDescription</c>).</param>
/// <param name="ProcessArchitecture">The process architecture (for example <c>x64</c> or <c>arm64</c>).</param>
/// <param name="RuntimeVersion">The runtime/framework description (from <c>RuntimeInformation.FrameworkDescription</c>).</param>
/// <param name="RuntimeIdentifier">The runtime identifier (RID) the process is running as.</param>
/// <param name="Facts">
///     The environment facts contributed by backends and unavailable candidates, in insertion
///     order followed by availability-derived facts; never re-sorted.
/// </param>
/// <remarks>
///     Recording the environment makes a degraded result reproducible and explicable: whether a
///     capability was available often depends on the OS, architecture, and RID, so capturing them
///     lets a reader understand why the same document might extract differently elsewhere. The
///     fact order is preserved deliberately so provenance reads chronologically. Instances are
///     immutable and thread-safe.
/// </remarks>
public sealed record ExtractionEnvironment(string OperatingSystem, string ProcessArchitecture,
    string RuntimeVersion, string RuntimeIdentifier, IReadOnlyList<EnvironmentFact> Facts);
