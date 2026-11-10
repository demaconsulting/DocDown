package DocDownOffice {
    part def WordSymbolMap {
        doc /* Translates a Word symbol character (font and font code point) to verified Unicode text, resolving anything it cannot confirm to the U+FFFD placeholder so the reader can count and report it. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Word/OpenXml/WordSymbolMap.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Word/OpenXml/WordSymbolMapTests.cs */
        comment designRef /* Design: docs/design/docdown-office/word/open-xml/word-symbol-map.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/word/open-xml/word-symbol-map.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/word/open-xml/word-symbol-map.yaml */
    }
}
