## VisioShapeLabeler Verification Design

This document describes the unit-level verification strategy for `VisioShapeLabeler`, the endpoint-labeling
decision.

### Verification Approach

`VisioShapeLabeler` is verified through unit tests in `Markdown/VisioShapeLabelerTests.cs` in
`DemaConsulting.DocDown.Visio.Tests`. The labeler is pure, so each test hands it a shape id and a shape map
and asserts the rendered label and its recorded provenance directly, covering every precedence branch and
the published convention.

This unit's behavior — endpoint-label provenance — **exceeds the Visio intent**, which asked only for the
directed source-to-target edges. It is verified here because it is genuinely tested, and is covered as an
additional honesty measure rather than because the intent called for it. *(See the developer report.)*

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: hand-built shape maps with authored text, master names, connective masters, and missing shapes
- **Mocking**: none; the labeler is pure over the shape map
- **Isolation**: each test builds its own shape map

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `VisioShapeLabeler` unit test run passes when the labeler renders the shape's own
text verbatim (recorded as authored text); a text-less shape's master name parenthesized with the shape id
(recorded as a type, two same-type shapes distinguishable); the bare shape id where the shape carries no
text and names no usable master, or is absent from the map (recorded as unresolved); refuses a connective or
line master as a label; and publishes a convention describing every rendered form. Any mislabeled endpoint or
convention that omits a rendered form is a failure.

### Test Scenarios

#### Authored text is used verbatim

**Test**: `VisioShapeLabeler_Label_ShapeWithText_UsesTextVerbatim`

Proves a shape's own text is the label, recorded as authored text. Evidence for
`DocDownVisio-Markdown-VisioShapeLabeler-UsesShapeTextVerbatim`.

#### A master type is parenthesized with the shape id

**Tests**: `VisioShapeLabeler_Label_TextLessShapeWithMaster_UsesParenthesizedTypeAndId`,
`VisioShapeLabeler_Label_TwoShapesOfSameType_RemainDistinguishable`

Prove a text-less shape is labeled with its master name parenthesized and paired with the shape id, recorded
as a type, so it is never mistaken for a name and two same-type shapes stay distinguishable. Evidence for
`DocDownVisio-Markdown-VisioShapeLabeler-UsesParenthesizedMasterType`.

#### A connective master is refused

**Test**: `VisioShapeLabeler_Label_ConnectiveMaster_FallsBackToShapeId`

Proves a connector or line master is refused as an endpoint label, falling back to the shape id. Evidence
for `DocDownVisio-Markdown-VisioShapeLabeler-RefusesConnectiveMasters`.

#### The shape id is the honest fallback

**Tests**: `VisioShapeLabeler_Label_TextLessShapeWithNoMaster_FallsBackToShapeId`,
`VisioShapeLabeler_Label_UnknownShapeId_FallsBackToShapeId`

Prove a shape with no text and no usable master, or a shape absent from the map, is labeled by its bare shape
id and recorded as unresolved. Evidence for `DocDownVisio-Markdown-VisioShapeLabeler-FallsBackToShapeId`.

#### The convention describes every rendered form

**Test**: `VisioShapeLabeler_Convention_DescribesEveryRenderedForm`

Proves the published convention describes each rendered label form — authored text, master type, and bare
shape id. Evidence for `DocDownVisio-Markdown-VisioShapeLabeler-PublishesConvention`.
