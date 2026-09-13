namespace DocDown.Core;

/// <summary>
///     An inclusive range of 1-based document page numbers.
/// </summary>
/// <param name="First">The first page in the range (inclusive).</param>
/// <param name="Last">The last page in the range (inclusive).</param>
/// <remarks>
///     Modeled as a <see langword="readonly"/> <see langword="record struct"/> so a page range
///     is a cheap, allocation-free value. Callers are responsible for supplying
///     <see cref="First"/> &lt;= <see cref="Last"/>; the members here do not throw on an inverted
///     range but instead treat it as empty (<see cref="Count"/> is <c>0</c> and
///     <see cref="Contains"/> is <see langword="false"/>) so a malformed range degrades safely
///     rather than producing negative counts. Instances are immutable and thread-safe.
/// </remarks>
public readonly record struct PageRange(int First, int Last)
{
    /// <summary>
    ///     Determines whether the given page number falls within this inclusive range.
    /// </summary>
    /// <param name="page">The 1-based page number to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="page"/> is between <see cref="First"/> and
    ///     <see cref="Last"/> inclusive; otherwise <see langword="false"/>. Always
    ///     <see langword="false"/> for an inverted range.
    /// </returns>
    /// <remarks>
    ///     Written to be safe for an inverted range so callers need not pre-validate. Pure and
    ///     thread-safe.
    /// </remarks>
    public bool Contains(int page) => page >= First && page <= Last;

    /// <summary>
    ///     Gets the number of pages in the inclusive range.
    /// </summary>
    /// <remarks>
    ///     Clamped to a minimum of zero so an inverted range (<see cref="Last"/> &lt;
    ///     <see cref="First"/>) yields <c>0</c> rather than a negative count. Pure and
    ///     thread-safe.
    /// </remarks>
    public int Count => Last < First ? 0 : Last - First + 1;
}
