// Views rendered into docs/design/generated/ for the Design document.
// Reopens each system's package so every 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package DocDownCore {
    view SoftwareStructureView {
        expose DocDownCore;
        expose DocDownPdf;
        expose DocDownPdfRendering;
        expose DocDownTool;
        expose DocDownWord;
    }

    view DocDownCoreView {
        expose DocDownCoreSystem;
    }

    view DetectionView {
        expose Detection;
    }

    view ExtractionView {
        expose Extraction;
    }

    view OutputView {
        expose Output;
    }
}

// The repository now holds more than one system, so the full-detail structure view
// above is joined by a per-system view here. DocDownPdf is flat - it has no
// subsystems - so it needs only the one system view, which already shows every unit.
package DocDownPdf {
    view DocDownPdfView {
        expose DocDownPdfSystem;
    }
}

// DocDownPdfRendering is the optional page-rendering system. It is flat - it has no
// subsystems - so it needs only the one system view, which already shows every unit.
package DocDownPdfRendering {
    view DocDownPdfRenderingView {
        expose DocDownPdfRenderingSystem;
    }
}

// DocDownTool is the third system: the docdown command-line tool. It has two subsystems
// (Cli, SelfTest) and one direct unit (Program), so it carries a system view plus a view
// per subsystem.
package DocDownTool {
    view DocDownToolView {
        expose DocDownToolSystem;
    }

    view CliView {
        expose Cli;
    }

    view SelfTestView {
        expose SelfTest;
    }
}

// DocDownWord is the Word extraction system. Like DocDownTool it has more than one
// subsystem (Markdown, OpenXml) and one direct unit (WordDocDownBuilderExtensions),
// so it carries a system view plus a view per subsystem.
package DocDownWord {
    view DocDownWordView {
        expose DocDownWordSystem;
    }

    view MarkdownView {
        expose Markdown;
    }

    view OpenXmlView {
        expose OpenXml;
    }
}
