---
title: Self-validation
summary: Prove that the installed tool works, and save the evidence as test results.
aliases:
  - validate
  - qualification
tags:
  - reference
order: 10
see-also:
  - command-line
examples:
  - name: run
    kind: command
    language: shell
    description: Run the self-tests. Prints one line per test.
    content: '{{tool}} --validate'
    expect: 'Failed: 0'
  - name: trx
    kind: command
    language: shell
    description: Also write the results as a TRX file.
    content: '{{tool}} --validate --results validation.trx'
  - name: junit
    kind: command
    language: shell
    description: Write the results as JUnit XML instead.
    content: '{{tool}} --validate --results validation.xml'
  - name: depth
    kind: command
    language: shell
    description: Start the report at heading level 2.
    content: '{{tool}} --validate --depth 2'
    expect: '## DEMA Consulting DocDown Tool'
  - name: bad-extension
    kind: command
    language: shell
    description: Any other extension is an error. Exits 1.
    content: '{{tool}} --validate --results validation.txt'
    exit: 1
    expect: 'Unsupported results file format'
---
`--validate` runs a short set of tests of the tool and of every backend
in a temporary folder, so a regulated project can show that the installed
version works on its build machine. It takes a few seconds.

**Report.** The output is Markdown. It starts with a heading and a table
of the tool version, machine name, operating system, .NET runtime, and
time stamp. Each test prints one line marked `[PASS]`, `[SKIP]` with a
reason, or `[FAIL]` with a reason. The report ends with `Total`,
`Passed`, `Skipped`, and `Failed` lines. `--depth` sets the level of the
first heading, as [[#depth]] shows.

**Tests.** `DocDownTool_Version` and `DocDownTool_Help` check the command
line. The `core.` tests check the output layout and the manifest. Each
backend extracts its own sample document (for example
`pdf.parseRoundTrip`) or renders one (`pdf-rendering.renderRoundTrip`).
The `pageRendering` test of a backend that only reads, such as
`pdf.pageRendering`, is always skipped with its reason; a separate
rendering backend tests the rendering where one exists.

**Exit code.** 0 when no test failed; a skipped test is not a failure.

**Results files.** `--results <file>` also writes the outcome; the
extension chooses the format. `.trx` writes a Visual Studio TRX file and
`.xml` writes JUnit XML, both readable by CI test-report tasks. Any other
extension is an error after the tests ran ([[#bad-extension]]). The
folder of the file must exist.
