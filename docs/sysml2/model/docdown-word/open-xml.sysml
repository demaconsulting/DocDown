package DocDownWord {
    part def OpenXml {
        doc /* The OpenXml subsystem: the managed backend — the extractor the engine selects, the reader that turns the Open XML DOM into the backend-neutral model, and the image reader that yields each embedded image's bytes and provenance. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Word.Tests/OpenXml/WordOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-word/open-xml.md */
        comment verificationRef /* Verification: docs/verification/docdown-word/open-xml.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-word/open-xml.yaml */

        part wordOpenXmlExtractor : WordOpenXmlExtractor;
        part wordOpenXmlReader : WordOpenXmlReader;
        part wordOpenXmlImageReader : WordOpenXmlImageReader;
    }
}
