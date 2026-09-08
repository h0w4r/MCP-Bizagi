# Native task, gateway and unbound-call conversion

`native_elements_convert` changes existing elements through the installed
Modeler 4.3.0.008 `ChangeElementTypeCommand` for same-category edits, or
`RefactorElementsCommand` for task-to-unbound-call conversion. It does not delete/recreate a
diagram through BPMN import, rewrite native archive XML or control desktop UI.
The source file is retained; a successful operation returns a new native artifact.

This is an **experimental file-based capability**, not full Modeler automation.
Same-category conversion is included in the 0.6 package. Task/call conversion
and destination attribute-scope checks are newer source additions, not changes
to the immutable 0.6.0-alpha.1 release ZIP.

## Explicit request

Obtain native IDs, exact types and the source revision from `native_inspect`.
Submit `path`, `expectedRevision` and a `changes` array. Each
`NativeTypeConversion` requires:

| Field | Meaning |
| --- | --- |
| `ElementId` | Existing nonzero canonical native GUID; not an imported BPMN identifier |
| `ExpectedType` | Exact current palette type, checked before the command executes; `CallActivity` requires an explicitly unbound source for conversion to a task |
| `TargetType` | Different allowed type within the same category, `CallActivity` for a task source, or a task type for an unbound call |

A batch contains 1–1,000 distinct identities. Duplicate identities, an unchanged
type, a stale source revision, an absent source identity and other cross-category
requests are rejected. Batch order is explicit; all changes are staged together.
An editable request is provided in [the JSON example](../examples/native-conversions.json).
Its sample path, revision and identity must be replaced with inspected values.

### Supported categories

- Tasks: `AbstractTask`, `UserTask`, `ManualTask`, `ServiceTask`, `ScriptTask`,
  `SendTask`, `ReceiveTask`, `BusinessRuleTask`.
- Gateways: `ExclusiveGateway`, `InclusiveGateway`, `ParallelGateway`,
  `ComplexGateway`, `EventBasedGateway`, `EventBasedGatewayExclusive`,
  `EventBasedGatewayParallel`.

The three event-based gateway selectors distinguish the native instantiation
and exclusive/parallel settings. They are not interchangeable aliases.
The native class for `AbstractTask` is `Task`; the three event gateway selectors
share the `EventBasedGateway` class. Both class and palette selector are checked.

Tasks in native processes and embedded subprocesses use the same contract.
Event conversion, task-to-embedded-subprocess conversion, conditional/bot task
options and live unsaved documents are not included.
Embedded-subprocess extraction has its own [refactoring contract](native-refactoring.md).

### Task to an unbound call

All eight task source types above may explicitly target `CallActivity`, at root
or inside an embedded subprocess. The actual native command's
`TasksToReusableSubProcess` mode preserves the activity identity, but **does not
create a diagram or choose a called process**. This is not extraction or inlining.

The fresh read must show empty `CallReference.CatalogProcessId`, `BpmnName` and
`BpmnNamespace`, and no external reference. To bind afterwards, submit an explicit
`native_mutate` update with `CallTarget.ProcessId` containing an inspected native
participant process ID. An empty `ProcessId` explicitly clears that binding.
Do not pass a diagram ID. See [native calls](native-calls.md) and the
[task-to-call request example](../examples/native-task-to-call.json).

The adapter supplies the installed element-configuration service normally assigned
by the desktop; it does not create an editor to initialize this dependency.
It restores the source shape size and graphics values that the menu command
would otherwise reset. Newly exposed `ExpandedGeometry` coordinates, colors and
expansion state must match the common source geometry. Hidden expanded dimensions
remain protected by whole-archive comparison; they are not silently assigned a
new layout. This does not make an unbound call executable by the simulator.

### Unbound call to a task

Use `ExpectedType=CallActivity` and one of the eight task `TargetType` values.
The installed `ChangeElementTypeCommand` performs the replacement, preserving
the native activity identity and common content. The result must have no
`CallReference`; it is a task, not an unresolved call with a different label.
An [editable reverse request](../examples/native-call-to-task.json) is provided;
replace its source, revision and identity after any explicit unlinking operation.

A bound local call must first be explicitly unlinked through `native_mutate`
with an empty `CallTarget.ProcessId`. Replacing an external reference also needs
the existing `ReplaceExternalReference=true` acknowledgement. Conversion never
clears these links implicitly, deletes the target process, or moves its children.
It is **not reverse subprocess inlining** and it is not a restoration of unseen
historical task content. The requested task receives its native factory defaults.

Nondefault call runtime content and expanded layout cannot silently disappear.
The reverse path requires a collapsed call with zero latent expanded dimensions
or the installed native factory's paired 270 × 180 baseline. That baseline is
not recalculated from the user's current collapsed rectangle. A native file with
other expanded layout is rejected rather
than normalized into that corpus. Destination attribute definitions must apply
to the requested task type, just as in other conversion directions.

## Execution and fidelity

1. Capture revision-checked source bytes and validate the native task/route
   selectors without modifying the input archive. Every existing attribute value
   on a converted element must have a native definition explicitly applicable
   to its destination type. If not, update the definition intentionally through
   `native_attributes_apply` before conversion; preserving hidden value bytes alone
   is not sufficient. This rule applies to same-category conversion too.
2. Read the source through an isolated native worker.
3. Execute the installed type-change command in a separate writer worker.
4. Restore the original collection ordinal and native label/graphics values
   that the command's generic graphics copy omits. Reconcile data links and
   compensation references through the native object model.
5. Persist the candidate `.bpm`, terminate the writer and load the candidate in
   another worker.
6. Check identity sets, requested selectors, native classes, graph properties,
   relationships, geometry, styling and all remaining archive leaves.

Only requested type selectors and exactly recognized neutral, type-owned
factory defaults are projected out **in comparison copies**. No projection is
written back into a native archive. Known defaults include zero cost/priority,
false native user/service flags, the empty script factory expression and the
service command's explicit `isAsynchronous=false` value. The constructor may
omit that nullable value; the actual command materializes it.

For task-to-call, the installed serializer also materializes collapsed
`Expanded=false`, zero expanded dimensions and call runtime defaults:
`priority=0`, empty `asynchronousBehavior`, `subProcessType=None`,
`inputMappingType=None`, `outputMappingType=None` and `exitMode=AllTokens`.
Only these exact neutral values are recognized. Changed dimensions, nondefault
mapping/exit settings and unknown runtime fields are still compared, not waived.

Unknown and nondefault content remains protected. Nonempty scripts, nondefault
implementation settings and gateway activation conditions cannot be silently
retired. Unknown runtime properties remain compared; a native command dropping
one causes failure. XML comments, namespace extensions, explicit whitespace
preservation and embedded bytes are not generic normalization exemptions.
There is no blanket `allowDataLoss` switch.

The result's `conversionInterpretationWarning` explains that selector/default
changes do not establish equivalent simulation behavior, model validity or
desktop visual compatibility. Converting a gateway can change process semantics
even when every common field and outgoing connection survives exactly.
Run `native_validate` separately and review its actual findings.

## Results and failures

Poll `operation_get` until the submitted operation is terminal. A successful
`OperationView.Result` contains `before`, `edited`, `reopened`, `fidelity`,
`requestedChangesVerified`, `outputArtifact`, `outputRevision` and
`nativeSourceUnmodified`. Only completed operations produce reusable artifacts.

Private operation evidence retains the captured request, three-process readback
and whole-archive fidelity report. A failure does not publish over the source.
Do not replay an uncertain write blindly; inspect its journal and artifacts.
Workspace publication remains a separate revision-checked `native_commit` call.

## Real acceptance

The independent official SDK client executes every directed pair among the
eight task types (56) and seven gateway types (42), plus one nested task:
**99 requested conversions**, followed by the 99 reverse conversions.
The corpus exercises Unicode descriptions, styles, label bounds, original
collection order, an incident sequence flow, fresh-worker reads, native no-op
save, nested offscreen rendering and original-file revision checks.

The enriched corpus authors standard/multi-instance loops on the 56 root tasks,
nondefault start/completion quantities, one resource assigned to all four RACI
roles, two native attribute definitions applicable across task types, a Unicode
text value and an embedded non-XML file. All content is created through native
MCP tools, not by fabricating or rewriting a `.bpm` archive. Independent
documentation/metadata reads and byte-exact attachment extraction supplement
the conversion host's own graph and archive gates in both directions.
Definition audit timestamps are validated separately from user content;
native save can update those timestamps without changing definition values.

Build and execute the actual installed-engine circuit:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build --no-restore -- . --native --conversions-only
```

The separate task-to-call circuit covers all eight root task types and one nested
task, native input associations, boundary/compensation references, incident flow,
loops, quantities, RACI, scoped Unicode attributes and a byte-exact attachment.
It exercises scope rejection followed by explicit definition repair, checks that
no diagram was created, binds and clears both root/nested calls, rejects conversion
while bound, and explicitly converts calls back to task types before native
save/render. Independent factory-created calls extend the reverse corpus beyond
the objects produced by task-to-call conversion. See the execution record for
the exact verified runs; a test's presence is not its operational accreditation.

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build --no-restore -- . --native --task-to-call-only
```

See [verification baselines](validation.md) for executed run identities and
their precise scope. Policy unit tests are separate from native accreditation;
neither the palette matrix nor offscreen rendering establishes exhaustive
rich-content preservation or independent Modeler desktop compatibility.
