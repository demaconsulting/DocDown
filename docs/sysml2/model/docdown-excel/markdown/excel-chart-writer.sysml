package DocDownExcel {
    part def ExcelChartWriter {
        doc /* Renders one chart's cached data as markdown: the labeling that makes the numbers mean something, then a table of categories against series values with a leading point-index column, sparse indices honored, pipes escaped, and the plotted points bounded with what was dropped stated. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Excel/Markdown/ExcelChartWriter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/Markdown/ExcelChartWriterTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/markdown/excel-chart-writer.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/markdown/excel-chart-writer.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/markdown/excel-chart-writer.yaml */
    }
}
