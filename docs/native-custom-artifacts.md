# Native custom artifacts

Custom artifacts have two distinct identities: a **model-owned definition**
(`NativeCustomArtifactDefinition.Id`) and a diagram element instance
(`NativeElement.Id`). Multiple root or embedded-subprocess instances can refer
to one definition through `NativeArtifactProperties.CustomArtifactTypeId`.
They are not ordinary image artifacts or extended-attribute attachments.

The adapter uses installed native entities, persistence and `.bca` manager
methods. It does not modify the operator's global custom-artifact palette,
launch a Modeler window, synthesize clicks or build substitute `.bca` archives.
The implementation remains part of the experimental, version-gated adapter.
See [verification evidence](validation.md) for accredited execution scopes.

## Tools and workflow

1. Use `native_inspect` to obtain the native model revision, graph and
   `EngineReply.CustomArtifacts` definitions.
2. Call `native_custom_artifacts_apply` with `path`, `expectedRevision` and
   `patch.Changes`. Each change has `Operation` (`create`, `update`, `delete`)
   and a canonical, nonempty native GUID in `Id`.
3. Creation requires `Name` and `Image`; updates are sparse. Omitted values
   preserve content. An empty name is an explicit native string, not omission.
4. Poll `operation_get`. Only a completed operation's `outputArtifact` and
   `outputRevision` are eligible for subsequent operations.
5. Create or update an instance with `native_mutate`, using
   `ElementType: "CustomArtifact"` and an explicit
   `ArtifactProperties.CustomArtifactTypeId`. Its parent is a native process
   or embedded subprocess, not the collaboration itself.
6. Export selected definition IDs with `native_custom_artifacts_export`.
7. Import a revision-checked `.bca` with `native_custom_artifacts_import`.
   A completed export's typed artifact reference is reusable directly.

See [the request example](../examples/native-custom-artifacts.json).
Paths and identities in that example are placeholders, not runnable evidence.

## Image conversion is explicit

`NativeCustomArtifactChange.Image` uses the [image import contract](native-images.md):
confined source path or completed image-export reference, exact SHA-256 revision,
explicit `AllowPngReencoding`, and explicit frame selection for multi-frame input.
The selected decoded image becomes a native PNG representation without original
container metadata, color profiles, other frames or greater source precision.

The installed `CustomArtifactType.SerializableImage` serializer additionally
redraws into a premultiplied-alpha bitmap. That can change transparent or
semitransparent RGB and DPI-dependent sampling. This differs from the ordinary
`ImageArtifact` path, which requires exact selected-frame pixels.

- Changed custom-definition pixels require `AllowNativeRasterization: true`.
- Exactly **one** installed serializer conversion is applied.
- Its resulting pixels and encoded PNG bytes must remain identical on the next
  native serialization. Unstable results fail; the adapter does not repeatedly
  normalize an image until some degraded output happens to converge.
- `NativeCustomArtifactReceipt` records source metadata, original and resulting
  pixel fingerprints, resulting serialized-byte SHA-256, explicit acknowledgement,
  whether pixels changed, and repeated-serialization stability.
- A separate worker reloads the saved `.bpm`; the host compares those receipts
  with durable archive bytes and the actual native-loaded definitions.

For existing loaded definitions the adapter detaches bitmaps from the streams
that the installed loader closes prematurely. It first compares the native
decoded pixels with the original archive payload. This stream-lifetime correction
is recorded as an integration adjustment, not a silent conversion.

## Native `.bca` interchange

The installed `ICustomArtifactTypeManager.SaveCustomArtifactTypesToFile` and
`LoadCustomArtifactTypesFromFile` perform export and import. The global-palette
`Import`, `LoadCustomArtifactTypes` and `SaveCustomArtifactTypes` methods are not
used. [Bizagi documents `.bca` as its custom-artifact exchange format](https://help.bizagi.com/platform/en/artifacts.htm).

Before native extraction, a shared bounded parser checks the exact definition
XML and nested image ZIP members. It rejects traversal, duplicate names, symlink
members, unknown fields, missing or extra images, unsupported XML and oversized
decompressed content. Worker-only `TEMP`/`TMP` is verified before the manager's
scratch-folder extraction and cleanup.
The native export helper removes every literal `.bca` substring when deriving
its scratch folder. A conflicting parent-directory name is rejected before that
helper runs, rather than allowing its scratch writes outside the intended path.

The native format contains both an embedded serialized PNG and a direct PNG
sidecar. Their decoded content must agree, or their difference must be exactly
explained by the explicitly acknowledged native serialization. An unexplained
conflict fails. Existing model definition IDs require `replaceExisting: true`;
replacement retains the shared definition object so linked instances remain linked.

Export verification re-imports the actual `.bca` in another worker, persists a
native model and reopens it in a third worker. The archive itself remains the
vendor export; verification never rewrites it to hide a discrepancy.

## Deletion, preservation and limits

- A definition with remaining instances, attribute applicability/order or other
  native XML references cannot be deleted implicitly. Remove those dependencies
  explicitly first. Unknown references also block deletion.
- Native persistence upserts custom-definition files but does not remove stale
  XML. Deletion therefore removes only that exact definition file in the isolated
  model scratch directory before native persistence. Fresh readback must prove
  that the definition did not reappear.
- Input archives with missing referenced definitions fail before the installed
  loader can omit their instances.
- The host compares all untargeted native entries, including unknown XML and
  binary content. A requested definition projection is comparison-only, never
  an archive rewrite. No unexplained changes are published as success.
- A batch contains 1–100 distinct definitions. Raster bounds follow the image
  contract; `.bca` input is bounded to 64 MiB and aggregate expanded members to
  128 MiB. Unsupported inputs fail explicitly rather than being truncated.
- Offscreen native SVG raster receipts verify embedded bytes and decoded pixels.
  They do not establish independent desktop Modeler GUI compatibility, complete
  visual equivalence or full Modeler automation.

## Reproduce the acceptance circuit

```powershell
dotnet build -c Release --no-restore
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- --native --custom-artifacts-only
```

This requires the actual supported Bizagi installation. Absence or failure is
not replaced by a mock. Private operation evidence includes source snapshots,
native phase logs, loaded assembly fingerprints, worker temporary paths,
process/window observations, full-container fidelity and fresh-reader results.
