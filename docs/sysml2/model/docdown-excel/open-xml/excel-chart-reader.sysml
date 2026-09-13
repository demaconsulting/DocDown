package DocDownExcel {
    part def ExcelChartReader {
        doc /* Reads the charts a worksheet shows into the model, recovering each chart's cached data series — the numbers as last plotted — with a name-driven XDocument walk that covers every plot type, keeping the source reference alongside the cache and returning an unparsable or extended chart as a reason rather than throwing. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Excel/OpenXml/ExcelChartReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/OpenXml/ExcelChartReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/open-xml/excel-chart-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/open-xml/excel-chart-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/open-xml/excel-chart-reader.yaml */
    }
}
