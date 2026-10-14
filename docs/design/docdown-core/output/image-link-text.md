### ImageLinkText

![Output Structure](OutputView.svg)

#### Purpose

`ImageLinkText` produces the alt text of an inline markdown image link. A backend links each embedded
image where it occurs in `content.md`, so the alt text must read honestly and must not break the link
syntax. Centralizing the rule keeps every Open XML backend linking images the same way.

#### Data Model

`ImageLinkText` is a `static class` with no state, safe for concurrent use. Its only constant is the
neutral placeholder `image`.

#### Key Methods

- **`string Alt(string? altText)`** — returns `image` when the text is null, empty, or whitespace, so a
  document that offered no description never appears to have offered one. Otherwise it returns the text with
  `[`, `]`, `(`, `)`, and `\` each preceded by a backslash, and with every line break (CRLF, lone CR, or
  LF) replaced by a single space so the link stays on one line. Pure.

#### Error Handling

No input throws. A null or blank description selects the placeholder.

#### Dependencies

`System.Text` (`StringBuilder`) only.

#### Callers

The PowerPoint, Excel, and Visio content emitters call `Alt` with the alt text of an `EmbeddedImage`, which is
set only for a `Descriptive` selection. See _EmbeddedImageWriter Design_.
