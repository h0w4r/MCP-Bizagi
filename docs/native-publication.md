# Native document publication

`native_publish` uses the installed Modeler documentation pipeline. No placeholder
document generator, Office automation, simulated clicks or foreground viewer is
used. Bizagi remains installed locally and is not included in the package.

## Inputs

| Argument | Meaning |
| --- | --- |
| `path` | Workspace `.bpm` or completed native artifact reference |
| `format` | `excel`, `word`, `pdf`; current source adds [`web`](native-web-publication.md) |
| `diagramIds` | Optional distinct native collaboration GUIDs; omission selects all |
| `title` | Word/PDF publication title; defaults to `Process documentation` |
| `allowImageResampling` | Defaults to false; explicit acceptance of bounded native PDF downsampling |

The title is explicit because the v5 file-level archive does not persist the
editor's display filename as a publication title. Diagram titles remain native
model content. Publication uses the locally installed `BizagiTemplate.dot`; it
does not follow an arbitrary template path embedded in an imported model.

## Actual pipeline

- The installed `IPublicationModelMapper` produces documentation content from
  selected native diagrams, processes and elements.
- Excel uses the installed mapper and `AsposeExcelWriter` generation/persistence
  methods, excluding its `OpenFile` step. Its process names appear as workbook
  content/sheet names; the collaboration title is not promised as a cell value.
- Word/PDF use the installed `IGeneratorFactory`, native generator and persister.
  A four-property `IProgressTracker` bridge forwards real native progress.
- Nonempty embedded subprocesses have their own native offscreen render surfaces.
  Chromium initialization is reused within the same isolated publication worker.
  Expanded children render bottom-up with explicit completion checks before the
  parent receives their SVG; asynchronous native helper return is not completion.
- Another worker reads the durable document through the installed parsers.
  It returns text, page/sheet counts, image counts and image dimensions.

The normal installed application's document-component initialization is used.
License resources are not copied, exported, redistributed or replaced. Component
initialization failures remain real operation failures.

## Verification and fidelity

The source archive and publication input snapshot remain byte-identical.
Selected process/diagram names and documented task names are checked in the
durable output; line/page wrapping whitespace is normalized for text comparison.
The native Word/PDF publisher can omit element sections without documentation;
their shapes remain in the diagram image. Excel's selection behavior differs.

Word/PDF image dimensions are checked against the actual native PNG surfaces,
not merely against the number of cover logos. Duplicate dimensions are matched
as a multiset. Exact image dimensions are required by default.

The native PDF persister can downsample larger images. This fails by default.
With `allowImageResampling=true`, bounded aspect-preserving downsampling can be
accepted and every match reports original/output dimensions and `Resampled`.
Dimensions are evidence of presence and scaling, **not pixel equivalence**.
The original SVG/PNG artifacts remain available without this PDF downsampling.

`publication-verification.json`, native module/renderer fingerprints, operation
phases, desktop observations and independent-reader results remain private in the
operation directory. Successful publication is not an independent visual review
of every possible document layout.

## Privacy and scope

Native files and generated documents can include Windows user identity, authors,
comments, filenames or model metadata. They are local artifacts, not automatically
uploaded portfolio examples. Inspect and sanitize before sharing.

Tested publication includes basic documented tasks and multi-diagram/nested
processes. Expanded import dimensions and nested render completion were corrected
and rerun through MCP with fresh readback, SVG geometry checks and independent
PDF raster review. Existing `.bpm` files retain their actual saved expanded sizes,
including oversized values from an earlier import. The native template can leave
a diagram heading on a separate sparse page; universal document layout is not
accredited. Web has its [own source contract and evidence](native-web-publication.md).
SharePoint, Wiki, custom templates, all selection/filter options,
comments, rich attachment workflows and every possible extended-attribute type
remain separate gates. Full Modeler automation is not implied by three working
publication formats. Resource strings follow the installed runtime language.

## Repeat a real circuit

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --publication-only --input C:/Processes/example.bpm
```

For a reviewed PDF downsampling case, add `--allow-image-resampling`. The harness
records that explicit choice through the actual MCP argument. To test one format,
add `--format excel`, `--format word` or `--format pdf`. Add `--package` with an
extracted package directory to verify the distributed server rather than build
outputs. Inspect the resulting documents independently before broader claims.

To repeat the corrected expanded import, full nested SVG and PDF circuit:

```powershell
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --expanded-render-only
```

Official context: [Bizagi documentation publication](https://help.bizagi.com/platform/en/generating_documentation.htm).
