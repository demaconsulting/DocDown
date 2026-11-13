package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelOpenXmlImageReader {
        doc /* Resolves a workbook's embedded images to their bytes and provenance from each worksheet's drawing part, yields each image unchanged deduplicated by package part, and records the tab index of every worksheet that references an image so the sheet association survives extraction. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelOpenXmlImageReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlReaderTests.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml/excel-open-xml-image-reader.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml/excel-open-xml-image-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml/excel-open-xml-image-reader.yaml");
        }
    }
}
