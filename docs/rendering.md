# Native offscreen rendering

`native_render_svg` reads a private native copy and uses the installed Modeler
DTO adapter, serializer, Chromium offscreen renderer and SVG exporter. It never
launches the desktop editor, sends input, changes focus or uses screenshot clicks.
The result contains the native SVG and a transparent PNG rasterized by that same
installed Chromium runtime.

The adapter checks JavaScript success, waits for expected top-level graphical
identities and stable SVG with loaded fonts, validates the XML, and checks transparent PNG corners. The
host supervises the entire owned Windows Job Object, including renderer children,
and retains sampled desktop observations and verified child-process exits.
Cache and logs use the worker's private directory. Installed files are not modified
or redistributed. Renderer asset fingerprints are recorded separately from
actually loaded managed-module fingerprints.

## Findings from real execution

- The vendor convenience method can return partial SVG after discarding a render
  script failure. An SVG root alone is insufficient acceptance.
- The adapter instead checks the native script response and expected graphical
  identities before publishing the result.
- The native configuration-manager constructor returns before its background
  initialization finishes. A missing DTO configuration can make asynchronous
  rendering fail after drawing only the pool. The adapter waits for populated
  native defaults and the completed configuration document before creating the
  DTO; asynchronous browser errors are also surfaced as operation failures.
- Native rendering can continue after the script call returns. Progress is
  measured from SVG/element changes, with the configured inactivity window;
  elapsed total operation time is not used to cancel an advancing render.
- The installed GDI SVG rasterization route was not accepted as equivalent to
  browser rendering. PNG uses the actual native SVG in the installed offscreen
  Chromium canvas, without manually redrawing a replacement process model.
- The basic diagram has been visually inspected with real SVG/PNG output.
  This is not a separate comparison against the interactive Modeler editor.

## Boundaries

This is not universal headless or Windows Service support. Complex embedded
subprocess images, formatted text, custom artifacts, fonts and attachments need
additional image-specific gates. The initial native renderer page references a
CDN script; offline operation is **not** claimed. No user browser profile or
authenticated desktop session is copied into the worker.

Transparent backgrounds are intentional. Use a light preview background to see
black connectors and labels. Model content retains its native colors.
