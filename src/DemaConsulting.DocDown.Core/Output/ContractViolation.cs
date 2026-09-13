namespace DocDown.Core;

/// <summary>
///     A single contract violation found when verifying a completed extraction folder against
///     its manifest and the honesty invariants.
/// </summary>
/// <param name="Code">The stable violation code (for example <c>DD0715</c>).</param>
/// <param name="Detail">A human-readable description of the specific violation.</param>
/// <remarks>
///     Violations are reported as data rather than thrown so a verifier can enumerate every
///     problem in one pass; an honest extraction yields an empty list. The fixed
///     <paramref name="Code"/> lets automated gates assert on specific violation classes.
///     Instances are immutable and thread-safe.
/// </remarks>
public sealed record ContractViolation(string Code, string Detail);
