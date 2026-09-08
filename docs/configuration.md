# Configuration

| Environment variable | Meaning | Default |
| --- | --- | --- |
| `MCP_BIZAGI_ROOT` | Allowed model workspace | LocalAppData/MCP-Bizagi/workspace |
| `MCP_BIZAGI_STATE` | Private journals and native artifacts | LocalAppData/MCP-Bizagi/state |
| `MCP_BIZAGI_WORKER` | Absolute worker executable path | worker/McpBizagi.Worker.exe beside server |
| `BIZAGI_MODELER_PATH` | Modeler installation directory | Windows installed-app registry discovery |
| `MCP_BIZAGI_EXPERIMENTAL_NATIVE` | `1` explicitly enables native diagnostics | Disabled |
| `MCP_BIZAGI_INACTIVITY_SECONDS` | No-activity window, minimum 10 seconds | 120 |
| `MCP_BIZAGI_CONNECTION_SECONDS` | Initial pipe connection limit only | 30 |
| `MCP_BIZAGI_CLEANUP_SECONDS` | Grace period before owned-process cleanup | 3 |
| `MCP_BIZAGI_ATOMIC_STEP_SECONDS` | Atomic renderer image-decode handshake limit | 30 |

Relative model paths are resolved within `MCP_BIZAGI_ROOT`. Traversal, alternate
data streams, and existing reparse points are rejected. Configuration is read
when the server starts; tools cannot change it.

Only one live host may own a state directory. A second host is rejected before
it can rewrite active operation journals. Separate clients can use distinct
state directories. Completed native artifact references can cross from private
state into later native operations without widening ordinary workspace access.

The installed settings provider derives its application namespace from the
worker executable metadata. Actual paths are checked before service resolution:
`LocalAppData/h0w4r/McpBizagi.Worker` and `AppData/h0w4r/McpBizagi.Worker`.
They are MCP-owned application defaults, **not** the operator's Modeler settings.
Nothing is copied from the Modeler profile. These defaults are shared across
worker runs; a process-lifetime exclusive file lease prevents different state
directories from entering native operations concurrently. A competing native
worker fails explicitly rather than racing or silently retrying a write.
Temporary models, renderer caches and logs remain operation-specific. Actual
settings paths are recorded in private worker evidence, never in public logs.

Standard output is MCP protocol traffic only. Standard error contains host
diagnostics. Native worker logs and operation journals are stored privately under
the state directory, with operation IDs for correlation.

For locked models or revision conflicts, inspect the latest file before retrying.
For a worker crash or interrupted write, inspect artifacts and backups before
starting another operation. Never infer success from a returned process exit code
alone.

Current size bounds are 64 MiB for model files and 16 Mi characters for parsed
XML. Native archive preflight also limits entry count, expanded size, nesting,
unsafe paths, and DTD usage. Encrypted native containers are not supported.
Native model/diagram export labels must be valid Windows file names of at most
120 characters; ambiguous labels are rejected rather than silently renamed.

## Native worker path handling

The worker executable opts into long Windows paths through its manifest and
explicit .NET Framework path switches. Native extraction adds model, diagram
and attachment-owner GUIDs beneath each operation directory, so even a modest
file name can exceed the old 260-character boundary. This is process-local
configuration: the server never changes the Windows registry or Bizagi files.
See Microsoft's [long-path guidance](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation)
and [.NET Framework compatibility notes](https://github.com/microsoft/dotnet/blob/main/Documentation/compatibility/long-path-support.md).

The private `worker-path-policy.json` records actual framework switches.
Bounded `native-path-length-*.txt` diagnostics retain the original exception
when an internal native loader obscures it with an old-format fallback error.
This does not waive path confinement, per-component file-name limits or OS
requirements, nor establish long-path support for every third-party exporter.
