---
title: Excel workbooks
summary: What is extracted from an .xlsx workbook, sheet by sheet.
aliases:
  - xlsx
  - workbook
  - spreadsheet
tags:
  - reference
order: 6
see-also:
  - output-layout
examples:
  - name: extract
    kind: command
    language: shell
    description: Extract every worksheet of a workbook.
    content: '{{tool}} --input budget.xlsx --scratch budget'
    expect: 'Extraction produced the output layout.'
---
An `.xlsx` file is read by the managed `excel-openxml` backend (Open XML
SDK). `content.md` links to one part per worksheet, and one per chart, in
`parts/`.

**Extracted.**

- **Cells.** Each worksheet part lists every populated cell with its
  address and its value, written in full. A cell with a formula shows the
  formula and its cached value; the tool does not calculate.
- **Grid table.** A dense rectangular region is also shown as a Markdown
  table for easy reading. A long value appears there as `…`; the cell
  listing below it is the full record.
- **Charts** with their title and cached series values, up to 500 points
  per chart.
- **Text in shapes** and **embedded images**, the images in `images/`.
- **Comments**, both classic notes and threaded comments, in
  `review-comments.md`, located as `Sheet1!B7`.
- **Metadata**: author, dates, and similar document properties.

When you report a value, cite the worksheet and the cell address.

**Limitations.** A workbook has no pages: `--pages` renders nothing and
`--page-range` does not apply. Values are what the file last saved, so a
workbook that was never recalculated can hold stale results.
