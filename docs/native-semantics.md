# Native activity and sequence-flow semantics

Current source extends the revision-checked `native_mutate` transaction with
explicit activity properties, gateway directions and sequence-flow conditions.
These fields are read and written through the installed native engine, followed
by a new worker reading the durable `.bpm`. Unknown native content remains part
of the whole-archive comparison. No expression interpreter or simulator is
implemented by this server.

## Activity properties

`NativeMutation.ActivityProperties` is a `NativeActivityProperties` patch:

| Field | Accepted value | Omission |
| --- | --- | --- |
| `StartQuantity` | Positive 32-bit integer | Preserve |
| `CompletionQuantity` | Positive 32-bit integer | Preserve |
| `IsForCompensation` | Boolean | Preserve |
| `State` | `None`, `Ready`, `Active`, `Completing`, `Completed`, `Aborted`, `Aborting` | Preserve |

An empty patch is rejected. Properties apply to native activities, including
task subclasses, embedded subprocesses and reusable calls, not events, gateways,
participants or their processes. A state is native model metadata; setting it
does not operate a Bizagi Studio/Automation case. Existing loop configuration
and all other activity fields are untouched by this patch.

`NativeElement.ActivityProperties` is null on non-activities. On an activity,
inspection fills all four fields with the actual native values.

The fidelity gate checks the exact native XPDL activity attributes. When a source
activity's completion quantity changes, Modeler's persistence adapter also
derives each outgoing native transition's `Quantity`. This dependency is
verified against the reopened source activity and projected explicitly; no
other transition attribute or unknown extension is ignored.

## Gateway direction

`NativeMutation.GatewayDirection` accepts `Unspecified`, `Converging`, `Diverging`
or `Mixed` on a native gateway. Null preserves the field. Inspection returns
`NativeElement.GatewayDirection`; it is null for non-gateway elements. The
comparison scopes the change to the native activity's `Route.GatewayDirection`.
This declaration alone does not make an invalid topology valid.

## Sequence-flow conditions

`NativeMutation.FlowCondition` is a complete `NativeFlowCondition` replacement:

- `Kind: "None"`: clear the condition/default designation.
- `Kind: "Expression"`: store `Text` as native expression text.
- `Kind: "Default"`: designate the source's default outgoing sequence flow.

Only `Expression` accepts nonempty `Text`; its maximum length is 1 MiB of .NET
characters. Omit `FlowCondition` to retain the existing condition. Unknown
expression attributes, nested extension elements and comments cannot be silently
discarded by this text-only contract. Native expression languages and arbitrary
mixed XML are not implied by the presence of an expression field.

Conditional/default flows require an activity, exclusive gateway, inclusive
gateway or complex gateway source. A source may have at most one default.
To switch defaults, explicitly clear the old default first, then designate the
new one in the ordered batch. The adapter never changes another flow's condition
to make the request succeed. Reconnecting a conditional/default flow to an
unsupported source kind is rejected. Reconnection and deletion synchronize only
the touched sources' native default-reference aliases.

Inspection returns `NativeElement.FlowCondition` on native sequence flows and
`NativeElement.DefaultSequenceFlowIds` on their sources. The latter is an array
derived from actual outgoing native flows, so an already-invalid model can
expose multiple identities rather than receiving a fabricated single ID.

Modeler treats these as documented conditions on decision branches. This server
stores the text; it does not evaluate it as application code. Simulation routing
parameters and actual branch behavior require their own BPSim acceptance.
[Official gateway-condition documentation](https://help.bizagi.com/platform/en/defining_gateway_conditions.htm).

## Strict typed input

The official MCP SDK's JSON parameter marshaller rejects unknown members inside
typed requests, including nested patches and mutation arrays. A misspelling such
as `CompletionQuantiy` must fail instead of being silently ignored. These binding
failures happen before the tool method; the SDK may return a plain-text tool
error rather than the operation journal's JSON error. The acceptance recorder
retains that actual error. This is not a blanket claim that every possible
unknown top-level method argument is rejected.

## Simulation input is not simulation behavior

`EngineReply.SimulationInputs` records actual input artifacts and
`NativeSimulationActivityInput` entries with native `ElementId`, `BpmnId`,
`StartQuantity` and `CompletionQuantity`. Every selected native activity must
appear exactly once with those quantities in the XML consumed by the installed
simulation/what-if manager. Missing or changed values fail the operation.

This proves transport/input fidelity, **not execution of the intended quantity
semantics**. In the local 4.3.0.008 corpus, twelve instances split into two parallel
branches. The task receiving the branches persisted and exported the requested
quantities, but the native simulator produced these end-event counts:

| Start / completion quantity | Intended corpus count | Observed native count |
| --- | --- | --- |
| 1 / 1 | 24 | 24 |
| 2 / 1 | 12 | 24 |
| 2 / 3 | 36 | 24 |

Nondefault activity quantities therefore produce the explicit
`nondefault_token_quantities_unaccredited` entry in
`EngineReply.SimulationLimitations`, including the exact activity and its input
properties. The mismatch is not concealed by changing the output, replacing the
native simulator or presenting the intended count as an actual result. The
vendor documents start/completion quantities as token-related activity
properties; the observed native behavior remains an open accreditation issue.
[Official simulation considerations](https://help.bizagi.com/platform/en/simulation_in_bizagi.htm).

## Reproduce and distinguish the gates

```powershell
# Editing, native readback, strict-input rejection and honest simulation diagnostics.
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  . --native --semantics-only --external-state

# Additional strict gate: remains failing while native quantity behavior differs.
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- `
  . --native --semantics-only --require-token-semantics --external-state
```

Add `--package <extracted-directory>` to test a clean distributed host/worker.
The property palette includes eight task subclasses, an embedded subprocess and
a reusable call. It deliberately has disconnected elements; it is not presented
as an executable process. The quantitative simulation uses a separate diagram.
The client also exercises source/default references, native rendering, Word
publication, wrong kinds, duplicate-default rejection and no-op save fidelity.

See [dated evidence](validation.md) for actually completed runs. Subsequent
[loop editing](native-loops.md) and [event payload](native-event-payloads.md)
contracts have their own implementation and acceptance; they are not wholly
unimplemented merely because this earlier semantic corpus does not cover them.
Advanced activity/gateway options, every conditional topology, nondefault token
execution and independent desktop visual compatibility are not accredited by
this contract. The full Modeler automation objective remains open.
