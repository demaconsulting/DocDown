# System Design

This document provides the system-level design for DocDown.Core.

![DocDown.Core Structure](DocDownCoreView.svg)

## Architecture

DocDown.Core is the shared foundation of the DocDown family. It detects a document format,
selects one registered extractor for that format, and writes a predictable scratch-folder record
showing what was extracted, where it was written, and which attempted steps could not be completed.
The system makes no acceptability judgment about document content.

The system is organized into three subsystems:

- **Detection** — names a document format from its file extension, falling back to a bounded
  leading-byte signature check. See _Detection Subsystem Design_.
- **Extraction** — holds the registered extractors, probes their availability, chooses one
  deterministically, and orchestrates one run end to end. See _Extraction Subsystem Design_.
- **Output** — prepares the scratch folder, allocates every output path, writes every artifact,
  and serializes the human- and machine-readable reports. See _Output Subsystem Design_.

Two rules govern every interaction among these subsystems:

- **The output layout is invariant.** Once Core accepts a scratch folder, it always uses the same
  root artifact names: `summary.txt`, `manifest.json`, and `metadata.json`. A produced run also
  writes `content.md`; `images/`, `pages/`, and `parts/` appear only when that run produced those
  resources, but their names and locations never vary.
- **Reporting is limited to inventory and notes.** Core reports what it extracted through the
  content inventory and reports only attempted-but-incomplete steps through plain notes. It does not
  issue verdicts, grades, remedies, or impact statements.

The public result is intentionally small: `ExtractionOutcome` is either `Produced` or `Unreadable`.
`Produced` means the standard extraction layout was written. `Unreadable` means Core could not read
the source or could not use the requested scratch folder, and the returned `ExtractionFailure`
explains why.

### Determinism is scoped to one environment

Selection and serialization are deterministic inside one environment. When the caller supplies
the extraction timestamp, two runs with the same inputs and the same available extractors
produce byte-identical `summary.txt` and `manifest.json`. Core does not claim identical content
across operating systems or across installations with different extractors available.

### A scratch-folder refusal is the one no-layout outcome

If `ScratchFolder.Prepare` refuses the target path, Core cannot write artifacts into the folder it
just declined to use. That one failure returns the requested scratch path and the paths the summary
and manifest would have used, but writes no files. Every other unreadable outcome writes the normal
root artifacts so the failure is self-describing.

### Explicit registration

Core discovers no extractors implicitly. Hosts register extractors explicitly through
`DocDownBuilder`, which keeps the library trim-safe, AOT-safe, and predictable: an extractor that a
host did not register does not participate in selection.

## External Interfaces

DocDown.Core exposes a managed .NET API only. Its principal public interfaces are:

- **`DocDownBuilder.AddExtractor` / `ConfigureDefaults` / `Build`** — host configuration and engine
  construction.
- **`DocDownEngine.ExtractAsync`** — runs one extraction and returns an `ExtractionResult`.
- **`DocDownEngine.GetBackends`** — returns the registered candidates with their cached
  availability.
- **`DocDownEngine.RefreshAvailability`** — clears cached availability so the next query re-probes.
- **`DocDownEngine.GetSelfTestCases`** — exposes Core's built-in self-tests and backend-contributed
  cases.
- **`FormatSniffer.Detect`** — returns a `FormatDetection` naming the document format and detection
  basis.
- **`IDocumentExtractor`** — the backend contract implemented by format-specific packages.
- **`IExtractionSink`** — the write-only surface handed to a backend; the only way an extractor can
  emit output.

## Dependencies

DocDown.Core has no runtime NuGet dependencies. It is implemented against the .NET Base Class
Library, with `System.Text.Json` used through a source-generated context for manifest serialization.
This keeps the library small, trim-safe, and easy for downstream tools to consume.

Build and verification use the repository's standard toolchain, including ReqStream, ReviewMark,
SysML2Tools, FileAssert, and xUnit. Those tools are build-time dependencies, not part of the runtime
surface.

## Risk Control Measures

N/A - DocDown.Core is an information-extraction library and has no safety-control partitioning
requirement under IEC 62304 §5.3.3.

The system treats untrusted document content as a security concern. File names, section titles, and
image labels can flow from the document into on-disk paths, so every write is funneled through
`ScratchFolder` and `ExtractionSink`, which enforce containment, reserved-name rejection, and
path-length bounds.

## Data Flow

A single extraction follows this fixed sequence:

1. **Snapshot options** — `DocDownEngine` clones the effective `ExtractionOptions` before any other
   work.
2. **Prepare scratch** — `ScratchFolder` accepts or refuses the target folder under the requested
   mode.
3. **Read source** — Core reads the source bytes once into a seekable buffer used for the
   manifest.
4. **Detect format** — `FormatSniffer` names the format and the basis for that decision.
5. **Enumerate candidates** — `ExtractorRegistry` returns each registered extractor paired with its
   cached availability.
6. **Select extractor** — `ExtractorSelector` keeps the format-matched, available candidates,
   prefers rendered-page providers only when pages were requested, then breaks ties by priority and
   identifier.
7. **Extract through the sink** — the selected backend writes content, images, pages, parts,
   metadata, content features, notes, and environment facts through `IExtractionSink`.
8. **Add Core-derived notes** — the engine records notes for caller-disabled image extraction, for a
   requested paginated render with no renderer, or for a renderer that produced no pages.
9. **Write artifacts** — `ContentWriter` finalizes `content.md` and any `parts/`; `MetadataWriter`
   writes `metadata.json`; `ManifestWriter` writes `manifest.json`; `SummaryWriter` writes
   `summary.txt`.
10. **Return result** — `ExtractionResult` mirrors the paths, outcome, selected extractor, failure,
    environment, and notes returned to the caller.

## Design Constraints

- **Output honesty** — the content inventory states what the extracted content contains; notes state
  only facts about attempted extraction steps that could not be completed.
- **Two-state outcomes** — the public outcome is only `Produced` or `Unreadable`; partial content is
  described by inventory counts and notes, not by a third verdict state.
- **Deterministic selection** — format, availability, optional render preference, priority, and
  identifier are the whole selection policy.
- **Zero runtime package dependencies** — no runtime NuGet closure beyond the BCL.
- **Trim and AOT safety** — no reflection-based extractor discovery and no reflection-based manifest
  serialization.
- **Path safety** — the scratch folder gate remains the sole write path and enforces containment,
  reserved-name rejection, and total-path-length bounds.
- **Portable destructive reuse** — `ScratchFolderMode.CleanIfDocDownFolder` deletes only the files a
  manifest written for that same folder accounts for; `Overwrite` is the explicit escape hatch.
