# Native reusable subprocess calls

Current source extends `native_mutate` and `native_inspect` with local native
`CallActivity` references. It uses the installed Modeler element factory,
catalog-reference representation and recursive clone-reference updater. It does
not replace the native model with BPMN XML or open another model remotely.

## Explicit local targets

`NativeMutation.CallTarget` is a `NativeCallTarget` object:

| Field | Meaning |
| --- | --- |
| `ProcessId` | An existing local participant's `NativeElement.Id` whose `Kind` is `Process` |
| `ReplaceExternalReference` | Explicit acknowledgement required before replacing an existing external-model reference; defaults to `false` |

An omitted/null `CallTarget` preserves the reference. `ProcessId: ""` explicitly
unlinks a call. Any nonempty ID must be a canonical lowercase, nonempty GUID.
A collaboration/diagram ID is not interchangeable with its participant process
ID. Find the desired `Process` in `native_inspect` using `NativeElement.ParentId`
and `NativeElement.DiagramId`; each participant has its own process identity.

Create a call with `NativeMutation.Operation: "create"`,
`NativeMutation.ElementType: "CallActivity"`, a new `ElementId`, a native process
or embedded subprocess `ParentId`, and the explicit `CallTarget`. A property-only
`update` can change the target without changing the name or geometry. Only a
native call activity accepts this property. `delete` and `reconnect` cannot
carry hidden call-target updates.

See the [request example](../examples/native-calls.json). Replace every example
identity with an identity read from your own model, except IDs for newly created
elements. Submit ordered batches and use each successful operation's new
`outputArtifact` and `outputRevision` for the next transaction.

## Inspection and durable verification

`NativeElement.CallReference` is null for non-call elements. For a call it exposes
the installed engine's separate representations:

- `NativeCallReference.CatalogProcessId`: local native catalog process reference.
- `NativeCallReference.BpmnName` and `BpmnNamespace`: original native BPMN QName.
- `NativeCallReference.External`: optional workspace/diagram/process identities,
  with no external model fetch or authentication implied.

An explicit local target replaces stale QName and external-reference state.
After native persistence, a separate worker must find the requested local
catalog target and no conflicting QName/external link. The fidelity comparator
projects only the requested native XPDL `Activity/Implementation/SubFlow` `Id`
attribute at its actual native location. All other XML, attributes, runtime
metadata, attachments and archive entries remain compared.

An external-reference replacement acknowledgement is a guard, not an accredited
external-model editing capability. External model creation/resolution, cloud
workspaces and external-to-local conversion corpora remain unverified. A native
conversion that changes unknown metadata fails fidelity rather than discarding
that metadata silently.

## Protected deletion and cloning

Deleting a diagram or participant/process checks incoming native calls,
including calls nested in embedded subprocesses. A surviving caller must be
explicitly unlinked, redirected or removed before its target is deleted.
Deleting one diagram never silently rewrites a caller in another diagram.
The checks inspect native catalog identities and recognizable local BPMN QNames;
they do not claim to resolve arbitrary external QNames.

`native_diagrams_apply` invokes the installed recursive call-reference updater
with the native clone's actual identity map. References to processes copied in
the same diagram map to the corresponding copied process. References to other
diagrams retain their original local target. A fresh worker and the full native
clone comparison verify those results independently.

The installed low-level cloner can reset invisible main-pool dimensions. The
adapter restores that source pool's actual size in the new native object before
persistence, instead of suppressing the resulting zero/default-size difference
in the fidelity comparator. This does not modify source pool dimensions.

## Simulation semantics: a real native black box

Modeler does **not** simulate the elements inside a reusable subprocess. Set
the overall processing time on the call shape; use an embedded subprocess when
the simulation must exercise its internal logic. This is documented native
behavior, not an MCP fallback. [Bizagi simulation considerations](https://help.bizagi.com/platform/en/simulation_in_bizagi.htm).

Both `native_simulate` and `native_simulate_what_if` return
`EngineReply.SimulationLimitations`. Each detected call in the selected diagram,
including embedded descendants, has a `NativeSimulationLimitation` containing:

- `Code: "reusable_subprocess_black_box"`.
- `DiagramId`, `ElementId` and `BpmnId` identifying the actual input call.
- `CallReference` captured before native simulation changes in-memory QNames.
- `Message` and `DocumentationUrl` explaining the native behavior.

An empty limitations array is not an exhaustive semantic-support assessment.
Raw generated task IDs and metrics remain available in `SimulationReports` and
the original XML. Do not attribute a generated black-box task's completions to
the linked process's internal tasks. Simulation does not save its transient
QName changes or results back into the input `.bpm`.

## Acceptance and remaining scope

The independent official MCP client can run both circuits against the locally
installed engine, optionally adding `--package <extracted-directory>`:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  . --native --calls-only --external-state
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  . --native --calls-behavior-only --external-state
```

The lifecycle corpus exercises local and embedded callers, reference-preserving
edits, invalid target/kind rejection, guarded deletion, relinking, unlinking,
native cloning, fresh-process readback, no-op fidelity and offscreen rendering.
The behavior corpus uses actual configured simulation and what-if results,
linked-model Word/PDF publication and independent content readback. Consult the
[dated evidence](validation.md) for completed runs and failures retained during
development; source code or command presence does not itself accredit a circuit.

Expanded reusable-process representation, arbitrary external targets, all
advanced reusable-process properties and independent desktop visual compatibility
remain separate acceptance families. This local-call contract does not close
the full Modeler automation objective.
