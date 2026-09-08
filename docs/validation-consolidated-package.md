# Consolidated Windows package acceptance — 2026-09-08 UTC

## Exact distribution and provenance

The framework-dependent `0.5.0-alpha.1` package was built from clean commit
`210455fe80bf2b261ee10f5b07e8a0faabf3b661`, extracted to a new private directory,
and tested through the independent official-SDK MCP client. Neither the source
build directory nor a mocked engine was used as the server under test.

- Archive SHA-256: `73c8ae0a6b45e6ebe57544ba23b2ea21e64e7e62c4fca44d733157be731cf051`.
- Version and clean-checkout provenance are recorded inside `package.json`.
- All **347 manifest entries** matched before testing and after each circuit;
  no extra package files appeared. The manifest excludes its own JSON file.
- Native engine: the operator's installed Modeler **4.3.0.008**, Windows x64.
- Source build: zero warnings/errors; **909 unit/component tests** passed separately.
- [Exact-source public CI](https://github.com/h0w4r/MCP-Bizagi/actions/runs/34187072952)
  passed. Public CI validates build/unit/XML MCP, not the proprietary engine.
- No vendor binaries, native operator models or private transcripts are distributed.

This is an **experimental prerelease**, not full Modeler automation. The package
includes code and contracts from its exact source commit; later documentation
can record its acceptance without rewriting the immutable tested archive.

## Ten independent packaged MCP circuits

All ten clients exited successfully. Their transcripts contain **174 terminal
native operations**: 145 completed, 25 intentionally failed, two explicitly
cancelled and two interrupted by deliberate host termination. A further 23
expected errors occurred at MCP preflight/binding boundaries. Negative outcomes
are required assertions, not unexpected failures hidden by the summary.

The tests recorded **280 exited workers** and **2,026 periodic desktop samples**
with no visible worker window or foreground ownership observed. Sampling is not
continuous tracing, Session 0 support or independent GUI compatibility proof.

| Circuit | Run | Native operations | Exited workers | Periodic samples |
| --- | --- | ---: | ---: | ---: |
| general | `20260908-043033-351edf` | 17 | 21 | 252 |
| refactoring | `20260908-043313-5daf48` | 15 | 30 | 204 |
| xpdl | `20260908-043617-ec1e7e` | 13 | 27 | 137 |
| conversions | `20260908-043847-830ecc` | 19 | 31 | 243 |
| images | `20260908-044210-e919b7` | 18 | 29 | 335 |
| custom-artifacts | `20260908-044516-cd8aef` | 20 | 38 | 231 |
| styles | `20260908-044855-fadc52` | 16 | 25 | 173 |
| attributes | `20260908-045124-129819` | 18 | 29 | 168 |
| metadata | `20260908-045418-19d2bc` | 16 | 25 | 148 |
| commit | `20260908-045648-e16db3` | 22 | 25 | 135 |

### What actually ran

- General: real stdio/XML, native BPMN/native persistence, nested/multi-diagram
  edits, validator, simulation, rendering, corrupt input, active cancellation,
  host-death cleanup, external state and native settings contention/recovery.
- Refactoring: independently extract root and nested subprocesses; retain rich
  content, attachment/image bytes, RACI, connection references and persisted tabs.
- XPDL: native Unicode import/export, selected/multiple diagrams, fresh restart,
  explicit exchange losses, native save, invalid input/revision rejection.
- Conversions: 99 directed task/gateway changes and their reverse, nondefault
  common properties, loops, RACI, Unicode metadata and byte-exact attachment.
- Images: original inputs, frame/pixel checks, native lifecycle, clone, extraction
  and rejection; no generated image is treated as independent GUI evidence.
- Custom artifacts: model-owned definitions/instances, root/nested clone,
  rendering, actual `.bca` exchange and separate native reader.
- Styles: actual installed-font inventory, sparse native typography/labels,
  durable clone/fidelity and native SVG property/geometry observations.
- Attributes: definition/value lifecycle, tables, embedded file/image bytes,
  reference guards and preservation across unrelated native operations.
- Metadata: local resources/RACI, explicit native scenario configuration,
  configured simulation and what-if results with recovery checks.
- Commit: actual save-as/replacement, stale/conflicting inputs, retained original
  and backup bytes, cancellation, post-write host death and reconciliation.

The exact independent test sources are retained at the package source commit.
Read the respective contracts for per-field limits; these ten tests are not an
exhaustive Cartesian product of all native features or every possible model.

## Transcript fingerprints

Raw evidence is private because native files may contain operator metadata.
Each hash below refers to the complete retained `mcp-transcript.json` for its
circuit, not a synthetic summary. Public correlations do not replace rerunning
acceptance on another machine or a different vendor version.

| Circuit | Transcript SHA-256 |
| --- | --- |
| general | `f026b1213286061ef64b3f49af133753943593b2e901b5d20bae88667700572d` |
| refactoring | `ac565bcbd8b8a45cb75be244ff4750016c32e7cc503d90396740cf74dbb53c37` |
| xpdl | `0c7694b88d4c0c3afdb2c3dfe24dfdd44409fba4855f8df27feece776ec4fe8b` |
| conversions | `62584a24f38bbbcb503fa998f3c5ed9924168a2ed031b870e7f31e424420727d` |
| images | `8ade80c581113d7bd92389b25fa005f58132c4f15f36d73ab136de6e436a778f` |
| custom-artifacts | `2afffa2b9257521ce8d0e4882191e93f1ef7df5b08d3c076df6f55a1e7e3620e` |
| styles | `71c1a35955667265d247631f5d862a3d4bf92758fb89144770ca4fca2d20bd35` |
| attributes | `1af5a0f36fa21c500a3f7586e3ebd32df62b5bda2d60b1698a0ae9f5c8c84244` |
| metadata | `b1b1bc91e81dd70470f631a69d878ac59b6d25624c2b49cb758fd55a5d99c7bb` |
| commit | `1fdcf8daecec91af2b84171c9e57b88b8c376f58652c632870e12ace827c23f7` |

## Reproduce and interpret

### Default worker discovery: additional operator-configuration circuit

The follow-up client run `20260908-050342-ca9a2e` used the same extracted server
with **no `MCP_BIZAGI_WORKER` override**, exercising `ServerOptions`' actual
sibling-worker lookup. It created native models, mutated and rendered one,
executed its task in simulation, checked an expected failure and recovery,
then cleared the graph and compared it with the original native model.

All nine terminal operations matched expectations: eight completed and one
intentionally failed. The 15 workers exited; 421 periodic samples observed no
visible worker window or foreground ownership. Its transcript SHA-256 is
`1366caf211e6a33454969bc06f106debd5817983e43634a71d72cbc28022b23b`.
All 347 package manifest entries remained unchanged afterward. This additional
test used the follow-up acceptance-client `--discover-worker` option, not a
rebuilt or altered server archive. Across the ten-case matrix and this extra
circuit, the totals are 183 native operations and 295 exited workers.

### Commands

Download the ZIP and checksum from the
[0.5.0-alpha.1 prerelease](https://github.com/h0w4r/MCP-Bizagi/releases/tag/v0.5.0-alpha.1).
Use the source at `210455fe80bf2b261ee10f5b07e8a0faabf3b661` to build the
independent acceptance client. Follow [packaging](packaging.md) and point every
client at the same extracted package using `--package`.

Execute the general command with `--native --extended --simulation --render
--recovery --external-state --settings-contention`; then independently use
`--native` with each of `--refactoring-only`, `--xpdl-only`, `--conversions-only`,
`--images-only`, `--custom-artifacts-only`, `--styles-only`, `--attributes-only`
and `--metadata-only`. Finish with `--native --commit-only --external-state`.
Run them sequentially. Retain actual logs, exit codes, revisions and readback;
do not rebuild loaded binaries, erase a failing run or retry writes blindly.

### Gates this package does not close

- General selection/reparenting/layout and reverse subprocess inlining.
- Refactoring migration of configured scenarios or presentation actions.
- Remaining I/O/event collections, richer publication/interchange options,
  broader simulation/distribution/result corpora and documented vendor semantics.
- The document actually open with unsaved changes inside Modeler.
- Independent verification of compatibility inside the Modeler desktop GUI.
- Any newer installation/version not separately accredited.

The full automation objective remains open. Neither 909 green unit tests nor
these 174 actual native operations justify changing that status to complete.
