# Live Modeler sessions: integration boundary

**Experimental stdio MCP bridge; managed launch and release-wide live acceptance remain open.** File operations and a separately loaded native
model are not access to the document currently open in the desktop application.
Unsaved edits, the editor's revision, selection and undo history belong to that
application instance. An MCP-generated replacement file must not be described
as a synchronized live document.

## Development companion

The source now contains a dedicated `McpBizagi.LiveHost` process and versioned
`LiveSessionRequest` contracts. Its native adapter hosts the installed desktop
editor, dispatches explicit property batches through the native command manager,
and retains operation receipts across pipe reconnects. Its preferences are separate
from both the operator's Modeler preferences and disposable native workers.

Engineering tests have exercised actual in-memory editing, stale-revision rejection,
native undo/redo callbacks and receipt replay over the companion's named pipe.
The initial tests were **native-bridge tests, not public MCP acceptance**. The earlier
prototype also exercised native saving and fresh-process readback.

The source companion now synchronizes pending **canvas label text** through the
installed editor's own `syncPendingChanges` route. The bridge observes native
callback completion, checks the resulting in-process graph, and retains per-operation
synchronization evidence. Engineering acceptance created actual pending editor
text, observed that the native model still had its old name, then read the new name
through the bridge without saving. This does not certify every property-panel draft
or interaction surface.

An explicit `checkpoint` action now saves only the session-owned `.bpm` working
copy. It requires both live and disk revisions, retains the entire previous archive,
uses native Save without the focus-taking outer toolbar handler, and produces a
separate durable checkpoint artifact. Native Save clears undo history; this is not
silently presented as undo-preserving persistence. Stale disk revisions, locked
files and receipt replay were exercised against the real companion. A separate
installed-engine worker reopened the saved artifact; the tested multi-diagram
container passed whole-archive fidelity comparison. The saved dedicated editor also
closed normally. None of these tests publishes to an operator's original file.

The source now also exposes `live_read`, `live_apply`, `live_history`,
`live_checkpoint` and `live_reconcile` through the official MCP SDK. A real stdio
client exercised native unsaved editing, revision rejection, undo/redo, checkpoint
and an independent `native_inspect` worker. Disposing the first stdio client/server
and starting a second server preserved the same unsaved native document and its
receipt. That does **not** accredit an automated production launcher: the test
companion was independently launched by the engineering harness.

The modern client verifies session UUID, configured executable, process start time
and the actual named-pipe server PID before sending requests. Cancellation or
inactivity disconnects the client but never kills the editor. Reconciliation calls
a separate receipt endpoint; it never resends the original mutation. An unknown or
unavailable receipt is not treated as proof of no side effect. See the library's
[disconnect semantics](https://microsoft.github.io/vs-streamjsonrpc/docs/disconnecting.html).

The companion is not yet a supported launch/configuration workflow. Remaining
integration work includes independent session ownership, the remaining pending-editor
boundaries, guarded destination publication and the real stdio MCP circuit. In particular,
native autosave can write a document without an MCP save request: the companion
therefore rejects an initial document outside its owner's staging directory.
Checkpoint validates that boundary again, including reparse points, in case another
document was opened later. An explicit checkpoint stops this companion's autosave
timer; it does not modify the operator's separate Modeler preferences.
Opening an arbitrary existing `BizagiModeler.exe` process is not implemented by
this managed-companion route. Do not infer that ability from process discovery.

## Evidence from the installed 4.3.0.008 components

Read-only inspection identified these distinct routes:

| Route | Observed responsibility | Why it does not close live editing |
| --- | --- | --- |
| `OpenInstancesFinder` and `InstanceManager` | Locate an existing process associated with a model and activate its window | Activation is not document inspection, mutation or synchronization |
| `InstanceItem` | Records `ProcessId` and `ModelLoaded` | Contains no graph, unsaved change set or revision |
| `MemoryInstancesReader.Read` | Reads instance records and can remove stale entries by rewriting the instance-store file | Not a side-effect-free discovery API and not a document channel |
| `FrmModeler.DiagramModel` | Exposes the model to code already inside that application instance | An in-process property is not an external automation endpoint |
| `/webPublish` command-line route | Loads a model from a file | Does not obtain the open editor's unsaved model |

The vendor documents file-based command-line publication in
[Publish from the command prompt](https://help.bizagi.com/platform/en/publishing_batch.htm).
That documentation does not establish live editing. The observations above are
specific to the inspected components, not proof that every possible extension
route is unavailable. No vendor source or binaries are redistributed here.

## Required acceptance

A live integration must identify the actual editor instance and document;
observe an edit that has not reached disk; apply an explicitly revision-checked
native command; verify the live result and the subsequent saved file; and reject
conflicting editor changes without overwriting them. Closing a document,
disconnecting the MCP or restarting its host must not lose the operator's work.

The integration must respect native thread affinity, command notifications,
undo/redo and document lifetime. Reading a saved copy, examining an autosave file,
or creating a second model in the worker does not satisfy these requirements.
Neither window clicks nor foreground activation are substitutes. The experimental
tools do not attach to arbitrary operator-owned Modeler processes.

Independent verification **inside Modeler** is another open gate. The installed
offscreen renderer and a fresh persistence worker establish different facts and
must not be relabeled as desktop compatibility evidence.
