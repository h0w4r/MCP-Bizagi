# Native task and gateway type conversion

`native_elements_convert` changes existing elements through the installed
Modeler 4.3.0.008 `ChangeElementTypeCommand`. It does not delete/recreate a
diagram through BPMN import, rewrite native archive XML or control desktop UI.
The source file is retained; a successful operation returns a new native artifact.

This is an **experimental file-based capability**, not full Modeler automation.
The source implementation is newer than the immutable 0.4.0-alpha.1 release ZIP.

## Explicit request

Obtain native IDs, exact types and the source revision from `native_inspect`.
Submit `path`, `expectedRevision` and a `changes` array. Each
`NativeTypeConversion` requires:

| Field | Meaning |
| --- | --- |
| `ElementId` | Existing nonzero canonical native GUID; not an imported BPMN identifier |
| `ExpectedType` | Exact current palette type, checked before the command executes |
| `TargetType` | Different allowed type within the same category |

A batch contains 1–1,000 distinct identities. Duplicate identities, an unchanged
type, a stale source revision, an absent source identity and cross-category
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
Event conversion, task-to-subprocess conversion, reusable subprocess refactoring,
conditional/bot task options and live unsaved documents are not included.

## Execution and fidelity

1. Capture revision-checked source bytes and validate the native task/route
   selectors without modifying the input archive.
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

See [verification baselines](validation.md) for executed run identities and
their precise scope. Policy unit tests are separate from native accreditation;
neither the palette matrix nor offscreen rendering establishes exhaustive
rich-content preservation or independent Modeler desktop compatibility.
