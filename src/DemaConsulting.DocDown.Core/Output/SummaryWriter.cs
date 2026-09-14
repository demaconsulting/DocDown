using System.Globalization;
using System.Text;

namespace DocDown.Core;

/// <summary>
///     Renders <c>summary.txt</c>, the human- and LLM-readable account of what an extraction
///     produced, where it lives, and — above all — what is missing and why.
/// </summary>
/// <remarks>
///     <para>
///         The summary always contains the same sections in the same order, even when a section has
///         nothing to report (in which case it says so in words), because a stable shape is what lets
///         a reader — human or agent — trust that no information was quietly dropped. The first block
///         states the absolute scratch-folder path, the single most important line in the product,
///         and a failed extraction still renders the whole layout with the failure explanation
///         included verbatim.
///     </para>
///     <para>
///         Output is plain UTF-8 without a BOM, uses <c>\n</c> line endings always, and stays within
///         120 columns (long note prose is word-wrapped with a hanging indent). Combined with a fixed
///         timestamp, that makes the file byte-identical across runs and platforms. The writer holds
///         no state and performs filesystem I/O through the scratch-folder gate; it is intended for
///         the single extraction flow, not concurrent invocation against one folder.
///     </para>
/// </remarks>
public static class SummaryWriter
{
    /// <summary>The fixed relative name of the summary file.</summary>
    /// <remarks>Part of the invariant output contract; never varies.</remarks>
    private const string SummaryFileName = "summary.txt";

    /// <summary>The maximum column width for wrapped prose, kept safely under the 120-column limit.</summary>
    /// <remarks>Leaves room for the hanging indent so wrapped continuation lines also stay in bounds.</remarks>
    private const int WrapWidth = 112;

    /// <summary>
    ///     Writes the summary document to <c>summary.txt</c> in the scratch folder.
    /// </summary>
    /// <param name="folder">The scratch folder to write into. Must not be null.</param>
    /// <param name="sink">The sink holding the recorded content. Must not be null.</param>
    /// <param name="report">The engine-side facts describing the extraction. Must not be null.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the summary has been written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Assembles every section into a single buffer with <c>\n</c> separators and writes it in one
    ///     pass. Performs filesystem I/O.
    /// </remarks>
    public static async ValueTask WriteAsync(
        ScratchFolder folder, ExtractionSink sink, ExtractionReport report,
        ContentWriteResult? content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();

        // Emit the fixed sections in their fixed order so the shape never varies
        AppendTitleAndHeader(builder, folder, report, sink, content);
        AppendFailure(builder, report);
        AppendBackend(builder, report);
        AppendEnvironment(builder, report.Environment);
        AppendDocumentMetadata(builder, sink.DocumentMetadata);
        AppendLayout(builder, sink, content);
        AppendWhatWasExtracted(builder, sink, content, report.DetectedFormat.Format.Id);
        AppendNotes(builder, sink.Notes);

        await folder.WriteTextAsync(SummaryFileName, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Appends the title banner, the one-line plain-English gist, and the header block,
    ///     including the absolute scratch path.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="folder">The scratch folder whose absolute path is stated.</param>
    /// <param name="report">The engine-side facts for the source, format, timestamp, and status.</param>
    /// <param name="sink">The sink holding the recorded images, pages, and document metadata.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <remarks>
    ///     The scratch-folder line is placed first among the labeled values because it is the single
    ///     most important value a consumer needs, but the gist precedes the whole block: a reader who
    ///     pastes this file into a model's context should learn what the document <em>is</em> before
    ///     reading a path. The trailing "all paths are relative" note frames every later path.
    /// </remarks>
    private static void AppendTitleAndHeader(
        StringBuilder builder, ScratchFolder folder, ExtractionReport report,
        ExtractionSink sink, ContentWriteResult? content)
    {
        // Title banner
        builder.Append("DocDown Extraction Summary\n");
        builder.Append("==========================\n\n");

        // One plain-English sentence, composed only from facts already established below
        AppendGist(builder, report, sink, content);

        // Header block: the scratch path first, then source, format, timestamp, and status
        builder.Append("Scratch folder  : ").Append(folder.AbsolutePath).Append('\n');
        builder.Append("Source document : ").Append(DescribeSource(report.Source)).Append('\n');
        builder.Append("Detected format : ").Append(report.DetectedFormat.Describe()).Append('\n');
        builder.Append("Extracted (UTC) : ").Append(FormatTimestamp(report.TimestampUtc)).Append('\n');
        builder.Append("Status          : ").Append(StatusLine(report.Outcome)).Append('\n');
        builder.Append('\n');
        builder.Append("All paths below are relative to the scratch folder shown above.\n\n");
    }

    /// <summary>
    ///     Appends a single plain-English sentence describing what the document is and what the
    ///     extraction produced, or nothing when the established facts do not support one.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="report">The engine-side facts supplying the detected format.</param>
    /// <param name="sink">The sink supplying the document metadata, images, and pages.</param>
    /// <param name="content">The content-write result, or <see langword="null"/>.</param>
    /// <remarks>
    ///     <para>
    ///         A reader who opens this file gets a filename and a table of metadata but no gist, so
    ///         the first question — "what am I looking at?" — is answered last. This line answers it
    ///         first.
    ///     </para>
    ///     <para>
    ///         Nothing here is inferred. The format noun comes from the detected format, the unit
    ///         count from the backend's reported document info, the title only from the document's
    ///         <em>authored</em> metadata (never the manifest's possibly heuristic title), and the
    ///         produced quantities from what was actually written. When the format is unrecognized
    ///         or nothing was produced, the sentence is omitted entirely: a confidently wrong gist
    ///         is far worse for a reader than no gist at all. Pure apart from the buffer.
    ///     </para>
    /// </remarks>
    private static void AppendGist(
        StringBuilder builder, ExtractionReport report, ExtractionSink sink, ContentWriteResult? content)
    {
        // An unrecognized format supplies no noun, and inventing one would be the exact failure mode
        // this line is meant to avoid
        if (FormatPhrase(report.DetectedFormat.Format.Id) is not { } formatPhrase)
        {
            return;
        }

        // Describe what was produced; with nothing produced there is no sentence worth writing
        var produced = ProducedPhrases(sink, content);
        if (produced.Count == 0)
        {
            return;
        }

        // The unit count is the backend's own, and its noun follows the format
        var unit = UnitCount(report.DetectedFormat.Format.Id, sink.DocumentInfo);
        var subject = new StringBuilder(unit is null ? formatPhrase.Article : ArticleFor(unit.Value.Count)).Append(' ');
        if (unit is { } known)
        {
            subject.Append(known.Count.ToString(CultureInfo.InvariantCulture)).Append('-')
                .Append(known.Noun).Append(' ');
        }

        subject.Append(formatPhrase.Noun);

        // Only an authored title is quoted; a title derived from a first slide or heading is a
        // heuristic and must never be presented as what the document calls itself
        if (FieldValue(sink.DocumentMetadata, "title") is { } title)
        {
            subject.Append(" titled \"").Append(title).Append('"');
        }

        AppendWrapped(builder, string.Empty, $"{subject}; extracted {JoinList(produced)}.");
        builder.Append('\n');
    }

    /// <summary>
    ///     Lists what the extraction actually produced, as plain-English noun phrases.
    /// </summary>
    /// <param name="sink">The sink holding the recorded images and pages.</param>
    /// <param name="content">The content-write result, or <see langword="null"/>.</param>
    /// <returns>The phrases in reading order; empty when nothing was produced.</returns>
    /// <remarks>Each phrase restates a count the summary states again below, so nothing here is new or inferred. Pure.</remarks>
    private static List<string> ProducedPhrases(ExtractionSink sink, ContentWriteResult? content)
    {
        var phrases = new List<string>(3);
        if (content is { ContentPresent: true })
        {
            phrases.Add($"{content.CharacterCount.ToString("N0", CultureInfo.InvariantCulture)} characters of text");
        }

        if (sink.Images.Count > 0)
        {
            phrases.Add(Pluralize(sink.Images.Count, "embedded image"));
        }

        if (sink.Pages.Count > 0)
        {
            phrases.Add(Pluralize(sink.Pages.Count, "rendered page image"));
        }

        return phrases;
    }

    /// <summary>
    ///     Maps a detected format identifier to the article and noun used to name it in prose.
    /// </summary>
    /// <param name="formatId">The stable format identifier (for example <c>pptx</c>).</param>
    /// <returns>The article and noun, or <see langword="null"/> when the format has no plain-English name.</returns>
    /// <remarks>
    ///     Returning <see langword="null"/> for anything unmapped is deliberate: the gist is omitted
    ///     rather than describing an unrecognized stream as some guessed kind of document. Pure.
    /// </remarks>
    private static (string Article, string Noun)? FormatPhrase(string? formatId) => formatId switch
    {
        "pdf" => ("A", "PDF document"),
        "docx" or "doc" => ("A", "Word document"),
        "xlsx" or "xls" => ("An", "Excel workbook"),
        "pptx" or "ppt" => ("A", "PowerPoint presentation"),
        "vsdx" or "vsdm" or "vsd" => ("A", "Visio drawing"),
        "html" => ("An", "HTML document"),
        "text" => ("A", "plain-text document"),
        _ => null
    };

    /// <summary>
    ///     Resolves the count and unit noun that best describe a document's extent for its format.
    /// </summary>
    /// <param name="formatId">The stable format identifier.</param>
    /// <param name="info">The backend's reported document info, or <see langword="null"/>.</param>
    /// <returns>The count and its singular unit noun, or <see langword="null"/> when the backend reported none.</returns>
    /// <remarks>
    ///     A backend that reported no count yields <see langword="null"/> so the gist simply omits the
    ///     extent rather than inventing one. Pure.
    /// </remarks>
    private static (int Count, string Noun)? UnitCount(string? formatId, DocumentInfo? info)
    {
        // A workbook reports its sheet count as the part count, because each sheet becomes a part
        var count = formatId is "xlsx" or "xls" ? info?.PartCount : info?.PageCount;
        return count is > 0 ? (count.Value, UnitNoun(formatId)) : null;
    }

    /// <summary>
    ///     Names the addressable unit of a format in the singular.
    /// </summary>
    /// <param name="formatId">The stable format identifier.</param>
    /// <returns><c>slide</c> for a presentation, <c>sheet</c> for a workbook, otherwise <c>page</c>.</returns>
    /// <remarks>
    ///     A deck is measured in slides and a workbook in sheets. Using the right noun matters beyond
    ///     style: a backend records a workbook image's worksheet index in the same field a PDF uses
    ///     for its page number, so calling it a page would be an outright false statement about a
    ///     document that has none. Pure.
    /// </remarks>
    private static string UnitNoun(string? formatId) => formatId switch
    {
        "pptx" or "ppt" => "slide",
        "xlsx" or "xls" => "sheet",
        _ => "page"
    };

    /// <summary>
    ///     Chooses the indefinite article that agrees with a spoken count.
    /// </summary>
    /// <param name="count">The count that opens the noun phrase.</param>
    /// <returns><c>An</c> when the count is spoken with a leading vowel sound; otherwise <c>A</c>.</returns>
    /// <remarks>
    ///     When a count opens the phrase it, not the noun, governs the article — "a 6-sheet workbook"
    ///     but "an 8-slide deck". Only the counts spoken with a leading vowel sound (anything reading
    ///     as "eight...", plus eleven and eighteen) take <c>An</c>. Pure.
    /// </remarks>
    private static string ArticleFor(int count)
    {
        var digits = count.ToString(CultureInfo.InvariantCulture);
        return digits[0] == '8' || digits is "11" or "18" ? "An" : "A";
    }

    /// <summary>
    ///     Joins phrases into an English list with a serial comma.
    /// </summary>
    /// <param name="phrases">The phrases to join; must not be empty.</param>
    /// <returns>The joined list (for example <c>a, b, and c</c>).</returns>
    /// <remarks>Prose, not data, so it reads as a sentence rather than a comma-separated field. Pure.</remarks>
    private static string JoinList(IReadOnlyList<string> phrases) => phrases.Count switch
    {
        1 => phrases[0],
        2 => $"{phrases[0]} and {phrases[1]}",
        _ => string.Join(", ", phrases.Take(phrases.Count - 1)) + ", and " + phrases[^1]
    };

    /// <summary>
    ///     Renders a count with its noun, pluralized by adding <c>s</c>.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="singular">The singular noun, which must pluralize by suffixing <c>s</c>.</param>
    /// <returns>The rendered phrase (for example <c>48 embedded images</c>).</returns>
    /// <remarks>Kept deliberately simple because every noun it is used with is regular. Pure.</remarks>
    private static string Pluralize(int count, string singular) =>
        $"{count.ToString("N0", CultureInfo.InvariantCulture)} {singular}{(count == 1 ? string.Empty : "s")}";

    /// <summary>
    ///     Appends the failure block, but only when the extraction failed.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="report">The engine-side facts carrying the failure, if any.</param>
    /// <remarks>
    ///     The explanation is emitted verbatim so the structured failure's carefully composed,
    ///     multi-line prose reaches the reader exactly as authored.
    /// </remarks>
    private static void AppendFailure(StringBuilder builder, ExtractionReport report)
    {
        // Only an unreadable outcome carries a failure block
        if (report.Outcome != ExtractionOutcome.Unreadable || report.Failure is null)
        {
            return;
        }

        AppendHeader(builder, "Failure");
        builder.Append(report.Failure.Explanation.TrimEnd('\n')).Append('\n');
        builder.Append('\n');
    }

    /// <summary>
    ///     Appends the backend block naming the selected extractor.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="report">The engine-side facts for the selection.</param>
    /// <remarks>
    ///     Names the backend the manifest also records; when no backend was selected it says so and
    ///     points at the failure block.
    /// </remarks>
    private static void AppendBackend(StringBuilder builder, ExtractionReport report)
    {
        AppendHeader(builder, "Backend");

        var selected = report.SelectedExtractor;
        if (selected is null)
        {
            builder.Append("  No backend was selected. See \"Failure\" for the reason.\n\n");
            return;
        }

        // Name the backend that ran; selection is by format and page-rendering preference only
        builder.Append("  Selected  : ").Append(selected.Id).Append(" - ").Append(selected.DisplayName)
            .Append('\n').Append('\n');
    }

    /// <summary>
    ///     Appends the environment block describing where the extraction ran.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="environment">The environment description.</param>
    /// <remarks>
    ///     The base runtime lines are always present. Facts the backend that actually ran
    ///     contributed follow, grouped under the component that reported them, preserving first-seen
    ///     source order and fact order within each group. Grouping keeps a component's honest
    ///     statement — such as a capability it does not offer — from reading as a whole-run failure.
    ///     <para>
    ///         Availability facts for the registered backends that did <em>not</em> run are a
    ///         different thing: in a full install they are eight near-identical "available" lines
    ///         that spend a paste-in reader's token budget describing code that never executed. Only
    ///         the ones reporting an <em>un</em>availability are kept, because those can explain a
    ///         gap; the rest are collapsed into a single counted line pointing at
    ///         <c>manifest.json</c>, which carries every fact unconditionally. Nothing is lost, only
    ///         relocated.
    ///     </para>
    /// </remarks>
    private static void AppendEnvironment(StringBuilder builder, ExtractionEnvironment environment)
    {
        AppendHeader(builder, "Environment");

        // Base runtime facts are always shown so the platform is never a mystery
        builder.Append("  Operating system   : ").Append(environment.OperatingSystem)
            .Append(", ").Append(environment.ProcessArchitecture).Append('\n');
        builder.Append("  Runtime            : ").Append(environment.RuntimeVersion).Append('\n');
        builder.Append("  Runtime identifier : ").Append(environment.RuntimeIdentifier).Append('\n');

        // Keep every backend-contributed fact, and of the candidate-availability facts keep only
        // those that report something missing — the ones that could explain a gap
        var shown = environment.Facts
            .Where(fact => fact.Origin == EnvironmentFactOrigin.Backend || fact.Available != true)
            .ToList();
        var elided = environment.Facts.Count - shown.Count;

        // Contributed facts explain capability availability; state explicitly when there are none
        if (shown.Count == 0)
        {
            builder.Append("  No additional environment facts were reported.\n");
        }
        else
        {
            AppendEnvironmentFactGroups(builder, shown);
        }

        // Account for the backends this section does not list, so the omission is visible rather
        // than silent. The pointer is to --list-backends, not the manifest: the manifest records
        // only the backend that actually ran.
        if (elided > 0)
        {
            builder.Append("  ").Append(elided.ToString(CultureInfo.InvariantCulture))
                .Append(elided == 1
                    ? " other registered backend was available but did not run"
                    : " other registered backends were available but did not run")
                .Append("; run 'docdown --list-backends' to see them.\n");
        }

        builder.Append('\n');
    }

    /// <summary>
    ///     Appends contributed environment facts grouped by their contributing component.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="facts">The contributed facts in emission order.</param>
    /// <remarks>
    ///     Groups preserve first-seen source order and, within each group, emission order — the same
    ///     never-re-sorted provenance guarantee the manifest keeps. Keys are aligned within a group
    ///     so a component's facts read as one attributed block. Pure apart from the buffer.
    /// </remarks>
    private static void AppendEnvironmentFactGroups(StringBuilder builder, IReadOnlyList<EnvironmentFact> facts)
    {
        // Bucket facts by source while preserving first-seen source order and per-source order
        var groups = new List<(string Source, List<EnvironmentFact> Facts)>();
        foreach (var fact in facts)
        {
            var index = groups.FindIndex(group => string.Equals(group.Source, fact.Source, StringComparison.Ordinal));
            if (index < 0)
            {
                groups.Add((fact.Source, new List<EnvironmentFact> { fact }));
            }
            else
            {
                groups[index].Facts.Add(fact);
            }
        }

        // Render each component's facts under its own sub-heading with keys aligned within the group
        foreach (var (source, groupFacts) in groups)
        {
            builder.Append("  ").Append(source).Append(":\n");
            var keyWidth = groupFacts.Max(fact => fact.Key.Length);
            foreach (var fact in groupFacts)
            {
                var prefix = "    " + fact.Key.PadRight(keyWidth) + " : ";
                AppendWrapped(builder, prefix, fact.Value + AvailabilitySuffix(fact.Available));
            }
        }
    }

    /// <summary>
    ///     Appends the layout block listing the standard artifacts and their presence.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="sink">The sink holding the recorded images and pages.</param>
    /// <param name="content">The content-write result, or <see langword="null"/> when no content was written.</param>
    /// <remarks>
    ///     Every standard artifact is listed with a short presence phrase (a count for the resource
    ///     folders), so an absent folder is always accompanied here by an explicit statement rather
    ///     than silence. The counts are the plain facts of what was written; there is no completeness
    ///     grade.
    /// </remarks>
    private static void AppendLayout(StringBuilder builder, ExtractionSink sink, ContentWriteResult? content)
    {
        AppendHeader(builder, "Layout");
        builder.Append("  summary.txt          this file\n");
        builder.Append("  manifest.json        machine-readable form of this summary\n");
        builder.Append("  metadata.json        what the document asserts about itself\n");
        builder.Append("  content.md           ")
            .Append(content is { ContentPresent: true } ? "PRESENT" : "not present").Append('\n');
        builder.Append("  images/              ").Append(FolderCountPhrase(sink.Images.Count, "extracted")).Append('\n');
        builder.Append("  pages/               ").Append(FolderCountPhrase(sink.Pages.Count, "rendered")).Append('\n');
        builder.Append('\n');
    }

    /// <summary>
    ///     Produces the presence phrase for a resource folder from its written count.
    /// </summary>
    /// <param name="count">The number of files written into the folder.</param>
    /// <param name="verb">The past participle describing what was done (for example <c>extracted</c>).</param>
    /// <returns>A short presence phrase.</returns>
    /// <remarks>States the plain count; a folder with nothing written reads as "not present". Pure.</remarks>
    private static string FolderCountPhrase(int count, string verb) => count > 0
        ? $"PRESENT - {count.ToString(CultureInfo.InvariantCulture)} {verb}"
        : "not present - none were written";

    /// <summary>
    ///     Appends the document-metadata block, inlining the author and modified date and naming
    ///     <c>metadata.json</c> for the rest.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="metadata">The self-reported metadata, or <see langword="null"/> when the backend reported none.</param>
    /// <remarks>
    ///     Only the two orientation values a reader most often wants — who authored the document and
    ///     when it was last changed — are inlined; every other self-reported field lives in
    ///     <c>metadata.json</c>, which this block always names so the summary never becomes the place
    ///     the metadata is read from. When the document supplied neither value, that is stated in
    ///     words rather than left blank, honoring the "every absence is stated" principle. The
    ///     inlined values are read from the authored metadata, never from the manifest's possibly
    ///     derived title, so a heuristic value can never leak into this block.
    ///     <para>
    ///         The author is resolved by preferring the <c>author</c> field, falling back to
    ///         <c>creator</c>. Only the PDF backend emits an <c>author</c> field (the human author),
    ///         and for PDFs <c>creator</c> is the producing application, so preferring <c>author</c>
    ///         avoids naming the application as the author. Every OPC backend (Word, Excel,
    ///         PowerPoint, Visio) emits no <c>author</c> field and a <c>creator</c> that <em>is</em>
    ///         the author, so the fallback keeps their output byte-identical.
    ///     </para>
    /// </remarks>
    private static void AppendDocumentMetadata(StringBuilder builder, DocumentMetadata? metadata)
    {
        AppendHeader(builder, "Document metadata");

        var author = FieldValue(metadata, "author") ?? FieldValue(metadata, "creator");
        var modified = FieldValue(metadata, "modified");

        // State the two inlined values when present; when neither is present, say so explicitly
        if (author is null && modified is null)
        {
            builder.Append("  The document supplied no author or modified date.\n");
        }
        else
        {
            if (author is not null)
            {
                builder.Append("  Author          : ").Append(author).Append('\n');
            }

            if (modified is not null)
            {
                builder.Append("  Modified (UTC)  : ").Append(modified).Append('\n');
            }
        }

        builder.Append("  See metadata.json for the document's full self-reported metadata.\n");
        builder.Append('\n');
    }

    /// <summary>
    ///     Reads a single self-reported field's value by name.
    /// </summary>
    /// <param name="metadata">The metadata to read from, or <see langword="null"/>.</param>
    /// <param name="name">The field's stable camelCase name.</param>
    /// <returns>The field's value, or <see langword="null"/> when absent.</returns>
    /// <remarks>Pure; used to inline only the author and modified date into the summary.</remarks>
    private static string? FieldValue(DocumentMetadata? metadata, string name) =>
        metadata?.Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal))?.Value;

    /// <summary>
    ///     Appends the block describing what was successfully extracted.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="sink">The sink holding the recorded images and pages.</param>
    /// <param name="content">The content-write result, or <see langword="null"/>.</param>
    /// <param name="formatId">The detected format identifier, which supplies the unit noun for image provenance.</param>
    /// <remarks>
    ///     Enumerates concrete outputs: the content document's size <em>and</em> an outline of the
    ///     structure it carries, an aggregate description of the images, and the page and part
    ///     counts. When nothing was produced it says so explicitly.
    /// </remarks>
    private static void AppendWhatWasExtracted(
        StringBuilder builder, ExtractionSink sink, ContentWriteResult? content, string? formatId)
    {
        AppendHeader(builder, "What WAS extracted");

        var wroteAnything = false;

        // Content: report the character count and what the content actually contains
        if (content is { ContentPresent: true })
        {
            builder.Append("  content.md   ").Append(content.CharacterCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" characters of text.\n");
            AppendContentOutline(builder, sink.ContentFeatures);
            wroteAnything = true;
        }

        // Images: describe the set in aggregate and point at the manifest for the inventory
        if (sink.Images.Count > 0)
        {
            AppendImageSummary(builder, sink.Images, UnitNoun(formatId));
            wroteAnything = true;
        }

        // Pages: report how many were rendered
        if (sink.Pages.Count > 0)
        {
            builder.Append("  pages/       ").Append(sink.Pages.Count.ToString(CultureInfo.InvariantCulture))
                .Append(sink.Pages.Count == 1 ? " rendered page.\n" : " rendered pages.\n");
            wroteAnything = true;
        }

        // Parts: report how many part files were written
        var partCount = content?.PartPaths.Count ?? 0;
        if (partCount > 0)
        {
            builder.Append("  parts/       ").Append(partCount.ToString(CultureInfo.InvariantCulture))
                .Append(partCount == 1 ? " content part.\n" : " content parts.\n");
            wroteAnything = true;
        }

        if (!wroteAnything)
        {
            builder.Append("  Nothing was extracted.\n");
        }

        builder.Append('\n');
    }

    /// <summary>
    ///     Appends the outline of what <c>content.md</c> contains, when the backend reported any.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="features">The accumulated content features in first-reported order.</param>
    /// <remarks>
    ///     A character count tells a reader how much text there is and nothing about what it is. That
    ///     omission is not cosmetic: an agent asked to find reviewer commentary in a document whose
    ///     <c>content.md</c> carried 53 author-attributed comments never learned they existed and
    ///     answered from a different document by inference. One line naming the structural features —
    ///     those present, plus any the backend declared it looked for and found none of — costs a
    ///     handful of tokens and makes that content discoverable. The line is inventory, not verdict:
    ///     a reported zero says what the document does not contain, never what DocDown could not do.
    ///     Pure apart from the buffer.
    /// </remarks>
    private static void AppendContentOutline(StringBuilder builder, IReadOnlyList<ContentFeature> features)
    {
        // With nothing reported there is nothing to say; an empty "Contains:" line would be noise
        if (features.Count == 0)
        {
            return;
        }

        var phrases = features
            .Select(feature => $"{feature.Count.ToString("N0", CultureInfo.InvariantCulture)} {feature.AgreeingLabel}")
            .ToList();
        AppendWrapped(builder, "               ", "Contains " + JoinList(phrases) + ".");
    }

    /// <summary>
    ///     Appends the aggregate description of the extracted images, replacing the per-image
    ///     inventory that now lives only in <c>manifest.json</c>.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="images">The recorded images in allocation order; must not be empty.</param>
    /// <param name="unitNoun">The singular noun for the document's addressable unit (<c>page</c>, <c>slide</c>, or <c>sheet</c>).</param>
    /// <remarks>
    ///     <para>
    ///         One line per image is the wrong default for a file whose purpose is to be pasted into
    ///         a context window: a fifty-slide deck spent forty-eight lines on dimensions and page
    ///         numbers that the manifest already records in full, with <c>sourcePage</c>,
    ///         <c>sourcePages</c>, <c>referencedByTemplate</c>, <c>sourceRef</c>, <c>transform</c>,
    ///         and dimensions per image. What a summary reader needs is the shape
    ///         of the set — how many, how big, and where they came from — plus where to find the
    ///         rest.
    ///     </para>
    ///     <para>
    ///         Images with no unit association are explained rather than left blank: the manifest
    ///         records that a layout, master, or stencil references them (they are logos and
    ///         decorations, not content), and that distinction is surfaced here as a count so a
    ///         reader is never left wondering why a number is missing. Any remaining unattributed
    ///         image is counted separately rather than folded into that explanation, because the
    ///         reason for its absence is genuinely unknown. Pure apart from the buffer.
    ///     </para>
    /// </remarks>
    private static void AppendImageSummary(
        StringBuilder builder, IReadOnlyList<RecordedImage> images, string unitNoun)
    {
        var totalBytes = images.Sum(image => image.SizeBytes);
        builder.Append("  images/      ").Append(images.Count.ToString(CultureInfo.InvariantCulture))
            .Append(images.Count == 1 ? " embedded image, " : " embedded images, ")
            .Append(totalBytes.ToString("N0", CultureInfo.InvariantCulture)).Append(" bytes total")
            .Append(UnitSpanClause(images, unitNoun)).Append(".\n");

        // Explain the images that carry no unit number instead of leaving an unexplained absence.
        // Template-sourced ones have a known reason; any others honestly have none.
        var unplaced = images.Where(image => image.SourcePage is null).ToList();
        var fromTemplate = unplaced.Count(image => image.ReferencedByTemplate);
        if (fromTemplate > 0)
        {
            AppendWrapped(
                builder, "               ",
                $"{UnplacedSubject(fromTemplate, images.Count)} no {unitNoun} number because a layout, master, or "
                + $"stencil references {UnplacedObject(fromTemplate, images.Count)} rather than {unitNoun} content; "
                + "these are logos and decorations.");
        }

        var unexplained = unplaced.Count - fromTemplate;
        if (unexplained > 0)
        {
            AppendWrapped(
                builder, "               ",
                $"{UnplacedSubject(unexplained, images.Count)} no {unitNoun} number: the backend could not attribute "
                + $"{UnplacedObject(unexplained, images.Count)} to a {unitNoun}.");
        }

        builder.Append("               See manifest.json for the per-image inventory.\n");
    }

    /// <summary>
    ///     Produces the subject and verb naming a subset of the images that carry no unit number.
    /// </summary>
    /// <param name="subset">How many images the clause is about.</param>
    /// <param name="total">How many images there are in all.</param>
    /// <returns>A phrase such as <c>It carries</c>, <c>One of them carries</c>, or <c>18 of them carry</c>.</returns>
    /// <remarks>
    ///     "One of them" when there is only one image reads as though something else were omitted, and
    ///     "1 of them carry" reads as a defect; either quietly undermines a reader's trust in the
    ///     counts beside it. Pure.
    /// </remarks>
    private static string UnplacedSubject(int subset, int total)
    {
        if (total == 1)
        {
            return "It carries";
        }

        return subset == 1
            ? "One of them carries"
            : $"{subset.ToString(CultureInfo.InvariantCulture)} of them carry";
    }

    /// <summary>
    ///     Produces the pronoun referring back to <see cref="UnplacedSubject"/>'s subject.
    /// </summary>
    /// <param name="subset">How many images the clause is about.</param>
    /// <param name="total">How many images there are in all.</param>
    /// <returns><c>it</c> for a single image; otherwise <c>them</c>.</returns>
    /// <remarks>Kept beside the subject helper so the two can never disagree. Pure.</remarks>
    private static string UnplacedObject(int subset, int total) => total == 1 || subset == 1 ? "it" : "them";

    /// <summary>
    ///     Produces the clause naming the range of pages, slides, or sheets the images span.
    /// </summary>
    /// <param name="images">The recorded images.</param>
    /// <param name="unitNoun">The singular unit noun to use in the clause.</param>
    /// <returns>A clause such as <c>, spanning slides 3-50</c>, or an empty string when no unit is known.</returns>
    /// <remarks>
    ///     A range is the one piece of per-image provenance worth keeping in aggregate, because it
    ///     tells a reader whether the illustrations are concentrated or spread through the document.
    ///     The noun follows the format so a workbook's images are never described as spanning pages
    ///     it does not have. Stated only from what the backend actually recorded, never interpolated.
    ///     Pure.
    /// </remarks>
    private static string UnitSpanClause(IReadOnlyList<RecordedImage> images, string unitNoun)
    {
        var units = images.Where(image => image.SourcePage is not null).Select(image => image.SourcePage!.Value).ToList();
        if (units.Count == 0)
        {
            return string.Empty;
        }

        var first = units.Min().ToString(CultureInfo.InvariantCulture);
        var last = units.Max().ToString(CultureInfo.InvariantCulture);
        return first == last ? $", from {unitNoun} {first}" : $", spanning {unitNoun}s {first}-{last}";
    }

    /// <summary>
    ///     Appends the notes block listing steps DocDown attempted but could not complete.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="notes">The recorded notes in emission order.</param>
    /// <remarks>
    ///     Each note is one plain-language fact about the extraction — for example that page rendering
    ///     was requested but no renderer was available. When there are none the block states so in
    ///     words, keeping the section shape stable. These are facts about the extraction only, never
    ///     a grade of the document; an absence of content the document does not contain is described
    ///     by the inventory above, not here.
    /// </remarks>
    private static void AppendNotes(StringBuilder builder, IReadOnlyList<ExtractionNote> notes)
    {
        AppendHeader(builder, "Could not read");

        // An empty note list is stated in words rather than left blank
        if (notes.Count == 0)
        {
            builder.Append("  Nothing was left incomplete.\n");
            return;
        }

        foreach (var note in notes)
        {
            AppendWrapped(builder, "  - ", note.Message);
        }
    }

    /// <summary>
    ///     Appends a section header and an underline of matching length.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="title">The section title.</param>
    /// <remarks>Underlining with the title's exact length keeps the layout tidy for variable headers.</remarks>
    private static void AppendHeader(StringBuilder builder, string title)
    {
        // Underline exactly as wide as the title for a consistent look across fixed and dynamic headers
        builder.Append(title).Append('\n');
        builder.Append('-', title.Length).Append('\n');
    }

    /// <summary>
    ///     Appends text after a prefix, word-wrapping to stay within the column budget with a hanging
    ///     indent.
    /// </summary>
    /// <param name="builder">The buffer to append to.</param>
    /// <param name="prefix">The label prefix that begins the first line.</param>
    /// <param name="text">The text to wrap.</param>
    /// <remarks>
    ///     Continuation lines are indented to the prefix width so wrapped prose reads as one labeled
    ///     block and never exceeds the 120-column limit. A single word longer than the budget is left
    ///     intact on its own line rather than broken. Pure and side-effect free apart from the buffer.
    /// </remarks>
    private static void AppendWrapped(StringBuilder builder, string prefix, string text)
    {
        var indent = new string(' ', prefix.Length);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Start the first line with the label prefix; subsequent lines use the matching indent
        var line = new StringBuilder(prefix);
        foreach (var word in words)
        {
            // Break before a word that would overflow, but never on an empty line
            if (line.Length > indent.Length && line.Length + 1 + word.Length > WrapWidth)
            {
                builder.Append(line).Append('\n');
                line.Clear();
                line.Append(indent);
            }

            if (line.Length > indent.Length)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        builder.Append(line).Append('\n');
    }

    /// <summary>
    ///     Describes the source document with its path (or file name) and size.
    /// </summary>
    /// <param name="source">The document source.</param>
    /// <returns>A one-line description, including the byte size when known.</returns>
    /// <remarks>Prefers the path for a file source and appends the size only when it is known. Pure.</remarks>
    private static string DescribeSource(DocumentSource source)
    {
        // Prefer the concrete path; fall back to the logical file name for stream sources
        var location = string.IsNullOrEmpty(source.Path) ? source.FileName : source.Path;
        return source.SizeBytes is { } size
            ? $"{location}  ({size.ToString("N0", CultureInfo.InvariantCulture)} bytes)"
            : location;
    }

    /// <summary>
    ///     Produces the status line phrase for the header block from the outcome.
    /// </summary>
    /// <param name="outcome">The extraction outcome.</param>
    /// <returns>The status phrase, including a pointer to the relevant section.</returns>
    /// <remarks>An unreadable phrase directs the reader to the failure block. Pure.</remarks>
    private static string StatusLine(ExtractionOutcome outcome) => outcome switch
    {
        ExtractionOutcome.Produced => "PRODUCED  - the standard output layout was written.",
        ExtractionOutcome.Unreadable => "UNREADABLE  - no output could be produced; see \"Failure\"",
        _ => outcome.ToString()
    };

    /// <summary>
    ///     Produces an availability suffix for an environment fact.
    /// </summary>
    /// <param name="available">The tri-state availability flag.</param>
    /// <returns>A short parenthetical marker, or an empty string when availability is not applicable.</returns>
    /// <remarks>Makes the availability of a capability explicit without a separate column. Pure.</remarks>
    private static string AvailabilitySuffix(bool? available) => available switch
    {
        true => "  (available)",
        false => "  (NOT available)",
        _ => string.Empty
    };

    /// <summary>
    ///     Formats a timestamp as ISO-8601 UTC with second precision.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>The formatted UTC timestamp (for example <c>2026-09-10T16:33:11Z</c>).</returns>
    /// <remarks>Uses the invariant culture so the value is stable across locales. Pure.</remarks>
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
