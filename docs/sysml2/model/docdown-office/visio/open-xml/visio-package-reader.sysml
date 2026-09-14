package DocDownVisio {
    part def VisioPackageReader {
        doc /* Reads a Visio drawing's Open Packaging container directly with System.IO.Packaging — because DocumentFormat.OpenXml has zero Visio types — into the backend-neutral model: each page's name, the text of every shape that carries text, the master each shape was instantiated from (classifying a text-less shape by its type), a symbol-font glyph recovered only within a run whose font declares it, and the directed connections resolved from the connector begin and end records, scoped to the page that owns them. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/OpenXml/VisioPackageReader.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/OpenXml/VisioPackageReaderTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/open-xml/visio-package-reader.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/open-xml/visio-package-reader.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/open-xml/visio-package-reader.yaml */
    }
}
