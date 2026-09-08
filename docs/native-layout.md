# Native layout integration research

**Isolated native diagnostics verified; public MCP layout integration remains
unimplemented.** Explicit geometry edits and same-diagram reparenting do not
establish automatic layout. The installed editor has now produced real native
alignment/distribution callbacks, durable files and independent readback in a
bounded diagnostic corpus. This is not full Modeler automation, a released MCP
layout tool, or general whole-diagram layout accreditation.

## Installed 4.3.0.008 responsibility map

Read-only inspection of the installed assemblies and frontend assets identified:

| Component | Observed responsibility | Integration consequence |
| --- | --- | --- |
| `DiagramEditorHandleManager.OnAlignShapes` | Delegates to an editor presenter | Does not calculate or persist layout itself |
| `DiagramEditorPresenter.AlignSelectedShapes` | Generates a script command for the current editor | Requires the editor frontend, not just native persistence objects |
| `window.alignSelectedShapes` | Calls the frontend layout handler | Programmatic entry point; no simulated mouse input is needed to invoke it |
| Frontend alignment/distribution services | Calculate selected-shape deltas and invoke native frontend modeling commands | Reuse these services before implementing replacement geometry formulas |
| Frontend `elements.align` command | Moves shapes through its modeling pipeline | Connected paths and other dependent geometry must be observed, not guessed |
| `DiagramEditorElementsHandler.UpdateElementShape` | Receives serialized frontend changes and delegates to the presenter | A frontend result is not yet durable native state |
| `DiagramEditorPresenter.UpdateElementShape` | Deserializes changes, builds native commands and dispatches them through `ElementManager` | The actual command path, thread affinity and notifications need an isolated adapter |
| `ElementEditorHandler.GetUpdateCommands` | Produces collection commands using `BaseEditorElement` observations and `DiagramEditorPresentationModel` | Resolved and exercised in an isolated diagnostic; public lifecycle and richer acceptance remain pending |
| `RoutingFixerService.GetElementFixingArgs` | Maps existing frontend waypoints into native connector event arguments | It is not an automatic routing algorithm |
| `BrowserImage` | Hosts the installed image-generator page in CefSharp OffScreen | Existing rendering support is not an editable frontend integration |

The inspected legacy `DirectionalRouter` accepts a Crainiate line. Its existence
does not prove that using it independently reproduces the current editor's
modeling, docking and connection behavior. `FrmModeler.OnResetLayout` concerns
application bars, not automatic placement of BPMN elements.

The frontend's observed alignment modes are `Top`, `Bottom`, `Left`, `Right`,
`Horizontal`, `Vertical`, `HorizontalEvenly` and `VerticalEvenly`.
`Horizontal` aligns vertical centers; `Vertical` aligns horizontal centers.
These selected-element operations are not proof of a general whole-diagram
auto-layout service. The inspected layout handler filters connection, pool,
lane, milestone, outer-icon and text-element shapes from that selection.

## Real CEF bindings are a mandatory acceptance boundary

The installed editor bundle has a development/demo path that installs mock
external handlers when its CEF host is absent. Some mutation handlers on that
path do nothing; some queries return demonstration content. Therefore:

- Loading the bundle successfully is not native integration proof.
- Seeing a selected shape move is not native persistence proof.
- A standalone browser page without verified .NET bindings cannot accredit the
  MCP feature, even if it produces visually plausible results.
- Missing bindings, rejected callbacks and unimplemented bridge methods must
  be explicit failures, never silent no-op success or demo fallbacks.

The observed desktop binding names are `diagramEditorElementsHandler`,
`diagramEditorViewEventsHandler`, `diagramEditorSynchronousHandler` and
`diagramEditorKeyboardCommandHandler`. Their presence alone is insufficient:
the actual callback must reach the owned worker and its native model.
The normal presenter also references UI services, runtime state and an STA
dispatch path, so constructing it wholesale is not yet a verified headless
solution.

The existing image-generator integration registers its own native synchronous
handler. It must not be relabeled as the complete editor bridge. No modification
to the installed bundle, personal configuration or running operator instance
was performed during this research.

For the embedding mechanism, the official
[CefSharp usage documentation](https://github.com/cefsharp/CefSharp/wiki/General-Usage)
describes `JavascriptObjectRepository.Register`, `CefSharp.BindObjectAsync` and
load/context notifications. Those APIs establish a transport mechanism, not
Bizagi-specific command fidelity or automatic Modeler compatibility.

## Evidence required for the next programmatic gate

The next route must retain the original file and use real native data throughout:

1. Load an actual native model into an isolated worker and obtain its native
   editor DTO/configuration through the installed adapters.
2. Load the installed editor asset in a windowless CEF instance with verified
   bindings backed by that real model. Reject demo or missing-handler paths.
3. Select actual native IDs programmatically and invoke an explicit layout mode.
4. Correlate and record the actual native frontend callback, including moved
   nodes, changed connector waypoints, labels and any dependent changes.
5. Apply those observations through the native command/adapter boundary; reject
   semantic, containment or identity changes not authorized by the operation.
6. Persist through the installed engine, terminate the editor worker and load
   the resulting `.bpm` in a new native process.
7. Check intended geometry, unchanged semantic content and every original
   archive leaf, including attachments, attributes and simulation configuration.
8. Exercise actual MCP dispatch, cancellation, missing-binding failure, worker
   recovery, rich nested/pool cases and independent visual compatibility.

Subtree placement, docking, labels, boundaries, swimlanes and reusable calls
need explicit contracts and acceptance cases. Selected alignment, selected
distribution, connector routing and whole-diagram automatic layout remain
distinct requirements; one successful trajectory must not stand in for all.

## Isolated native diagnostic checkpoint — September 8, 2026

The private probe now traverses this actual installed-engine path:

1. Load a native `.bpm` and author the diagnostic selection using native domain
   objects; retain the original source unchanged.
2. Obtain `IDiagramModelAdapter.GetDataCollectionForView` data and native element
   configuration; load the **unchanged installed editor asset** in CefSharp
   OffScreen, not a standalone browser with demo handlers.
3. Identify the new document with a unique nonce. Evict stale CEF binding cache
   entries and require a real .NET challenge/response on all four bindings.
4. Select actual model IDs and invoke the selected native alignment mode.
5. Capture `UpdateElementShape` through the registered .NET object. Reject
   unrequested semantic/participant changes before applying any command.
6. Translate through installed `ElementEditorHandler.GetUpdateCommands` and
   execute the installed `CommandFactory` commands on the engine thread, not a
   CEF callback thread. Avoid the GUI `ElementManager.SetDiagramModel` lifecycle,
   which can insert defaults or initialize collaboration.
7. Persist with the native manager, dispose the owned offscreen browser, exit
   the writer, and load the durable file in another native process.
8. Independently verify the requested alignment mathematics, graph readback and
   the entire native archive using the existing fidelity comparator.

### Verified bounded corpus

Private batch `fidelity-batch-20260908-020528` exercised all eight modes on a
native model with Unicode task names and connected sequence flows:

| Mode | Native write + separate read | Graph restart differences | Whole-archive geometric fidelity |
| --- | --- | ---: | --- |
| `Top` | Passed | 0 | Passed |
| `Bottom` | Passed | 0 | Passed |
| `Left` | Passed | 0 | Passed |
| `Right` | Passed | 0 | Passed |
| `Horizontal` | Passed | 0 | Passed |
| `Vertical` | Passed | 0 | Passed |
| `HorizontalEvenly` | Passed | 0 | Passed |
| `VerticalEvenly` | Passed | 0 | Passed |

These are **eight native diagnostics, not eight MCP acceptance gates**. The
batch retained 1,851 read-only desktop samples, with no observed visible owned
window or foreground ownership. All 71 observed owned-process identities were
verified exited; the source SHA-256 was unchanged. The diagnostic supervisor
uses the production kill-on-close worker job and retains process handles for
exit verification rather than trusting potentially recycled PIDs.

The private consolidated diagnostic record has SHA-256
`1020b6ebc41f13069b41df004709ed9564ab81a2b592fa6e1ad9ddc4735309be`.
Raw model data, callbacks and proprietary inspection remain private.

A subsequent `Bottom` diagnostic (`rich-preserved-20260908-021826`) used a
two-diagram native corpus containing three embedded subprocesses, a boundary
event, data/association content, a resource, extended attributes, embedded
content, one image and two simulation scenarios. The selected subjects were
new root-level diagnostic tasks; this **does not accredit alignment inside
embedded subprocesses**.

Both preservation boundaries passed: original source to the explicitly authored
fixture (1,138 checked atoms), and fixture to aligned output (1,272 checked
atoms across 25 archive entries). The separate reader reported zero graph
differences. All nine observed process identities exited; 106 desktop samples
showed no visible owned window or foreground ownership. Its private summary
SHA-256 is `95138b01542d0ed4373d1101f80fca45ce2069b708c113079bbb366b9668d36c`.

The fixture is persisted and actually reloaded before editor use. This avoids
comparing constructor-time connection aliases against durable native references;
the complete restart comparator was not weakened. Fixture construction has its
own archive-fidelity check, so it cannot hide loss of original rich content.

### Failures retained rather than normalized into success

- **Navigation race:** the previous image-generator page exposes the same web
  component. Checking for that component alone could accept the old document.
  A document nonce and positive binding handshake now identify the correct
  editor context.
- **Binding cache:** unregistering/re-registering .NET objects alone did not
  replace a cached synchronous handler. The probe clears the old CEF object
  cache, rebinds and checks actual callbacks before rendering model data.
- **Semantic detachment:** an out-of-pool alignment produced a callback with a
  missing sequence-flow target. A negative diagnostic rejected the whole
  result before native command execution; no resulting `.bpm` was published.
  Alignment must not silently authorize reconnection or reparenting.
- **Automatic label materialization:** native commands populated manual label
  rectangles from frontend fallback bounds. Two initial modes also produced
  fractional label positions which changed when the native serializer rounded
  them on reload. The adapter now preserves the exact original all-zero native
  automatic-label rectangle; it does not erase existing manual label bounds.
  The strict restart comparison was retained, not relaxed to hide rounding.

For archive comparison, selected-node geometry is derived independently from
the requested mode and original bounds. Dependent connector paths come from
the recorded real CEF callback and are restricted to connections touching the
selection. They are **not** inferred from the output being checked. Only those
explicit deltas are projected by `NativeMutationFidelity`; the remaining XML
and binary content stays subject to the complete native comparison. Raw
no-change comparison reports are retained separately and still show the edits.

### Still required before a public layout capability

- Typed MCP contracts and a production host-to-worker-to-editor path, including
  revision capture, durable operation evidence and normal artifact publication.
- Broader multidiagram/subprocess, manual-label and boundary-editing cases. The
  root-level rich preservation trajectory above does not establish editing of
  those surrounding structures or all their combinations.
- No-op selection handling, cancellation, missing-binding failure, recovery,
  and rejection of unsupported or nonrepresentable native changes.
- Separate validation of subtree placement, cross-container editing and
  independently observed compatibility in desktop Modeler.
- A new packaged distribution containing the eventual production integration;
  the immutable `0.5.0-alpha.1` package does not contain this private probe.

## Private research provenance

The inspected local installation contained these unchanged assets:

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| `modeler-bpmn-editor.min.js` | 2,535,766 | `eec7db9e1474632e0e712c5df29ddc5b93aecb765cd8bc93422a1007ad8de1a9` |
| `modeler-bpmn-viewer.min.js` | 1,710,978 | `d10aabeb9c38acf34df3f7fc7c2577dfc19a6ce0b538b32efee339009959ec94` |

These are inspection fingerprints, not an operational compatibility allowlist.
Proprietary source, bundles and decompilation dumps are not redistributed.
The isolated native writes described above are bounded diagnostics, not a
public MCP layout feature or live-document synchronization. No vendor bundle
or decompiled source is redistributed.
