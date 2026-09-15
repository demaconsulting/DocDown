## System.IO.Packaging

### Purpose

`System.IO.Packaging` is the OPC (Open Packaging Conventions) container reader — the Zip-based
package format that Office documents physically are. `DocDown.Office` uses it in two ways. The Visio
backend calls it **directly**, opening a `.vsdx` with `Package.Open` and resolving each part it needs
by relationship, because the Open XML SDK does not read Visio drawings. The Word, Excel and
PowerPoint backends reach the same library **transitively** through the SDK, which projects a
strongly-typed DOM on top of the parts it exposes.

It is a separate OTS item from `DocumentFormat.OpenXml` because it is genuinely
independently-sourced: it is shipped as part of the .NET runtime component family with its own
OS- and framework-conditional version resolution rather than as an implementation split of the
SDK. See the *DocumentFormat.OpenXml* OTS design for the sibling item that includes the SDK and
its `Framework` companion, and the rule (one OTS item per independently-sourced component) that
draws the boundary between the two.

### Features Used

Named directly by `VisioPackageReader` and `VisioImageReader`:

- `Package.Open(Stream, FileMode.Open, FileAccess.Read)` to open a `.vsdx` read-only
- `PackagePart` content type, URI, and stream access to read the pages and masters parts
- The `PackageRelationship` graph to locate each part from the package root and to resolve a
  page's image references

Reached transitively through the SDK for the other three formats:

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

**Direct runtime dependency.** `DocDown.Office` declares `System.IO.Packaging` as an explicit
package reference because its own source names the type. It would also arrive transitively through
`DocumentFormat.OpenXml` → `DocumentFormat.OpenXml.Framework` → `System.IO.Packaging`, but a library
whose code calls an API declares that API rather than inheriting it by accident.

**Version resolution — different per target framework.** Because the package is part of the .NET
runtime component family, the version referenced depends on the target framework:

- `net8.0` and `net9.0` — `System.IO.Packaging 8.0.1`
- `net10.0` — `System.IO.Packaging 10.0.2`

Both are the runtime-family builds current for their respective TFMs, pinned to exact versions per
TFM in the Office project so restore is deterministic and cannot drift with the SDK's own graph.

**Native assets.** None. The Office package's build output contains no `runtimes/` folder and no
native binary of any origin; `System.IO.Packaging` is a fully managed component in every
resolution.

**Licensing.** MIT (Microsoft, .NET runtime family), compatible with this repository's MIT
license.

**Evidence.** The direct container behavior is exercised by the Visio package-reader tests, which
call into the packaging API through `VisioPackageReader` with no SDK involved:
`VisioPackageReader_Read_WashSystem_SurfacesPageName` and
`VisioPackageReader_Read_TwoPages_ReturnsPagesInOrder` both require the container to open and its
parts and relationships to resolve. `DocDownWord_Extract_GeneratedDocx_ProducesContractLayout` adds
transitive end-to-end proof through the SDK path. There is no dedicated OTS test project, and
coverage is not overclaimed: nothing beyond these read paths is asserted about
`System.IO.Packaging` itself.
