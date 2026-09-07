# Native model creation

`native_model_create` constructs a new native document through the installed
Modeler model constructor, native diagram/domain defaults and native persistence.
It does not generate synthetic BPMN and import it as a substitute for native
creation. This is a file/artifact operation, not control of an open Modeler window.

## Request

```json
{
  "diagramNames": ["Request handling", "Escalation"]
}
```

Supply 1–100 distinct, nonempty diagram names, up to 120 characters each. The
names must also be valid native Windows export labels. Case-insensitive duplicates
are rejected before starting the worker. Unicode and spaces are supported.
The same request is available as [a JSON example](../examples/native-model-create.json).

The host generates each diagram GUID once and records the explicit creation
request under the operation. The requested array order becomes the persisted
opened-diagram tab order, with the first diagram selected. Diagram archive
enumeration order is not used as a substitute for tab preferences.

## Result and verification

Poll `operation_get` using the returned `OperationView.OperationId`. A completed
operation includes:

- `Result.outputArtifact`: `artifact:<operation-id>:model.bpm`, accepted by the
  other native tools even when private state is outside the workspace.
- `Result.outputRevision`: SHA-256 of the durable native file.
- `Result.created` and `Result.reopened`: native graph, diagram preferences and
  default simulation scenarios from separate worker processes.
- `Result.fidelity`: whole-container comparison against a subsequent native
  no-op save by another fresh worker.

The creation gate checks exact native diagram IDs/names, installed blank-diagram
defaults, ordered/selected preferences and all exposed graph/scenario semantics.
The no-op gate independently checks every archive entry, including properties
outside the graph DTO. Unknown materialization or loss fails the operation rather
than being dismissed as a harmless first-save change.

The published artifact is the original created file, not the stability-test copy.
No existing workspace model is overwritten. Native `DiagramModel.Id` is not
exposed as a stable document identifier: it is regenerated for runtime/scratch
use. Model name/path is file context, not asserted persistent business metadata.
Root-level diagram/resource `NativeElement.ParentId` is therefore empty, rather
than exposing the current worker's transient `DiagramModel.Id` as a durable owner.

## Continue editing

Use `Result.reopened.Elements` for native identities and containment. A visible
pool owns its `Process` element; use that process's `NativeElement.Id` as
`NativeMutation.ParentId` for new root-level tasks and flows. The separate main
participant is an installed native default and is not an extra user-created pool.
`NativeElement.IsMainParticipant` identifies the native invisible main boundary
(`true`), a visible pool (`false`), or a non-participant (`null`). Rendering checks
all graphical children but does not require a shape for an invisible empty shell.

Pass `Result.outputArtifact` and `Result.outputRevision` to `native_mutate`,
`native_diagrams_apply`, or the other explicit native edit tools. Every edit
produces another independently verified artifact. Explicit [file adoption and
replacement](native-commit.md) use `native_commit`, with separate source/target
revisions, backups and interruption reconciliation. Unsaved-session
synchronization and visual compatibility still have separate open gates.

## Real acceptance

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --model-create-only
```

The client creates single- and two-diagram models without a BPMN input, adds
start/task/end nodes and flows, renders and runs the actual native simulator,
then checks rejected editing and a fresh read of both edited and original files.
It also explicitly deletes the final added nodes/flows and compares the cleared
native file against the original blank artifact, without an expected-change waiver.
Simulation acceptance matches the actual added task's `NativeElement.BpmnId`
against `NativeSimulationElement.Id` and checks 1,000 default-case completions.
Native-generated black-box task metrics are retained, not confused with or
silently discarded in favor of the requested task.
Use `--package <extracted-directory>` to run the same public MCP path against a
distribution. Actual completed runs, not this command's existence or unit tests,
are recorded in [validation](validation.md).
