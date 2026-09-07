# Verification baseline

## Scope

This is a sanitized record of real local execution on **2026-09-07 UTC** with
Bizagi Modeler **4.3.0.008**, a Windows x64 worker, the official MCP C# client,
the official stdio server, StreamJsonRpc, and durable files. No native engine
mocks, desktop clicks, UI Automation, or simulated success responses were used.

It establishes the tested basic route, **not** complete Modeler automation or
lossless preservation of every native feature. The test input is the project's
own `examples/minimal.bpmn`, including supplied Diagram Interchange geometry.

## Observed acceptance

| Circuit | Observed result |
| --- | --- |
| Build | Seven projects; zero warnings and errors |
| Core unit tests | 22 passed, zero failures/skips |
| Independent stdio MCP client | 11 tools discovered, then actual tool calls |
| XML create, inspect, name batch, reread | Exact Unicode value persisted |
| XML stale revision and path escape | Actual tool errors, no successful overwrite |
| Native import → `.bpm` → fresh reader → BPMN | Completed through the installed engine |
| Existing native file inspection | Native IDs returned; original bytes unchanged |
| Native task + start-event name batch | Both requested names verified after another worker loaded the new `.bpm` |
| Native stale revision | Rejected before starting the edit |
| Missing native element | Real engine error journaled as `failed` |
| Corrupt native input | Rejected rather than passed off as BPMN |
| Cancellation during native registration | `cancelled`, worker exit recorded, owned PID no longer active |
| Native operation after cancellation | Fresh bootstrap completed |
| Owned-worker desktop observation | 38 samples across eight workers; no visible window or foreground ownership observed |

The desktop check is sampled and read-only. It is not a continuous trace, a
claim about Session 0/Windows Services, or proof that every internal library
operation is GUI-independent. The actual engine execution thread in this run
was MTA. Loaded-module fingerprints and x64/CLR metadata are retained locally.

## Correlation references

The full local transcript has SHA-256:

`3a2ebe90f706fe4cff6c1b1e4ba565c98cedfd870b06e334123f36a7c22d45ee`

| Operation | Correlation ID | Terminal state |
| --- | --- | --- |
| Native roundtrip | `463d8e526a5c49b2868740a7ffdd571e` | completed |
| Existing native inspection | `cfa0ee42bdd0408dab726c1a593cd4ce` | completed |
| Two-element native name batch | `16008a21b5a942c0a2582085056e2710` | completed |
| Invalid native identity | `ec6721bdf9bf4e858e9672bcf9559278` | failed, as expected |
| Active cancellation | `77c174b7b9604fafa6e1c7cd8d4b816e` | cancelled |
| Recovery probe | `225641bab0d9472a92e1f1f102ef3d82` | completed |

Raw archives and logs remain private because Bizagi can embed Windows user
metadata in generated native files. The correlation IDs and transcript hash
make this record traceable to retained operator evidence; they are not a
substitute for rerunning the acceptance client on a different installation.

## Fidelity findings, not hidden differences

The basic native interchange regenerated identifiers and normalized some names
and structural metadata. `BpmnFidelity.Compare` reports identifier differences,
selected element-count/name changes, and limitations of the comparison.
It does not claim full graph, geometry, attribute, or attachment equivalence.

Bizagi explicitly documents that BPMN export excludes extended attributes:
[official BPMN export documentation](https://help.bizagi.com/platform/en/exporting_to_bpmn.htm).
The native `.bpm` artifact is retained separately; exported XML is a projection,
not an interchangeable replacement for the native model.

## Gates still open

- Multi-diagram and deeply nested native preservation, with pools, lanes,
  gateways, messages, and every supported BPMN element category.
- No-op native save equivalence for extended attributes, attachments, resources,
  documentation, and simulation configurations.
- Cancellation specifically during persistence, interruption at each publication
  boundary, abrupt host termination, and journal recovery across a restarted host.
- Missing dependency and unaccredited-version circuits on independently prepared
  real installations; version rejection in code alone is not operational proof.
- Independent visual compatibility inside Modeler.
- Rich semantic edits beyond names, documentation publishing, and simulation.

Native editing remains opt-in and copy-only until these preservation gates are
accredited. This restriction is explicit, not an XML conversion fallback.

## Packaged-server acceptance

The same complete acceptance client was also executed against a freshly
extracted framework-dependent ZIP, not the development output directory.
All 301 manifest entries matched their SHA-256 values. XML calls, native
roundtrip, existing native inspection, two-element native edits, real failure
reporting, active cancellation, and subsequent recovery passed in that run.

This package validation checks executable behavior and dependency inclusion.
It does not close any rich-preservation or visual gate listed above. The
packaging script records the source commit and whether its checkout was dirty;
preview packages must not be described as reproducible release builds solely
because the extraction test passed.

## Repeat the tests

Use the commands in [README](../README.md#verification) and
[packaging](packaging.md#verify-the-packaged-server). Public CI runs the unit
suite and actual MCP XML circuit only; it must not imply native accreditation.
