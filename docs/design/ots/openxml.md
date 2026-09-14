## DocumentFormat.OpenXml

### Purpose

`DocumentFormat.OpenXml` is the managed Open XML SDK `DocDown.Word` is built on. It was chosen
because it is 100% managed with no native assets, MIT-licensed, and compatible with this
repository's MIT license, and because it is the canonical reader for the `.docx` package format —
authoritative for both the OPC container it opens and the Wordprocessing schema its DOM projects.
That combination is the single property that lets the Word package be deployed anywhere the .NET
runtime is, without a per-platform build and without a native binary to locate at run time.

It provides package opening, part enumeration and relationship resolution, a strongly-typed DOM
over Wordprocessing markup, the document properties (title, author, extended properties), and a
writer used to build the parse round-trip probe document.

### One OTS item, not two

`DocumentFormat.OpenXml` and its `DocumentFormat.OpenXml.Framework` companion are recorded as a
**single** OTS item, not two. The governing rule is *one OTS item per independently-sourced
component*, and the two packages fail every independence test that would make them two:

- They ship together from one maintainer (Microsoft) under one MIT license.
- `DocumentFormat.OpenXml.Framework` is an implementation split of the same project — a
  factoring introduced by upstream so the base object model is available separately, referenced
  transitively by the main package on every restore.
- They are versioned in lockstep. A restore of `DocumentFormat.OpenXml 3.5.1` produces
  `DocumentFormat.OpenXml.Framework 3.5.1`; the two do not float relative to each other.
- Neither ships a native asset; both are `runtimes/`-free managed assemblies.

Compare with `PdfPig` (its own item), `PDFium` (its own item), and `SkiaSharp` (its own item)
under the OTS integration design: each of those is independently sourced under a different
maintainer or with a different license, and each of PDFium and SkiaSharp carries its own set of
per-RID native asset packages. Treating each as its own OTS item reflected genuine independence.
Treating OpenXml plus its Framework companion as one item reflects the equally genuine fact that
they are one component with an implementation seam, not two components.

### Features Used

- `WordprocessingDocument.Open(Stream, bool)` and `WordprocessingDocument.Create(Stream,
  WordprocessingDocumentType)` — opening a package read-only for extraction, and building the
  self-test probe document
- `WordprocessingDocument.MainDocumentPart`, `HeaderPart`, `FooterPart`, `ImagePart`,
  `NumberingDefinitionsPart`, `FootnotesPart`, `WordprocessingCommentsPart`,
  `ExtendedFilePropertiesPart`, and the `PackageProperties` (`Title`, `Creator`) surface — the
  full part enumeration and metadata access
- `OpenXmlPartContainer.GetPartById` — relationship resolution scoped to a container, used by
  `WordOpenXmlImageReader.Resolve` to pick up a header's or footer's own image parts
- The `DocumentFormat.OpenXml.Wordprocessing` type namespace (aliased `W`) — `Body`, `Paragraph`,
  `Run`, `Text`, `Break`, `TabChar`, `Hyperlink`, `SimpleField`, `FieldChar`, `FieldCode`,
  `InsertedRun`, `DeletedRun`, `Table`, `TableRow`, `TableCell`, `TableHeader`, `GridSpan`,
  `VerticalMerge`, `NumberingProperties`, `AbstractNum`, `Level`, `NumberingInstance`,
  `FootnoteReference`, `Footnote`, `Comment`, `SectionProperties`, `HeaderReference`,
  `FooterReference`, `ParagraphStyleId`, `OutlineLevel`, `Drawing`, `OnOffType`
- The `DocumentFormat.OpenXml.Drawing` namespace (aliased `A`) — `Blip`, from which the `r:embed`
  relationship id is read
- The `DocumentFormat.OpenXml.Drawing.Wordprocessing` namespace (aliased `WP`) — `DocProperties`,
  from which a drawing's preferred name is read
- `ImagePart.ContentType`, `ImagePart.Uri`, `ImagePart.GetStream(FileMode, FileAccess)` — the
  bytes-and-provenance surface for embedded images

The DOM is walked through the strongly-typed `ChildElements` sequence and the `Elements<T>`
enumerable; `InnerXml` is never materialized, so memory scales with the document rather than
with a serialized copy of it.

### Integration Pattern

**Runtime dependency, transitive to consumers.** OpenXml is a real runtime dependency of the
`DocDown.Word` package and flows to consumers, and it is confined to the OpenXml subsystem —
`WordOpenXmlExtractor`, `WordOpenXmlReader`, and `WordOpenXmlImageReader` are the only files that
name an SDK type. `WordDocDownBuilderExtensions` deliberately carries no SDK type on its public
surface, so a host referencing the registration seam does not pull the SDK's types into its own
compilation.

**Version pinning — reproducibility, not fragility.** The package reference is pinned to the
exact version range `[3.5.1]` (see the comment in
`src/DemaConsulting.DocDown.Office/Word/DemaConsulting.DocDown.Word.csproj`). The pin's stated purpose
is restore determinism and SBOM reproducibility: a floating reference would let a restore
substitute a different patch, which would change the resolved dependency set recorded in the
generated SBOM and break build reproducibility. This is **not** an API-fragility pin — the SDK
follows semantic versioning, and its public API has been stable for years — it is a supply-chain
reproducibility pin.

**Transitive chain.** `DocumentFormat.OpenXml` → `DocumentFormat.OpenXml.Framework` →
`System.IO.Packaging`. The Framework companion is treated as part of this OTS item (see above);
`System.IO.Packaging` is a distinct OTS item because it is a separately-shipped .NET runtime
component with its own OS-conditional version resolution — see *System.IO.Packaging*.

**Native assets.** None. The `DocDown.Word` build output contains no `runtimes/` folder and no
`.dll`, `.so`, or `.dylib` native binary. A `dotnet list package --include-transitive` for
`DocDown.Word` resolves exactly `DocumentFormat.OpenXml [3.5.1]` → `DocumentFormat.OpenXml.Framework
3.5.1` → `System.IO.Packaging` with nothing else.

**Evidence.** The SDK's behavior is exercised transitively by the `DocDown.Word` extraction
tests — there is no dedicated OTS test project, and coverage is not overclaimed. The extraction
tests that most directly exercise the SDK are `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout`
(end-to-end open, read, extract), `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`
(styles, numbering, and table structure through the SDK's DOM), and
`DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem` (image parts, part URIs, and
content types through the SDK's `ImagePart` surface). Together they reach the full set of
Wordprocessing surfaces this package uses; nothing beyond the extraction path is asserted about
the SDK itself.
