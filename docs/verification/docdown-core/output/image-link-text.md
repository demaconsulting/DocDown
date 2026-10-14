### ImageLinkText Verification Design

This document describes the unit-level verification strategy for `ImageLinkText`, the alt-text policy for
inline markdown image links.

#### Verification Approach

`ImageLinkText` is verified in isolation in `ImageLinkTextTests.cs` under the `Output` folder of
`DemaConsulting.DocDown.Core.Tests`, with method names beginning with `Alt_`. The tests call the real `Alt`
with descriptions supplied by the test, using xUnit theories for the blank, escaped, and multi-line inputs.
Nothing is mocked because the unit is a pure function.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK, targeting net8.0, net9.0, and net10.0
- **Inputs**: description strings supplied by the test
- **Filesystem**: N/A
- **Isolation**: each test is independent with no shared state

#### Acceptance Criteria

Per IEC 62304 §5.5.2, an `ImageLinkText` unit test run passes when a null, empty, or whitespace description
yields the placeholder `image`; when a plain description is returned unchanged; when `[`, `]`, `(`, and `)`
are each escaped so the bracket and the link target cannot be closed early; and when LF, CRLF, and lone CR
line breaks collapse to a single space so the result stays on one line.

#### Test Scenarios

##### A missing description yields the neutral placeholder

**Test**: `Alt_NoDescription_UsesNeutralPlaceholder`

Proves null, empty, spaces-only, and tab-and-newline inputs all return `image`. Evidence for
`DocDownCore-Output-ImageLinkText-SafeAltText`.

##### A plain description is preserved

**Test**: `Alt_PlainDescription_IsPreserved`

Proves `Site plan` is returned unchanged. Evidence for `DocDownCore-Output-ImageLinkText-SafeAltText`.

##### Link-structural characters are escaped

**Test**: `Alt_LinkStructuralCharacters_AreEscaped`

Proves a description containing `[`, `]`, `(`, or `)` has no unescaped occurrence of that character in the
result. Evidence for `DocDownCore-Output-ImageLinkText-SafeAltText`.

##### A multi-line description stays on one line

**Test**: `Alt_MultiLineDescription_StaysOnOneLine`

Proves LF, CRLF, and lone CR separated text returns `first second` with no line-break character. Evidence for
`DocDownCore-Output-ImageLinkText-SafeAltText`.
