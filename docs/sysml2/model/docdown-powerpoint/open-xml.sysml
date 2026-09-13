package DocDownPowerPoint {
    part def OpenXml {
        doc /* The OpenXml subsystem: the guaranteed managed content backend — the extractor the engine selects, the reader that turns the presentation package into the backend-neutral model preserving slide order, titles, body text, and speaker notes, and the image reader that yields each embedded image's bytes and slide association. */

        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/OpenXml/PowerPointOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint/open-xml.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint/open-xml.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint/open-xml.yaml */

        part powerPointOpenXmlExtractor : PowerPointOpenXmlExtractor;
        part powerPointOpenXmlReader : PowerPointOpenXmlReader;
        part powerPointOpenXmlImageReader : PowerPointOpenXmlImageReader;
    }
}
