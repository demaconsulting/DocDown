package DocDownOffice {
    part def VisioOpenXml {
        doc /* The OpenXml subsystem: the guaranteed content backend — the extractor the engine selects for a content extraction, the reader that turns a Visio Open Packaging container into the backend-neutral model (page names, shape text, master classification, and the directed connector topology) directly through System.IO.Packaging because DocumentFormat.OpenXml has zero Visio types, and the image reader that resolves each embedded image's bytes and page association. Fully managed, no native asset. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/OpenXml/VisioOpenXmlExtractorTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/open-xml.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/open-xml.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/open-xml.yaml */

        part visioOpenXmlExtractor : VisioOpenXmlExtractor;
        part visioPackageReader : VisioPackageReader;
        part visioImageReader : VisioImageReader;
    }
}
