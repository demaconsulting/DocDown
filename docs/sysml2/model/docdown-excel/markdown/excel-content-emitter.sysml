package DocDownExcel {
    part def ExcelContentEmitter {
        doc /* The single emission path: writes one Sheet content part per worksheet and one Chart part per chart, the verbatim listing and the additive grid table with its elision and merged-range notes, the shape annotations, the inline image links, the content inventory, and the notes the model implies. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Excel/Markdown/ExcelContentEmitter.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Excel.Tests/Markdown/ExcelContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-excel/markdown/excel-content-emitter.md */
        comment verificationRef /* Verification: docs/verification/docdown-excel/markdown/excel-content-emitter.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-excel/markdown/excel-content-emitter.yaml */
    }
}
