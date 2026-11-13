package DocDownOffice {
    private import SoftwareItemMetadata::*;

    part def ExcelMarkdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the workbook model onto markdown — the content emitter that drives every output the model implies, and the chart writer that renders a chart's cached series as a table. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.DocDown.Office.Tests/Excel/Markdown/ExcelContentEmitterTests.cs");
            designFiles = ("docs/design/docdown-office/excel/markdown.md");
            verificationFiles = ("docs/verification/docdown-office/excel/markdown.md");
            requirementFiles = ("docs/reqstream/docdown-office/excel/markdown.yaml");
        }

        part excelContentEmitter : ExcelContentEmitter;
        part excelChartWriter : ExcelChartWriter;
    }
}
