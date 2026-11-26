---
title: PowerPoint presentations
summary: What is extracted from a .pptx presentation, and how to render slides.
aliases:
  - pptx
  - slides
  - presentation
  - speaker-notes
tags:
  - reference
order: 7
see-also:
  - page-images
  - output-layout
examples:
  - name: extract
    kind: command
    language: shell
    description: Extract the text and images of every slide.
    content: '{{tool}} --input deck.pptx --scratch deck'
    expect: 'Extraction produced the output layout.'
  - name: slide-two
    kind: command
    language: shell
    description: Render slide 2 only; the text covers every slide.
    content: '{{tool}} --input deck.pptx --scratch deck-p2 --pages --page-range 2-2'
    expect: 'Extraction produced the output layout.'
---
A `.pptx` file is read by the managed `powerpoint-openxml` backend (Open
XML SDK).

**Extracted.**

- **Slides** in order in `content.md`, one section per slide with its
  title and the text of every shape, grouped shape, SmartArt diagram,
  and table (one line per table row).
- **Speaker notes** with their slide; `summary.txt` counts the sets of
  notes.
- **Embedded images** in `images/`, linked from the slide that shows
  them. An image that several slides share is written once.
- **Comments** (classic comments) in `review-comments.md`, located by
  slide.
- **Metadata**: title, author, dates, and similar document properties.
- **Slide images** with `--pages`, from the `powerpoint-rendering`
  backend, as `pages/page0001.png` for slide 1; see [[page-images]].

**Slide range.** `--page-range a-b` limits only the rendered slides, as
the command that [[#slide-two]] prints shows; `content.md` still holds
every slide.

**Limitations.** Modern comments, which newer versions of PowerPoint
store in the cloud-synced format, are not read; a note says how many
there are. The data of a chart is not extracted; view the chart in a
slide image. Legacy `.ppt` files must be converted to `.pptx` first.
