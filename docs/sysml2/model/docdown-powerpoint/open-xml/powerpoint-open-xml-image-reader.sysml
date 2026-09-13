package DocDownPowerPoint {
    part def PowerPointOpenXmlImageReader {
        doc /* Resolves the embedded images of a presentation to their bytes and provenance across slides, notes, layouts, and masters, deduplicating by package part, recording every referring slide, exposing each slide's ordered image occurrences, and flagging an image reached only through a layout or master as template furniture. No dedicated test class; proved through the reader and emitter tests. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.PowerPoint/OpenXml/PowerPointOpenXmlImageReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/OpenXml/PowerPointOpenXmlReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint/open-xml/powerpoint-open-xml-image-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint/open-xml/powerpoint-open-xml-image-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint/open-xml/powerpoint-open-xml-image-reader.yaml */
    }
}
