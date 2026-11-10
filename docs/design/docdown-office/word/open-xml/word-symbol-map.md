## WordSymbolMap

![DemaConsulting.DocDown.Word Structure](WordView.svg)

### Purpose

`WordSymbolMap` translates a Word symbol character (`w:sym`, carrying a font name and a hexadecimal
code) into visible Unicode text. Its single responsibility is the translation table and the rules for
addressing it; the reader decides where the result goes.

**Decision.** A `w:sym` code is a code point in a symbol-encoded font such as Symbol or Wingdings,
not a Unicode scalar. Dropping it loses bullets and check marks that tables and lists rely on, and
emitting the raw code produces unrelated characters. The map therefore includes only entries
confirmed against published sources, and everything else resolves to U+FFFD so the loss is visible
and counted rather than guessed at.

### Data Model

`WordSymbolResult(Text, Mapped)` is a readonly record struct: the Unicode text to emit and whether it
is a verified mapping. Per-font tables are frozen dictionaries from font code (`byte`) to string,
held in a frozen dictionary keyed by case-insensitive font name.

### Key Methods

- **`static WordSymbolResult Resolve(string? font, string? charHex)`** — parses `charHex` as
  hexadecimal; a code in `F000`–`F0FF` has `F000` removed, so `F0B7` and `00B7` are the same glyph;
  a code outside `0x20`–`0xFF` is unmapped. The trimmed font name selects a table; an unrecognized
  or absent font, malformed code, or unlisted code returns `Placeholder` with `Mapped` false.
- **`const string Placeholder`** — U+FFFD, the text emitted for an unmapped symbol.

### Table Sources

- **Symbol** — the Unicode Consortium's Adobe Symbol Encoding table (`symbol.txt`), excluding its
  Corporate Use Subarea assignments. Code `0xB7` maps to U+2022 BULLET.
- **Wingdings** — the Unicode Wingdings/Webdings mapping (UTC L2/11-052R2); only commonly used
  bullet, check, and box glyphs are listed (for example `0xFC` to U+2713, `0xFE` to U+1F5F9).
- **Wingdings 2, Wingdings 3, Webdings** — recognized, but no entry has been confirmed yet, so every
  code in them is unmapped.

### Error Handling

`Resolve` never throws: any input it cannot map yields the placeholder.

### Dependencies

None beyond the .NET base class library.

### Callers

`WordOpenXmlReader` (run text) and `WordRevisionScanner` (location context and snippets), so both
agree on what a symbol looks like in `content.md`.
