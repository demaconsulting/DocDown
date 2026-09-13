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
internal static class NamespaceDoc
{
}
