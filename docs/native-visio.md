# Native Visio VDX interchange

`native_visio_export` and `native_visio_import` use the installed Modeler
4.3.0.008 `IVisioManager` and its Aspose-backed implementation. They do not
generate Visio shapes by hand, automate the desktop, activate Visio through COM,
or redistribute the installed libraries. This is an **experimental source
capability**, newer than the immutable 0.5.0-alpha.1 package.

## Format boundary: explicitly lossy

Both tools require `acknowledgeFormatLimits: true`. VDX is an interchange
projection, **not a backup or an alternative way to preserve a native model**.
The original `.bpm`/`.vdx` bytes are captured and never overwritten.

The real local corpus exposed important native limitations:

- Root task Unicode labels survive the tested export/import route.
- The tested `UserTask` becomes a generic native `Task` after Visio import.
- The exporter reserves an extra page for a populated embedded subprocess,
  but leaves that page empty. The nested task does **not** survive exchange.
  A page-count match alone would incorrectly accredit this case.
- Initial import can normalize graphics, styles and serialized reference caches.
- Native identities are regenerated. Original and imported graphs are not
  compared as if those identities were preserved.
- Native simulation configuration is not a promise of this interchange route.
  Metadata differences are returned explicitly.

These losses are exposed in `pages`, `emptyPages`, `projectionDifferences`,
`graphDifferences`, `metadataDifferences` and `nativeRoundtripComparison`.
`projectionDifferences` is a kind/name multiset comparison, **not an identity
mapping or a proof of geometry, connection, execution or visual equivalence**.
An empty multiset difference would not imply a lossless conversion.

There is no claim of nested-content preservation, arbitrary stencil support,
Microsoft Visio visual compatibility, or Full Modeler Automation. The native
exporter's blank-subprocess-page behavior remains an explicit unresolved route.
Review exported files and evidence for private text/metadata before sharing.

## Export

Call `native_inspect` to obtain actual collaboration GUIDs and source revision.
Then call `native_visio_export`:

| Input | Contract |
| --- | --- |
| `path` | Workspace `.bpm` or completed native artifact reference |
| `expectedRevision` | SHA-256 of the exact source bytes |
| `diagramIds` | 1–100 distinct existing native collaboration GUIDs |
| `acknowledgeFormatLimits` | Required explicit `true` |

The installed exporter produces `export.vdx`. A separate native importer creates
a verification `.bpm`; fresh workers read, save without requested changes and
read it again. The native no-op must pass the existing whole-archive fidelity
policy as well as graph and metadata equality. A completed export returns:

- `outputArtifact`: `artifact:<operationId>:export.vdx`
- `outputRevision`: exact VDX SHA-256
- `verificationModel`: `artifact:<operationId>:model.bpm`
- `verificationRevision`: exact verification model SHA-256
- `selectedDiagramIds`, page/shape inventory, explicit loss reports and native
  import/no-op/restart evidence.

Only completed Visio export artifacts can be reused by the Visio import tool.
The verification model is a **lossy roundtrip output**, not the original model.

## Import

Call `native_visio_import` with:

| Input | Contract |
| --- | --- |
| `input.Path` | Workspace `.vdx` or completed Visio export artifact |
| `input.ExpectedRevision` | Exact source SHA-256 |
| `modelName` | Optional new model name; default `Imported Visio` |
| `acknowledgeFormatLimits` | Required explicit `true` |

The current contract accepts Visio 2003-namespace XML `.vdx`, **not binary
`.vsd` or packaged `.vsdx`**. Those formats need their own bounded preflight,
native corpus and compatibility evidence. Files with DTDs, XInclude, processing
instructions, external base contexts, duplicate identities or excessive nesting
are rejected before native loading. This is not exhaustive Visio schema validation.

The source page/shape inventory is preserved in the result alongside the actual
imported native graph. Pages reported by the native manager as having unmapped
shapes fail explicitly instead of publishing its partial result. The inventory
does not claim that every source shape can be mapped to a BPMN element.

Import initialization is deliberately separate from preservation:

1. Native import constructs a new model. The worker records its transient graph.
2. Missing native empty attribute collections and invisible-pool default sizes
   are initialized before its first persistence, using installed domain defaults.
3. A fresh native read is compared with the imported graph. Only the explicitly
   observed graphics/style and derived-reference normalization fields are allowed;
   changes to identities, kinds, labels, containment or semantic properties fail.
4. Metadata can only gain verified empty value containers for existing elements.
   Nonempty/unknown content or removed metadata is not a normalization exception.
5. A separate no-op writer and final reader must prove stable graph, metadata
   and whole-archive fidelity. The original global fidelity policy is unchanged.

The returned `outputArtifact` is the independently read native `model.bpm`, with
its exact `outputRevision`. Failed outputs remain quarantined in local evidence.

## Progress, recovery and diagnostics

Poll `operation_get`; use `operation_cancel` for cancellation. Native progress
events are forwarded as reported by the vendor. In particular, an export progress
maximum that includes reserved subprocess pages is not proof those pages contain
content. There is no fabricated completion percentage or total-operation timeout.

Diagnostics retain `visio-import-conflicts.json`, `visio-transient-graph.json`,
`visio-import-normalizations.json`, `visio-native-restart.json` and the final
import/export readback. Worker process/desktop observations are separate evidence;
they do not establish compatibility inside the Modeler GUI.

The real acceptance entry point is:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --visio-only
```

See [validation](validation.md) for the actual run record. Unit fixtures test
preflight/difference policy only and never replace this installed-engine circuit.

## Primary documentation and local evidence

Bizagi's [Visio export documentation](https://help.bizagi.com/platform/en/visio_documentation.htm)
describes a Microsoft Visio installation prerequisite. The inspected and executed
4.3.0.008 route instead uses the locally installed Aspose-backed manager without
Visio COM activation. This observation is version-specific, not a blanket
statement about every Bizagi build or format. The
[Aspose Diagram API reference](https://reference.aspose.com/diagram/net/aspose.diagram/diagram/)
documents its document model; local binary fingerprints and real worker results,
not documentation alone, identify the adapter route actually exercised.
