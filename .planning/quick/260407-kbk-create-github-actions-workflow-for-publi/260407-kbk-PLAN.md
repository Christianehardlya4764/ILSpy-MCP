---
phase: quick
plan: 260407-kbk
type: execute
wave: 1
depends_on: []
files_modified:
  - .github/workflows/release.yml
  - README.md
autonomous: true
must_haves:
  truths:
    - "Pushing a v* tag triggers the release workflow"
    - "Self-contained binaries are built for win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64"
    - "Windows and macOS artifacts are .zip, Linux artifacts are .tar.gz"
    - "A GitHub Release is created with all platform binaries attached"
    - "README documents how to download and run pre-built binaries"
  artifacts:
    - path: ".github/workflows/release.yml"
      provides: "GitHub Actions release workflow"
    - path: "README.md"
      provides: "Updated install instructions with binary download section"
  key_links:
    - from: "git tag v*"
      to: ".github/workflows/release.yml"
      via: "on.push.tags trigger"
      pattern: "tags:.*v"
---

<objective>
Create a GitHub Actions workflow that builds self-contained release binaries for 5 platforms when a version tag is pushed, and update README.md with instructions for downloading pre-built binaries.

Purpose: Enable airgapped deployment by providing self-contained binaries that need no .NET runtime.
Output: release.yml workflow file, updated README.md
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@ILSpy.Mcp.csproj
@README.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create GitHub Actions release workflow</name>
  <files>.github/workflows/release.yml</files>
  <action>
Create `.github/workflows/` directory and `release.yml` with:

**Trigger:** `on.push.tags` matching `v*` pattern (e.g., `v1.0.0`, `v2.1.0-beta`).

**Strategy:** Use a matrix build across 5 RIDs: `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Each matrix entry specifies:
- `rid`: the .NET runtime identifier
- `os`: the GitHub runner (`windows-latest` for win-x64, `ubuntu-latest` for linux-*, `macos-latest` for osx-*)
- `archive`: `zip` for win-x64/osx-*, `tar.gz` for linux-*

**Build step:** Run `dotnet publish ILSpy.Mcp.csproj` with these flags:
- `-c Release`
- `-r {rid}`
- `--self-contained true`
- `-p:PublishSingleFile=true`
- `-p:EnableCompressionInSingleFile=true`
- `-p:IncludeNativeLibrariesForSelfExtract=true`
- `-o ./publish/{rid}`

**Package step:**
- For zip targets: use `Compress-Archive` (Windows) or `zip` command to create `ilspy-mcp-{rid}.zip` from `./publish/{rid}/`
- For tar.gz targets: use `tar -czf ilspy-mcp-{rid}.tar.gz -C ./publish/{rid} .`

**Checksum step:** Generate SHA256 checksums for each archive. Output to `ilspy-mcp-{rid}.sha256`.

**Upload step:** Use `actions/upload-artifact@v4` to upload the archive and checksum as build artifacts.

**Release job** (runs after build matrix completes):
- Uses `actions/download-artifact@v4` to download all artifacts
- Creates a GitHub Release using `softprops/action-gh-release@v2` with:
  - Tag name from `github.ref_name`
  - Auto-generated release notes enabled (`generate_release_notes: true`)
  - All `.zip`, `.tar.gz`, and `.sha256` files attached
  - `draft: false`, `prerelease: false` (can manually mark pre-release for beta tags)

**Permissions:** Set `contents: write` at workflow level for release creation.

**Setup .NET:** Use `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`. Since net10.0 is the target framework, ensure the workflow installs the correct SDK. If 10.0.x is not yet available on GitHub-hosted runners, add `include-prerelease: true` to setup-dotnet.
  </action>
  <verify>
    <automated>python -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml'))" 2>/dev/null || node -e "const fs=require('fs'); const f=fs.readFileSync('.github/workflows/release.yml','utf8'); if(!f.includes('on:')) process.exit(1); console.log('Valid structure')"</automated>
  </verify>
  <done>release.yml exists with tag trigger, 5-platform matrix build, self-contained publish, archive packaging with checksums, and GitHub Release creation</done>
</task>

<task type="auto">
  <name>Task 2: Update README with pre-built binary installation instructions</name>
  <files>README.md</files>
  <action>
Add a new section to README.md **after the existing "Installation" section** (the `dotnet tool install` block) and **before "Configure MCP Client"**. Title it `### Pre-built Binaries (No .NET Required)`.

Content should include:

1. **Download instructions**: Direct users to the GitHub Releases page at `https://github.com/gentledepp/ILSpy-Mcp/releases` to download the binary for their platform.

2. **Platform table** listing available binaries:
   | Platform | File |
   |----------|------|
   | Windows x64 | `ilspy-mcp-win-x64.zip` |
   | Linux x64 | `ilspy-mcp-linux-x64.tar.gz` |
   | Linux ARM64 | `ilspy-mcp-linux-arm64.tar.gz` |
   | macOS x64 | `ilspy-mcp-osx-x64.zip` |
   | macOS ARM64 | `ilspy-mcp-osx-arm64.zip` |

3. **Quick start per platform** with extract and run commands:
   - Windows: `Expand-Archive ilspy-mcp-win-x64.zip -DestinationPath ilspy-mcp` then `.\ilspy-mcp\ILSpy.Mcp.exe`
   - Linux/macOS: `tar -xzf ilspy-mcp-linux-x64.tar.gz -d ilspy-mcp` (or `unzip` for macOS zip), `chmod +x ilspy-mcp/ILSpy.Mcp`, `./ilspy-mcp/ILSpy.Mcp`

4. **MCP client configuration for binary path**: Show how to configure `.mcp.json` pointing to the extracted binary path instead of the `ilspy-mcp` tool command. Example:
   ```json
   {
     "mcpServers": {
       "ilspy-mcp": {
         "type": "stdio",
         "command": "/path/to/ilspy-mcp/ILSpy.Mcp",
         "args": []
       }
     }
   }
   ```

5. **Note** that these are self-contained binaries requiring no .NET runtime installation, making them ideal for airgapped environments.

Also update the Prerequisites section: change ".NET 9.0 SDK or higher" to ".NET 10.0 SDK or higher (not needed for pre-built binaries)".
  </action>
  <verify>
    <automated>grep -q "Pre-built Binaries" README.md && grep -q "self-contained" README.md && grep -q "releases" README.md && echo "README updated" || echo "FAIL"</automated>
  </verify>
  <done>README.md contains pre-built binary download instructions with platform table, extract/run commands, MCP config for binary path, and airgapped deployment note</done>
</task>

</tasks>

<verification>
- `.github/workflows/release.yml` is valid YAML with correct trigger, matrix, and release job
- README.md has both dotnet tool and pre-built binary installation paths documented
- No existing README content is removed or broken
</verification>

<success_criteria>
- Workflow file triggers on `v*` tag push
- Builds self-contained single-file binaries for all 5 platforms
- Creates GitHub Release with archives and SHA256 checksums
- README documents both installation methods (dotnet tool + binary download)
</success_criteria>

<output>
After completion, create `.planning/quick/260407-kbk-create-github-actions-workflow-for-publi/260407-kbk-SUMMARY.md`
</output>
