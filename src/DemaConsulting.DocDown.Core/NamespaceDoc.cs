namespace DocDown.Core;

/// <summary>
/// The extraction contract every DocDown backend implements and the machinery that runs it:
/// format detection, backend registration and selection, the caller-facing options, and the
/// fixed output layout (<c>summary.txt</c>, <c>manifest.json</c>, <c>content.md</c>,
/// <c>images/</c>, <c>pages/</c>). Start at <c>DocDownBuilder</c> to register backends and build
/// a <c>DocDownEngine</c>, which selects one <c>IDocumentExtractor</c> per document and gives it
/// an <c>IExtractionSink</c> — the sole write path to the scratch folder. This package carries no
/// format-specific code; add <c>DocDown.Pdf</c>, <c>DocDown.Word</c> and the other backend
/// packages for the formats you need.
/// </summary>
/// <remarks>
/// The common pattern is four steps, and almost every caller writes exactly these:
/// <list type="number">
///   <item><description>
///     Build once. <c>new DocDownBuilder()</c>, one <c>Add…()</c> call per backend package you
///     reference (<c>AddPdf</c>, <c>AddWord</c>, <c>AddExcel</c>, <c>AddPowerPoint</c>,
///     <c>AddVisio</c>, and <c>AddPdfRendering</c> for PDF page images), then <c>Build()</c>. The
///     resulting <c>DocDownEngine</c> is immutable and safe to reuse and to share across threads.
///   </description></item>
///   <item><description>
///     Extract. <c>ExtractAsync</c> takes the document (a path or a <c>DocumentSource</c>), the
///     scratch folder to write into, optional <c>ExtractionOptions</c>, and a
///     <c>CancellationToken</c>. Adverse conditions are returned on the result rather than thrown.
///   </description></item>
///   <item><description>
///     Branch on <c>ExtractionResult.Outcome</c>. <c>Produced</c> means the invariant output layout
///     was written; <c>Unreadable</c> means the document or scratch folder could not be read and
///     <c>Failure.Explanation</c> says why.
///   </description></item>
///   <item><description>
///     Read the result. <c>SummaryPath</c> is the <c>summary.txt</c> to hand to a model;
///     <c>ManifestPath</c>, <c>ContentPath</c>, <c>ImagePaths</c>, and <c>PagePaths</c> address the
///     rest of the layout. <c>Notes</c> may carry short factual messages even on a produced
///     extraction — they describe work DocDown attempted but could not finish.
///   </description></item>
/// </list>
/// </remarks>
/// <example>
/// The smallest program that does something useful — register one backend, extract, and report.
/// <code language="csharp">
/// using System;
/// using System.Threading;
/// using DocDown.Core;
/// using DocDown.Excel;
///
/// var engine = new DocDownBuilder()
///     .AddExcel() // .xlsx - cells, formulas, charts; workbooks are never rendered
///     .Build();
///
/// var result = await engine.ExtractAsync(
///     "workbooks/sample-budget.xlsx",
///     "scratch/sample-budget",
///     options: null,
///     cancellationToken: CancellationToken.None);
///
/// if (result.Outcome == ExtractionOutcome.Produced)
/// {
///     Console.WriteLine(result.SummaryPath);
/// }
/// else
/// {
///     Console.Error.WriteLine(result.Failure?.Explanation);
/// }
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
