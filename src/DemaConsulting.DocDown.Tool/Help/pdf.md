---
title: PDF documents
summary: What is extracted from a PDF file, and what is not.
tags:
  - reference
order: 4
see-also:
  - page-images
  - output-layout
examples:
  - name: extract
    kind: command
    language: shell
    description: Extract the text and images of a PDF.
    content: '{{tool}} --input report.pdf --scratch report'
    expect: 'Extraction produced the output layout.'
  - name: first-page
    kind: command
    language: shell
    description: Extract and render page 1 only.
    content: '{{tool}} --input report.pdf --scratch report-p1 --pages --page-range 1-1'
    expect: 'Extraction produced the output layout.'
---
A `.pdf` file is read by the managed `pdf` backend (PdfPig).

**Extracted.**

- **Text** of every page in reading order, as Markdown paragraphs and
  headings in `content.md`.
- **Embedded images** in `images/`, linked from the text. A JPEG or
  JPEG 2000 image is copied unchanged (`.jpg`, `.jp2`); other images are
  decoded to PNG. An image that cannot be decoded is listed as a note.
- **Annotations** (reviewer comments) in `review-comments.md`, located by
  page, such as `Page 12`.
- **Metadata**: title, author, dates, and the page count.
- **Page images** with `--pages`, from the `pdf-rendering` backend; see
  [[page-images]].

**Page range.** `--page-range a-b` limits the text, images, annotations,
and page images to those pages, as in the command that [[#first-page]]
prints. A range wider than the document keeps the pages that exist.

**Limitations.** There is no OCR. A scanned page has no text layer, so
its text is missing from `content.md`; render the page and view the PNG
instead. An encrypted or damaged file cannot be read: the tool prints
why and exits 1.
