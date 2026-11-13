package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelOpenXml {
        doc /* The OpenXml subsystem: the managed backend — the extractor the engine selects, the reader that turns the spreadsheet package into the backend-neutral model, the image reader that yields each embedded image's bytes and sheet association, the chart reader that recovers each chart's cached data, and the drawing-text reader that recovers each shape's annotation. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml.yaml");
        }

        part excelOpenXmlExtractor : ExcelOpenXmlExtractor;
        part excelOpenXmlReader : ExcelOpenXmlReader;
        part excelOpenXmlImageReader : ExcelOpenXmlImageReader;
        part excelChartReader : ExcelChartReader;
        part excelDrawingTextReader : ExcelDrawingTextReader;
    }

    part def ExcelDocumentModel {
        doc /* The reader-neutral model of a workbook: its sheets, their cells, and the charts drawn on them (documented as a supporting type). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelDocumentModel.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml.yaml");
        }
    }
}
