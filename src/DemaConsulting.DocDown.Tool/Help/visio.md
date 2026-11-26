---
title: Visio drawings
summary: What is extracted from a .vsdx or .vsdm drawing, including its connections.
aliases:
  - vsdx
  - vsdm
  - drawing
  - diagram
tags:
  - reference
order: 8
see-also:
  - page-images
  - output-layout
examples:
  - name: extract
    kind: command
    language: shell
    description: Extract the shapes and connections of a drawing.
    content: '{{tool}} --input drawing.vsdx --scratch drawing'
    expect: 'Extraction produced the output layout.'
  - name: render
    kind: command
    language: shell
    description: Also render every page at 96 DPI.
    content: '{{tool}} --input drawing.vsdx --scratch drawing-pages --pages --dpi 96'
    expect: 'Extraction produced the output layout.'
---
A `.vsdx` drawing, or a macro-enabled `.vsdm` drawing, is read by the
managed `visio-openxml` backend (Open Packaging). Macros are not run.

**Extracted.** `content.md` has one section per page, named as in the
drawing:

- **Shapes**: the text of every labeled shape.
- **Connections**: each connector as a directed edge between two
  shapes, such as `Inlet Tank → Transfer Pump`. A shape without text is
  named after its master shape, or by its number, and never by a guess.
- **Embedded images** (pictures placed in the drawing) in `images/`.
- **Metadata**: title, author, dates, and similar document properties.
- **Page images** with `--pages`, from the `visio-rendering` backend;
  see [[page-images]]. Render the pages to see the layout, which the text
  does not describe.

**Limitations.** Comments in a drawing are not read, so
`review-comments.md` is never written for it. Legacy `.vsd` files must
be converted to `.vsdx` first.
