## VisioPackageReader Verification Design

This document describes the unit-level verification strategy for `VisioPackageReader`, the reader that turns
a Visio package into the backend-neutral model.

### Verification Approach

`VisioPackageReader` is verified through unit tests in `OpenXml/VisioPackageReaderTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`, reading drawings built at test time by `VisioPackageBuilder` and
asserting the resulting model. The tests cover page names, shape text, the directed topology and its
direction, page order, page-scoped edge resolution, master-name classification, symbol-font recovery, and
the empty page.

Two of this unit's covered behaviors — master-name classification and symbol-font glyph recovery —
**exceed the Visio intent**, which asked for page names, shape text, and the directed edges. They are
verified here because they are genuinely tested, and are covered as tested-but-additional behavior. *(See
the developer report.)*

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: Visio Open Packaging drawings synthesized in memory by `VisioPackageBuilder` — master-typed
  schematics, parallel connectors, reversed connector records, reused shape ids, symbol-font runs, and
  drawings with or without a masters part
- **Mocking**: none; the reader is pure over the package
- **Isolation**: each test builds its own package and reads it back

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioPackageReader` unit test run passes when the reader surfaces each page's name,
extracts each shape's text, resolves each connector into a directed edge in the direction its records name
(regardless of record order, one edge per parallel connector), preserves page order, scopes each page's
edges to its own shapes, classifies a text-less shape by its master name (falling back to the universal name
and carrying none where there is no master, a dangling reference, or no masters part), recovers a symbol-font
glyph only within a run whose font declares it, and yields an empty page cleanly. Any dropped page name,
corrupted text, cross-linked edge, or mis-scoped connector is a failure.

### Test Scenarios

#### Page names are surfaced

**Test**: `VisioPackageReader_Read_WashSystem_SurfacesPageName`

Proves the reader carries each page's declared name into the model. Evidence for
`DocDownVisio-OpenXml-VisioPackageReader-SurfacesPageNames`.

#### Shape text is extracted

**Test**: `VisioPackageReader_Read_WashSystem_ExtractsShapeText`

Proves each shape's text is read verbatim from the page-contents part. Evidence for
`DocDownVisio-OpenXml-VisioPackageReader-ExtractsShapeText`.

#### The directed topology is resolved

**Tests**: `VisioPackageReader_Read_WashSystem_ResolvesDirectedTopology`,
`VisioPackageReader_Read_ConnectorWithEndRecordBeforeBegin_YieldsSingleEdge`,
`VisioPackageReader_Read_ParallelConnectors_YieldEdgePerConnector`

Prove each connector is resolved into a directed edge in the direction its begin and end records name,
regardless of the order the records appear, and one edge per parallel connector. Evidence for
`DocDownVisio-OpenXml-VisioPackageReader-ResolvesDirectedTopology`.

#### Pages are returned in order

**Test**: `VisioPackageReader_Read_TwoPages_ReturnsPagesInOrder`

Proves the pages come out in the order the drawing declares them. Evidence for
`DocDownVisio-OpenXml-VisioPackageReader-PreservesPageOrder`.

#### Edges are scoped to their own page

**Test**: `VisioPackageReader_Read_MultiplePagesReusingShapeIds_ReportOnlyOwnEdges`

Proves a shape id reused on another page does not cross-link, because each page's connectors resolve against
only that page's shapes. Evidence for `DocDownVisio-OpenXml-VisioPackageReader-ScopesEdgesToOwnPage`.

#### Master names are resolved

**Tests**: `VisioPackageReader_Read_MasterTypedSchematic_ResolvesMasterNames`,
`VisioPackageReader_Read_MasterWithOnlyNameU_FallsBackToNameU`,
`VisioPackageReader_Read_ShapeWithNoMaster_CarriesNoMasterName`,
`VisioPackageReader_Read_DanglingMasterReference_CarriesNoMasterName`,
`VisioPackageReader_Read_DrawingWithoutMastersPart_CarriesNoMasterNames`

Prove a text-less shape is classified by its master name, falling back to the universal name and carrying
none where there is no master, a dangling reference, or no masters part. This exceeds the intent and is
covered because it is tested. Evidence for `DocDownVisio-OpenXml-VisioPackageReader-ResolvesMasterNames`.

#### A symbol-font glyph is recovered without corrupting text

**Test**: `VisioPackageReader_Read_SymbolFontRun_RecoversArrowWithoutCorruptingText`

Proves a Wingdings arrow byte is recovered to its Unicode equivalent only within a run whose font declares
that symbol font, leaving text in other fonts unchanged. This exceeds the intent and is covered because it
is tested. Evidence for `DocDownVisio-OpenXml-VisioPackageReader-RecoversSymbolFontGlyphs`.

#### An empty page is yielded cleanly

**Test**: `VisioPackageReader_Read_EmptyPage_YieldsNoShapesOrConnections`

Proves a page with no recoverable shape text and no connections is returned as an empty page rather than an
error. Evidence for `DocDownVisio-OpenXml-VisioPackageReader-YieldsEmptyPageCleanly`.
