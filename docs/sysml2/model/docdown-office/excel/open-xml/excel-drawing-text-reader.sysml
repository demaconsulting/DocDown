package DocDownExcel {
    part def ExcelDrawingTextReader {
        doc /* Reads the text carried by the drawing shapes floating over a worksheet — callouts, labels, and annotations that live in the drawing layer and in no cell — in drawing order, flattening group membership and joining a shape's paragraphs into one readable annotation. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Excel/OpenXml/ExcelDrawingTextReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Excel/Markdown/ExcelContentEmitterTests.cs */
        comment designRef /* Design: docs/design/docdown-office/excel/open-xml/excel-drawing-text-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/excel/open-xml/excel-drawing-text-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/excel/open-xml/excel-drawing-text-reader.yaml */
    }
}
