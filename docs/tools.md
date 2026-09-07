# Tool reference

All tools use the official MCP SDK over stdio. No tool accepts arbitrary code,
assembly names, reflection member names, or remote engine commands.

## File and discovery tools

| Tool | Required input | Output and boundary |
| --- | --- | --- |
| `capabilities_get` | None | Installed version, prerequisites, implementation status; not auto-accreditation |
| `bpmn_create` | `path`, `xml` | Saved path, byte revision, size; existing files rejected |
| `bpmn_inspect` | `path` | `ModelSummary.Revision`, process/diagram counts, nested elements |
| `bpmn_validate` | `path` | Structural findings with severity, code, optional element ID |
| `bpmn_apply_changes` | `path`, `expectedRevision`, `changes` | New revision and recoverable original backup |

`ModelChange` has `elementId`, `property`, and `value`. Only `property: "name"`
is implemented. Unknown edit types fail before persistence. File paths must
remain inside `MCP_BIZAGI_ROOT`. `.bpm` is rejected by every XML-only tool.

## Native tools

Native calls require explicit opt-in and Modeler 4.3.0.008. Each successful tool
submission returns an `OperationView`, **not** the completed engine result.

| Tool | Required input | Behavior |
| --- | --- | --- |
| `native_probe` | None | Resolve actual native services in an isolated worker |
| `native_roundtrip` | `path`; optional `modelName` | Import BPMN, persist `.bpm`, reopen in another worker, export BPMN, report fidelity findings |
| `native_inspect` | `path` | Read a private copy of existing `.bpm`; return native element IDs and projected BPMN |
| `native_apply_changes` | `path`, `expectedRevision`, `changes` | Native name batch to a new copy, then fresh-reader verification |
| `operation_get` | `operationId` | Durable state, latest phase, results or actual error |
| `operation_cancel` | `operationId` | Cancel owned work and clean up its worker |

`NativeNameChange` has `elementId` and `name`. Obtain native IDs from the
`native_inspect` operation result's `result.Elements` collection. Obtain the
expected byte revision from `sourceRevision`. Do not reuse IDs from the original
BPMN XML: the importer may regenerate them.

## Native edit sequence

1. Place an existing `.bpm` inside the configured workspace.
2. Call `native_inspect` with its relative path.
3. Poll `operation_get` until `OperationView.State` is terminal.
4. If completed, select IDs from `OperationView.Result.result.Elements` and
   retain `OperationView.Result.sourceRevision`.
5. Call `native_apply_changes` with that path, revision, and requested name batch.
6. Poll again. `Result.requestedChangesVerified` must match the requested batch.
7. Review the new file in `Result.edited.Artifacts`, its `outputRevision`, the
   fresh-reader result, and the explicit fidelity warning. The input is unchanged.

This is a copy-only experimental workflow. It does not silently publish a model
over the operator's source, assert preservation of every native field, or claim
that a BPMN export contains extended attributes and attachments.

## Results and failure handling

Immediate validation failures return `isError: true`. A submitted native
operation may fail later; inspect `OperationView.State` and `OperationView.Error`.
Terminal states are `completed`, `failed`, `cancelled`, and `interrupted`.
`running` and `cancelling` are not successful outcomes.

Results contain actual artifact paths and evidence. Those files are local to
the server account, not embedded downloads. Content and local path information
must be reviewed before forwarding results to an external service.

Do not automatically retry a failed write. Inspect its state and artifacts;
after a server restart, unfinished journal entries become `interrupted`.
