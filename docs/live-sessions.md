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
closed normally. Checkpoint alone never publishes to an operator's original file.

The source now also exposes `live_read`, `live_apply`, `live_history`,
`live_checkpoint`, `live_publish`, `live_close` and `live_reconcile` through the official MCP SDK. A real stdio
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
integration work includes independent session ownership, owner cleanup and packaged
lifecycle acceptance. Pending-editor coverage beyond canvas labels remains explicit, not presumed. In particular,
native autosave can write a document without an MCP save request: the companion
therefore rejects an initial document outside its owner's staging directory.
Checkpoint validates that boundary again, including reparse points, in case another
document was opened later. An explicit checkpoint stops this companion's autosave
timer; it does not modify the operator's separate Modeler preferences.
Opening an arbitrary existing `BizagiModeler.exe` process is not implemented by
this managed-companion route. Do not infer that ability from process discovery.

## Publish a retained checkpoint safely

`live_publish` bridges the session registry and the configured model workspace. It
accepts a **checkpoint operation ID**, not arbitrary private paths or executable
arguments. Both native artifact names, hashes, request identity and completed save
receipt are verified before adoption. Publication can also read the native retained
receipt when a disconnected MCP host did not record its reply; it never repeats Save.
The editor need not remain open to publish already retained evidence.

1. Obtain a current snapshot with `live_read` and poll `operation_get`.
2. Apply explicit edits using `live_apply` and the snapshot's live revision.
3. Call `live_checkpoint` with current live and working-copy disk revisions; wait
   for its completed receipt. Save clears native undo and stops companion autosave.
4. Call `live_publish` with that checkpoint operation ID, an **existing** workspace
   `.bpm`, its observed SHA-256, and the complete expected Name/Documentation changes
   relative to that destination. An empty array requires semantic equivalence.
5. Poll `operation_get`. Success includes the destination revision, backup path,
   whole-archive fidelity and an independent installed-engine readback.

Example `live_publish` arguments (replace IDs and hash with observed values):

```json
{
  "checkpointOperationId": "0123456789abcdef0123456789abcdef",
  "destinationPath": "models/approval.bpm",
  "expectedDestinationRevision": "<observed-destination-sha256>",
  "expectedChanges": [
    {
      "ElementId": "11111111-2222-3333-4444-555555555555",
      "Name": "Review request",
      "Documentation": "Check the submitted request before approval."
    }
  ]
}
```

The existing `NativeMutationFidelity` policy accounts only for explicitly requested,
independently read-back text changes. Unknown XML, nested diagrams, attachments and
other native content are still checked. An unexplained difference rejects publication;
it is not waived because native Save succeeded. This initial live publication contract
does not accept arbitrary structural or geometry changes made manually in the editor.

`WorkspaceFiles.Commit` rechecks the destination, retains a recoverable backup and
performs controlled byte-exact replacement. An immutable commit intent precedes that
replacement. A conflict leaves the checkpoint and live editor intact. After a lost
reply, cancellation or host restart, use `native_commit_reconcile` on the **publication**
operation ID. It observes destination/stage/backup and performs another native read
when applied; it never replays the write. `live_reconcile` remains for native editor
operations, not destination publication. Missing evidence is not proof of no effect.

Publishing a checkpoint **does not retarget the editor** or mark later unsaved edits
as published. Results identify the precise saved document revision. Subsequent edits
require another checkpoint and a freshly observed destination revision. Session and
operation-state directories are not valid publication destinations.

### Verified source-level publication trajectory

The independent stdio acceptance client exercised installed Modeler **4.3.0.008**:

| Check | Observed result |
| --- | --- |
| Native name and documentation edits | Preserved through Save and independent native reload |
| Original-file publication | Exact checkpoint bytes, recoverable byte-identical original backup |
| Full native container | 20 entries and 691 atoms checked; only requested text and recognized metadata changes |
| External destination replacement | Revision conflict; external replacement left untouched |
| Undeclared native differences | Publication rejected before destination replacement |
| Later unsaved editor edit | Same live revision/name/dirty state after older checkpoint publication |
| MCP restart | Publication intent reconciled with another native reader; no write replay |
| Missing MCP checkpoint reply | Native durable receipt used without repeating native Save |

These are source integration results, not packaged lifecycle or universal GUI
compatibility claims. The dedicated test editor closed normally afterward; automatic
production ownership and packaged lifecycle still require their own acceptance.

## Close only a durably retained clean editor

`live_close` requires the session UUID, current live revision, current working-copy
SHA-256 and the MCP operation ID of its matching native checkpoint. The native
adapter synchronizes pending canvas labels, rechecks the document on its UI thread,
and rejects dirty state, stale revisions, absent checkpoints or changed artifacts.
It does not silently save, discard unsaved work or publish to the original.

The bridge uses the installed application's normal `Form.Close` lifecycle, not
clicks, synthetic input, forced process termination or direct form disposal. Native
closing can cancel; see [the Windows Forms contract](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.close).
A flushed **closing** receipt means admission only. The MCP client pins the actual
editor process and reports completion only after observing exit code zero and
rechecking working-copy/checkpoint hashes. A broken RPC pipe alone is not success.

Native CPU and journal/log activity reset a configurable inactivity window. Expiry
or cancellation stops observation, **never kills the editor** and never authorizes
another close attempt. The exit observation is independently flushed to disk;
`live_reconcile` can read it after both editor exit and MCP restart, without a pipe.
If that observation is absent, reconciliation returns **unknown**, not an invented
successful close or permission to replay it.

The current `LiveEditorExit.OwnedTreeVerified` is explicitly false: the client proves
the editor's OS exit, not the independent owner's descendant cleanup. Production
owner lifecycle and packaged acceptance remain separate release requirements.
Native closing may call its own focus-related handlers; no blanket no-foreground
claim follows from clean file persistence or process exit.

The source stdio acceptance suite exercised dirty-document and wrong-checkpoint
rejection, normal native closing, unchanged durable file hashes, retained exit
reconciliation after editor exit, and the same reconciliation from a fresh MCP
process. The dedicated editor exited with code zero; its independent engineering
supervisor also verified all 20 observed owned processes exited. That supervisor
evidence does not turn the still-missing production owner into an implemented one.

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
