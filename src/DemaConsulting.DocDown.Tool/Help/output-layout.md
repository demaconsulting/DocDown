---
title: Output layout
summary: The files that an extraction writes to the scratch folder, and how to reuse it.
aliases:
  - output
  - layout
  - scratch
  - summary
  - manifest
  - comments
  - overwrite
tags:
  - reference
order: 3
see-also:
  - getting-started
  - page-images
  - troubleshooting
examples:
  - name: extract
    kind: command
    language: shell
    description: Extract a Word document; then read summary.txt.
    content: '{{tool}} --input memo.docx --scratch memo'
    expect: 'summary.txt'
  - name: reuse
    kind: command
    language: shell
    description: Reuse the folder; the earlier extraction is replaced.
    content: '{{tool}} --input report.pdf --scratch memo'
    expect: 'Extraction produced the output layout.'
  - name: regenerate
    kind: command
    language: shell
    description: Empty the folder first, then extract into it.
    content: '{{tool}} --input report.pdf --scratch memo --overwrite overwrite'
    expect: 'Extraction produced the output layout.'
---
Every extraction writes the same layout below `--scratch`, whatever the
format. All paths in it are relative to the scratch folder.

**summary.txt** is the file to read first. It opens with one line on the
document, then names the scratch folder, the source, the detected format,
the status, the backend that ran, and the environment. *Document
metadata* shows what the document says about itself. *Layout* states for
each file and folder whether it is present. *What WAS extracted* counts
what each artifact holds, such as pages, headings, tables, and comments.
*Could not read* lists every note on what was not extracted, with the
reason, or says that nothing was left incomplete.

**manifest.json** is the machine-readable twin of `summary.txt`
(schema version 3.2). **metadata.json** holds the metadata that the
document asserts about itself, such as author and dates, with the source
of each value.

**content.md** is the text as Markdown, in reading order, with links to
the files in `images/`. An Excel workbook keeps one part per worksheet in
`parts/`, linked from `content.md`.

**review-comments.md** is written only when the document has reviewer
comments, annotations, or tracked changes.

**images/** holds the embedded images and **pages/** the rendered page
images (`page0001.png` and up). Each folder is created only when
something is written to it; otherwise *Layout* says
`not present - none were written`.

**Reusing a folder.** A new or empty folder is always used. For a
folder that holds files, `--overwrite` takes one of two modes:

- `--overwrite clean`, the default, deletes only the files that the
  `manifest.json` of an earlier extraction in that folder lists, as
  [[#reuse]] shows. It refuses the folder, with exit code 1, when the
  folder holds any other file or when its `manifest.json` was written
  for another folder.
- `--overwrite overwrite` deletes everything in the folder first, as
  [[#regenerate]] shows. Use it only for a folder that you mean to
  empty.

**Committed or moved output.** `manifest.json` records the absolute
path of the folder that it was written in. When the output is
committed and later extracted again from another checkout or path,
such as in CI, or when the folder was moved or renamed, the default
`--overwrite clean` refuses it: the message says that the manifest
`was written for` another folder. Use `--overwrite overwrite` for that
folder, or delete the folder first. Otherwise, prefer a new folder for
each document.
