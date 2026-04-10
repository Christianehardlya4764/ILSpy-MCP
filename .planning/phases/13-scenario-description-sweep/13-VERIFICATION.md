---
phase: 13-scenario-description-sweep
verified: 2026-04-11T00:00:00Z
status: gaps_found
score: 1/3 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 2/3
  gaps_closed:
    - "find_usages description accurately describes the tool's interface (paginated output claim vs. actual parameters)"
  gaps_remaining:
    - "list_assembly_types and list_namespace_types descriptions cross-reference each other with guidance on scope difference"
  regressions:
    - "ListAssemblyTypesTool.cs description reverted from scenario-oriented to NuGet-consumer language by commit 1740974"
    - "ListNamespaceTypesTool.cs description lost cross-reference to list_assembly_types and lost scenario orientation in commit 1740974"
gaps:
  - truth: "list_assembly_types and list_namespace_types descriptions cross-reference each other with guidance on scope difference"
    status: failed
    reason: >
      Two regressions introduced by gap-closure commit 1740974.
      (1) ListAssemblyTypesTool.cs description was reverted from the proper Phase 13-01 scenario text
      ("Use this when you need a broad inventory of what an unfamiliar binary contains...")
      back to NuGet-consumer language ("Use this when you added a NuGet package...").
      (2) ListNamespaceTypesTool.cs description lost its cross-reference to list_assembly_types
      entirely — the Phase 13-01 text said "For a lighter assembly-wide listing by namespace
      (names only, no signatures), use list_assembly_types instead" but this was removed.
      The cross-reference is now one-directional only (list_assembly_types → list_namespace_types).
      SC #3 requires both tools to cross-reference each other.
    artifacts:
      - path: "Transport/Mcp/Tools/ListAssemblyTypesTool.cs"
        issue: >
          Line 25 description says "Use this when you added a NuGet package..." — NuGet-consumer
          language. Should say "Use this when you need a broad inventory of what an unfamiliar
          binary contains before narrowing scope." (Phase 13-01 commit 99b2457 had the correct text).
          Also: assemblyPath parameter description regressed to "Path to the .NET assembly file"
          from the standardized "Path to the .NET assembly (.dll/.exe)".
      - path: "Transport/Mcp/Tools/ListNamespaceTypesTool.cs"
        issue: >
          Line 28 description "Lists all types in a namespace with full signatures, member counts,
          and public method signatures. Returns a summary -- use decompile_type to get full source
          for individual types." — missing cross-reference to list_assembly_types and missing
          scenario orientation. Phase 13-01 commit c610ba0 had the correct text:
          "Use this when you know which namespace to investigate and want a detailed inventory
          before drilling into individual types. For a lighter assembly-wide listing by namespace
          (names only, no signatures), use list_assembly_types instead."
    missing:
      - "Restore ListAssemblyTypesTool.cs description to scenario-oriented language (not NuGet-consumer) and standardize assemblyPath param"
      - "Restore ListNamespaceTypesTool.cs cross-reference to list_assembly_types and scenario orientation"
      - "Both tools must reference each other bidirectionally per SC #3"
---

# Phase 13: Scenario Description Sweep Verification Report

**Phase Goal:** Every tool description tells the agent "when is the agent reaching for this?" rather than "what does this tool produce?", and overlapping tools cross-reference each other with cost guidance
**Verified:** 2026-04-11T00:00:00Z
**Status:** gaps_found
**Re-verification:** Yes — after gap closure plan 13-03

## Goal Achievement

### Observable Truths (from Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Every mechanical description (21 tools from audit) rewritten to "Use this when..." format | PARTIAL | 25 of 27 Phase 13 target tools pass. ListAssemblyTypesTool.cs (line 25) was reverted to NuGet-consumer language by commit 1740974. ListNamespaceTypesTool.cs (line 28) lost scenario orientation in same commit. Both technically contain "Use this" or "Lists all" phrases but the content quality regressed. |
| 2 | decompile_type and get_type_members cross-reference each other with cost/use-case guidance | VERIFIED | decompile_type: "For a quick structural overview without source (cheaper), use get_type_members instead". get_type_members: "faster and cheaper than decompile_type. For full implementation details and method bodies, use decompile_type instead." Bidirectional with cost guidance. |
| 3 | list_assembly_types and list_namespace_types cross-reference each other with guidance on scope difference | FAILED | Cross-reference is broken in both directions. ListAssemblyTypesTool.cs → list_namespace_types: present (line 25) but surrounding text is NuGet-consumer language ("added a NuGet package"), not scope guidance. ListNamespaceTypesTool.cs → list_assembly_types: ABSENT — removed by commit 1740974. SC #3 requires bidirectional scope-guidance cross-references. |

**Score:** 1/3 truths verified

### Re-verification: Gaps from Previous VERIFICATION.md

| Previous Gap | Was Closed? | Evidence |
|-------------|-------------|---------|
| find_usages description claims "Returns paginated matches" but has no pagination params | CLOSED | FindUsagesTool.cs now has maxResults (default 100) and offset (default 0) parameters. Description now says "Find all call sites, field accesses... Use this to trace..." — no false pagination claim. PaginationEnvelope used in FindUsagesUseCase.cs. |
| list_namespace_types does not exist as a registered tool name | CLOSED | ListNamespaceTypesTool.cs exists with `[McpServerTool(Name = "list_namespace_types")]`. No decompile_namespace references remain anywhere in .cs files. |

### Re-verification: Regression Introduced by Gap Closure (commit 1740974)

| File | Before (commit 99b2457 / c610ba0) | After (commit 1740974) | Regression |
|------|-----------------------------------|------------------------|------------|
| ListAssemblyTypesTool.cs | "Use this when you need a broad inventory of what an unfamiliar binary contains before narrowing scope. For a single namespace with full signatures and member counts, use decompile_namespace instead." | "Use this when you added a NuGet package but don't know what classes/types it provides. Lists all available types by namespace..." | NuGet-consumer language restored; also cross-ref now uses list_namespace_types name (correct) but framing is wrong |
| ListNamespaceTypesTool.cs | "Use this when you know which namespace to investigate and want a detailed inventory before drilling into individual types. For a lighter assembly-wide listing by namespace (names only, no signatures), use list_assembly_types instead." | "Lists all types in a namespace with full signatures, member counts, and public method signatures. Returns a summary -- use decompile_type to get full source for individual types." | Cross-reference to list_assembly_types removed entirely; scenario framing lost |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Transport/Mcp/Tools/ListNamespaceTypesTool.cs` | Tool registered as list_namespace_types | VERIFIED | `[McpServerTool(Name = "list_namespace_types")]` present at line 27 |
| `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` | Scenario description + cross-ref to list_namespace_types | PARTIAL | Cross-ref to list_namespace_types present but surrounding text is NuGet-consumer language |
| `Transport/Mcp/Tools/FindUsagesTool.cs` | maxResults and offset parameters | VERIFIED | Lines 33-34: int maxResults = 100, int offset = 0 |
| `Application/UseCases/FindUsagesUseCase.cs` | PaginationEnvelope usage | VERIFIED | Skip/Take pagination at line 53; PaginationEnvelope.AppendFooter at lines 89, 96, 110 |
| `Application/UseCases/DecompileNamespaceUseCase.cs` | Must NOT exist | VERIFIED | File does not exist |
| `Transport/Mcp/Tools/DecompileNamespaceTool.cs` | Must NOT exist | VERIFIED | File does not exist |
| `Tests/Tools/ListNamespaceTypesToolTests.cs` | Renamed test class | VERIFIED | class ListNamespaceTypesToolTests present |
| `Tests/Tools/FindUsagesToolTests.cs` | Pagination tests | VERIFIED | FindUsages_Pagination_MaxResultsLimitsOutput and FindUsages_Pagination_MaxResultsExceedsLimit_ThrowsError at lines 103, 121 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| list_assembly_types | list_namespace_types | Description text | PARTIAL | Cross-reference name is correct ("use list_namespace_types instead") but context is NuGet-consumer language |
| list_namespace_types | list_assembly_types | Description text | NOT_WIRED | Cross-reference absent from ListNamespaceTypesTool.cs description — removed by commit 1740974 |
| decompile_type | get_type_members | Description text | WIRED | "use get_type_members instead" present |
| get_type_members | decompile_type | Description text | WIRED | "use decompile_type instead" with cost guidance present |
| find_usages | FindUsagesUseCase | maxResults/offset passthrough | WIRED | Parameters passed at line 50 of FindUsagesTool.cs |
| Program.cs | ListNamespaceTypesTool | DI registration | WIRED | AddScoped<ListNamespaceTypesTool>() at line 201 |
| ToolTestFixture.cs | ListNamespaceTypesTool | DI registration | WIRED | AddScoped<ListNamespaceTypesTool>() at line 100 |

### Data-Flow Trace (Level 4)

Not applicable. Phase 13 modifies only `[Description]` attribute strings on MCP tool methods and pagination parameters. No dynamic data rendering changes relevant to data-flow tracing.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds with 0 errors | dotnet build ILSpy.Mcp.sln --no-restore -v q | "Build succeeded. 0 Warning(s) 0 Error(s)" | PASS |
| No decompile_namespace references in .cs files | grep -rn "decompile_namespace" Transport/ Application/ Tests/ Program.cs | 0 matches | PASS |
| list_namespace_types registered in tool | grep McpServerTool ListNamespaceTypesTool.cs | `[McpServerTool(Name = "list_namespace_types")]` found | PASS |
| find_usages has maxResults parameter | grep maxResults FindUsagesTool.cs | "int maxResults = 100" found at line 33 | PASS |
| PaginationEnvelope used in FindUsagesUseCase | grep PaginationEnvelope FindUsagesUseCase.cs | Found at lines 89, 96, 110 | PASS |
| ListAssemblyTypesTool description is scenario-oriented | grep -v NuGet ListAssemblyTypesTool.cs | NuGet-consumer language still present at line 25 | FAIL |
| list_namespace_types description cross-references list_assembly_types | grep list_assembly ListNamespaceTypesTool.cs | No match | FAIL |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| DESC-01 | Plans 13-01, 13-02, 13-03 | Every mechanical description rewritten to scenario-oriented "Use this when..." format | PARTIAL | 25 of 27 Phase 13 target tools have correct scenario descriptions. ListAssemblyTypesTool.cs reverted to NuGet-consumer language by commit 1740974. ListNamespaceTypesTool.cs lost scenario framing in same commit. |
| DESC-02 | Plans 13-01, 13-02, 13-03 | Overlapping tools cross-reference each other with cost/use-case guidance | PARTIAL | decompile_type ↔ get_type_members: DONE with cost guidance. list_assembly_types → list_namespace_types: present (wrong framing). list_namespace_types → list_assembly_types: ABSENT (removed by gap closure regression). |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` | 25 | Description contains "NuGet package" — consumer framing not RE-tool framing | Blocker | Contradicts DESC-01 goal; agent misidentifies tool as a NuGet consumer utility rather than a .NET static analysis tool |
| `Transport/Mcp/Tools/ListNamespaceTypesTool.cs` | 28 | Description missing cross-reference to list_assembly_types | Blocker | Breaks SC #3 bidirectional cross-reference; agent cannot navigate from list_namespace_types to list_assembly_types |
| `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` | 27 | assemblyPath parameter uses "Path to the .NET assembly file" instead of standard "Path to the .NET assembly (.dll/.exe)" | Warning | Minor inconsistency; introduced by commit 1740974 regression |

### Human Verification Required

None. All success criteria are verifiable programmatically via file content inspection.

### Gaps Summary

One root-cause produces two symptoms: the gap-closure commit `1740974` read `ListAssemblyTypesTool.cs` and `DecompileNamespaceTool.cs` from a stale base state (without the Phase 13-01 description rewrites applied), then wrote new content that only added the `list_namespace_types` cross-reference without preserving the scenario-oriented descriptions that Phase 13-01 had already installed.

**Regression in ListAssemblyTypesTool.cs:** Phase 13-01 (commit `99b2457`) had written: "Lists all public types in an assembly grouped by namespace... Use this when you need a broad inventory of what an unfamiliar binary contains before narrowing scope." The gap closure replaced this with the pre-Phase-13 NuGet-consumer text ("Use this when you added a NuGet package..."). The cross-reference name was correctly updated to `list_namespace_types` but the body text violates DESC-01.

**Regression in ListNamespaceTypesTool.cs:** Phase 13-01 (commit `c610ba0`) had written the decompile_namespace description with "Use this when you know which namespace to investigate... For a lighter assembly-wide listing by namespace... use list_assembly_types instead." The gap closure removed the `list_assembly_types` cross-reference entirely when creating ListNamespaceTypesTool.cs. SC #3 requires bidirectional cross-referencing.

**Fix is minimal:** Restore both descriptions to the Phase 13-01 text, updating only the tool names (decompile_namespace → list_namespace_types in list_assembly_types cross-ref, already done). The logic and structure are correct in the Phase 13-01 commits; the gap closure just needs to not have overwritten them.

---

_Verified: 2026-04-11T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
