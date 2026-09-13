package DocDownExcel {
    doc
    /*
     * DocDown.Excel is the Excel extraction package: a separately distributed backend that reads a
     * workbook's worksheet cell values verbatim, the formulas behind computed cells, the charts each
     * worksheet shows, the annotations drawn over it, and its embedded images and metadata into the
     * DocDown output contract. A workbook is a prose and data container, not a picture, so the package
     * deliberately never renders. It has two subsystems (Markdown, OpenXml) and one direct unit
     * (ExcelDocDownBuilderExtensions).
     */

    part def DocDownExcelSystem {
        doc /* The DocDown.Excel system as a whole. */

        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/DocDownExcelTests.cs */
        comment designRef /* Design: docs/design/docdown-excel.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel.yaml */

        part excelDocDownBuilderExtensions : ExcelDocDownBuilderExtensions;
        part markdown : Markdown;
        part openXml : OpenXml;
    }
}
