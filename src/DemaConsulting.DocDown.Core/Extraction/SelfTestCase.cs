namespace DocDown.Core;

/// <summary>
///     A single named self-test case: a runnable check contributed by Core or a backend.
/// </summary>
/// <param name="Name">A short, unique, human-readable name for the case.</param>
/// <param name="Category">
///     A grouping category (for example <c>core</c> or a backend identifier) used to organize
///     and filter results.
/// </param>
/// <param name="Run">
///     The delegate that executes the case against a <see cref="SelfTestContext"/> and returns
///     its <see cref="SelfTestResult"/>. The work happens only when this delegate is invoked.
/// </param>
/// <remarks>
///     Deferring the work behind a delegate lets Core enumerate cases cheaply — for example to
///     count or wrap them — without running anything, and lets an unavailable backend's cases be
///     replaced with skip-returning wrappers. Instances are immutable and thread-safe; the
///     delegate's own thread-safety is the responsibility of its author.
/// </remarks>
public sealed record SelfTestCase(string Name, string Category, Func<SelfTestContext, SelfTestResult> Run);
