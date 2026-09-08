# Native presentation-action migration

Current source after 0.6.0-alpha.1 extends `native_elements_reparent` with explicit
`migratePresentationActions: true`. The immutable 0.6 ZIP does not include this
addition. It moves existing native action definitions with their graphical owners;
it does not play presentations, edit unsaved documents or establish GUI equivalence.

## Explicit consent and ownership

```json
{
  "path": "<native model path or artifact URI>",
  "expectedRevision": "<actual source revision>",
  "migratePresentationActions": true,
  "moves": [{
    "ElementId": "<native subtree root GUID>",
    "ExpectedParentId": "<current process or subprocess GUID>",
    "TargetParentId": "<destination process or subprocess GUID>",
    "ExpectedDiagramId": "<current diagram GUID>",
    "TargetDiagramId": "<destination diagram GUID>"
  }]
}
```

The [request template](../examples/native-action-migration.json) requires actual
IDs from inspection. The ordinary [reparenting contract](native-reparenting.md)
still governs allowed containers, full subtree/reference closure and coordinates.
The flag defaults to false. Without it, nonempty action collections in affected
diagrams remain protected by rejection. Same-diagram requests do not need this
flag; enabling it for a same-diagram-only request is rejected.

The host derives every transferred action from the independently loaded source
and the already validated final graph. Clients do not supply arbitrary transfer
records. An action follows its `NativePresentationAction.ElementId`, including
owners inside a selected nested subtree. No new action or owner IDs are allocated.

## Preserved contents and files

- The worker removes and appends the **existing native action objects**, rather
  than reconstructing them from a smaller XML model.
- Surviving source records retain their relative order. Existing destination
  records retain their order; incoming records append in source traversal order.
  An inverse move does not promise to restore an earlier global list position.
- Normal text, links, display names, action types, attribute identities and cached
  description/reference values stay exact. A deliberately stale cache is retained,
  not silently refreshed. Use `native_presentation_apply` to refresh it explicitly.
- Normal File/Image payloads move to the destination native `Actions/` folder.
  A source file remains when an unmoved normal action still references it.
  Existing destination bytes can be reused only when byte-identical. Different-byte
  filename collisions reject; rename the conflicting action explicitly first.
- GUID-stem filenames are subject to Bizagi's owner-based cleanup. A migration
  that would cause native persistence to prune a surviving shared reference is
  rejected before dispatch; use an explicit supported filename change first.
- Referenced FileEmbedded/Image cache paths follow the owner attachment folder
  already moved by native reparenting. Referenced FileLinked paths and URLs are
  literal content and are never opened or fetched.
- Unknown collection annotations, duplicate/orphaned moved owners, missing payloads,
  unrepresented action fields and unexplained archive changes reject. Native
  action files remain opaque bytes even when named `.xml` or `.diag`.

## Verification and diagnostics

The workflow uses a source reader, native editor/persistence process and a fresh
reader. It compares exact final action XML, record order and every action payload
against intent derived from the original archive. Only after that comparison
passes are action locations restored **in comparison copies**, allowing the
existing whole-model reparenting gate to check all other native contents.
No persisted archive is rewritten by the comparator.

`OperationView.Result.presentationMigration.Transfers` records each original
`NativePresentationTransfer.SourceAction` and its `TargetDiagramId`.
`presentationMigration.Verification` contains the independently checked action
archive comparison. `before`, `edited`, `reopened`, `fidelity`, `outputArtifact`
and `outputRevision` retain the normal reparenting contract. The source is not
overwritten; adoption remains a separate revision-checked commit operation.

Private operation evidence includes request options, expected action/file records,
native readbacks, the action verification and the full native fidelity report.
Failed verification retains diagnostics rather than silently adopting a partial
result. Source hashes and previous durable outputs can be rechecked after recovery.

## Scenarios remain separately authorized

`migratePresentationActions` does not authorize changing simulation parameters,
copying resource/calendar context, or discarding results. Configured movement
also requires the explicit `simulationMigration` mappings and permissions from
the [scenario contract](native-scenario-migration.md). Saved results are not
transported as if still valid; retirement requires its own consent.

The exact runtime combinations tested are recorded in [validation](validation.md).
Parameter migration, action migration and saved-result retirement retain separate
verification. None of them proves arbitrary scenario execution or playback.

## Repeat the actual circuits

After the documented Windows source build:

```powershell
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --action-migration-only
dotnet tests/McpBizagi.Acceptance/bin/Release/net10.0-windows/McpBizagi.Acceptance.dll . --native --action-scenario-migration-only
```

Run native clients sequentially. These independent clients author their models
through MCP, use installed-engine files/results, inspect durable native archives,
exercise failures and verify owned process cleanup. Unit fixtures do not replace
these circuits. Separate Modeler GUI compatibility, automatic layout, unsaved
sessions and full desktop automation remain open work.
