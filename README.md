<div align="center">

# MCP-Bizagi

### Programmatic process modeling. Native fidelity as the acceptance criterion.

[![CI](https://github.com/h0w4r/MCP-Bizagi/actions/workflows/ci.yml/badge.svg)](https://github.com/h0w4r/MCP-Bizagi/actions/workflows/ci.yml)
![Status: experimental](https://img.shields.io/badge/status-experimental-orange)
![Platform: Windows x64](https://img.shields.io/badge/platform-Windows%20x64-0078D4)
![Host: .NET 10](https://img.shields.io/badge/host-.NET%2010-512BD4)
[![Source version](https://img.shields.io/badge/source-0.6.0--alpha.1-orange)](Directory.Build.props)
[![Verified package](https://img.shields.io/badge/package-0.6.0--alpha.1-blue)](https://github.com/h0w4r/MCP-Bizagi/releases/tag/v0.6.0-alpha.1)
[![License: custom attribution](https://img.shields.io/badge/license-custom%20attribution-blue)](LICENSE)

A local **Model Context Protocol server for Bizagi Modeler**.
Designed for programmatic BPMN and native `.bpm` workflows—without mouse macros,
foreground-window automation, or redistributing Bizagi binaries.

[Quick start](#quick-start) · [Capabilities](#capabilities) · [Architecture](docs/architecture.md) · [Verification](#verification) · [Contributing](CONTRIBUTING.md)

</div>

> **Experimental—not full Modeler automation yet.** Native operations are opt-in,
> version-gated integrations with the installed Modeler **4.3.0.008** engine.
> Files and isolated workers come first; live unsaved desktop sessions and
> independent GUI compatibility remain open.
>
> **Real execution, explicit boundaries.** Native persistence, structural edits,
> documentation, configured simulation, offscreen rendering and recovery have
> their own [acceptance records](docs/validation.md), not a blanket fidelity claim.
> The 0.6 package passed [274 terminal native operations across twenty circuits](docs/validation-package-0.6.md).
>
> **Packaged milestone: [0.6.0-alpha.1](https://github.com/h0w4r/MCP-Bizagi/releases/tag/v0.6.0-alpha.1).**
> Reparenting, selection copying, native alignment and nested-body VDX exchange
> join the consolidated Windows ZIP. All 392 manifest entries remained unchanged
> through real MCP/native acceptance, including failure and recovery cases.
> Earlier archives remain immutable. Check each contract and the
> [capability ledger](docs/capabilities.md) before relying on a feature;
> package acceptance is not full automation or GUI compatibility.

## Why this project

Modeling is more than drawing boxes. Native models can contain multiple diagrams,
documentation, extended attributes, attachments, and simulation configuration.
MCP-Bizagi treats these as fidelity requirements, rather than silently flattening
every operation into a BPMN XML export.

- **Explicit backends:** XML processing and the native Bizagi engine are not conflated.
- **No foreground automation:** no UIA clicks, input simulation, or window-focus takeover.
- **Guarded changes:** workspace confinement, SHA-256 revisions, staged replacement, and backups.
- **Observable operations:** durable status, meaningful phases, cancellation, and local diagnostics.
- **Evidence before claims:** builds and unit tests are separate from native and MCP acceptance.
- **Independent architecture:** built against the actual local installation and primary documentation.

## Capabilities

| Capability | Current implementation | Important boundary |
| --- | --- | --- |
| Inspect BPMN XML | Available | Element IDs, names, nesting, revision |
| Save supplied BPMN XML | Available | Preserves document content; does not invent layout |
| Batch edits | Element names only | Explicit revision required; other edit types rejected |
| Structural validation | Available | Not complete OMG XSD or behavioral validation |
| Native engine bootstrap | Opt-in diagnostic | Internal interfaces, not a supported vendor API |
| BPMN → `.bpm` → fresh-worker reload → BPMN | Opt-in diagnostic | No broad fidelity or visual accreditation claimed |
| Native XPDL 2.2 import/export | Experimental current source | Installed Unicode serializer/importer, selected diagrams, fresh native readback and explicit XML/graph/archive losses; not a native backup; [contract](docs/native-xpdl.md) |
| Native Visio VDX interchange | Experimental; 0.6 package verified | Installed mapper, explicit page/kind/label loss reports and strict native no-op/readback; separate nested-body pages with source-page receipts and tested internal flows; not reconstructed hierarchy, complete Visio support or a native backup; [contract](docs/native-visio.md) |
| Inspect existing `.bpm` | Opt-in, copy-only | Native graph, containment, geometry, descriptions, scenarios and revision |
| Create a blank native `.bpm` | Experimental current source | Native model constructor, one or more named diagrams, ordered tabs, fresh-reader and no-op stability gates; [contract](docs/native-models.md) |
| Adopt/save-as/replace native `.bpm` | Experimental current source | Byte-exact publication, separate source/target revisions, recoverable backups and intent reconciliation after host death; [contract](docs/native-commit.md) |
| Native `.bpm` name batches | Opt-in, copy-only | Fresh-worker readback and whole-container fidelity gate; tested nested/multi-diagram inputs |
| Native structural batches | Opt-in, copy-only | 22 task/event/gateway types tested; create/delete, connection endpoints, bounds, colors and descriptions; [limits](docs/native-editing.md) |
| Pools, lanes, milestones and embedded subprocesses | Experimental current source | Explicit process IDs, complete partitions, nested lifecycle and expanded/collapsed sizes; [contract](docs/native-containers.md) |
| Diagram lifecycle and persisted tab preferences | Experimental current source | Create, rename, native clone, delete and ordered/selected diagram or subprocess tabs; [contract](docs/native-diagrams.md) |
| Local reusable subprocess calls | Experimental current source | Create/link/unlink, preserve references, protect targets and remap native clones; explicit simulation black-box semantics; [contract](docs/native-calls.md) |
| Native activity/flow properties | Experimental current source | Activity quantities/compensation/state, gateway direction and conditions/default references; actual simulation input checked, nondefault token behavior unaccredited; [contract](docs/native-semantics.md) |
| Native activity loops | Experimental current source | Standard/multi-instance configuration and removal through native persistence; iteration simulation is a separate boundary; [contract and evidence](docs/native-loops.md) |
| Special subprocesses | Experimental current source | Transaction, ad hoc and event-triggered native lifecycle, nested surfaces and context guards; [contract and evidence](docs/native-subprocesses.md) |
| Native events and boundaries | Experimental current source | Explicit catch/throw/boundary modes, interruption, same-container references and clone remapping; [contract](docs/native-events.md) |
| Event definition payloads | Experimental current source | Existing-kind names, conditions, timers, codes and same-container compensation references; no implied collection editing or execution; [contract](docs/native-event-payloads.md) |
| Native data and activity/event I/O | Experimental current source | Data objects, shared stores, references and association-derived bindings; explicit nested-loader adjustments and clone checks; [contract](docs/native-data.md) |
| Native content artifacts | Experimental current source | Annotations, formatted text, diagram groups and native headers; exact text, geometry and clone fidelity; [contract](docs/native-artifacts.md) |
| Native image artifacts | Experimental current source | Revision-checked raster inputs, explicit frame selection, decoded-pixel/native-file fidelity, clone, extraction and deletion; [contract](docs/native-images.md) |
| Native custom artifacts | Experimental current source | Model-owned definitions and instances, explicit native pixel conversion, root/nested clone and rendering, native `.bca` import/export and fresh-reader fidelity; [contract](docs/native-custom-artifacts.md) |
| Native typography, colors and label bounds | Experimental current source | Installed-font inventory, sparse native styles, fresh-reader/clone fidelity and measured SVG subsets; internal/external labels and pool persistence have distinct limits; [contract](docs/native-styles.md) |
| Native task/gateway type conversion | Experimental current source | Installed command, explicit source/target types, durable identities and whole-archive protection; 99-pair native matrix and reverse path; [contract](docs/native-conversions.md) |
| Native event kind conversion | Experimental source after 0.6 | Explicit same-role changes through the installed command; neutral definitions only, guarded references/context, preserved event I/O, attachments and whole-archive checks; not role changes or payload migration; [contract](docs/native-conversions.md#events-preserve-the-role-replace-neutral-definitions) |
| Task / unbound-call conversion | Experimental source after 0.6 | Installed commands for eight task types and nested tasks; explicit call binding/unlinking, guarded conversion back to a task; no implicit diagram creation, process deletion or inlining; [contract](docs/native-conversions.md#task-to-an-unbound-call) |
| Extract embedded subprocess into a reusable process | Experimental current source | Installed refactoring command, root/nested content relocation, attachments/images and persisted-tab remapping; no behavioral equivalence or arbitrary-selection claim; [contract](docs/native-refactoring.md) |
| Move native elements between containers | Experimental; 0.6 package verified | Explicit same-diagram process/subprocess ownership, subtree IDs, reference closure and optional position; not cross-diagram migration or auto-layout; [contract](docs/native-reparenting.md) |
| Align/distribute selected native shapes | Experimental; 0.6 package verified | Eight installed-editor modes, typed MCP request, native callback acknowledgment, durable readback and archive fidelity; not whole-diagram auto-layout; [contract](docs/native-layout.md) |
| Copy closed native selections | Experimental; 0.6 package verified | Explicit source/destination, native clone identity maps, original/archive and fresh-reader fidelity, exact attachments/images; no OS clipboard or live-unsaved editing; [contract](docs/native-selection-copy.md) |
| Native no-op save and comparison | Opt-in | Every archive leaf checked; unknown differences reject the result |
| Native model validation | Opt-in | Actual vendor validator; successful execution may report model errors |
| Resources and activity RACI | Opt-in, copy-only | Create/update/delete local resources; explicit assignment sets and fresh-reader fidelity |
| Native scenario configuration | Opt-in, copy-only | Complete per-diagram BPSim replacement; explicit result discard; [contract](docs/native-simulation.md) |
| Native simulation and what-if | Experimental | Defaults and configured levels 2–4, costs, resource contention, shift calendars and replications verified on the documented corpus |
| Native SVG/PNG export | Experimental | Installed offscreen renderer, transparent PNG; basic diagram verified |
| Excel, Word and PDF publication | Opt-in native generators | Fresh-reader content/image checks; installed template; [publication boundaries](docs/native-publication.md) |
| Native Web publication | Experimental source after 0.6 | Selected root/subprocess pages, native search, byte-verified PNGs and tested embedded file; browser quality remains partial; [contract](docs/native-web-publication.md) |
| Extended attributes and embedded files | Experimental current source | Explicit native XML, complete element values, tables and byte transactions; [contract](docs/native-attributes.md) |
| Live unsaved Modeler sessions | Not implemented | Files and isolated engine first |

The desktop GUI does not need to be controlled by this server. This does **not**
claim Windows Service/Session 0 compatibility or universal headless support for
every internal Bizagi component.

## Requirements

- Windows x64.
- .NET 10 SDK **10.0.202** (latest installed patch in that feature band is accepted) to build.
- .NET Framework 4.8 for the isolated worker.
- For native diagnostics: an independently installed Bizagi Modeler **4.3.0.008**.
- An MCP client supporting local stdio servers.

The current engine version is intentionally gated. Future versions require fresh
integration evidence before native writes are enabled. Bizagi is not bundled.

## Quick start

**Use a package:** download the ZIP and checksum from the
[experimental release](https://github.com/h0w4r/MCP-Bizagi/releases/tag/v0.6.0-alpha.1),
verify SHA-256, then follow [packaged configuration](docs/packaging.md#verify-the-packaged-server).
The package requires the .NET 10 runtime but not the SDK.

**Build from source:**

```powershell
git clone https://github.com/h0w4r/MCP-Bizagi.git
cd MCP-Bizagi
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test tests/McpBizagi.Core.Tests -c Release --no-build
```

Configure your MCP client's stdio server entry using absolute paths:

```json
{
  "mcpServers": {
    "bizagi-modeler": {
      "command": "dotnet",
      "args": ["C:/Projects/MCP-Bizagi/src/McpBizagi.Server/bin/Release/net10.0-windows/McpBizagi.Server.dll"],
      "env": {
        "MCP_BIZAGI_ROOT": "C:/Processes",
        "MCP_BIZAGI_WORKER": "C:/Projects/MCP-Bizagi/src/McpBizagi.Worker/bin/Release/net48/McpBizagi.Worker.exe"
      }
    }
  }
}
```

This is a generic MCP configuration example, not a claim that every client's
configuration file uses the same schema. See [configuration](docs/configuration.md).

### Try the file workflow

1. Ask the client to save [`examples/minimal.bpmn`](examples/minimal.bpmn) using `bpmn_create`.
2. Inspect it using `bpmn_inspect` and retain the returned revision.
3. Rename `Task_Review` using `bpmn_apply_changes` and that revision.
4. Inspect again; review the persisted change and backup.
5. Use `bpmn_validate` for the currently implemented structural checks.

### Try the native diagnostic

Set `MCP_BIZAGI_EXPERIMENTAL_NATIVE=1` in the server's environment, then restart
the server. `BIZAGI_MODELER_PATH` can override automatic installation discovery;
its value is the **installation directory**, not an executable filename.

Call `native_probe` or `native_roundtrip`, then poll `operation_get` with the
returned operation ID. Failed, cancelled, interrupted, and completed operations
are different states. A completed diagnostic is not full native accreditation.

For an existing `.bpm`, call `native_inspect`, retain its `sourceRevision` and
native element IDs, then pass a name-change batch to `native_apply_changes`.
The result is a **new artifact**, not an overwrite of the input. BPMN IDs and
native IDs are not interchangeable. See the [tool reference](docs/tools.md).

Use `nativeArtifact` / `outputArtifact` references to feed generated models into
the next native tool, even when private state is outside the model workspace.
Try `native_save_copy` for no-op fidelity, `native_validate` for vendor findings,
or `native_simulate` with a native diagram ID. `native_render_svg` produces SVG
and transparent PNG using the installed offscreen renderer—never desktop clicks.
See [rendering boundaries](docs/rendering.md) and [native fidelity](docs/native-fidelity.md).

Use `native_mutate` for explicit structural/geometry/documentation batches, and
`native_publish` for Excel, Word, PDF or Web (Web requires source after 0.6). Both return operation IDs and retain
original files. PDF image downsampling is rejected unless explicitly accepted;
accepted resampling is reported, not hidden. Subprocess surfaces can be rendered
with `native_render_svg` and its optional `subProcessId`.

Use `native_metadata_get` to read resources, activity assignments and BPSim XML,
then `native_metadata_apply` to edit a revision-checked native copy. Run selected
scenarios with `native_simulate` or `native_simulate_what_if`. Completed results
include structured metrics as well as the actual engine-generated XML artifacts.

<details>
<summary>Real native render from the acceptance run</summary>

![Native Modeler rendering of the original request-handling example](docs/assets/native-request.png)

Generated through MCP and the installed offscreen renderer, not an illustration.
The PNG has a transparent background; black labels/connectors are best viewed
against a light background. Provenance is recorded in [validation](docs/validation.md).

</details>

### Build a Windows package

```powershell
./scripts/package.ps1
```

The script produces an ignored `artifacts/` directory and ZIP containing the
host, worker, redistributable dependencies, complete collected license texts,
dependency inventory, and SHA-256 manifest. It refuses an existing destination.
It does not include Bizagi, the .NET runtime, operator models, or local research.
See [packaging](docs/packaging.md) for packaged-server acceptance.

## Architecture

| Component | Runtime | Responsibility |
| --- | --- | --- |
| Server | .NET 10 | Official MCP SDK, tools, lifecycle, evidence |
| Core | .NET 10 | Guarded XML operations and workspace persistence |
| Contracts | .NET Standard 2.0 | Versioned worker request/result types |
| Worker | .NET Framework 4.8, x64 | Private named pipe and isolated native execution |
| Bizagi adapter | .NET Framework 4.8 | Version-specific integration with installed assemblies |

The worker runs separately, admits serialized operations, and loads the user's
installed libraries. No arbitrary reflection or generic code execution tool is
exposed through MCP. Native writes in this release are confined to diagnostic
artifacts, not the source model.

## Verification

```powershell
# Real MCP client -> real stdio server -> durable XML files
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- .

# Also run the actual native-engine roundtrip; failure exits nonzero
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native

# Expanded real-engine coverage, including process death and external private state
dotnet run --project tests/McpBizagi.Acceptance -c Release --no-build -- . --native --extended --simulation --render --recovery --external-state
```

Evidence is written under ignored `.local/acceptance/` directories. Native
diagnostics can contain local paths and Windows identity metadata embedded by
Bizagi. Do not publish raw native files or transcripts without sanitization.

| Evidence | What it establishes |
| --- | --- |
| Build | Compilation and dependency resolution |
| Unit tests | Isolated behavior and integrity checks |
| MCP XML acceptance | Actual protocol calls and file persistence |
| Native roundtrip diagnostic | Only the specific tested native route |
| Visual Modeler verification | Separate gate; not inferred from file parsing |

There are no placeholder screenshots, invented coverage percentages, or simulated
native success badges. [Change history](CHANGELOG.md) records bounded improvements,
not a blanket claim that every Modeler feature works.

## Safety and limitations

- Preserve originals; do not use experimental native diagnostics as your only backup.
- Root confinement and reparse-point rejection are defensive boundaries, not an OS sandbox.
- Operators sharing the same Windows identity can still modify their own files and processes.
- Native source models are never overwritten by the current tools. Adopting an
  output copy requires reviewing its fidelity warnings first.
- A Windows Job Object owns each worker. Read-only desktop observations record
  visible/foreground behavior without collecting unrelated window titles.
- The supervisor monitors phases, owned-job CPU and I/O, and diagnostic activity,
  including offscreen child processes. Liveness alone is not progress.
- The source filename and current revision must be checked before replaying a failed write.
- Cloud, Studio, Automation, authentication bypass, and foreground macros are outside this release.

## License and attribution

Developed by **[h0w4r](https://github.com/h0w4r)**.

The [custom attribution license](LICENSE) permits use, modification, forks,
redistribution, and commercial use while requiring visible credit in derivatives'
READMEs and accompanying documentation. **No watermark is required in user models
or generated process documents.** This is not standard MIT and does not claim OSI approval.

See [attribution](ATTRIBUTION.md) and [third-party notices](THIRD-PARTY-NOTICES.md).
This independent project is not affiliated with or endorsed by Bizagi.
