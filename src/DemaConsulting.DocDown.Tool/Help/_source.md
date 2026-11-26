---
name: docdown
schema: 2
title: DocDown Tool
summary: Built-in help of docdown, which turns PDF and Office documents into Markdown for AI agents.
---
{{tool}} extracts a PDF, Word, Excel, PowerPoint, or Visio document into a scratch folder with a fixed
layout: a summary, the text as Markdown, embedded images, and optional page images.

Every operation is an option. The only command is `{{tool}} help`, which must be the first argument and takes
topic names, such as `{{tool}} help command-line`.
