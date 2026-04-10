---
phase: 13-scenario-description-sweep
verified: 2026-04-10T14:11:13Z
status: gaps_found
score: 2/3 must-haves verified
overrides_applied: 0
gaps:
  - truth: "list_assembly_types and list_namespace_types descriptions cross-reference each other with guidance on scope difference"
    status: failed
    reason: "list_namespace_types does not exist as a tool name. The tool was never renamed from decompile_namespace (Phase 9 CLEAN-02 was not completed). list_assembly_types cross-references decompile_namespace instead, using the old name. SC #3 requires the cross-reference to use the name list_namespace_types."
    artifacts:
      - path: "Transport/Mcp/Tools/ListAssemblyTypesTool.cs"
        issue: "Cross-reference says 'use decompile_namespace instead' — should say 'use list_namespace_types' after Phase 9 rename"
      - path: "Transport/Mcp/Tools/DecompileNamespaceTool.cs"
        issue: "Tool is still registered as decompile_namespace, not list_namespace_types — Phase 9 CLEAN-02 rename was not applied to current branch"
    missing:
      - "Phase 9 CLEAN-02 must be completed: rename DecompileNamespaceTool to ListNamespaceTypesTool, change tool name from decompile_namespace to list_namespace_types"
      - "After rename, update list_assembly_types description to reference list_namespace_types"
  - truth: "find_usages description accurately describes the tool's interface (paginated output claim vs. actual parameters)"
    status: failed
    reason: "The description for find_usages says 'Returns paginated matches with declaring type, method signature, and IL offset.' but the tool has NO maxResults or offset parameters. There is no way for an agent to paginate. This is a description/signature mismatch introduced in Phase 13 commit fb6a7d4 — that commit operated against the pre-pagination index (19e610e) rather than the post-pagination restored version (3d347d0 from fix e955eea). The pagination parameters that were present before Phase 13 are now absent."
    artifacts:
      - path: "Transport/Mcp/Tools/FindUsagesTool.cs"
        issue: "Line 28 description claims 'Returns paginated matches' but ExecuteAsync has only assemblyPath, typeName, memberName, and CancellationToken — no maxResults or offset"
    missing:
      - "Either restore the maxResults and offset parameters to FindUsagesTool.ExecuteAsync (and pass them to FindUsagesUseCase), or correct the description to say 'Returns all matches' instead of 'Returns paginated matches'"
      - "If pagination is restored, also restore the corresponding FindUsagesUseCase pagination logic that was present in commit 3d347d0"
---

# Phase 13: Scenario Description Sweep Verification Report

**Phase Goal:** Every tool description tells the agent "when is the agent reaching for this?" rather than "what does this tool produce?", and overlapping tools cross-reference each other with cost guidance
**Verified:** 2026-04-10T14:11:13Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (from Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Every mechanical description (21 tools from audit) rewritten to "Use this when..." format | VERIFIED | 27 of 28 tool files contain "Use this when" or "Use this to". The 28th (AnalyzeReferencesTool) was scheduled for deletion in Phase 9 CLEAN-01 and was not in the Phase 13 audit scope. All 27 updated tools pass the pattern check. |
| 2 | decompile_type and get_type_members cross-reference each other with cost/use-case guidance | VERIFIED | decompile_type description: "For a quick structural overview without source (cheaper), use get_type_members instead". get_type_members description: "faster and cheaper than decompile_type. For full implementation details and method bodies, use decompile_type instead." Both cross-reference each other with cost and use-case guidance. |
| 3 | list_assembly_types and list_namespace_types cross-reference each other with scope guidance | FAILED | list_namespace_types does not exist as a registered tool name. The tool is still named decompile_namespace (Phase 9 CLEAN-02 not completed). list_assembly_types cross-references "decompile_namespace" — the wrong name. |

**Score:** 2/3 truths verified

### Additional Gap: find_usages Description/Signature Mismatch

This is not a standalone success criterion but is a DESC-01 quality gap: the find_usages description claims "Returns paginated matches" but the tool exposes no pagination parameters (maxResults/offset are absent from ExecuteAsync). An agent reading the description would expect to paginate but cannot. This was introduced by Phase 13 commit fb6a7d4 operating against a stale base that lacked the Phase 10 pagination parameters.

### Required Artifacts

All 27 tool files listed in SUMMARY 13-01 and SUMMARY 13-02 exist on disk. All contain scenario-oriented descriptions.

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Transport/Mcp/Tools/AnalyzeAssemblyTool.cs` | Scenario description | VERIFIED | "Use this when starting analysis of an unfamiliar binary" |
| `Transport/Mcp/Tools/DecompileMethodTool.cs` | Scenario + cross-ref | VERIFIED | Cross-refs disassemble_method |
| `Transport/Mcp/Tools/DecompileNamespaceTool.cs` | Scenario + cross-ref | PARTIAL | Has scenario description and cross-ref to list_assembly_types, but tool name should be list_namespace_types |
| `Transport/Mcp/Tools/DecompileTypeTool.cs` | Scenario + cross-refs | VERIFIED | Cross-refs get_type_members and disassemble_type with cost guidance |
| `Transport/Mcp/Tools/DisassembleMethodTool.cs` | Scenario + cross-refs | VERIFIED | Cross-refs disassemble_type and decompile_method |
| `Transport/Mcp/Tools/DisassembleTypeTool.cs` | Scenario + cross-refs | VERIFIED | Cross-refs disassemble_method and decompile_type |
| `Transport/Mcp/Tools/ExportProjectTool.cs` | Scenario description | VERIFIED | "Use this when you need to browse or search decompiled source across an entire assembly" |
| `Transport/Mcp/Tools/ExtractResourceTool.cs` | Scenario description | VERIFIED | "Use this when you need to read configuration, localization tables" |
| `Transport/Mcp/Tools/FindCompilerGeneratedTypesTool.cs` | Scenario description | VERIFIED | "Use this when an assembly contains mysterious '<>c__DisplayClass'" |
| `Transport/Mcp/Tools/FindDependenciesTool.cs` | Scenario description | VERIFIED | "Use this when assessing what an unfamiliar method touches" |
| `Transport/Mcp/Tools/FindExtensionMethodsTool.cs` | Scenario description | VERIFIED | "Use this when investigating what additional operations are available" |
| `Transport/Mcp/Tools/FindImplementorsTool.cs` | Scenario description | VERIFIED | "Use this when mapping the concrete implementations behind an abstraction" |
| `Transport/Mcp/Tools/FindInstantiationsTool.cs` | Scenario description | VERIFIED | "Use this when tracing object creation patterns" |
| `Transport/Mcp/Tools/FindTypeHierarchyTool.cs` | Scenario description | VERIFIED | "Use this when you need to understand what contracts a type fulfills" |
| `Transport/Mcp/Tools/FindUsagesTool.cs` | Scenario description | PARTIAL | Scenario language present but description claims "paginated" with no pagination params |
| `Transport/Mcp/Tools/GetAssemblyAttributesTool.cs` | Scenario description | VERIFIED | "Use this when investigating assembly-wide configuration" |
| `Transport/Mcp/Tools/GetAssemblyMetadataTool.cs` | Scenario description | VERIFIED | "Use this when you need to determine an assembly's runtime requirements" |
| `Transport/Mcp/Tools/GetMemberAttributesTool.cs` | Scenario description | VERIFIED | "Use this when checking authorization rules, validation constraints" |
| `Transport/Mcp/Tools/GetTypeAttributesTool.cs` | Scenario description | VERIFIED | "Use this when checking serialization settings, ORM mappings" |
| `Transport/Mcp/Tools/GetTypeMembersTool.cs` | Scenario + cross-ref + cost | VERIFIED | "faster and cheaper than decompile_type. For full implementation details...use decompile_type instead" |
| `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` | Scenario + cross-ref | PARTIAL | Has scenario language, but cross-references decompile_namespace (old name) instead of list_namespace_types |
| `Transport/Mcp/Tools/ListEmbeddedResourcesTool.cs` | Scenario + cross-ref | VERIFIED | Cross-refs extract_resource |
| `Transport/Mcp/Tools/LoadAssemblyDirectoryTool.cs` | Scenario + cross-ref | VERIFIED | Cross-refs resolve_type |
| `Transport/Mcp/Tools/ResolveTypeTool.cs` | Scenario + cross-ref | VERIFIED | Cross-refs load_assembly_directory |
| `Transport/Mcp/Tools/SearchConstantsTool.cs` | Scenario description | VERIFIED | "Use this to find magic numbers, status codes, buffer sizes" |
| `Transport/Mcp/Tools/SearchMembersByNameTool.cs` | Scenario description | VERIFIED | "Use this when you know the operation you need but not which type implements it" |
| `Transport/Mcp/Tools/SearchStringsTool.cs` | Scenario description | VERIFIED | "Use this to find hardcoded URLs, connection strings, error messages, API keys" |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| decompile_type | get_type_members | Description text | WIRED | "use get_type_members instead" in decompile_type description |
| get_type_members | decompile_type | Description text | WIRED | "use decompile_type instead" in get_type_members description |
| list_assembly_types | list_namespace_types | Description text | NOT_WIRED | References "decompile_namespace" instead of "list_namespace_types" |
| decompile_namespace | list_assembly_types | Description text | WIRED | "use list_assembly_types instead" present (but tool has wrong name) |
| list_embedded_resources | extract_resource | Description text | WIRED | "Use extract_resource to retrieve individual resource contents" |
| load_assembly_directory | resolve_type | Description text | WIRED | "then use resolve_type to find which assembly defines a specific type" |
| resolve_type | load_assembly_directory | Description text | WIRED | "Use load_assembly_directory first to scan the directory" |
| decompile_method | disassemble_method | Description text | WIRED | "use disassemble_method" cross-reference present |
| disassemble_type | decompile_type | Description text | WIRED | "use decompile_type" cross-reference present |

### Data-Flow Trace (Level 4)

Not applicable. This phase modifies only `[Description]` attribute strings on MCP tool methods. No dynamic data rendering involved.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds with 0 errors | `dotnet build ILSpy.Mcp.sln --no-restore -v q` | "Build succeeded. 0 Warning(s) 0 Error(s)" | PASS |
| "Use this when" or "Use this to" in all updated tools | grep across 27 tool files | 27 matches (excluding AnalyzeReferencesTool) | PASS |
| No NuGet-consumer language in any description | grep for "NuGet", "just installed", "library type" | 0 matches | PASS |
| find_usages has pagination parameters | grep for maxResults/offset in FindUsagesTool.cs | 0 matches (description says paginated but params absent) | FAIL |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| DESC-01 | Both plans (13-01, 13-02) | Every mechanical description rewritten to scenario-oriented "Use this when..." format | PARTIAL | 27 of 27 Phase 13 target tools updated. find_usages description is inaccurate (claims pagination that doesn't exist). AnalyzeReferencesTool excluded (Phase 9 CLEAN-01 scope). |
| DESC-02 | Both plans (13-01, 13-02) | Overlapping tools cross-reference each other with cost/use-case guidance | PARTIAL | decompile_type <-> get_type_members: DONE with cost guidance. list_assembly_types <-> list_namespace_types: BLOCKED (list_namespace_types doesn't exist, tool named decompile_namespace). |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Transport/Mcp/Tools/FindUsagesTool.cs` | 28 | Description claims "Returns paginated matches" but method has no maxResults/offset parameters | Blocker | Agent will read description expecting pagination, discover it's impossible, and either fail or use a workaround |
| `Transport/Mcp/Tools/ListAssemblyTypesTool.cs` | 25 | Cross-reference names "decompile_namespace" — a tool name that will be wrong after Phase 9 completes | Warning | Description will become stale when Phase 9 CLEAN-02 is eventually applied |
| `Transport/Mcp/Tools/AnalyzeReferencesTool.cs` | 37 | Tool not updated to scenario-oriented format (not in Phase 13 scope, but still registered as active tool) | Warning | Agent sees this tool with pre-Phase-13 mechanical description style — inconsistent with all other tools |

### Human Verification Required

None identified. All success criteria are verifiable programmatically via file content inspection.

### Gaps Summary

Two gaps block full goal achievement:

**Gap 1 — Pre-condition failure (Phase 9 CLEAN-02 not applied):** Success criterion 3 requires `list_assembly_types` and `list_namespace_types` to cross-reference each other. The tool `list_namespace_types` does not exist in the current codebase — it was never renamed from `decompile_namespace` because Phase 9 CLEAN-02 was not completed before Phase 13 ran. Phase 13 updated `DecompileNamespaceTool.cs` (the old name) and `list_assembly_types` cross-references `decompile_namespace` (the old name). This is a pre-condition gap: Phase 13 depended on Phase 9 being complete. The fix requires completing Phase 9 CLEAN-02 (rename the tool) and then updating the cross-reference in `ListAssemblyTypesTool.cs`.

**Gap 2 — Description/signature mismatch in find_usages (merge regression):** Phase 13 commit `fb6a7d4` rewrote the `find_usages` description to say "Returns paginated matches with declaring type, method signature, and IL offset." This description is accurate for the version that existed after Phase 10's pagination restore (`fix e955eea`). However, `fb6a7d4` operated against a stale base index (`19e610e`, the pre-pagination version) and the resulting file has no `maxResults` or `offset` parameters in `ExecuteAsync`. The Phase 10 pagination parameters were effectively dropped. An agent reading the description will expect pagination parameters that don't exist. The fix is either to restore the pagination parameters or correct the description.

These two gaps have different root causes but both mean the phase goal is not fully achieved. The 26 other tools (excluding find_usages and AnalyzeReferencesTool) have accurate, scenario-oriented descriptions with correct cross-references.

---

_Verified: 2026-04-10T14:11:13Z_
_Verifier: Claude (gsd-verifier)_
