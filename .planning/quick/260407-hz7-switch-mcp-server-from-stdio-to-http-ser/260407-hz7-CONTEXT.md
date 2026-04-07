# Quick Task 260407-hz7: Switch MCP server from stdio to HTTP server transport - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Task Boundary

Switch MCP server from stdio-only to support HTTP server transport, enabling remote access (e.g., VM to host machine).

</domain>

<decisions>
## Implementation Decisions

### Stdio replacement vs dual mode
- **Dual mode**: Keep stdio as default transport. Add HTTP as an alternative activated via flag/config. Existing local usage preserved.

### Port & binding config
- **All three layers**: appsettings.json default, overridable by environment variable, overridable by CLI argument. Standard .NET configuration layering.

### Auth & network security
- **No auth**: Open endpoint. Rely on network-level security (firewall, VM networking). Simplest approach for the VM-to-host use case.

### Claude's Discretion
- Default port number selection
- HTTP transport implementation details (SSE vs Streamable HTTP based on MCP SDK capabilities)

</decisions>

<specifics>
## Specific Ideas

- User wants README.md updated to document the new HTTP transport option
- Primary use case: running in a VM and exposing port to host machine

</specifics>

<canonical_refs>
## Canonical References

- MCP SDK docs: builder pattern with `WithHttpServerTransport()` or equivalent
- Existing `Program.cs` line 26: `mcpBuilder.WithStdioServerTransport()` is the current transport setup

</canonical_refs>
