## System.IO.Packaging

### Purpose

`System.IO.Packaging` is the OPC (Open Packaging Conventions) container reader underneath the
Open XML SDK — the Zip-based package format that a `.docx` file physically is. Its job in the
`DocDown.Word` stack is to open the package, expose its parts and their relationships as streams
and URIs, and hand them up to the SDK, which projects the strongly-typed Wordprocessing DOM on
top. It reaches `DocDown.Word` transitively through the SDK; the Word package does not name it
directly.

It is a separate OTS item from `DocumentFormat.OpenXml` because it is genuinely
independently-sourced: it is shipped as part of the .NET runtime component family with its own
OS- and framework-conditional version resolution rather than as an implementation split of the
SDK. See the *DocumentFormat.OpenXml* OTS design for the sibling item that includes the SDK and
its `Framework` companion, and the rule (one OTS item per independently-sourced component) that
draws the boundary between the two.

### Features Used

`DocDown.Word` uses `System.IO.Packaging` only through the SDK; no source file in `DocDown.Word`
names a `System.IO.Packaging` type directly. Reached transitively:

- The Zip-based `Package` container that fronts the `.docx` file — opened read-only through the
  SDK's `WordprocessingDocument.Open`, which composes with `Package.Open`
- The `PackagePart` surface behind each `HeaderPart`, `FooterPart`, `ImagePart`,
  `NumberingDefinitionsPart`, `FootnotesPart`, and `WordprocessingCommentsPart` — content type,
  URI, and stream access
- The `PackageRelationship` graph that resolves an `r:embed` id to an image part (through the
  SDK's `OpenXmlPartContainer.GetPartById`) and enumerates each section's `w:sectPr` header and
  footer references

`InnerXml` is never materialized above this layer; each part is read as a stream and parsed by
the SDK on the way to the strongly-typed DOM, so `System.IO.Packaging`'s streaming access is what
keeps memory scaling with the document rather than with a serialized copy of it.

### Integration Pattern

**Transitive runtime dependency.** `System.IO.Packaging` reaches `DocDown.Word` transitively
through `DocumentFormat.OpenXml` → `DocumentFormat.OpenXml.Framework` → `System.IO.Packaging`.
There is no direct package reference in the Word project; the transitive resolution is what a
`dotnet list package --include-transitive` reports.

**Version resolution — different per target framework.** Because the package is part of the .NET
runtime component family, the version restore resolves depends on the target framework:

- `net8.0` and `net9.0` — `System.IO.Packaging 8.0.1`
- `net10.0` — `System.IO.Packaging 10.0.2`

Both are the runtime-family builds current for their respective TFMs at the pinned SDK version
(`3.5.1`). The version divergence is not a repository decision — the SDK's own package graph
carries the appropriate resolution per TFM, and the pin on the SDK is what makes it deterministic
per build.

**Native assets.** None. The Word package's build output contains no `runtimes/` folder and no
native binary of any origin; `System.IO.Packaging` is a fully managed component in every
resolution.

**Licensing.** MIT (Microsoft, .NET runtime family), compatible with this repository's MIT
license.

**Evidence.** The container behavior is exercised transitively by the same `DocDown.Word`
extraction tests that exercise the SDK — there is no dedicated OTS test project, and coverage is
not overclaimed. `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout` is the direct end-to-
end proof that opening the package, reading its parts, and following its relationships all work
in the current environment; `WordOpenXmlReader_Read_HeadingsListsAndTables_ProducesStructuredBlocks`
adds structured content through the same path; `DocDownWord_Extract_DocxWithImages_WritesImagesAndLinksThem`
exercises image parts, which reach the container's stream access most directly. Nothing beyond
the extraction path is asserted about `System.IO.Packaging` itself.
