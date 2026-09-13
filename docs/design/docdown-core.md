# System Design

This document provides the system-level design for DocDown.Core.

![DocDown.Core Structure](DocDownCoreView.svg)

## Architecture

DocDown.Core is the shared foundation of the DocDown family: it defines the output contract and
implements the full extraction pipeline against the .NET Base Class Library, with no format-specific
backend of its own beyond a plain-text path. Format-specific extractors (PDF, Office, and so on) are
supplied by separate packages and registered explicitly.

The system is organized into three subsystems, each a distinct architectural boundary with its own
public surface:

- **Detection** — identifies a document's format from its file-name extension, falling back to a
  leading-byte content signature, and reports the evidence behind the identification. See
  _Detection Subsystem Design_.
- **Extraction** — registers backends, negotiates capabilities, selects the best available extractor
  by a deterministic ranking, and orchestrates the pipeline end to end. See
  _Extraction Subsystem Design_.
- **Output** — the sole write path: it prepares the scratch folder, allocates and contains every
  path, writes `content.md`, images, pages and parts, serializes `summary.txt` and `manifest.json`,
  and verifies the result against the contract. See _Output Subsystem Design_.

Data flows in one direction — Detection feeds Extraction, which drives Output — and the three
subsystems collaborate only through immutable value types. The `DocDownEngine` unit in Extraction is
the public facade that runs the pipeline; the `ExtractionSink` unit in Output is the only component an
extractor is ever handed, so an extractor never sees a filesystem path or a `System.IO` output API.

### The invariant this system exists to guarantee

> **The output LAYOUT is invariant. The output CONTENT is not.**
>
> `summary.txt`, `manifest.json`, `content.md`, `images/` and `pages/` always have the same names and
> the same shape, on every platform, for every format, for every outcome including failure. What they
> _contain_ legitimately varies with operating system, installed applications and available native
> binaries. A `.docx` on Windows with Word may yield rendered pages and richer structure than the
> same `.docx` on Linux. **That is correct behavior, not a defect.**
>
> The second invariant is **honesty**: everything absent or partial is enumerated, with a reason.

Honesty also governs the fields that _are_ present. Every claim the manifest makes must be one a
consumer can act on, so no field is allowed to be decorative. Image provenance is the sharpest case:
each extracted image records whether its bytes are the document's own, written unchanged, or a new
encoding the backend produced by decoding and re-encoding the source samples. A manifest that
described the latter as the former would be false in the machine-readable artifact, and would
undermine confidence in every other field alongside it — so the claim is drawn from a closed
vocabulary, reported by the backend that alone knows the answer, and defaulted rather than guessed.

This wording is stated so bluntly on purpose, and the bluntness is a design control, not editorial
emphasis. The natural instinct of a future implementer is to write a cross-platform test that asserts
two machines produce equal output, watch it fail, and then "fix" the product to satisfy that test —
thereby breaking the one property that must never churn. **It is easy for a later implementer to
build the wrong tests. This document says so explicitly so that the wrong test is recognized as
wrong.** A cross-platform equality assertion is a defect in the test, not in the product. The
verification strategy therefore splits environment-invariant properties (the layout, the honesty
reconciliation) from environment-dependent ones (the content), and only the former may be asserted
across environments.

### Determinism is scoped to an environment

Determinism is a property _within_ a single environment, never _across_ environments. Output is
byte-exact between two runs only when the caller supplies `ExtractionOptions.TimestampUtc`; without
it, `summary.txt` and `manifest.json` differ only in the single wall-clock timestamp line. Two runs on
different operating systems, or with different backends installed, are expected to differ in content
and that difference is not a determinism violation. The determinism claim is deliberately _not_ a
cross-platform claim.

### A failed extraction still writes the full layout

A failed extraction is not an empty folder. Every failure returns the complete artifact layout,
including a `summary.txt` whose `Failure` section reproduces the failure explanation verbatim and a
`manifest.json` whose `failure` object carries the same structured detail. The LLM-facing contract has
no hole where failures live: an unreadable source, an unrecognized format, a missing backend, or a
backend that threw all produce a self-describing layout that explains what went wrong and how to
remedy it.

There is exactly one documented exception. When the scratch folder itself is refused
(`ExtractionFailureKind.ScratchFolderRefused`, code `DD0501`), Core cannot write `summary.txt` or
`manifest.json` into a folder it just declined to use. That single failure is returned with
`ScratchFolder` set to the requested absolute path and `SummaryPath`/`ManifestPath` set to the paths
that _would_ have been used, but no files are written — because writing them would contradict the very
refusal being reported. Every other failure writes the full layout.

### Backends are co-equal and capability-differentiated

The architecture deliberately does **not** encode a primary/fallback hierarchy among backends. Two
extractors that support the same format are co-equal candidates differentiated only by their declared
capabilities, their effective (environment-probed) capabilities, and an integer priority used solely
as a tie-break. There is no privileged "default" backend and no hard-coded fallback chain; selection
is a pure ranking over the candidate set (see _Extractor Selector Design_). This keeps the model open
to backends the authors have never seen and prevents a hidden preference from silently overriding a
higher-fidelity option.

### Explicit registration

Extractors are registered explicitly through `DocDownBuilder.AddExtractor`. Core uses **no reflection,
no assembly scanning, no `[ModuleInitializer]`**, and no other implicit discovery mechanism. This is a
hard architectural constraint, not a convenience: it keeps the `docdown` command-line tool
single-file-publish, trim, and ahead-of-time (AOT) safe, because there is no runtime type discovery
for the trimmer to defeat. A backend that is not registered simply does not exist as far as the engine
is concerned, and that absence is reported honestly.

## External Interfaces

DocDown.Core exposes a managed .NET API; it has no network, file-format, or process interfaces of its
own beyond reading a source document and writing the scratch-folder layout. The principal public
entry points are:

- **`DocDownBuilder.AddExtractor` / `ConfigureDefaults` / `Build`** — Inbound; fluent method calls;
  non-null arguments; duplicate extractor ids rejected at `Build`.
- **`DocDownEngine.ExtractAsync`** — Inbound/Outbound; async method call returning `ExtractionResult`;
  non-null, non-empty `source`/`documentPath` and `scratchFolder`.
- **`DocDownEngine.GetBackendStatus`** — Outbound; returns `IReadOnlyList<BackendStatus>`; reflects the
  last availability probe.
- **`DocDownEngine.GetSelfTestCases`** — Outbound; returns `IReadOnlyList<SelfTestCase>`.
- **`FormatSniffer.Detect`** — Inbound/Outbound; method call returning `FormatDetection`; requires a
  readable, seekable stream.
- **`IDocumentExtractor`** — Inbound; the interface contract implemented by external backend packages.
- **`IExtractionSink`** — Outbound; the interface contract handed to backends; the only surface an
  extractor may write through.
- **`ContractVerifier.Verify`** — Inbound/Outbound; static method returning
  `IReadOnlyList<ContractViolation>`; requires a readable scratch-folder path.
- **Scratch-folder layout** — Outbound; `summary.txt`, `manifest.json`, `content.md`, `images/`,
  `pages/`, and `parts/` with fixed names and shapes (the output contract).

The two interfaces a third-party backend author implements or consumes are `IDocumentExtractor` (the
backend contract) and `IExtractionSink` (the write surface). Both are detailed in
_Extraction Subsystem Design_ and _Output Subsystem Design_.

## Dependencies

DocDown.Core has **zero runtime NuGet dependencies** — it is implemented exclusively against the .NET
Base Class Library. `System.Text.Json` is used for manifest serialization and is in-box on `net8.0`
and later, so it adds no external package reference. This zero-dependency posture is a deliberate
design constraint: it keeps the library trivial to trim and AOT-compatible and gives consuming
tools a minimal transitive closure.

The following OTS items are used to build and verify the system (not consumed at runtime); see
_OTS Integration Design_ and each item's dedicated design document for details:

- **BuildMark** — generates build-notes documentation; see _BuildMark Design_
- **FileAssert** — validates generated documents against acceptance criteria; see _FileAssert Design_
- **Pandoc** — converts Markdown documentation to HTML; see _Pandoc Design_
- **ReqStream** — enforces requirements-to-test traceability; see _ReqStream Design_
- **ReviewMark** — enforces file review coverage and currency; see _ReviewMark Design_
- **SarifMark** — converts CodeQL SARIF results to markdown; see _SarifMark Design_
- **SonarMark** — generates SonarCloud quality reports; see _SonarMark Design_
- **SysML2Tools** — lints the architecture model and renders the design diagrams; see
  _SysML2Tools Design_
- **VersionMark** — captures and publishes tool-version information; see _VersionMark Design_
- **WeasyPrint** — converts HTML documentation to PDF; see _WeasyPrint Design_
- **xUnit** — executes unit and integration tests; see _xUnit Design_

## Risk Control Measures

N/A - DocDown.Core extracts information from documents into a scratch folder and has no
safety-critical functionality requiring risk-control segregation (IEC 62304 §5.3.3). The subsystem
boundaries described under _Architecture_ exist for maintainability and testability, not for hazard
segregation.

The library does, however, treat untrusted document content as a security concern rather than a safety
one. Image names, part names, sheet names, slide titles, and (in a later phase) embedded attachment
file names all originate in untrusted content and are used to derive on-disk paths. All such path
allocation is funneled through a single containment gate (the `ScratchFolder` unit) that enforces
directory containment, rejects reserved device names, and bounds path length; see _ScratchFolder
Design_ and the _Design Constraints_ security note below.

## Data Flow

A single extraction flows through the pipeline in a fixed order:

1. **Input** — a `DocumentSource` (a file path or a named stream) and a target scratch-folder path,
   with optional `ExtractionOptions`.
2. **Options snapshot** — the effective options are cloned on entry so later caller mutation cannot
   affect this run (see the C5 constraint below).
3. **Scratch preparation** — the Output subsystem resolves, contains, and prepares the scratch folder
   per the requested mode. A refusal short-circuits to a `ScratchFolderRefused` failure (the one case
   with no writable layout).
4. **Source read** — the source bytes are read and hashed (SHA-256). An I/O fault becomes a
   `SourceUnreadable` failure with the full layout written.
5. **Detection** — the Detection subsystem names the format from the file-name extension, falling back
   to the leading bytes, and returns a `FormatDetection`. An unrecognized format becomes a
   `FormatNotRecognized` failure with the full layout written.
6. **Selection** — the Extraction subsystem ranks the registered candidates for the detected format. A
   selection failure (no backend, none available, capabilities unavailable, override not applicable)
   is returned with the full layout written and a complete candidate trace.
7. **Extraction** — the selected backend is invoked with only the `DocumentSource` and an
   `IExtractionContext`. A thrown exception (other than cancellation, which propagates) becomes an
   `ExtractorFailed` failure with the full layout written.
8. **Gap derivation and reconciliation** — Core adds the gaps it knows about that the backend cannot
   report, builds the completeness ledger, and reconciles it so every partial or absent artifact is
   explained by a matching gap.
9. **Serialization** — `content.md`/`parts/` are finalized, then `manifest.json`, then `summary.txt`.
10. **Output** — an `ExtractionResult` is returned, mirroring on disk exactly what the caller receives
    in memory.

## Design Constraints

- **Layout invariance** — the output layout is fixed and identical for every format, platform, and
  outcome; only content varies. This is the load-bearing constraint of the whole product (see
  _Architecture_).
- **Honesty** — every absence or partial result is enumerated with a reason; a missing folder is never
  an implicit signal. Core synthesizes an explaining gap (diagnostic `DD0701`) if a backend leaves an
  absence unexplained, so a silent hole is structurally impossible.
- **Zero runtime dependencies** — implemented against the BCL only; `System.Text.Json` is in-box.
- **Trim/AOT safety** — explicit registration and source-generated JSON serialization
  (`DocDownJsonContext`) keep the product single-file-publish, trim, and AOT safe.
- **Determinism scoped to an environment** — byte-exact between runs when `TimestampUtc` is supplied;
  never claimed across environments.

### Options thread safety (Correction C5)

`ExtractionOptions` is a mutable configuration object: it has public setters and a `Clone()` method.
Because a caller may hold and later mutate the same options instance, the engine **clones the effective
options before any other work** on entry to `ExtractAsync` (`options?.Clone() ?? _defaults.Clone()`),
and `DocDownBuilder.Build()` likewise clones the configured defaults into the engine. As a result,
mutating an options object after the call returns — or from another thread while a call is in flight —
cannot affect an in-flight or subsequent extraction. Each extraction runs against a private snapshot.
This is stated in the XmlDoc of both `ExtractionOptions` and `DocDownEngine.ExtractAsync` and is a
design constraint, not an incidental implementation detail. Consequently the engine is safe for
concurrent `ExtractAsync` calls that target _different_ scratch folders; two concurrent calls into the
_same_ scratch folder are a caller error and are not supported.

### Path safety (Correction C3)

Containment is necessary but **not sufficient**. Promoting a `SafePathCombine`-style containment check
into Core catches directory traversal, but it catches neither the Windows reserved-device-name hazard
(`CON`, `NUL`, `LPT1`, …) nor the total-path-length hazard. The `ScratchFolder` unit therefore layers
three distinct controls — containment, an explicit reserved-name check on all platforms, and an
explicit total-path-length bound — and this document states plainly that containment alone would give
false confidence. See _ScratchFolder Design_ for the full treatment.

### Platform Support

The library targets the following frameworks, enabling broad compatibility across supported .NET
runtimes:

| Target Framework | Runtime / Environment |
|------------------|-----------------------|
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL) APIs
available across all target frameworks. No platform-specific native interop, OS-specific APIs, or
framework-version-specific features are used. The reserved-device-name and total-path-length checks in
the Output subsystem run on _all_ platforms — including Linux — so that output produced on one
operating system stays portable to Windows, where those names and lengths are hard errors.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution
- **Explicit backend registration**: Backends are added through `DocDownBuilder`; no reflection or
  assembly scanning is used, preserving single-file, trim, and AOT publishing
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Review Management**: Systematic file review using ReviewMark patterns
