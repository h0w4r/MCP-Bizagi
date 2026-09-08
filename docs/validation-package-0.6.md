# Windows package acceptance — 0.6.0-alpha.1

## Exact distribution, not a source-only smoke test

The clean framework-dependent Windows x64 archive was extracted once to a new
private directory and tested through **twenty independent official-SDK MCP
clients** against installed Bizagi Modeler **4.3.0.008**. The clients used actual
stdio, isolated workers, native engines, durable artifacts and separate readers.
Neither source `bin` outputs nor mocked engines substituted for the packaged server.

- Package source commit: `27e59ae7df098ec76a496f166708a5a901f516ca`.
- Archive: `MCP-Bizagi-0.6.0-alpha.1-win-x64.zip`.
- Archive SHA-256: `8fe5906dd8e53d47fb3d60984605bd17cdb0bcb5e5a6ceea14dc67f2234e39dd`.
- **392 manifest entries**, unchanged before testing, after every circuit and
  at final verification; no missing or extra package files. The manifest excludes itself.
- Twenty circuits ran sequentially on 2026-09-08 UTC. The candidate was never
  rebuilt, re-extracted, patched or silently retried during acceptance.
- Nineteen circuits used default sibling-worker discovery, with no worker-path
  override. The connection-failure circuit intentionally used an explicit
  damaged private worker copy and then verified recovery; discovery would defeat that test.
- [Exact-source CI](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34232931889)
  passed: locked restore, build, **1,054 unit/component tests**, **18 package-policy
  filesystem tests** and actual XML MCP. Public CI does not contain the vendor engine.

The published ZIP remains the exact pre-acceptance source build. Its bundled
README correctly described a candidate at build time; this later record and
the release notes report completed acceptance **without rewriting that archive**.
The [0.5 package](validation-consolidated-package.md) remains immutable.

This is an **experimental prerelease, not full Modeler automation**. None of
these circuits establishes independent desktop Modeler GUI compatibility,
live unsaved document synchronization, arbitrary native-model fidelity or
compatibility with an untested vendor version.

## Actual terminal outcomes

All twenty clients exited successfully. Their transcripts contain **274 terminal
native operations**: **230 completed**, **37 expected failures**, **five explicit
cancellations** and **two interruptions from deliberate host termination**.
There were also **33 expected MCP preflight/binding errors**. The negative
outcomes are asserted rejection/recovery cases, not unexpected failures discarded
to make the success count look better.

The evidence includes **499 exited workers**, **715 owned-process exit records**
(including renderer descendants, not 715 additional workers), and **5,308 periodic
desktop samples** with no owned visible window or worker foreground ownership
observed. Sampling is not continuous tracing, Session 0 support or GUI acceptance.
No owned acceptance host or native worker remained when the final process check ran.

| Circuit | Run | Native operations | Exited workers | Desktop samples |
| --- | --- | ---: | ---: | ---: |
| visio | `20260908-133857-1c2ee9` | 13 | 39 | 322 |
| reparenting | `20260908-134358-b63ab8` | 21 | 44 | 1316 |
| selection-copy | `20260908-135251-b87ca0` | 6 | 13 | 66 |
| alignment | `20260908-135414-33bdf9` | 14 | 39 | 644 |
| alignment-rich | `20260908-135926-81c3c3` | 9 | 21 | 309 |
| general | `20260908-140316-631992` | 17 | 21 | 132 |
| refactoring | `20260908-140543-2caab9` | 15 | 30 | 210 |
| xpdl | `20260908-140905-c28f5f` | 13 | 27 | 180 |
| conversions | `20260908-141229-c47d7c` | 19 | 31 | 243 |
| images | `20260908-141614-a6e883` | 18 | 29 | 407 |
| custom-artifacts | `20260908-142013-4164a6` | 20 | 38 | 239 |
| styles | `20260908-142405-711379` | 16 | 25 | 185 |
| attributes | `20260908-142648-f03582` | 18 | 29 | 178 |
| metadata | `20260908-142955-1dd035` | 16 | 25 | 131 |
| commit | `20260908-143225-34fac1` | 22 | 25 | 116 |
| data | `20260908-143451-01a43d` | 20 | 35 | 284 |
| model-create | `20260908-143857-6ae7fe` | 9 | 15 | 91 |
| connection | `20260908-144027-853d93` | 2 | 2 | 4 |
| publication | `20260908-144054-103a1a` | 3 | 6 | 145 |
| expanded-render | `20260908-144159-bd18fc` | 3 | 5 | 106 |

## What the circuits cover

- **Visio:** actual VDX import/export, separate populated nested-body pages,
  source-page receipts, tested internal flows and strict native no-op/readback;
  explicit lossy projection, invalid stencil/selection and cancellation recovery.
- **Reparenting:** rich same-diagram container moves and inverse moves, preserved
  identities, I/O, metadata, images, attachments and whole-container fidelity.
- **Selection copy:** actual native paste/clone maps, reference-closed selections,
  original preservation, independent archive/readback checks, cancellation/recovery.
- **Alignment and alignment-rich:** eight installed-editor modes, actual native
  callbacks, root/nested persistence, dependent connections, rich/manual-label
  preservation, active-renderer cancellation and recovery. Not global auto-layout.
- **General:** BPMN/native persistence, nested edits, validator, simulation,
  rendering, corrupt input, active cancellation, host-death cleanup/journal recovery,
  external state and native settings contention/recovery.
- **Refactoring:** root/nested embedded-to-reusable extraction, rich content,
  byte-exact attachments/images, references and persisted tabs.
- **XPDL:** native Unicode import/export, selected/multiple diagrams, explicit
  exchange losses, fresh native restart and invalid input/revision rejection.
- **Conversions:** directed native task/gateway conversions and reverse changes,
  preserved common properties, loops, RACI, Unicode metadata and attachment bytes.
- **Images and custom artifacts:** native lifecycle, frame/pixel or definition
  checks, clone/render, extraction, `.bca` exchange and separate native readers.
- **Styles and attributes:** native typography/label persistence, graph/archive
  fidelity, extended definitions/values, tables, files/images and reference guards.
- **Metadata:** local resources/RACI, native scenario configuration, quantitative
  configured simulation and what-if results, with recovery checks.
- **Commit:** native save-as/replacement, revisions/conflicts, original and backup
  bytes, cancellation, post-write host death and durable intent reconciliation.
- **Data:** actual native objects/stores, associations/I/O, clone/persistence,
  reference guards and native documentation of the tested data model.
- **Model creation and connection:** blank models, lifecycle, rendering/simulation,
  failure/recovery and explicit worker-connection failure followed by success.
- **Publication:** real Excel, Word and PDF generators, separate durable content
  readers, nested-diagram images and source preservation.
- **Expanded rendering:** nested native geometry, generated surfaces and PDF
  readback. Offscreen output remains separate from independent Modeler GUI proof.

These are the concrete tested corpora, not an exhaustive Cartesian product.
Per-field restrictions and remaining cases in the [capability ledger](capabilities.md)
and each family contract still apply.

## Dependency and source provenance

The distribution contains own binaries and redistributable dependencies, not
installed Bizagi/Aspose/CEF components, operator models or private transcripts.
An additional read-only audit matched all **154 DLL/EXE files** byte-for-byte to
members of **60 restored NuGet archives**, or to the existing clean own build
outputs. NuGet archives matched the locked restore content hashes using the
installed SDK's signed-package-aware `PackageArchiveReader.GetContentHash`;
raw signed ZIP hashes were retained separately. Dependency license texts and
NuGet metadata are included in the distribution.

All **66 public documentation/example/script files** matched tracked source at
the package commit using Git path-filtered blob identities, accounting for
checkout line endings. Own binary matching is build-output provenance, not an
independently reproducible-build or certificate-trust claim. These checks do not
replace the real MCP/native acceptance above.

## Retained transcript fingerprints

Raw model files and transcripts remain private. Each SHA-256 identifies the full
retained `mcp-transcript.json` for its actual run, not a synthesized test result.

| Circuit | Transcript SHA-256 |
| --- | --- |
| visio | `c10545908dac68d2bca7690a61ace8037eedf3723c0cda0544fa73c828d24c50` |
| reparenting | `8239d66f1e0bf17a3189314b2abfc05460c94e9ed9eb1919738d4906a4d648c0` |
| selection-copy | `ea74761dd2aad03ebaf6bb9da3751dd96a1d0c6162d77d3e1c99ad754a933755` |
| alignment | `44c1f1db52e6aff8bec1a7e82c0cd5edcbd0f7f3c105717b45e53d95cfb5871e` |
| alignment-rich | `8a9993d9da54c629bbc1229f7dd61f82513966a46c2bde396b26b1e95cf22c04` |
| general | `9bb4dd4d2dd1e6a67a87b9de52825c7f69557c30fac75d4c398bc8f6b06262de` |
| refactoring | `0aaf5c5d4bf06a8dfc34692720936b207f65759a4f174e90a209878142e21e03` |
| xpdl | `f8c13600e76db731d386d091645ca254f5583e2f41edefadcc96c920d2f3162c` |
| conversions | `f76aeb43a2ec1cc619af314fc7da24f41575966a5dc477dcc82a93e7c55783a3` |
| images | `f20a419fed3abb21d937a6ae9a60004ceae3b9225a04a0765e05372594e65a9a` |
| custom-artifacts | `9388b7ca868ff289535c4143e059b93db3f8aa5e4b7d7929669e85d6a5df2e5e` |
| styles | `456a237f620e266a75533c23376cb31adf4708c96c81f8b6a7fa62fa617483b6` |
| attributes | `8e52ff7ce131b1043b4059959af5540dd73592a518313732615b53ecf50a5989` |
| metadata | `cd55e615b214b2d09c79d0060c6daad9c464b67e0574df834c6d1c5d8972d416` |
| commit | `495689ab775ff09159d165ad4f00248fef919101ba5997b61235121bfe09afb0` |
| data | `a78cac1de8fb581b237a360be5c98b41ba43509724fedb5df08309d1dd86e457` |
| model-create | `57fb7458b879adbb190d909189994212ed27a6f5d534315720e3420a8a1268b0` |
| connection | `5a502022683ee40915ba6eefeb96f0d80312469cd9bb76dac4eb1d74dbcffa2f` |
| publication | `fc13c3371b2e73720d75e192bc051d2f4a54da6c536b60dd914a19af786ed97f` |
| expanded-render | `fe9de6f262fe94f2cc8a1c380174fb63c90aa6f97fbcaba16d62d3f5e6ad504b` |

## Reproduce

1. Download the ZIP and `SHA256SUMS.txt` from the
   [0.6.0-alpha.1 prerelease](https://github.com/h0w4r/MCP-Bizagi/releases/tag/v0.6.0-alpha.1).
   Compare the trusted ZIP checksum, extract once to a fresh directory, and run
   `scripts/verify-package.ps1` with the source commit listed above.
2. Build the independent acceptance client from that exact source commit with
   the pinned SDK and locked dependencies. See [packaging](packaging.md).
3. Point every client at the same extracted package using `--package <directory>`.
   Run sequentially; retain logs and source bytes, and stop on an actual failed
   assertion. Never rebuild a loaded client/worker or silently repeat writes.
4. Use `--native --discover-worker` plus each focused flag below. For `general`,
   use `--extended --simulation --render --recovery --external-state --settings-contention`
   instead of an `--only` flag. `commit` also needs `--external-state`.

| Circuit | Focused client flag |
| --- | --- |
| visio | `--visio-only` |
| reparenting | `--reparenting-only` |
| selection-copy | `--selection-copy-only` |
| alignment | `--alignment-only` |
| alignment-rich | `--alignment-rich-only` |
| general | See combined flags above |
| refactoring | `--refactoring-only` |
| xpdl | `--xpdl-only` |
| conversions | `--conversions-only` |
| images | `--images-only` |
| custom-artifacts | `--custom-artifacts-only` |
| styles | `--styles-only` |
| attributes | `--attributes-only` |
| metadata | `--metadata-only` |
| commit | `--commit-only` |
| data | `--data-only` |
| model-create | `--model-create-only` |
| connection | `--connection-failure` **without** `--discover-worker` |
| publication | `--publication-only` |
| expanded-render | `--expanded-render-only` |

The `publication` case additionally takes `--input <native-model.bpm>`.
This run used the own `nested-source.bpm` created by the Visio acceptance client's
native roundtrip of public `examples/collaboration-nested.bpmn`. Use that generated
file from your own Visio evidence directory; no private operator model is needed.
The `expanded-render` case prepares its own public nested corpus.

Recheck the exact package manifest after each case. Preserve operation IDs,
terminal assertions, worker/descendant exits, source revisions, durable readback
and desktop observations. Do not infer full automation from an all-green table.
