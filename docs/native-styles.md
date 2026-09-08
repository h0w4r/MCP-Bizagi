# Native typography, graphical styles and label bounds

`native_mutate` accepts a sparse `NativeMutation.Style` on `create` and `update`.
The adapter changes the installed native graphical objects, saves a staged
`.bpm`, terminates the writer and checks the supplied fields in a fresh worker.
The host also compares the remaining archive content; a style edit is not an
excuse to discard unknown XML, images, custom definitions or simulation data.

This is an **experimental file-based capability**, not full Modeler automation,
live-session synchronization or independent desktop visual accreditation.
See [executed baselines](validation.md) for the measured acceptance scope.

## Discover fonts, then edit

1. Call `native_fonts_get` without parameters and poll `operation_get`.
2. Its completed `EngineReply.Fonts` contains the installed Windows/GDI+ family
   names and regular, bold, italic and bold-italic face availability.
3. Obtain native identities and the model revision using `native_inspect`.
4. Submit `native_mutate` with `path`, `expectedRevision` and explicit mutations.
5. Poll to a terminal state. Reuse only a completed `outputArtifact` and its
   `outputRevision`; failed operations are not usable artifact sources.

Font enumeration uses the installed Windows font collection, does not install
fonts and does not alter user settings. A family name must be observed on the
worker machine; an absent family fails rather than being silently substituted.
Face enumeration does **not** prove that a font covers every requested glyph or
that Chromium and GDI+ resolve every face identically. See the primary
[InstalledFontCollection documentation](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.text.installedfontcollection?view=windowsdesktop-10.0).

## Sparse style contract

Null or omitted properties preserve the existing value. `false` is an explicit
boolean edit, not an omitted value. An empty style object is rejected.

| `NativeStylePatch` field | Meaning and validation |
| --- | --- |
| `FontName` | Explicit installed family; nonblank, no surrounding whitespace |
| `FontSize` | Whole native font units, 1–512; not CSS pixels |
| `Alignment` | Exactly `Near`, `Center` or `Far` |
| `Bold`, `Italic`, `Underline`, `Strikeout` | Independent nullable booleans |
| `FontArgb` | Signed 32-bit native ARGB font color |
| `BackgroundArgb`, `BorderArgb` | Native fill and outline colors |
| `BorderVisible` | Native graphical outline flag where the type persists it |
| `TextBackgroundArgb` | Native label background color, distinct from shape fill |
| `TextDirection` | Exactly `Horizontal`, `TopToBottom` or `BottomToTop` |
| `LabelBounds` | All four `NativeLabelBoundsPatch` members: `X`, `Y`, `Width`, `Height`; native label rectangle |

Label coordinates are whole, finite and nonnegative, bounded to 1,000,000.
A nonzero rectangle requires positive width and height. Four zeroes explicitly
clear the manual location and size. Fractions are rejected because the installed
serializer rounds these coordinates; no rounding is hidden from the caller.
An empty or partial bounds object is rejected, not interpreted as a zero reset.

Do not supply the same fill/outline color in both `NativeMutation.Geometry`
and `NativeMutation.Style`. That ambiguous double intent is rejected. Node
bounds, expanded dimensions and connection points retain their existing
[geometry and container contracts](native-containers.md).

## Type-specific persistence boundaries

| Native object | Style boundary |
| --- | --- |
| Tasks, events, gateways, embedded subprocesses and supported graphical artifacts/data references | Explicit style fields require successful fresh-reader and whole-container checks |
| Lanes and milestones | Native font/graphics adapter; container partition rules still apply |
| Pools (`Participant`) | Font/formatting and fill/outline colors only; manual labels, text direction/background and independent boundary visibility are rejected |
| Sequence flows, message flows and associations | No fill or outline-visibility intent; font, outline and native label fields use the connector adapter |
| `Process`, `Collaboration`, shared `DataStore`, `Resource`, `LaneSet` | Not independently styled shapes; style the graphical object/reference instead |

A pool's persisted boundary visibility identifies the native main participant;
changing a graphical flag is not a substitute for a semantic pool conversion.
The adapter initializes only newly constructed objects with the defaults that
the installed reader materializes. It never rewrites existing readback values
to make a comparison pass.

## Persisted values versus rendered output

`NativeElement.Style` reports actual loaded values through `NativeStyleInfo`.
It is not a measurement of pixels, glyphs or the SVG layout engine.

- The installed renderer uses native font units with its own conversion.
  On the measured 4.3 build, 14 native units produced approximately 18.5941
  CSS pixels; assuming the usual 96/72 conversion would be inaccurate.
- `Near` and `Far` are native alignment enums, not universal left/right promises
  for every writing system. The authored left-to-right acceptance checks the
  actual SVG alignment attributes separately.
- The renderer chooses internal or external labels by element type. Internal
  task labels can ignore manual `LabelBounds` and text background values even
  though those fields survive native persistence.
- The acceptance circuit checks a sequence-flow **external** label's actual
  position and size against its requested native rectangle.
- Pools and lanes may apply intrinsic rotation. A `textDirection` SVG attribute
  alone does not prove the intended glyph rotation or pixel appearance.
- Opaque fill, border and font colors are checked on actual generated SVG.
  Semitransparent color rendering is not accredited by that check.
- Installed font availability does not establish Unicode glyph coverage.
  Formatted artifact HTML may impose its own text formatting.

The mutation result includes `styleInterpretationWarning` whenever styling is
requested. Native offscreen rendering remains independent from a real desktop
Modeler compatibility check; no clicks, foreground input or substitute
renderer are used to satisfy the programmatic requirement.

## Integrity and failure behavior

The fidelity policy verifies requested leaf values before projecting them out
of the whole-container comparison. Unknown child elements and attributes stay
visible to that comparison; malformed, missing or ambiguous graphics fail.
Native diagram cloning additionally compares the source and cloned loaded
style snapshots, including label bounds. Subsequent source changes must not
mutate the cloned style.

Absent fonts, unsupported type/property combinations, invalid label rectangles,
stale revisions and unexplained native persistence changes remain explicit
failures. No failed write replaces the original, and a subsequent valid
operation starts its own isolated worker rather than replaying a failed write.

## Reproduce the operator circuit

After building the current source and opting into the installed native adapter:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build --no-restore -- . --native --styles-only
```

The independent official SDK client records the MCP transcript, operation IDs,
worker exits, fresh-reader results, style assertions and durable SVG hashes in
the private `.local/acceptance` run directory. Unit policy tests are separate
and do not accredit the installed-engine route.

An editable request is available in [the JSON example](../examples/native-styles.json).
Replace its authored sample identities, path and revision with values obtained
from your model; it does not pretend that those identities already exist.
