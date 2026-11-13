package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelDrawingTextReader {
        doc /* Reads the text carried by the drawing shapes floating over a worksheet — callouts, labels, and annotations that live in the drawing layer and in no cell — in drawing order, flattening group membership and joining a shape's paragraphs into one readable annotation. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelDrawingTextReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/Markdown/ExcelContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml/excel-drawing-text-reader.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml/excel-drawing-text-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml/excel-drawing-text-reader.yaml");
        }
    }
}
