package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelOpenXmlReader {
        doc /* The one place that touches the Open XML spreadsheet object model: resolves shared strings, walks each worksheet in workbook order, keeps only cells that carry a value or a formula, never truncates a value, keeps a formula alongside its value and address, reads each worksheet's reviewer comments from both the legacy and the modern threaded grammars and reports a cell carrying both only once by preferring the threaded comment, and delegates the drawing layer to the image, chart, and drawing-text readers. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelOpenXmlReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlReaderTests.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml/excel-open-xml-reader.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml/excel-open-xml-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml/excel-open-xml-reader.yaml");
        }
    }
}
