package DocDownPowerPoint {
    part def PowerPointOpenXmlReader {
        doc /* The one place that touches the Open XML object model: turns a .pptx package into the backend-neutral deck model, walking slides in presentation order, separating the title placeholder from the body shapes, reading the notes slide's body placeholder so the narration is recovered from the file, counting embedded charts, and wrapping an unopenable package as a structured failure. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.PowerPoint/OpenXml/PowerPointOpenXmlReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.PowerPoint.Tests/OpenXml/PowerPointOpenXmlReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-powerpoint/open-xml/powerpoint-open-xml-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-powerpoint/open-xml/powerpoint-open-xml-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-powerpoint/open-xml/powerpoint-open-xml-reader.yaml */
    }
}
