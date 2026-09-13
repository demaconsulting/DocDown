package DocDownVisio {
    part def VisioOpenXmlExtractor {
        doc /* The managed backend the engine selects for a content extraction: declares the modern Visio formats, the higher priority, and the text, embedded-image, document-metadata, and document-structure capabilities and not rendered-pages; probes available unconditionally with no I/O; records its environment facts; buffers and opens the drawing; delegates reading to VisioPackageReader and emission to VisioContentEmitter; and contributes a topology round-trip self-test and a reasoned page-rendering skip. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Visio/OpenXml/VisioOpenXmlExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Visio.Tests/OpenXml/VisioOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-visio/open-xml/visio-open-xml-extractor.md */
        comment verificationRef /* Verification: docs/verification/docdown-visio/open-xml/visio-open-xml-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-visio/open-xml/visio-open-xml-extractor.yaml */
    }
}
