---
title: Troubleshooting
summary: What to do when an extraction fails or leaves something out.
aliases:
  - errors
  - problems
  - unreadable
tags:
  - troubleshooting
order: 11
see-also:
  - command-line
  - output-layout
examples:
  - name: missing-file
    kind: command
    language: shell
    description: A document that does not exist. Exits 1.
    content: '{{tool}} --input missing.pdf --scratch missing'
    exit: 1
    expect: 'could not be read'
  - name: text-file
    kind: command
    language: shell
    description: A plain text file has no extractor. Exits 1.
    content: '{{tool}} --input notes.txt --scratch notes'
    exit: 1
    expect: "No registered extractor supports 'text'"
  - name: legacy-format
    kind: command
    language: shell
    description: A legacy .doc file is refused. Exits 1.
    content: '{{tool}} --input legacy.doc --scratch legacy'
    exit: 1
    expect: 'legacy binary Office formats'
  - name: busy-folder
    kind: command
    language: shell
    description: A folder with other files is refused. Exits 1.
    content: '{{tool}} --input report.pdf --scratch .'
    exit: 1
    expect: 'is not a recognizable DocDown folder'
  - name: no-input
    kind: command
    language: shell
    description: Without options the tool needs --input. Exits 1.
    content: '{{tool}}'
    exit: 1
    expect: '--input is required for extraction'
---
**Exit code 1.** The run failed, and the reason replaces the path of
`summary.txt` in the output. For a failed extraction it is printed on
standard error; with `--silent` it goes only to the `--log` file. The
usual causes are:

- **The document could not be read** ([[#missing-file]]): it does not
  exist, or it is encrypted or damaged. Check the path, and remove the
  password or repair the file.
- **No extractor for the format** ([[#text-file]]): plain text,
  Markdown, and CSV files need no tool; read them directly.
- **A legacy format** ([[#legacy-format]]): `.doc`, `.xls`, `.ppt`,
  and `.vsd` must be saved as `.docx`, `.xlsx`, `.pptx`, or `.vsdx`
  first, or printed to PDF.
- **The scratch folder was refused** ([[#busy-folder]]): it holds files
  that an earlier extraction did not write. Use a new folder. The message
  names `ScratchFolderMode.Overwrite`, which is `--overwrite overwrite`
  on the command line; use that only for a folder you mean to empty.
- **An argument is wrong** ([[#no-input]]): the message starts with
  `Error:` and is printed even with `--silent`. See [[command-line]].

**Exit code 0 with notes.** Output was written, but a line such as
`1 note(s) recorded` means that something was left out. Read *Could not
read* in `summary.txt`: each note says what was not extracted and why,
such as a chart that is not read, an image over a size limit, or a page
that could not be rendered.

**Text is missing.** There is no OCR. Text in a scanned page, a picture,
or a drawing is only visible in a page image; render it with `--pages`
and view the PNG. See [[page-images]].

**Too much output.** For a large PDF, extract only the pages you need
with `--page-range a-b`.
