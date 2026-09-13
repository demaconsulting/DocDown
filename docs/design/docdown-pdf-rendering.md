# DocDown.Pdf.Rendering System Design

![DocDown.Pdf.Rendering Structure](DocDownPdfRenderingView.svg)

`DocDown.Pdf.Rendering` is the optional PDF page-rendering backend for the DocDown output contract.
It produces the same text, embedded images, and document metadata the managed PDF backend produces,
and adds the one thing that backend cannot: raster images of the pages themselves. It is a
separately distributed, opt-in NuGet package that a host registers explicitly alongside
`DocDown.Core` and `DocDown.Pdf`.

It is also the first and only DocDown package that carries native binaries. That single fact shapes
its design, its packaging, and the honesty obligations it must meet, and it is why page rendering is
a separate package rather than part of `DocDown.Pdf`, which stays fully managed and
runtime-identifier agnostic.

## Architecture

The system is flat: it has no subsystems, because there is one architectural boundary here —
rasterization — rather than several. Three units divide the work.

- **PdfPageRenderingExtractor** is the backend the engine selects and invokes. It exposes the stable
  PDF-rendering identity this package uses for selection and reporting; answers the cheap
  availability probe that reports
  rendered-page support when the native stack is usable here; delegates the managed aspects to the
  base PDF backend; drives the rasterization of the requested pages; reports a plain note when a
  page it attempted could not be rasterized; and returns `Produced` unless the delegated managed
  extraction reports `Unreadable`.
- **PageRenderer** is the single native-interop seam. It rasterizes one page to a PNG behind a
  process-wide lock, and answers a cheap, non-throwing question about whether the native stack can
  load. It is the only place a PDFtoImage, PDFium, or SkiaSharp type appears.
- **PdfRenderingDocDownBuilderExtensions** is the one visible edge from a host to this package: the
  single `AddPdfRendering` call that registers the backend. It carries no native-rasterizer type on
  its surface, so a host can reference it without those types entering its own compilation.

### The single-backend selection problem, and its resolution

The engine selects exactly one backend for an extraction and runs only that backend. A rendering
backend that produced only raster images would therefore leave the managed PDF backend's text,
embedded-image, and metadata work undone. So the rendering backend cannot be a "pages-only" add-on
layered onto the base backend: once selected, it must deliver the same managed artifacts the base
backend delivers and add rendered pages on top.

It delivers the first three not by re-implementing them but by **delegating to a
`PdfDocumentExtractor`** with a cloned options object whose `RenderPages` is forced off. The managed
backend writes the content, the images, the document info, and its own notes; because rendering is
suppressed on the delegate, it does not attempt rendered-page output itself. This backend then
rasterizes the requested pages and adds them. The one honest artifact of the delegation is that the
managed backend's `pdf.pageRendering = not provided by this extractor` environment fact still appears
in a run that did render — which is literally true of the managed inner backend and is complemented,
not contradicted, by this backend's own `pages.renderer` fact.

Selection learns that this backend can satisfy a render request from
`ProbeAvailability()`. When the native stack can load, the probe returns
`ExtractorAvailability.Available(providesRenderedPages: true)`; otherwise it returns
`ExtractorAvailability.Unavailable(reason)`. That is the only selection-time statement this package
makes about rendered pages.

### The division of honesty between Core, the managed backend, and this package

Core knows the selection-time picture: which backends were registered, which one was chosen, and
whether any available backend in this environment could render pages. The managed backend knows what
a PDF reader knows: which encoding an image used and whether the pages carried glyphs. This package
supplies the third layer — the facts only a rasterizer knows.

If the native stack cannot load for the current runtime identifier, this package reports that only
through `ProbeAvailability()`, and Core handles the selection consequence before `ExtractAsync` runs.
If rendering begins and a specific page cannot be rasterized, this package reports one short factual
note — `Page N could not be rasterized.` — because it attempted that step and could not complete it.
The note stays limited to that extraction fact itself.

## External Interfaces

| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `IDocumentExtractor` | Inbound, from the engine | .NET interface | See the probe obligations below |
| `ISelfValidating` | Inbound, from the engine | .NET interface | Enumeration must be cheap |
| `IExtractionSink` | Outbound, to Core | .NET interface | The only output channel |
| `PdfDocumentExtractor` | Outbound, to DocDown.Pdf | .NET class | Delegated to with rendering suppressed |
| `DocDownBuilder` | Inbound, from a host | .NET extension method | `AddPdfRendering` is the whole surface |
| Source document | Inbound | PDF byte stream | Not guaranteed seekable; buffered once |

- **`IDocumentExtractor`** is implemented by `PdfPageRenderingExtractor`. `ProbeAvailability` must be
  well under 50 ms, side-effect free, must not open the document, and must not throw; it answers a
  cached native-loadability check, returns `Available(providesRenderedPages: true)` only when page
  rendering is usable here, and never rasterizes.
- **`ISelfValidating`** enumeration is cheap; the render round-trip work happens only when the case's
  delegate is invoked.
- **`IExtractionSink`** is the only output channel; no filesystem path is ever constructed here.
- **`PdfDocumentExtractor`** is constructed and driven directly to produce the managed aspects, so
  the managed extraction is a single source of truth rather than a re-implementation.
- **`DocDownBuilder`** is extended by exactly one method, `AddPdfRendering`.
- **The source document** is buffered before use because both the managed backend and the native
  renderer read it from the start, and a stream source is read-once and possibly non-seekable.

No PDFtoImage, PDFium, or SkiaSharp type appears on any public interface. That containment is
machine-enforced by a reflection test over the package's exported types.

## Dependencies

- **DocDown.Core** — the extraction contract, the sink, the options, and the output layout.
- **DocDown.Pdf** — the managed text/embedded-image/metadata extractor this package delegates to.
  This is a project reference, not a native dependency; it adds no native asset.
- **PDFtoImage** (OTS) — the managed rasterization API, wrapping PDFium (native renderer) and
  SkiaSharp (native 2D/PNG-encode backend). Confined to `PageRenderer` and absent from the public
  API. See *PDFtoImage*, *PDFium*, and *SkiaSharp* under the OTS integration design.

## Risk Control Measures

- **Native containment.** PDFtoImage, PDFium, and SkiaSharp types appear in exactly one file
  (`PageRenderer.cs`) and in no public signature. A reflection test fails the build if any reaches
  the exported surface, so the native stack stays confined and replaceable.
- **Portability of the other packages.** `DocDown.Core`, `DocDown.Pdf`, and `DocDown.Tool` remain
  free of native assets and runtime-identifier-specific dependencies. That `DocDown.Pdf` ships no
  native asset is asserted against its produced `.nupkg`, not merely its build output, precisely
  because this package now exists to carry the native stack instead.
- **Per-page fault isolation.** Each page is rasterized independently. An unsupported page, an
  out-of-memory at a high DPI, or a native fault mid-run is caught per page, recorded as the plain
  note `Page N could not be rasterized.`, and the run continues with the remaining pages. No render
  fault reaches the caller as an exception.
- **Cheap, honest availability.** The probe loads the native once, caches the result, and never
  rasterizes, so a plain extraction pays nothing and a missing native binary is reported with a
  reason rather than discovered at render time or as a crash.
- **Serialized native calls.** PDFium is not thread-safe, so every native call runs under a single
  process-wide lock; concurrent extractions rasterize one at a time rather than corrupting shared
  native state.

## Data Flow

1. The engine selects this backend when a PDF is detected and page rendering is requested, and calls
   `ExtractAsync` with a context exposing the options, the sink, and a cancellation token.
2. `PdfPageRenderingExtractor` buffers the source once, then delegates to a `PdfDocumentExtractor`
   with a cloned options object whose `RenderPages` is off. The managed backend writes the content,
   images, metadata, inventory, and its own notes. If the delegated extractor reports
   `Unreadable`, this backend returns `Unreadable` and does not attempt page rendering. Because
   rendering is suppressed on the delegate and selection already handled ordinary renderer
   unavailability, this backend adds no "renderer unavailable" note of its own.
3. It records the authoritative `pages.renderer` environment fact.
4. It selects the pages to render, honoring any requested range, and for each renders a PNG through
   `PageRenderer` and writes it through the sink, named by its document page number. A per-page fault
   becomes the note `Page N could not be rasterized.`; the run continues.
5. It returns `Produced` after the attempted page loop completes.
6. The engine finalizes the content and writes `summary.txt` and `manifest.json`.

## Design Constraints and Native-Binary Consequences

This package's native stack has consequences that a reader of this system's constraints needs stated
plainly and completely.

- **PDFtoImage is chosen over the Phase-1 plan's lean toward `PdfPig.Rendering.Skia`, deliberately.**
  Three concrete reasons. First, `PdfPig.Rendering.Skia` pulls in the three PdfPig filter add-ons
  (`PdfPig.Filters.Dct.JpegLibrary`, `PdfPig.Filters.Jbig2.PdfboxJbig2`,
  `PdfPig.Filters.Jpx.OpenJpeg`) that `DocDown.Pdf` deliberately and with documented intent does not
  reference; adopting it would silently reverse that decision. Second, it publishes no `net10.0`
  asset, its stable line tops out below its current prerelease, and it lags SkiaSharp's major
  version. Third, PDFtoImage resolves to eight packages with complete, automatic runtime-identifier
  coverage, whereas the Skia option resolves to thirteen or more and is missing
  `HarfBuzzSharp.NativeAssets.Linux`, leaving Linux runtimes without `libHarfBuzzSharp` unless a
  fourteenth package is hand-added. PDFtoImage ships real `net8.0`, `net9.0`, and `net10.0` assets,
  matching this repository's target frameworks exactly.
- **Single-file publish is always runtime-identifier specific.** A portable single binary cannot
  select the per-runtime native assets, so `dotnet publish -r <rid>` is mandatory for a
  self-contained single-file `docdown`, and a standalone distribution needs a **per-RID publish
  matrix** (win-x64/arm64, linux-x64/arm64, osx-x64/arm64). Documenting that matrix and its recipe is
  in scope; adding the CI workflow that produces it is a separable, later increment.
- **`IncludeNativeLibrariesForSelfExtract=true` is a genuine single file, but with costs.** It
  extracts the natives to `%TEMP%\.net` (Windows) or `$HOME/.net` (Unix) at startup, which **breaks
  under systemd where `$HOME` is undefined**, adds first-run latency, and needs a writable directory
  (bad for a read-only container). It is a tradeoff to choose deliberately, not a default.
- **The `dotnet tool` package stays runtime-identifier agnostic.** `PackAsTool` produces one
  RID-agnostic package; a framework-dependent tool resolves `runtimes/<rid>/native` from the package
  at run time via `deps.json`, so `dotnet tool install -g` works on every runtime identifier from one
  (larger) package. The per-RID matrix is required only for the optional self-contained single-file
  executable, which is a different artifact.
- **PDFium is not thread-safe.** PDFtoImage serializes every call into PDFium behind a lock, so only
  one document can be rasterized at a time in a process; `PageRenderer` mirrors this with its own
  process-wide lock. Concurrent extractions cannot rasterize in parallel.
- **Trim and AOT compatibility are unverified for this stack.** They are not claimed. `PublishTrimmed`
  and AOT are left off on the rendering-inclusive tool, and this constraint is recorded rather than
  worked around; explicit, reflection-free registration keeps single-file publish viable regardless.
- **No `<RuntimeIdentifier>` in the library project.** The package stays RID-agnostic; only its
  transitive natives are RID-specific, and they are resolved by consumers.
