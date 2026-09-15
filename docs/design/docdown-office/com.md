## Com Subsystem

![DocDown.Office Com Structure](OfficeComView.svg)

### Overview

The Com subsystem holds what the PowerPoint and Visio COM automation backends share. It exists
because those two backends do the same three things in the same way, and did them in two copies until
the duplication was removed: they probe whether their application can be reached, they delegate
content extraction to their managed counterpart, and they reconcile that delegate's output with the
rendering they then perform.

### Interfaces

Every type here is internal. The subsystem's consumers are the two COM extractors in their own
format subsystems; nothing outside `DemaConsulting.DocDown.Office` can reach these types.

### Design

Three types, none of which touches a COM interface itself.

- **OfficeComAvailability** — answers whether a named Office application's automation can run here.
  It reads the operating system and, on Windows, whether a ProgID resolves. It launches nothing.
- **ComposingDelegatedSink** — forwards every write a delegated managed run makes, suppressing one
  specific environment fact.
- **DelegatedExtractionContext** — the context a COM backend hands its managed counterpart: page
  rendering switched off, output routed through the composing sink.

The COM calls themselves live in each format's own `Com` subsystem, in its automation adapter and
dispatch helper. Those are not shared, because they diverge in ways that matter: Visio is driven
through `Visio.InvisibleApp` and PowerPoint through `PowerPoint.Application`, the process names
differ, and each needs a different value converter. Collapsing them would produce one type with four
knobs, which trades one kind of complexity for a worse one.

#### Why the delegate's honesty needs composing

A managed backend, asked to extract on its own terms, states plainly that it does not render pages.
That statement is true of the managed backend and false of the run the reader is looking at, because
the COM backend rendered the pages sitting beside it in the same output folder.

`ComposingDelegatedSink` suppresses exactly that one fact, and only when it is reported unavailable.
Every other fact, note, image, and content part passes through untouched. The fact's key is supplied
by the caller rather than hard-coded, because each managed backend names its own — the PowerPoint
backend reports `powerpoint.pageRendering` and the Visio backend `visio.pageRendering`. That single
string was the only difference between what were once two copies of this class.

#### Risk control measures

- **The probe never launches an application.** The engine probes every registered backend before
  selecting one, so a probe that started Microsoft Office would do so on every extraction, including
  on machines that cannot run it.
- **The probe never throws.** Any fault while reading the registry is reported as unavailable, which
  is what Core's availability contract requires.
- **Reasons state facts, not instructions.** An unavailable reason says the application is not
  present; it does not tell the reader to install it. The reader of a summary is usually an agent
  that cannot install anything, and a reason it cannot act on should not be phrased as an action.
- **Suppression is narrow.** One key, and only when unavailable. A broader filter could hide a fact
  the reader needs.

#### Design constraints

- The subsystem adds no native asset and no interop assembly. COM is reached through late-bound
  `IDispatch`, so the package stays runtime-identifier agnostic even though these backends only
  function on Windows.
- The availability probe is cheap enough to run on every extraction, because it does.
