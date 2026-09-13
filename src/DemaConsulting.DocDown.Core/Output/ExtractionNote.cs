namespace DocDown.Core;

/// <summary>
///     A short, plain-language note recorded when DocDown attempted a step during an extraction and
///     could not complete it.
/// </summary>
/// <param name="Message">
///     A single, human-readable sentence stating the fact about the extraction — for example that
///     page rendering was requested but no renderer was available, or that an image could not be
///     decoded. Must not be null or blank.
/// </param>
/// <remarks>
///     <para>
///         A note is one of the only two ways DocDown reports what happened during extraction (the
///         other is the content inventory of counts, including a deliberate zero for anything a
///         reader genuinely looked for). It carries a single fact and nothing else: no identifier,
///         no code, no severity, no remedy, and no impact. DocDown describes what it extracted and
///         where; it does not grade the document or advise the reader, so a note never characterizes
///         the document's quality and never suggests a fix.
///     </para>
///     <para>
///         A note states a fact about the <em>extraction</em>, never about the document. A document
///         that is merely empty is described by the inventory (a count of zero), not by a note; a
///         note is reserved for the case where DocDown tried to produce something and could not.
///         Instances are immutable and thread-safe.
///     </para>
/// </remarks>
public sealed record ExtractionNote(string Message);
