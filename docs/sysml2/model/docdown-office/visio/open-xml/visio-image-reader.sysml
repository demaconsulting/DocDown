package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def VisioImageReader {
        doc /* Resolves the embedded images of a Visio drawing to their bytes and provenance, directly from the Open Packaging container: follows every internal image relationship to its media part, yields the bytes unchanged and deduplicated by package part, records every page that references an image, flags an image reached only through a master as template furniture rather than fabricating a page, and excludes the package thumbnail as document furniture. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Visio/OpenXml/VisioImageReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Visio/OpenXml/VisioImageReaderTests.cs");
            designFiles = ("docs/design/docdown-office/visio/open-xml/visio-image-reader.md");
            verificationFiles = ("docs/verification/docdown-office/visio/open-xml/visio-image-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/visio/open-xml/visio-image-reader.yaml");
        }
    }
}
