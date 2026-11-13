package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelChartWriter {
        doc /* Renders one chart's cached data as markdown: the labeling that makes the numbers mean something, then a table of categories against series values with a leading point-index column, sparse indices honored, pipes escaped, and the plotted points bounded with what was dropped stated. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.DocDown.Office/Excel/Markdown/ExcelChartWriter.cs");
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/Markdown/ExcelChartWriterTests.cs");
            designFiles = ("docs/design/docdown-office/excel/markdown/excel-chart-writer.md");
            verificationFiles = ("docs/verification/docdown-office/excel/markdown/excel-chart-writer.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/markdown/excel-chart-writer.yaml");
        }
    }
}
