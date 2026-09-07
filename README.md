<div align="center">

# MCP-Bizagi

### Programmatic process modeling. Native fidelity as the acceptance criterion.

[![CI](https://github.com/h0w4r/MCP-Bizagi/actions/workflows/ci.yml/badge.svg)](https://github.com/h0w4r/MCP-Bizagi/actions/workflows/ci.yml)
![Status: experimental](https://img.shields.io/badge/status-experimental-orange)
![Platform: Windows x64](https://img.shields.io/badge/platform-Windows%20x64-0078D4)
![Host: .NET 10](https://img.shields.io/badge/host-.NET%2010-512BD4)
[![License: custom attribution](https://img.shields.io/badge/license-custom%20attribution-blue)](LICENSE)

A local **Model Context Protocol server for Bizagi Modeler**.
Designed for programmatic BPMN and native `.bpm` workflows—without mouse macros,
foreground-window automation, or redistributing Bizagi binaries.

[Quick start](#quick-start) · [Capabilities](#capabilities) · [Architecture](docs/architecture.md) · [Verification](#verification) · [Contributing](CONTRIBUTING.md)

</div>

> **Experimental foundation, not full Modeler automation yet.**
> BPMN XML tools are implemented. Native operations are opt-in diagnostics;
> resolving internal services is not evidence of complete `.bpm` support.
> See the [capability ledger](docs/capabilities.md) before relying on a feature.

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
| Native `.bpm` editing | Not released | Never redirected to the XML backend |
| Documentation and simulation | Investigated, not implemented | Native types discovered; workflows not accredited |
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
```

Evidence is written under ignored `.local/acceptance/` directories. Native
diagnostics may contain local paths and must be sanitized before sharing.

| Evidence | What it establishes |
| --- | --- |
| Build | Compilation and dependency resolution |
| Unit tests | Isolated behavior and integrity checks |
| MCP XML acceptance | Actual protocol calls and file persistence |
| Native roundtrip diagnostic | Only the specific tested native route |
| Visual Modeler verification | Separate gate; not inferred from file parsing |

There are no placeholder screenshots, invented coverage percentages, or simulated
native success badges. Demonstrations will be added only from verified runs.

## Safety and limitations

- Preserve originals; do not use experimental native diagnostics as your only backup.
- Root confinement and reparse-point rejection are defensive boundaries, not an OS sandbox.
- Operators sharing the same Windows identity can still modify their own files and processes.
- The supervisor monitors phase, CPU, and log activity. Native I/O-only progress
  without those signals may need a larger configured inactivity window.
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
