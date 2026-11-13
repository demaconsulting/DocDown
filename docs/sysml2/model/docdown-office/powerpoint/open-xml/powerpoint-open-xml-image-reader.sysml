package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def PowerPointOpenXmlImageReader {
        doc /* Resolves the embedded images of a presentation to their bytes and provenance across slides, notes, layouts, and masters, deduplicating by package part, recording every referring slide, exposing each slide's ordered image occurrences, and flagging an image reached only through a layout or master as template furniture. No dedicated test class; proved through the reader and emitter tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/PowerPoint/OpenXml/PowerPointOpenXmlImageReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/PowerPoint/OpenXml/PowerPointOpenXmlReaderTests.cs");
            designFiles = ("docs/design/docdown-office/powerpoint/open-xml/powerpoint-open-xml-image-reader.md");
            verificationFiles = ("docs/verification/docdown-office/powerpoint/open-xml/powerpoint-open-xml-image-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/powerpoint/open-xml/powerpoint-open-xml-image-reader.yaml");
        }
    }
}
