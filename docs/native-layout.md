# Native alignment and distribution

**Experimental source integration; eight modes verified through actual MCP.**
`native_elements_align` executes the installed editor, native commands and native
persistence in an isolated worker. Independent source and result readers verify
the complete archive. This is selected-shape alignment/distribution, not global
auto-layout, live unsaved editing or full Modeler automation. The immutable
`0.5.0-alpha.1` package does not contain this subsequent source addition.

## Operator contract

Supply `path`, its SHA-256 `expectedRevision`, and `alignment`:

| Field | Meaning |
| --- | --- |
| `DiagramId` | Actual native diagram ID returned by inspection |
| `SubProcessId` | Optional embedded editing surface; not the outer diagram ID |
| `ElementIds` | 2–1,000 distinct native IDs with one direct process/subprocess owner |
| `Mode` | One of the eight modes below; distribution requires at least three shapes |

Poll `operation_get` using the returned operation ID; use `operation_cancel` to
cancel only owned work. A successful result contains the immutable source/result
observations, fidelity report, native callback receipt, editor-policy evidence,
output artifact and revision. It never replaces the supplied original. Save-as
or replacement is a separate explicit guarded commit operation.

An independently established no-op still invokes the native editor and performs
native persistence/readback, but does not invent a mutation callback. Semantic
endpoint changes, participant reassignment, unsupported selection shapes and
unrepresented archive losses fail the transaction rather than reduce its scope.

## Production MCP evidence

Run `20260908-075431-883ad3` completed **13 native operations** and one expected
semantic-containment failure through the official stdio MCP client. It covered
all eight modes, a repeated Bottom no-op, stale-revision and unsupported-mode
rejections, and a successful operation after the native failure. The client
authored two diagrams, three differently sized Unicode tasks and a connection.
Its retained transcript SHA-256 is
`c7a26994216dccb0e7f998f121e1d5ac6daa2d84b4f13d8a7fdeda64f7921775`.

The final source rerun `20260908-081959-a690d6`, after nested-label and shared
cancellation hardening, passed the same 13 completed operations and one expected
failure. All 39 workers and 94 observed owned-process identities exited. Its
602 read-only desktop samples observed no owned visible window or foreground
ownership. Transcript SHA-256:
`7ace5a3dfeb9fe22fbd9e5a229cd2a8edb7d8ecbf597e44e844ebdbcb077b2c3`.

The same overlapping distribution fixture initially caused the editor to insert
a task into a flow, creating a connection and changing an endpoint. The adapter
now uses the installed command event bus to suppress that optional insertion
intent *before* native movement/routing executes. Suppressions are recorded;
callbacks are not filtered to conceal semantic changes. The vendor bundle and
its alignment/distribution algorithms are unchanged.

CEF Task return support is enabled before the first browser. Three async binding
challenges, one synchronous challenge and the actual mutation Promise's
fulfillment establish the transport boundary. Completing a .NET Task alone is
not treated as JavaScript acknowledgment. The earlier isolated diagnostics did
not establish this stronger acknowledgment gate.

Reproduce from a built source checkout on the supported Windows installation:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --alignment-only
```

The separate `--alignment-rich-only` corpus exercises nested surfaces, embedded
content, manual labels and active-CEF cancellation; its results must be recorded
separately from the root-level eight-mode baseline above.

### Nested surface, manual label and cancellation record

Run `20260908-081644-6316c6` passed **eight completed native operations and one
cancelled operation**. The client authored two diagrams, a two-level embedded
subprocess hierarchy, connected tasks and an actual file attribute. Bottom
alignment inside the inner subprocess passed whole-archive checks (716 atoms),
and subsequent attachment export matched the original bytes. A selected manual
label retained its size and node-relative offset across native commands and a
fresh reader: `(35,40,80,40)` became `(35,100,80,40)` after a 60-unit vertical
node movement. That trajectory checked 720 archive atoms.

The client observed the actual offscreen-render phase through MCP before
requesting cancellation, verified owned-process exits, then aligned again
successfully. Across this corpus, 21 workers and 43 observed process identities
exited; 302 read-only desktop samples recorded no visible owned window or
foreground ownership. Sampling is not continuous GUI observation.

- Transcript SHA-256: `a9f68eaea9bbdca56d45cc447bfb216c9acb87d24182e5000e5f53a35b085741`.
- Exported attachment SHA-256: `5846b01b6f762f57ca7be911075066de6a6ad462938329c85b8339b247bc2984`.

The version adapter records the native editor's transient participant aliases
before editing embedded surfaces. Its command hooks retain native ownership and
the existing flow target when the move helper proposes root-canvas docking.
Routing still executes in the installed editor; endpoint and archive checks are
not relaxed. Selected manual label translation is explicit host-derived intent
applied by the existing native styling adapter, not acceptance of arbitrary text
changes. Fractional, nonrepresentable manual bounds are rejected without rounding.
Manual connector-label routing and every nested diagram combination remain
separate from this selected-node label corpus.

A development cancellation exposed a later reader stopped before native settings
initialization. Cleanup acceptance now requires explicit no-dispatch/cancellation
evidence for that case, rather than inventing settings evidence. Normal completed
workers still require actual isolated settings paths. Common worker launch,
dispatch and result-return boundaries also check cancellation.

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

### Remaining broader acceptance

- The typed production MCP path above supersedes the private diagnostic-only
  interface; future binaries still need their own extracted-package acceptance.
- Broader multidiagram/subprocess, manual-label and boundary-editing cases. The
  root-level rich preservation trajectory above does not establish editing of
  those surrounding structures or all their combinations.
- Broader cancellation and missing-binding failures beyond the recorded real
  no-op and semantic-failure/recovery baseline.
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

The production alignment adapter pins the investigated editor fingerprint and
rejects changes until version-specific validation. Other assets retain their
separate worker inventory and rendering checks.
Proprietary source, bundles and decompilation dumps are not redistributed.
The historical isolated runs above are distinct from the later production MCP
baseline. Neither establishes live-document synchronization. No vendor bundle
or decompiled source is redistributed.
