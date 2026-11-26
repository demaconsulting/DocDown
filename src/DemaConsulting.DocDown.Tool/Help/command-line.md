---
title: Command-line options
summary: Every option, the order in which a run handles them, and the exit codes.
aliases:
  - options
  - flags
  - usage
  - exit-codes
tags:
  - reference
order: 2
see-also:
  - output-layout
  - page-images
  - troubleshooting
examples:
  - name: usage
    kind: command
    language: shell
    description: Print the short usage text. It ends with a pointer to this help.
    content: '{{tool}} --help'
    expect: "Run '{{tool}} help' for topics and examples."
  - name: silent-log
    kind: command
    language: shell
    description: Extract without console output; keep it in a log.
    content: '{{tool}} --silent --log docdown.log --input report.pdf --scratch report'
  - name: unknown-option
    kind: command
    language: shell
    description: An unknown option is an error, even with --silent. Exits 1.
    content: '{{tool}} --silent --pages-range 1-2'
    exit: 1
    expect: "Error: Unsupported argument '--pages-range'"
  - name: missing-scratch
    kind: command
    language: shell
    description: An extraction needs --scratch as well. Exits 1.
    content: '{{tool}} --input report.pdf'
    exit: 1
    expect: 'Error: --scratch is required for extraction'
  - name: backends
    kind: command
    language: shell
    description: List the backends and whether each is available.
    content: '{{tool}} --list-backends'
    expect: 'visio-rendering - Visio pages (CanvasNet.Vsdx)'
---
Options start with `--` (or one character after `-`) and may be given in
any order. An option given twice keeps its last value.

| Option | Meaning |
| --- | --- |
| `-v`, `--version` | Print only the version. |
| `-?`, `-h`, `--help` | Print the short usage text. |
| `--input <file>` | The document to extract. |
| `--scratch <dir>` | The folder to write the extraction into. |
| `--pages` | Also render page images as PNG files. |
| `--no-pages` | Do not render page images (the default). |
| `--page-range <a-b>` | Only pages a to b, with 1 <= a <= b. |
| `--dpi <n>` | Page image resolution, 36 to 1200; default 150. |
| `--no-images` | Do not write the embedded images. |
| `--max-image-dim <n>` | Skip images wider or taller than n pixels. |
| `--max-image-bytes <n>` | Skip images larger than n bytes. |
| `--accept-changes` | Show Word tracked changes as accepted text. |
| `--overwrite <mode>` | Scratch folder policy, `clean` (default) or `overwrite`. |
| `--list-backends` | List the backends and their availability. |
| `--silent` | Hide console output. Errors still set exit code 1. |
| `--log <file>` | Also write the output to a file. |
| `--validate` | Run the self-tests; see [[self-validation]]. |
| `--results <file>` | Write self-test results as `.trx` or `.xml`. |
| `--depth <n>` | First heading level of the self-test report, 1 to 6. |

`--result` is another name for `--results`. A skipped image is listed as
a note in `summary.txt`. See [[page-images]] for `--pages` and
[[output-layout]] for `--overwrite`.

**Order.** `--version`, when all arguments are valid, prints only the
version and skips everything else. Otherwise the tool prints a banner and
then handles the first of `--help`, `--list-backends`, `--validate`, or
an extraction, which needs both `--input` and `--scratch`. The command
that [[#silent-log]] prints writes everything to the log file only.

**Errors.** An unknown option, an option without its value, a number out
of range, a bad `--page-range` or `--overwrite` value, a missing
`--input` or `--scratch`, and a `--log` file that cannot be opened print
`Error: ...` and exit 1 ([[#unknown-option]]). These errors are found
outside `--silent` and `--log`, so they are always printed and never
logged.

**Exit codes.** 0 when the output layout was written, even when
`summary.txt` lists notes on what could not be read. 1 when the document
could not be read, the scratch folder was refused, an argument was wrong,
a self-test failed, or the results file could not be written.
2 for a wrong `help` argument or an unknown help topic.

**Help.** `help` is a command, not an option, and must be the first
argument. `--silent` and `--log` do not apply to it. A file named `help`
is still extracted with `--input help`.
