## WordSymbolMap Verification Design

This document describes the unit-level verification strategy for `WordSymbolMap`, the unit that
translates Word symbol characters to Unicode.

### Verification Approach

`WordSymbolMap` is verified by pure unit tests in `OpenXml/WordSymbolMapTests.cs` in
`DemaConsulting.DocDown.Office.Tests`, calling `Resolve` directly with invented font and code
combinations. Reader-level behavior (rendering inside tables, tracked changes, and the unmapped
count) is verified in the `WordOpenXmlReader` and tracked-change tests.

### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, net10.0, and net11.0
- **Inputs**: font names and hexadecimal codes supplied inline
- **Filesystem**: none
- **Mocking**: none
- **Isolation**: the map is static and read-only

### Acceptance Criteria

Per IEC 62304 §5.5.2, a `WordSymbolMap` test run passes when the Symbol bullet maps to U+2022, the
verified Wingdings codes map to their Unicode text, the font name is matched without regard to case,
Private Use Area and bare codes agree, and every unrecognized font, malformed or out-of-range code,
and unlisted code resolves to U+FFFD flagged as unmapped.

### Test Scenarios

#### Symbol bullet

**Test**: `WordSymbolMap_Resolve_SymbolBullet_MapsToBullet`

Proves Symbol `F0B7` resolves to U+2022. Evidence for
`DocDownWord-OpenXml-WordSymbolMap-MapsSymbolCharacters`.

#### Wingdings codes

**Test**: `WordSymbolMap_Resolve_WingdingsCodes_MapToUnicode`

Proves the verified Wingdings check, box, and square codes resolve to their Unicode mappings.
Evidence for `DocDownWord-OpenXml-WordSymbolMap-MapsSymbolCharacters`.

#### Font name matching

**Test**: `WordSymbolMap_Resolve_FontNameCaseAndWhitespace_Ignored`

Proves the font name is matched ignoring case and surrounding whitespace. Evidence for
`DocDownWord-OpenXml-WordSymbolMap-MapsSymbolCharacters`.

#### Private Use Area and bare codes

**Test**: `WordSymbolMap_Resolve_PrivateUseAndBareCodes_AreEquivalent`

Proves `F0xx` and `00xx` codes, in either hex case, resolve identically. Evidence for
`DocDownWord-OpenXml-WordSymbolMap-MapsSymbolCharacters`.

#### Unmappable input

**Test**: `WordSymbolMap_Resolve_UnmappableInput_ReturnsPlaceholder`

Proves unknown fonts, null inputs, malformed, out-of-range, and unlisted codes, and the recognized
fonts with no verified entries all yield U+FFFD flagged as unmapped. Evidence for
`DocDownWord-OpenXml-WordSymbolMap-MapsSymbolCharacters`.
