---
title: Word documents
summary: What is extracted from a .docx file, including tracked changes and comments.
aliases:
  - docx
  - tracked-changes
  - redline
tags:
  - reference
order: 5
see-also:
  - output-layout
  - troubleshooting
examples:
  - name: extract
    kind: command
    language: shell
    description: Extract a Word document with its tracked changes.
    content: '{{tool}} --input memo.docx --scratch memo'
    expect: 'Extraction produced the output layout.'
  - name: accepted
    kind: command
    language: shell
    description: Show the text with every tracked change accepted.
    content: '{{tool}} --input memo.docx --scratch memo-accepted --accept-changes'
    expect: 'Extraction produced the output layout.'
  - name: no-pages
    kind: command
    language: shell
    description: Word pages are not rendered; the run records a note.
    content: '{{tool}} --input memo.docx --scratch memo-pages --pages'
    expect: '1 note(s) recorded'
---
A `.docx` file is read by the managed `word-openxml` backend (Open XML
SDK).

**Extracted.**

- **Text** with headings, lists, emphasis, and links in `content.md`.
- **Tables** as Markdown tables. Merged or nested cells are flattened,
  and a note says how many.
- **Headers and footers** in a `## Document Control` section after the
  title.
- **Footnotes** as `[^n]` references and a `## Footnotes` section.
- **Embedded images** in `images/`, linked from the text.
- **Comments** in `review-comments.md`, one entry each with its author
  and location.
- **Tracked changes** inline as `{++inserted++}` and `{--deleted--}`,
  counted in `summary.txt` and listed in `review-comments.md`. Add
  `--accept-changes` to read the text as if every change was accepted,
  as in the command that [[#accepted]] prints. A change of formatting
  only is listed but not marked in the text.
- **Metadata**: title, author, dates, and similar document properties.

**Left out on purpose.** A table of contents repeats the headings, so it
is omitted from `content.md` and counted in `summary.txt` instead.

**Limitations.** There are no page images for Word documents: `--pages`
records a note instead, as the command that [[#no-pages]] prints shows.
Convert the document to PDF to render its pages. Embedded charts are not
read; a note says how many there are. `--page-range` does not apply to
Word documents, which have no fixed pages.
