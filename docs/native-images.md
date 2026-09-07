# Native image artifacts

Image artifacts are native graphical elements, not extended-attribute attachments.
The source adapter uses `native_mutate` for their lifecycle and
`native_image_export` for exact payload extraction. All model writes use private
copies, the installed Bizagi persistence engine and independent worker readback.

## Explicit image intent

Create `ImageArtifact` with a stable GUID, a process or embedded-subprocess parent,
explicit geometry and `ArtifactProperties.Image`. Update the same property to
replace a picture; omission preserves it. Deletion uses the regular native
`delete` mutation and removes only the corresponding image payload.

| `NativeImageImport` field | Meaning |
| --- | --- |
| `SourcePath` | Confined workspace path or a completed `native_image_export` artifact reference |
| `ExpectedRevision` | Lowercase SHA-256 of the exact input bytes |
| `AllowPngReencoding` | Required explicit acknowledgment of selected-frame 8-bit RGBA PNG conversion |
| `FrameDimension` | Optional native `Page`, `Time` or `Resolution` selector; ambiguity fails |
| `FrameIndex` | Zero-based selected frame; mandatory when the selected dimension has multiple frames |

PNG conversion does **not** preserve source-container metadata, color profiles,
greater source precision, compression settings or other frames. The source bytes
and their hash remain in the private operation evidence. There is no implicit
animation-to-first-frame conversion, vector rasterization, resizing, compositing
or linked-image download. The installed GDI+ raster decoder is used with embedded
color management disabled, not a separately implemented image decoder.

Inputs are limited to 32 MiB each, 128 MiB per batch, and 64 Mi decoded pixels per
image. Native archive bounds still apply. Unsupported codecs or invalid frames
fail explicitly; a familiar file extension is not proof of a supported image.
The authored acceptance corpus covers PNG transparency, BMP, JPEG, palette GIF,
animated GIF and multi-page TIFF, with explicit frame selection.

The host captures input bytes and verifies revisions before queueing. It stages
immutable private files for the worker: the worker never directly opens an
unconfined user-supplied image path. Text and image payloads are mutually exclusive
in an artifact patch. Image content on another native element type fails.

## Native integration and fidelity

The installed artifact factory creates real image objects. The installed
persistence utility resolves each model-owned `ImageArtifactImages` folder;
Bizagi's persistence engine creates the actual `.bpm` archive. The server does
not construct or rewrite the native ZIP to imitate persistence.

Two locally observed vendor-helper behaviors require explicit adapter handling:

1. The native bitmap helper redraws into premultiplied alpha, changing some RGB
   values. Image payload encoding therefore uses `System.Drawing`'s PNG encoder
   on an independent straight-alpha bitmap, without the compositing step.
   Receipts identify `PayloadEncoder` as `System.Drawing.PNG.straight-alpha`.
2. The installed loader closes picture streams. A later bitmap copy can fail
   even after initial inspection succeeds. After native loading, the adapter
   compares its actual picture pixels with an independently detached bitmap
   created while the native payload stream is alive. It records a per-image
   `native_image_stream_lifetime_detached` integration adjustment. A pixel
   mismatch fails; the original model and encoded payload are not changed.

The stream lifetime and separate output-stream requirements follow Microsoft's
[`Image.FromStream`](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.image.fromstream?view=netframework-4.8.1)
and [`Image.Save`](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.image.save?view=netframework-4.8.1)
contracts. These are integration adjustments, not changes to Bizagi binaries.

`NativeElement.Artifact.Image` reports width, height, transparency and a SHA-256
of row-major BGRA pixels, without stride padding. This is separate from each
`EngineReply.ImageFiles` entry's file name, owner, encoded byte length and SHA-256.
Import receipts also record source format, decoded pixel format, frame selection,
observed metadata IDs and the actual encoder used.

The acceptance gate requires:

- Exact decoded pixel equality before encoding, after encoding and after native
  persistence/restart, not dimensions alone.
- An exact payload hash in the persisted `.bpm` and the fresh worker's native
  scratch file, with exactly one file per native image identity.
- Whole-archive preservation of every unrequested image and unknown leaf.
- Explicit native image ownership before projecting a requested payload change
  out of the comparison. These projections never modify a file.
- Original source-byte preservation during import, replacement, export and clone.

## Clone and export

The installed native cloner supplies the identity map. Its low-level artifact
clone does not copy separate image files, so the adapter copies each actual
source payload byte-for-byte into the mapped native image folder and assigns an
independent bitmap to the cloned artifact. Both clone pixels and encoded file
hashes must match. No re-encoding is authorized merely by requesting a clone.

`native_image_export(path, diagramId, elementId)` loads the model through the
native engine and extracts that exact image payload. It compares the output
against the original archive and returns `file`, `image`, `outputRevision` and
`outputArtifact`. The private output uses the neutral name `image.bin`: existing
native images need not be PNG. `file.FileName` retains the actual native name.

The typed `artifact:<operation-id>:image.bin` reference is reusable as an image
input only when its operation completed as `native_image_export`. It does not
grant access to arbitrary private logs, configuration or attachment files.

## Reproduce

See [the image mutation example](../examples/native-images.json). Obtain native
parent IDs from `native_inspect`; compute the source-image SHA-256 before
submitting it. Poll `operation_get` and use only completed output revisions.

```powershell
dotnet restore McpBizagi.slnx --locked-mode
dotnet build McpBizagi.slnx -c Release --no-restore
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --images-only
```

The small test images are independently authored and checked against their
manifest. `tests/McpBizagi.Acceptance/Fixtures/generate_images.py` reproduces them
with Pillow 11.0.0; Python/Pillow is not a server or acceptance-runtime dependency.
Pure image-policy tests are separate from installed-engine accreditation.

This family does not establish full Modeler automation, a color-managed image
editor, live unsaved-session integration or independent GUI compatibility.
Native SVG/PNG and Word publication must retain their own actual acceptance
evidence; an image's existence in the model does not prove it was rendered.

Native rendering additionally verifies one embedded raster payload for each image
identity on the exact requested surface. `EngineReply.RenderedImages` records its
surface, owner, declared MIME label, actual encoded SHA-256 and decoded-pixel
evidence. The decoded pixels must match the native model picture. The client
independently checks the durable SVG's embedded bytes and those receipts. A
vendor-provided MIME label is not trusted as the image codec: the observed native
renderer can label PNG payload bytes as `image/jpeg`.

## Local operational evidence — 2026-09-07

The independent SDK circuit `20260907-225232-e04385` passed **18 terminal native
operations: 14 completed and four expected worker failures**. It exercised all
six authored raster inputs, explicit animated-GIF/TIFF frame selection, root and
nested image ownership, replacement, export/reuse through an opaque reference,
unrelated edits, byte-exact native cloning, deletion, no-op saves and unchanged
original revisions. A separate stale-image SHA-256 request failed at MCP preflight.

Actual worker failures covered ambiguous multi-frame input, an out-of-range
frame, image content applied to a task and corrupt image bytes. Later operations
completed without adopting any failed output. Lossless fixture pixels were also
checked against independently generated Pillow fingerprints; JPEG's first
decoder rounding is codec-specific and is not equated with its pre-compression
source pixels.

The root SVG verified **four image payloads** and the requested nested SVG verified
**two**, including decoded pixels and exact durable embedded-byte receipts.
Word readback reported **six pages and 11 images**, including document icons.
Word image dimensions/content checks are not pixel-equivalence or layout review.

The run retained **29 owned-worker observations and 1,848 periodic desktop
samples**. No visible owned-worker window or worker foreground ownership was
observed. Transcript SHA-256:
`4cc907a0d6941c74278e464fc62716a1e0f89ba4b4363d3ececb73f4861eaaea`.
Native publication continued for over five minutes while actual phases, files
and process activity advanced; no total-duration cutoff replaced inactivity
observation.

An earlier complete lifecycle `20260907-224718-8ce30f` also passed (14 completed,
four expected failures; 29 workers/417 samples; transcript
`7ca11530c5459b057c08d6784f16a1cdcfadfc91032a955cab59cf700cf68af9`).
That earlier run preceded the additional rendered-image payload gate.
Development failures exposed the two integration adjustments above; their raw
diagnostics remain private and were not relabeled as successful operations.

This source checkpoint exposes **30 MCP tools** and passes **787 unit/component
tests**. It is not yet part of the earlier immutable packaged release or a claim
that the persistent full-automation objective has closed. See
[verification baselines](validation.md) for regression and public-build evidence.
