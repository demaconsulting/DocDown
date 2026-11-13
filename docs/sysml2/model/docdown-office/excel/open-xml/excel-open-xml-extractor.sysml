package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelOpenXmlExtractor {
        doc /* The managed backend the engine selects and invokes: declares its priority, declares page rendering not applicable to a non-paginated workbook, probes unconditionally, buffers and opens the workbook, reads it into the model, delegates emission to the shared emitter, and contributes the self-tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelOpenXmlExtractor.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelOpenXmlExtractorTests.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml/excel-open-xml-extractor.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml/excel-open-xml-extractor.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml/excel-open-xml-extractor.yaml");
        }
    }
}
