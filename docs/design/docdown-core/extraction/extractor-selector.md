### ExtractorSelector

![Extraction Structure](ExtractionView.svg)

#### Purpose

`ExtractorSelector` chooses the best extractor for a detected format from a set of candidates and
produces a complete, auditable decision record. Its single responsibility is *selection*, expressed as
a pure function so it can be exhaustively unit-tested with hand-built candidates and options.

#### Data Model

`ExtractorSelector` is a stateless `sealed class`. It holds no fields; every value it needs is derived
from the three arguments to `Select`. It is therefore inherently thread-safe and a single instance may
be shared by concurrent extractions. The decision it produces is carried in a `SelectionResult` (the
selected descriptor or null, the mode, the required and satisfied capability sets, the full candidate
`Trace`, and any `Failure`); each per-candidate entry is a `CandidateVerdict`.

#### Key Methods

- **`SelectionResult Select(FormatDetection format, ExtractionOptions options,
  IReadOnlyList<ExtractorCandidate> candidates)`** — the selection algorithm.
  - *Preconditions*: all three arguments non-null.
  - *Guarantees*: `Selected` is non-null on success and `Failure` is non-null (with a displayable
    explanation) on failure; `Trace` always contains exactly one verdict per candidate; the result is
    a pure function of the inputs (equal inputs give equal results).
  - *Algorithm*: the seven-step ranked selection policy documented in *Extraction Subsystem Design* —
    format filter, caller override (never falls back), availability filter, required-capability
    computation, hard-requirement check, total-order ranking, and winner-plus-losers verdicts. The
    four-level total ordering (full satisfier first, then descending satisfied-capability population
    count, then descending priority, then ascending identifier) makes the winner and the trace
    independent of
    registration order.

Private helpers isolate each concern: `ResolveAvailable` applies the override or availability filter;
`ComputeRequired` derives the required capability set (`Text` always, plus image/page/explicit
requirements); `Supports` matches a descriptor to a format by identifier; `SetVerdict`/`BuildTrace`
record and order the verdicts (selected first, then by identifier); and `MakeFailure` composes the
multi-line explanation — a headline, the detected format, one indented block per candidate naming the
backend, its verdict label, and its specific reason, then a `Remedy:` line — which is what makes a
selection failure a useful message rather than a vague one.

`WellKnownPackages` is a static ordinal map from a format identifier to the DocDown package that
provides its extractor (`pdf` → `DemaConsulting.DocDown.Pdf`, `docx`/`doc` →
`DemaConsulting.DocDown.Word`, `xlsx`/`xls` → `DemaConsulting.DocDown.Excel`, `pptx`/`ppt` →
`DemaConsulting.DocDown.PowerPoint`, `vsdx`/`vsd` → `DemaConsulting.DocDown.Visio`, `html` →
`DemaConsulting.DocDown.Html`). `RemedyFor(format)` consults
it when composing the `NoExtractorForFormat` remedy, producing "No extractor is registered for
'docx'. Core does not extract this format itself; that capability comes from the separate
DemaConsulting.DocDown.Word extractor package, which a host registers with the engine." A format
absent from the table falls back to the generic "Register an extractor that supports '{id}'." — no
package name is ever invented.

The four legacy binary Office ids — `doc`, `xls`, `ppt`, `vsd` — are deliberately **absent** from
the package-naming table and are handled first, from the `LegacyBinaryFormatIds` set, by a remedy of
their own: "DocDown does not support the legacy binary Office formats, so '<id>' cannot be extracted
by any DocDown package. Only the modern XML-based Office formats are supported; re-saving the
document in its modern format makes it extractable." Detection of these formats stays, because
telling the reader what the file is beats calling it unrecognized; naming a providing package would
not, because none will ever deliver the capability. It is remedy text only — it never changes which
extractor is chosen — and it carries no install verb, so it makes no instruction that cannot
presently succeed.

The remedy states a **fact, not a command**. None of the named extractor packages is published yet,
so an imperative such as "install the DemaConsulting.DocDown.Word package" would instruct an action
that cannot succeed — a confidently-wrong output, which this project treats as a defect rather than
a rough edge. Wording it as a statement about where the capability lives keeps three properties at
once: it explains why the extraction failed, it names the class of thing that would fix it, and it
remains exactly true on the day the extractor packages are published, so no code change is needed
then. Expiring phrasing such as "not yet available" is deliberately avoided for that last reason.

This matters because Detection deliberately recognizes more formats than Core can extract: a `.docx`
handed to a Core-only host will always reach this branch, and "unsupported" without a package name
would read as "DocDown cannot do this at all". The table is a naming map only: it introduces no
project reference, no `PackageReference`, and no dependency, so Core's zero-runtime-dependency
property is unchanged.

#### Error Handling

`Select` throws `ArgumentNullException` for any null argument; those are the only exceptions it raises.
Every adverse *selection* condition is returned as a `SelectionResult` whose `Failure` is populated,
never thrown: no in-format backend (`NoExtractorForFormat`, `DD0402`), none available
(`NoAvailableExtractor`, `DD0403`), required capabilities unmet (`RequiredCapabilitiesUnavailable`,
`DD0404`), an override that does not apply (`RequestedExtractorNotApplicable`, `DD0405`), or an
override that is unavailable (`RequestedExtractorUnavailable`, `DD0406`). Each failure carries the full
candidate trace so the engine can write it into the summary and manifest. The selector performs no I/O,
so it has no I/O faults to handle.

#### Dependencies

- **FormatDetection** (Detection subsystem) — the format to satisfy.
- **ExtractionOptions**, **ExtractorCandidate**, **ExtractorCapabilities**, **SelectionResult**,
  **SelectionMode**, **CandidateVerdict**, **CandidateOutcome**, **ExtractionFailure**,
  **ExtractionFailureKind**, **DiagnosticCodes** (supporting types; see
  *Extraction Subsystem Design*).

#### Callers

`DocDownEngine` calls `Select` during the selection step of the pipeline, passing the detected format,
the effective options, and the registry's candidates. `ExtractorSelector` is also public and may be
exercised directly. See *DocDownEngine Design*.
