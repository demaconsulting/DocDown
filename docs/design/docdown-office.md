# DocDown.Office System Design

`DocDown.Office` is the Microsoft Office extraction system for the DocDown output contract. It reads
Word documents, Excel workbooks, PowerPoint presentations, and Visio drawings, and writes what it
finds through the `IExtractionSink` the engine supplies. It is a separately distributed NuGet package
that a host registers explicitly alongside `DocDown.Core`.

## Why one package

Word, Excel, PowerPoint, and Visio were four packages, four test projects, and four registration
calls for what is, to a consumer, one decision: whether this host reads Microsoft Office documents.
They share a dependency, a release cadence, and an audience. Splitting them bought a consumer nothing
but four references to keep in step, and it produced duplication the compiler could not see — two
byte-equivalent copies of the COM composition helpers, and two identical COM availability probes.

`AddOffice()` registers every backend in one call. The per-format methods remain: a service that only
ever sees spreadsheets can call `AddExcel()` and register one backend rather than six. That
granularity costs nothing to keep and would be real capability to lose.

The namespaces did not move. `DocDown.Word`, `DocDown.Excel`, `DocDown.PowerPoint`, and
`DocDown.Visio` still exist and still hold the same types. One assembly containing four namespaces is
ordinary; renaming them would have been churn that served the packaging rather than the caller.

## Architecture

The system has four format subsystems and one shared subsystem.

- **Word** — a managed Open XML SDK backend for `.docx`. See the *DocDown.Office Word Subsystem
  Design*.
- **Excel** — a managed Open XML SDK backend for `.xlsx`, including cached chart data and drawing
  annotations. See the *DocDown.Office Excel Subsystem Design*.
- **PowerPoint** — a managed Open XML SDK backend for `.pptx`, and a COM automation backend that
  renders slide images where Microsoft PowerPoint is installed. See the *DocDown.Office PowerPoint
  Subsystem Design*.
- **Visio** — a managed Open Packaging backend for `.vsdx` and `.vsdm`, and a COM automation backend
  that renders page images where Microsoft Visio is installed. See the *DocDown.Office Visio
  Subsystem Design*.
- **Com** — the two helpers the PowerPoint and Visio COM backends share, and the availability probe
  all COM backends use. See the *DocDown.Office Com Subsystem Design*.

Legacy binary formats — `.doc`, `.xls`, `.ppt` — are not supported and are reported as unreadable
rather than routed to a backend that cannot read them.

## External Interfaces

The package's entire consumer surface is its registration methods. `AddOffice` registers all six
backends; `AddWord`, `AddExcel`, `AddPowerPoint`, and `AddVisio` register one format's backends each.
Every other type is internal. No Open XML SDK type appears on the registration surface, so a host can
reference it without the SDK's types entering its compilation.

## Dependencies

- **DocDown.Core** — the output contract, the extractor abstractions, and the writers.
- **DocumentFormat.OpenXml** (OTS) — the managed Open XML SDK the Word, Excel, and PowerPoint readers
  are built on.
- **System.IO.Packaging** (OTS) — the OPC package reader the Visio backend uses directly, because a
  Visio part is not WordprocessingML, SpreadsheetML, or PresentationML and the SDK models none of
  them.

No native asset ships in this package. The COM backends reach Microsoft Office through late-bound
`IDispatch` with no interop assembly, so the package stays runtime-identifier agnostic even though
two of its backends only function on Windows.

## Risk Control Measures

- **Explicit registration.** Backends are registered from one explicit chain, with no reflection or
  assembly scanning, so single-file publishing cannot silently drop one and the active backend set
  stays a decision rather than an accident of deployment.
- **Availability is probed, never assumed.** A COM backend reports itself unavailable — cheaply, and
  without launching anything — when its application is not registered or the process is not on
  Windows. The managed backend then serves the format.
- **Self-tests read what the applications write.** Each backend's self-test extracts a document
  authored in the application whose format it reads, embedded in the package. A backend that
  synthesized its own document could only ever prove a library agreed with itself.

## Data Flow

The engine selects a backend, and the backend reads the document into a format-specific model, emits
markdown through the sink, and writes any embedded images. A COM backend additionally delegates text
extraction to its managed counterpart and answers page rendering itself, composing the delegate's
output so the delegate's "rendering not provided" statement does not contradict the rendering that
just happened.

## Design Constraints

- The package reports what a document contains and where the extracted pieces are. It does not grade,
  score, or classify that content.
- An absence the reader looked for is reported as a count, including zero. It is never reported as a
  verdict about the document.
- COM automation is Windows-only and single-instance, so the backends own the application process
  they start and terminate it if it stops responding.
