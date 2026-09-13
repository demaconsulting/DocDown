## Detection Subsystem

![Detection Structure](DetectionView.svg)

The Detection subsystem names a document's format and records how it reached that answer so
selection, the manifest, and a human reader all see the same evidence.

### Overview

Detection answers one question: *what format is this document?* It follows a fixed precedence of
signals:

1. **The file-name extension** — the primary signal.
2. **A leading-byte content signature** — the fallback used only when the name yields no recognized
   extension.

The extension is trusted deliberately. The caller already possesses the document and named it, so
Core uses that name rather than opening or unpacking the file just to second-guess it. Whatever the
result, Detection stays read-only and non-destructive: it requires a seekable stream and restores
that stream's position before returning.

Detection deliberately recognizes more formats than Core can extract by itself. That shared
vocabulary is what lets Extraction return precise failure prose such as naming the package that owns a
modern Office format or stating plainly that a legacy binary Office format is unsupported, instead of
collapsing everything into "unknown format."

The subsystem contains one software unit:

- **FormatSniffer** — identifies the format from the file-name extension and, failing that, from the
  leading content bytes. See *FormatSniffer Design*.

### Interfaces

The subsystem exposes:

- **`FormatSniffer.Detect(Stream content, string? fileName)`** — a static method returning a
  `FormatDetection`. The stream must be readable and seekable; malformed content yields `Unknown`
  rather than throwing.

It consumes only the .NET Base Class Library. The Extraction subsystem calls `FormatSniffer.Detect`
and passes the resulting `FormatDetection` to `ExtractorSelector`.

### Design

`FormatSniffer` first performs a case-insensitive extension lookup against a fixed table. A match
returns immediately with basis `Extension` and confidence `0.9`.

Only when the name yields nothing does the sniffer read up to 512 leading bytes once and test, in
order, the `%PDF-` signature and a BOM- and whitespace-tolerant HTML doctype or root element. A
match returns basis `ContentSignature`; otherwise the result is `Unknown` with confidence `0.0`.

The supporting value types remain unchanged:

- **`DocumentFormat`** — the stable format vocabulary shared across systems.
- **`DetectionBasis`** — `Extension`, `ContentSignature`, or `CallerSpecified`.
- **`FormatDetection`** — the identified format, its basis, and confidence.
