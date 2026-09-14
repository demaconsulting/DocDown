package DocDownOffice {
    part def VisioImageReader {
        doc /* Resolves the embedded images of a Visio drawing to their bytes and provenance, directly from the Open Packaging container: follows every internal image relationship to its media part, yields the bytes unchanged and deduplicated by package part, records every page that references an image, flags an image reached only through a master as template furniture rather than fabricating a page, and excludes the package thumbnail as document furniture. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/OpenXml/VisioImageReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/OpenXml/VisioImageReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/open-xml/visio-image-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/open-xml/visio-image-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/open-xml/visio-image-reader.yaml */
    }
}
