## Detection Subsystem

![Detection Structure](DetectionView.svg)

The Detection subsystem names a document's format and records how it reached that answer, so
downstream selection, the manifest, and human reviewers can all see the evidence.

### Overview

Detection answers one question — *what format is this document?* — and answers it honestly. It
follows a fixed precedence of evidence:

1. **The file-name extension** — the primary signal.
2. **A leading-byte content signature** — the fallback, consulted only when the name carries no
   extension or an extension this library does not recognize.

The extension is trusted deliberately. The caller already possesses the document and named it, so
the name is the best available statement of what the document is. Sniffing the bytes would change
only *which parser runs*, not *whether hostile bytes are parsed*, and a wrongly-named file is
rejected honestly by the decoder that receives it — which is a better place to discover the
mismatch than a heuristic in Detection. Trusting the name also means Detection never opens,
unpacks, or parses a document, so it adds no parsing attack surface and no container-handling code
to Core.

Every result carries not just a `DocumentFormat` but the `DetectionBasis` (which of the two signals
was used) and a confidence score. That evidence is written into `manifest.json`, so an extraction
record always states *how* the format was determined.

The subsystem contains one software unit:

- **FormatSniffer** — the entry point; identifies the format from the file-name extension and,
  failing that, from the leading content bytes. See *FormatSniffer Design*.

Detection deliberately recognizes more formats than Core can extract. `.docx`, `.xlsx`, `.pptx`,
`.vsdx`, and `.html`, along with the legacy binary Office formats `.doc`, `.xls`, `.ppt`, and
`.vsd`, are part of DocDown's vocabulary, and naming them is what allows the failure
"No extractor is registered for `docx`. Core does not extract this format itself; that capability
comes from the separate DemaConsulting.DocDown.Word extractor package, which a host registers with
the engine." to be stated precisely instead of collapsing to "unknown format". The legacy binary
formats instead carry a remedy that states plainly that DocDown does not support them (see
*ExtractorSelector Design*).

Detection is strictly read-only and non-destructive: it requires a seekable stream and restores the
stream's position before returning, so the same stream can be handed to an extractor afterwards. It
performs no allocation of output and touches no scratch folder.

### Interfaces

The subsystem exposes:

- **`FormatSniffer.Detect(Stream content, string? fileName)`** — a static method returning a
  `FormatDetection`. The input stream must be readable and seekable; malformed content never throws
  but yields an `Unknown` detection.

It consumes only the .NET Base Class Library (`System.IO`, `System.Text`). No archive, compression,
or XML facility is used anywhere in the subsystem. The Extraction subsystem consumes Detection's
output: `DocDownEngine` calls `FormatSniffer.Detect` and passes the resulting `FormatDetection` to
`ExtractorSelector`.

### Design

`FormatSniffer` first performs a case-insensitive lookup of the file-name extension against a fixed
table. A match returns immediately with basis `Extension` and confidence `0.9` — no byte is read at
all, so the common case costs no I/O.

Only when the name yields nothing does the sniffer read up to 512 leading bytes once and test, in
order: the five-byte `%PDF-` signature (basis `ContentSignature`, confidence `1.0`), then a
BOM-and-whitespace-tolerant HTML doctype/root-tag check (basis `ContentSignature`, confidence
`0.9`). If neither matches, the result is `Unknown` with basis `Extension` and confidence `0.0` —
an honest statement that nothing identified the document.

Extension confidence is `0.9` rather than `1.0` because the name is trusted but only the decoder can
prove the bytes agree with it. The `%PDF-` signature scores `1.0` because it is the bytes
themselves.

The unit restores the caller's stream position in a `finally` block so detection is always
non-destructive, and it is a static class because detection is now a pure function of a stream and
a name — no collaborator and no state remain, so it is inherently safe for concurrent use provided
each call is given its own stream.

#### Supporting types

The following value and enumeration types are defined by the Detection subsystem and documented here
because they have no dedicated unit design of their own. They are immutable and therefore thread-safe.

- **`DocumentFormat`** (`readonly record struct`) — Identifies a format by a short stable `Id` (for
  example `pdf`) and its IANA `MediaType`. Exposes the well-known formats `Pdf`, `Docx`, `Doc`,
  `Xlsx`, `Xls`, `Pptx`, `Ppt`, `Vsdx`, `Vsd`, `Html`, `Text`, and `Unknown` as static properties,
  a `Custom(id, mediaType)` factory, an `IsUnknown` predicate (null-safe, so a defaulted value reads
  as unknown), and a `ToString` of the form `{Id} ({MediaType})`. The `Doc`, `Xls`, `Ppt`, and `Vsd`
  descriptors name the legacy binary Office formats (each with its own IANA media type, for example
  `application/msword` and the legacy `application/vnd.visio`, distinct from the modern `.vsdx`
  type); they are detectable but not extractable. The well-known set is DocDown's shared *vocabulary*
  of formats, not a claim that Core can extract any of them. Modeled as a record struct so a format
  compares, copies, and keys a dictionary cheaply and without heap allocation.
- **`DetectionBasis`** (`enum`) — Describes the evidence behind a detection: `ContentSignature`,
  `Extension`, `CallerSpecified`. Reported alongside every detection and serialized into
  `manifest.json` so the evidence survives the extraction.
- **`FormatDetection`** (`sealed record`) — Pairs the identified `Format` with its `Basis` and a
  `Confidence` score in `[0.0, 1.0]`. Provides `Describe()`, a one-line explanation such as
  `pdf (application/pdf) - detected by file extension`.
