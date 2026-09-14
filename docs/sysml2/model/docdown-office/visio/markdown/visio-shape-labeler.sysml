package DocDownVisio {
    part def VisioShapeLabeler {
        doc /* Decides how a shape is named in the rendered topology, from what the drawing actually says about it and nothing more: the shape's own text (authored), else the name of the master it was instantiated from (a type, parenthesized with the shape id, unless the master describes connective geometry), else the bare shape id (unresolved). Carries each label's provenance in the data and publishes the labeling convention. This endpoint-label provenance exceeds the Visio intent and is covered because it is genuinely tested. */

        comment sourceRef /* Source: src/DemaConsulting.DocDown.Office/Visio/Markdown/VisioShapeLabeler.cs */
        comment testRef /* Test: test/DemaConsulting.DocDown.Office.Tests/Visio/Markdown/VisioShapeLabelerTests.cs */
        comment designRef /* Design: docs/design/docdown-office/visio/markdown/visio-shape-labeler.md */
        comment verificationRef /* Verification: docs/verification/docdown-office/visio/markdown/visio-shape-labeler.md */
        comment reqRef /* Requirements: docs/reqstream/docdown-office/visio/markdown/visio-shape-labeler.yaml */
    }
}
