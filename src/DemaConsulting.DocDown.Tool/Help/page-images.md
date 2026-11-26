---
title: Page images
summary: Render pages, slides, and drawings to PNG files that you can view.
aliases:
  - pages
  - rendering
  - render
  - dpi
  - page-range
tags:
  - reference
order: 9
see-also:
  - pdf
  - powerpoint
  - visio
examples:
  - name: render
    kind: command
    language: shell
    description: Render every page of a PDF.
    content: '{{tool}} --input report.pdf --scratch report-pages --pages'
    expect: 'Extraction produced the output layout.'
  - name: high-dpi
    kind: command
    language: shell
    description: Render slides at 200 DPI for small text.
    content: '{{tool}} --input deck.pptx --scratch deck-hires --pages --dpi 200'
    expect: 'Extraction produced the output layout.'
  - name: dpi-out-of-range
    kind: command
    language: shell
    description: A resolution outside 36 to 1200 is an error. Exits 1.
    content: '{{tool}} --input report.pdf --scratch r --pages --dpi 20'
    exit: 1
    expect: 'Error: --dpi requires an integer between 36 and 1200'
---
Text extraction misses what is drawn: diagrams, charts, scanned pages,
the layout of a slide, and complex tables. Page images show it. They are
off by default; add `--pages` to write one PNG file per page to `pages/`
as `page0001.png`, `page0002.png`, and so on, numbered by the page of the
document.

**Formats.** PDF pages, PowerPoint slides, and Visio pages are rendered,
by the `pdf-rendering`, `powerpoint-rendering`, and `visio-rendering`
backends that `{{tool}} --list-backends` shows. Word documents have no
fixed pages: `--pages` records a note instead. Excel workbooks have no
pages, so `--pages` writes nothing for them.

**Resolution.** `--dpi <n>` sets the resolution, from 36 to 1200; the
default is 150. Raise it for small print, as in the command that
[[#high-dpi]] prints; larger images cost more to read.

**Page range.** `--page-range a-b`, with 1 <= a <= b, renders only those
pages. For a PDF it also limits the text; for PowerPoint and Visio the
text still covers every page. Pages beyond the end are ignored.

A page that cannot be rendered is listed as a note in `summary.txt`, and
the other pages are still written.
