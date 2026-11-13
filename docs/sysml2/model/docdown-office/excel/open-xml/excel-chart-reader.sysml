package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelChartReader {
        doc /* Reads the charts a worksheet shows into the model, recovering each chart's cached data series — the numbers as last plotted — with a name-driven XDocument walk that covers every plot type, keeping the source reference alongside the cache and returning an unparsable or extended chart as a reason rather than throwing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelChartReader.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/OpenXml/ExcelChartReaderTests.cs");
            designFiles = ("docs/design/docdown-office/excel/open-xml/excel-chart-reader.md");
            verificationFiles = ("docs/verification/docdown-office/excel/open-xml/excel-chart-reader.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/open-xml/excel-chart-reader.yaml");
        }
    }
}
