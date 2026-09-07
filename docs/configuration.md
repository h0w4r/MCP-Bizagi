# Configuration

| Environment variable | Meaning | Default |
| --- | --- | --- |
| `MCP_BIZAGI_ROOT` | Allowed model workspace | LocalAppData/MCP-Bizagi/workspace |
| `MCP_BIZAGI_STATE` | Private journals and native artifacts | LocalAppData/MCP-Bizagi/state |
| `MCP_BIZAGI_WORKER` | Absolute worker executable path | worker/McpBizagi.Worker.exe beside server |
| `BIZAGI_MODELER_PATH` | Modeler installation directory | Windows installed-app registry discovery |
| `MCP_BIZAGI_EXPERIMENTAL_NATIVE` | `1` explicitly enables native diagnostics | Disabled |
| `MCP_BIZAGI_INACTIVITY_SECONDS` | No-activity window, minimum 10 seconds | 120 |

Relative model paths are resolved within `MCP_BIZAGI_ROOT`. Traversal, alternate
data streams, and existing reparse points are rejected. Configuration is read
when the server starts; tools cannot change it.

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
