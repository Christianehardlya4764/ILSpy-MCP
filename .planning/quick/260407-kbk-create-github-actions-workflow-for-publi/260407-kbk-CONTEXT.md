# Quick Task 260407-kbk: GitHub Actions release workflow - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Task Boundary

Create a GitHub Actions workflow that publishes self-contained release binaries when a version tag is pushed, and update README.md with installation instructions for downloading pre-built binaries.

</domain>

<decisions>
## Implementation Decisions

### Target platforms
- **Full coverage**: win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64

### Release trigger
- **Tag push**: Push a git tag like `v1.0.0` to trigger the release. Standard GitHub convention.

### Artifact format
- **.zip for Windows/macOS, .tar.gz for Linux**: Standard packaging conventions per platform.

### Claude's Discretion
- Workflow file naming and structure
- Whether to include checksums in the release
- dotnet publish flags (trimming, compression, etc.)

</decisions>

<specifics>
## Specific Ideas

- Self-contained binaries (no .NET runtime required on target machine)
- User's primary use case: deploying to an airgapped VM
- README should document how to download and run the pre-built binary

</specifics>

<canonical_refs>
## Canonical References

- Existing README.md for update context
- ILSpy.Mcp.csproj for project configuration (currently targets net10.0, SDK is Microsoft.NET.Sdk.Web)

</canonical_refs>
