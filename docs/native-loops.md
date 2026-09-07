# Native activity loops

`NativeMutation.ActivityLoop` is an explicit complete replacement of an activity's
native loop configuration. Omit it to preserve existing metadata. `Kind` accepts
`None`, `Standard` or `MultiInstance`, with exactly the matching configuration
object. Non-activities, contradictory configurations and unknown typed fields
are rejected. Setting `Kind: "None"` explicitly removes the loop.

## Standard configuration

`NativeActivityLoop.Standard` contains:

| Field | Value |
| --- | --- |
| `Maximum` | Nonnegative native 32-bit integer, default 0 |
| `Counter` | Nonnegative native 32-bit integer, default 0 |
| `TestBefore` | Boolean, default false (test after) |
| `Condition` | Nullable native expression text, up to 1 MiB of .NET characters |

Zero is the native stored value, not a server-defined iteration policy. Null
condition means absent expression; empty string is explicit empty text. This
server does not evaluate either as executable application code.

## Multi-instance configuration

`NativeActivityLoop.MultiInstance` contains:

| Field | Value |
| --- | --- |
| `IsSequential` | Boolean, false for parallel |
| `Counter` | Nonnegative native 32-bit integer |
| `Behavior` | `All`, `One`, `None` or `Complex`, default `All` |
| `CompletionCondition` | Nullable native expression text |
| `ComplexCondition` | Nullable text; permitted only with `Behavior: "Complex"` |

Expression bounds match the standard condition. These are fields the installed
native persistence adapter represents. A counter is not advertised as a BPMN
loop-cardinality expression. Data-input/output references, arbitrary expression
languages, event references and multiple complex definitions are not implied by
this contract; they require their own integration and fidelity evidence.

## Native persistence and preservation

The adapter uses native `Activity.LoopType`, `Activity.StandardLoop` and
`Activity.MultiInstanceLoop`, maintaining the in-memory BPMN alias separately.
It does not write XPDL or regenerate a `.bpm` archive itself. The installed engine
persists the staged model, a new worker loads it and every requested loop field
is checked against that reader. Inspection returns `NativeElement.ActivityLoop`
on activities and null on other elements.

The whole-archive fidelity policy projects only the exact native activity's
known loop subtree after verifying the durable fields. Unknown attributes,
extension children, expression metadata, comments, significant text, duplicate
definitions and preserved-whitespace XML are not silently discarded. Other
activity fields, diagrams, resources, attachments and metadata remain compared.

## Simulation is a separate capability

Bizagi documents multi-instance task/subprocess simulation as unsupported.
Actual native simulation requests expose
`multi_instance_simulation_unsupported` with the activity identity and input
loop configuration; standard loops currently expose
`standard_loop_simulation_unaccredited`. These entries are in
`EngineReply.SimulationLimitations`, separate from native result metrics.
The server neither interprets conditions nor invents iterations to compensate.
[Official simulation considerations](https://help.bizagi.com/platform/en/simulation_in_bizagi.htm).

## Acceptance

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  . --native --loops-only --external-state
```

Add `--package <extracted-directory>` for a clean host/worker candidate. The
corpus creates loops on eight task subclasses, an embedded subprocess and a
reusable call; changes standard/multi-instance/none configurations; checks native
readback, no-op preservation, unrelated edits, wrong-kind failure, offscreen
rendering and Word publication. It separately clones the standard and
multi-instance palettes through the native diagram cloner, comparing loop
metadata using actual source/target identities. A separate single-task diagram records actual
simulation diagnostics, not accreditation of intended iteration behavior.

See [dated validation](validation.md) for completed circuits. The presence of
this contract, a compiled adapter or passing unit fixtures alone is not an
operational acceptance claim. Independent Modeler desktop visual compatibility
and complete automation remain separate gates.
