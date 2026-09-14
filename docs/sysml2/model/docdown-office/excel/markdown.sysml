package DocDownExcel {
    part def Markdown {
        doc /* The Markdown subsystem: the reader-neutral projection of the workbook model onto markdown — the content emitter that drives every output the model implies, and the chart writer that renders a chart's cached series as a table. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Excel/Markdown/ExcelContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-office/excel/markdown.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/excel/markdown.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/excel/markdown.yaml */

        part excelContentEmitter : ExcelContentEmitter;
        part excelChartWriter : ExcelChartWriter;
    }
}
