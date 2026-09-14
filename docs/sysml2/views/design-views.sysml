// Views rendered into docs/design/generated/ for the Design document.
// Reopens each system's package so every 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package DocDownCore {
    view SoftwareStructureView {
        expose DocDownCore;
        expose DocDownOffice;
        expose DocDownPdf;
        expose DocDownPdfRendering;
        expose DocDownTool;
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

// DocDownOffice is the Microsoft Office extraction system. It has five subsystems - the four
// format subsystems and Com, which holds what the COM automation backends share - and one
// direct unit (OfficeDocDownBuilderExtensions), so it carries a system view plus a view per
// subsystem. The design documents embed the system view (DocDownOfficeView.svg).
package DocDownOffice {
    view DocDownOfficeView {
        expose DocDownOfficeSystem;
    }

    view OfficeComView {
        expose OfficeCom;
    }

    view WordView {
        expose Word;
    }

    view WordMarkdownView {
        expose WordMarkdown;
    }

    view WordOpenXmlView {
        expose WordOpenXml;
    }

    view ExcelView {
        expose Excel;
    }

    view ExcelMarkdownView {
        expose ExcelMarkdown;
    }

    view ExcelOpenXmlView {
        expose ExcelOpenXml;
    }

    view PowerPointView {
        expose PowerPoint;
    }

    view PowerPointMarkdownView {
        expose PowerPointMarkdown;
    }

    view PowerPointOpenXmlView {
        expose PowerPointOpenXml;
    }

    view PowerPointComView {
        expose PowerPointCom;
    }

    view VisioView {
        expose Visio;
    }

    view VisioMarkdownView {
        expose VisioMarkdown;
    }

    view VisioOpenXmlView {
        expose VisioOpenXml;
    }

    view VisioComView {
        expose VisioCom;
    }
}
