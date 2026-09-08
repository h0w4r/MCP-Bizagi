# Live Modeler sessions: integration boundary

**Not implemented or accredited.** File operations and a separately loaded native
model are not access to the document currently open in the desktop application.
Unsaved edits, the editor's revision, selection and undo history belong to that
application instance. An MCP-generated replacement file must not be described
as a synchronized live document.

## Evidence from the installed 4.3.0.008 components

Read-only inspection identified these distinct routes:

| Route | Observed responsibility | Why it does not close live editing |
| --- | --- | --- |
| `OpenInstancesFinder` and `InstanceManager` | Locate an existing process associated with a model and activate its window | Activation is not document inspection, mutation or synchronization |
| `InstanceItem` | Records `ProcessId` and `ModelLoaded` | Contains no graph, unsaved change set or revision |
| `MemoryInstancesReader.Read` | Reads instance records and can remove stale entries by writing the registry | Not a side-effect-free discovery API and not a document channel |
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
Neither window clicks nor foreground activation are substitutes. No established
live route is currently exposed as an MCP tool.

Independent verification **inside Modeler** is another open gate. The installed
offscreen renderer and a fresh persistence worker establish different facts and
must not be relabeled as desktop compatibility evidence.
