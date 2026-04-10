---
phase: 10-find-tool-pagination-match-enrichment
verified: 2026-04-10T08:00:00Z
status: gaps_found
score: 15/16 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 10/16
  gaps_closed:
    - "`find_usages` accepts `int maxResults = 100` and `int offset = 0` parameters with ceiling rejection at Transport boundary"
    - "Calling `find_usages` against ≥105-call-site target returns paginated footer with truncated=true on page 1"
    - "`find_usages` match lines show declaring type FQN AND FQN method signature AND IL offset (OUTPUT-01)"
    - "`find_usages` uses stable ordinal sort (DeclaringType asc, ILOffset asc)"
    - "`TestTargets/Types/PaginationTestTargetsUsages.cs` declares ≥105 call sites of IUsagesTarget.Ping"
    - "`FindUsagesToolTests` contains 7 Pagination_* facts and at least one OUTPUT-01 enrichment fact"
  gaps_remaining:
    - "`ListNamespaceTypesUseCase` uses PaginationEnvelope.AppendFooter (merge regression — file deleted by 10-05 worktree merge)"
  regressions: []
gaps:
  - truth: "`ListNamespaceTypesUseCase` uses `PaginationEnvelope.AppendFooter` instead of the inline JsonSerializer block (retrofit proves shape compatibility) AND all existing `ListNamespaceTypesToolTests.Pagination_*` tests still pass byte-identically"
    status: failed
    reason: "Merge regression: `ListNamespaceTypesUseCase.cs` was created by commit 004c6b5 (10-01 work) and commit e2f11a7 (Phase 9 CLEAN-02), but was deleted by commit 70388b4 when the 10-05 executor worktree was merged. The 10-05 worktree had not incorporated the Phase 9 rename work, and the merge dropped the file. The current HEAD only has `DecompileNamespaceUseCase.cs` (no PaginationEnvelope call, old maxTypes=200 pattern). The tool is still registered as `decompile_namespace` (not `list_namespace_types`). `AnalyzeReferencesTool` is still registered as `analyze_references` (Phase 9 CLEAN-01 also reverted by the same merge)."
    artifacts:
      - path: "Application/UseCases/ListNamespaceTypesUseCase.cs"
        issue: "MISSING — deleted by 10-05 worktree merge (commit 70388b4). File existed in commits 004c6b5 and e2f11a7 but was not present in the 10-05 worktree branch."
      - path: "Application/UseCases/DecompileNamespaceUseCase.cs"
        issue: "Still present at HEAD with no PaginationEnvelope.AppendFooter call and maxTypes=200 hard-cap pattern"
      - path: "Transport/Mcp/Tools/DecompileNamespaceTool.cs"
        issue: "Tool still named 'decompile_namespace' — Phase 9 CLEAN-02 rename reverted"
    missing:
      - "Restore `Application/UseCases/ListNamespaceTypesUseCase.cs` from git history (commit 004c6b5 contains the correct version with PaginationEnvelope.AppendFooter)"
      - "Restore `Transport/Mcp/Tools/ListNamespaceTypesTool.cs` and remove `DecompileNamespaceTool.cs` (or re-execute Phase 9 CLEAN-02)"
      - "Verify all ListNamespaceTypesToolTests.Pagination_* tests pass after restore"
---

# Phase 10: Find-Tool Pagination & Match Enrichment Verification Report

**Phase Goal:** Every `find_*` tool returns paginable, self-describing match records so the agent understands where each match lives without follow-up calls
**Verified:** 2026-04-10T08:00:00Z
**Status:** gaps_found
**Re-verification:** Yes — after gap closure

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `PaginationEnvelope.AppendFooter(StringBuilder, int total, int returned, int offset)` exists as a reusable helper emitting the 5-field minified JSON footer | ✓ VERIFIED | `Application/Pagination/PaginationEnvelope.cs` line 17 — method signature confirmed, field order locked |
| 2 | `ListNamespaceTypesUseCase` uses `PaginationEnvelope.AppendFooter` (retrofit proves shape compatibility) AND all `ListNamespaceTypesToolTests.Pagination_*` tests pass | ✗ FAILED | Merge regression: file deleted by 10-05 worktree merge (commit 70388b4). Tool still named `decompile_namespace`. `ListNamespaceTypesUseCase.cs` not present in HEAD. |
| 3 | `ILSpyCrossReferenceService.FormatMethodSignature` returns FQN form (`System.Void ProcessRequest(...)`) | ✓ VERIFIED | `ILSpyCrossReferenceService.cs` line 628-629: uses `p.Type.FullName` and `method.ReturnType.FullName` |
| 4 | `find_usages` accepts `maxResults = 100` and `offset = 0` parameters with ceiling rejection at Transport boundary | ✓ VERIFIED | `FindUsagesTool.cs` lines 33-49: parameters declared, `maxResults > 500` and `maxResults <= 0` checks with `McpToolException("INVALID_PARAMETER", ...)` |
| 5 | `find_usages` against ≥105-call-site target returns paginated footer with truncated=true on page 1 | ✓ VERIFIED | `FindUsagesUseCase.cs` lines 54-60: stable sort + Skip/Take + PaginationEnvelope.AppendFooter at line 110; fixture has 105 call sites |
| 6 | `find_usages` match lines show declaring type FQN AND FQN method signature AND IL offset (OUTPUT-01) | ✓ VERIFIED | `FindUsagesUseCase.cs` line 106: `$"  [{result.Kind}] {result.DeclaringType}.{result.MethodName} (IL_{result.ILOffset:X4}){signature}"` where `signature = result.MethodSignature != null ? $" {result.MethodSignature}" : ""` |
| 7 | `find_usages` uses stable ordinal sort `(DeclaringType asc, ILOffset asc)` for deterministic pagination | ✓ VERIFIED | `FindUsagesUseCase.cs` lines 55-57: `.OrderBy(r => r.DeclaringType, StringComparer.Ordinal).ThenBy(r => r.ILOffset)` |
| 8 | `FindUsagesToolTests` has 7 Pagination_* facts and at least 1 OUTPUT-01 enrichment fact | ✓ VERIFIED | `Tests/Tools/FindUsagesToolTests.cs` lines 103-243: `Pagination_FooterPresent`, `Pagination_FooterShapeRegex`, `Pagination_FirstPageTruncated`, `Pagination_FinalPage`, `Pagination_OffsetBeyondTotal`, `Pagination_CeilingRejected`, `Pagination_ZeroMaxResultsRejected` + `FindUsages_Enrichment_ShowsFqnMethodSignature` asserting `System.Void` |
| 9 | `find_implementors` paginated with flat sorted per-line `[direct]`/`[transitive]` layout (OUTPUT-03), ceiling rejection, tests | ✓ VERIFIED | `FindImplementorsUseCase.cs`: `OrderByDescending(IsDirect)`, `ThenBy(TypeFullName, Ordinal)`, `Skip/Take`, `PaginationEnvelope.AppendFooter` line 117; `FindImplementorsTool.cs` lines 39-50: ceiling rejection; 14 tests in `FindImplementorsToolTests` including 3 enrichment facts |
| 10 | `find_dependencies` paginated with flat sorted per-line `[Kind] Member [DefiningAssembly]` layout (OUTPUT-02), ceiling rejection, DependencyResult.DefiningAssembly, ResolveDefiningAssembly helper, tests | ✓ VERIFIED | `FindDependenciesUseCase.cs`: Sort by Kind, `Skip/Take`, `PaginationEnvelope.AppendFooter` line 116; `CrossReferenceResult.cs`: `DefiningAssembly` + `ResolutionNote`; `ILSpyCrossReferenceService.cs` line 640: `ResolveDefiningAssembly`; 10 tests |
| 11 | `find_instantiations` paginated with FQN MethodSignature on each match line (OUTPUT-04), ceiling rejection, tests | ✓ VERIFIED | `FindInstantiationsUseCase.cs` line 117: `PaginationEnvelope.AppendFooter`; match line appends `result.MethodSignature`; 12 tests |
| 12 | `find_extension_methods` paginated with ceiling rejection, tests | ✓ VERIFIED | `FindExtensionMethodsUseCase.cs`: `maxResults`/`offset`, `Skip/Take`, `PaginationEnvelope.AppendFooter` line 92; `FindExtensionMethodsTool.cs`: ceiling rejection; 10 tests |
| 13 | `find_compiler_generated_types` paginated with ceiling rejection, tests | ✓ VERIFIED | `FindCompilerGeneratedTypesUseCase.cs`: `maxResults`/`offset`, `Skip/Take`, `PaginationEnvelope.AppendFooter` line 113; `FindCompilerGeneratedTypesTool.cs`: ceiling rejection; 10 tests |
| 14 | All 6 pagination fixture files exist with ≥105 triggering elements | ✓ VERIFIED | `PaginationTestTargetsUsages.cs` (105 call sites, 3 classes × 35 methods), `PaginationTestTargetsImplementors.cs` (111 classes), `PaginationTestTargetsInstantiations.cs` (106 `new` sites), `PaginationTestTargetsDependencies.cs`, `PaginationTestTargetsExtensionMethods.cs` (105 extension methods), `PaginationTestTargetsCompilerGenerated.cs` (105 async methods) |
| 15 | `Domain/Services/ICrossReferenceService.FindUsagesAsync` signature unchanged (D-06) | ✓ VERIFIED | Service interface not modified; slicing happens in `FindUsagesUseCase` as required |
| 16 | `dotnet build` passes with zero errors | ✓ VERIFIED | Build succeeded with 0 errors, 2 warnings (unrelated CS0649, CS0169) |

**Score:** 15/16 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Application/Pagination/PaginationEnvelope.cs` | Static helper with AppendFooter | ✓ VERIFIED | `AppendFooter(StringBuilder, int, int, int)` at line 17; correct field order locked |
| `Application/UseCases/ListNamespaceTypesUseCase.cs` | Retrofitted to use PaginationEnvelope.AppendFooter | ✗ MISSING | Deleted by 10-05 worktree merge (commit 70388b4). `DecompileNamespaceUseCase.cs` present instead with no PaginationEnvelope usage |
| `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` | FormatMethodSignature uses FullName | ✓ VERIFIED | Lines 628-629: `p.Type.FullName` and `method.ReturnType.FullName` |
| `Application/UseCases/FindUsagesUseCase.cs` | Paginated with sort, slice, footer, FQN signature display | ✓ VERIFIED | maxResults/offset params, stable sort, Skip/Take, PaginationEnvelope.AppendFooter, MethodSignature appended |
| `Transport/Mcp/Tools/FindUsagesTool.cs` | Pagination parameters + ceiling rejection | ✓ VERIFIED | Lines 33-49: maxResults=100, offset=0, >500 and <=0 rejection |
| `TestTargets/Types/PaginationTestTargetsUsages.cs` | ≥105 call sites of IUsagesTarget.Ping | ✓ VERIFIED | 105 call sites (3 classes × 35 methods) in `ILSpy.Mcp.TestTargets.Pagination.Usages` |
| `Tests/Tools/FindUsagesToolTests.cs` | 7 Pagination_* + 1 OUTPUT-01 enrichment facts | ✓ VERIFIED | 7 Pagination_* facts + `FindUsages_Enrichment_ShowsFqnMethodSignature` asserting `System.Void` |
| `Application/UseCases/FindImplementorsUseCase.cs` | Paginated, flat layout, direct/transitive markers | ✓ VERIFIED | maxResults/offset, `OrderByDescending(IsDirect)`, PaginationEnvelope.AppendFooter |
| `Transport/Mcp/Tools/FindImplementorsTool.cs` | Pagination parameters + ceiling rejection | ✓ VERIFIED | Lines 39-50 |
| `TestTargets/Types/PaginationTestTargetsImplementors.cs` | ≥105 implementors (mix direct/transitive) | ✓ VERIFIED | 111 classes (70 direct + 1 anchor + 40 transitive) |
| `Tests/Tools/FindImplementorsToolTests.cs` | 7 Pagination_* + 3 OUTPUT-03 enrichment facts | ✓ VERIFIED | All 7 Pagination_* facts + 3 enrichment facts |
| `Domain/Models/CrossReferenceResult.cs` | DependencyResult with DefiningAssembly + ResolutionNote | ✓ VERIFIED | `DefiningAssembly` (required string), `ResolutionNote` (string?) |
| `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` | ResolveDefiningAssembly helper | ✓ VERIFIED | Line 640 |
| `Application/UseCases/FindDependenciesUseCase.cs` | Paginated, flat layout, [Kind] Member [Assembly] | ✓ VERIFIED | Sort, Skip/Take, PaginationEnvelope.AppendFooter, DefiningAssembly in format |
| `Transport/Mcp/Tools/FindDependenciesTool.cs` | Pagination parameters + ceiling rejection | ✓ VERIFIED | Lines 33-48 |
| `Application/UseCases/FindInstantiationsUseCase.cs` | Paginated, FQN MethodSignature displayed | ✓ VERIFIED | result.MethodSignature appended; PaginationEnvelope.AppendFooter line 117 |
| `Transport/Mcp/Tools/FindInstantiationsTool.cs` | Pagination parameters + ceiling rejection | ✓ VERIFIED | Lines 32-47 |
| `TestTargets/Types/PaginationTestTargetsInstantiations.cs` | ≥105 `new InstantiationsTarget()` sites | ✓ VERIFIED | 106 occurrences |
| `Tests/Tools/FindInstantiationsToolTests.cs` | 7 Pagination_* + 1 OUTPUT-04 enrichment fact | ✓ VERIFIED | Lines 82-213; enrichment test at line 202 |
| `Application/UseCases/FindExtensionMethodsUseCase.cs` | Paginated with PaginationEnvelope | ✓ VERIFIED | maxResults/offset, OrderBy(Name), Skip/Take, PaginationEnvelope.AppendFooter line 92 |
| `Transport/Mcp/Tools/FindExtensionMethodsTool.cs` | Pagination parameters + ceiling rejection | ✓ VERIFIED | Lines 29-44 |
| `TestTargets/Types/PaginationTestTargetsExtensionMethods.cs` | ≥105 extension methods for ExtensionTarget | ✓ VERIFIED | 105 extension methods (Ext001..Ext105) |
| `Tests/Tools/FindExtensionMethodsToolTests.cs` | 7 Pagination_* facts | ✓ VERIFIED | Lines 66-181 |
| `Application/UseCases/FindCompilerGeneratedTypesUseCase.cs` | Paginated with PaginationEnvelope | ✓ VERIFIED | maxResults/offset, OrderBy(ParentType??FullName), Skip/Take, PaginationEnvelope.AppendFooter line 113 |
| `Transport/Mcp/Tools/FindCompilerGeneratedTypesTool.cs` | Pagination parameters + ceiling rejection | ✓ VERIFIED | Lines 31-46 |
| `TestTargets/Types/PaginationTestTargetsCompilerGenerated.cs` | ≥105 async methods | ✓ VERIFIED | 105 async methods (Async001..Async105) |
| `Tests/Tools/FindCompilerGeneratedTypesToolTests.cs` | 7 Pagination_* facts | ✓ VERIFIED | Lines 88-203 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `FindUsagesUseCase.cs` | `PaginationEnvelope.cs` | `PaginationEnvelope.AppendFooter` call | ✓ WIRED | Line 110 |
| `FindUsagesTool.cs` | `FindUsagesUseCase.cs` | passes maxResults/offset | ✓ WIRED | Line 51: `_useCase.ExecuteAsync(assemblyPath, typeName, memberName, maxResults, offset, cancellationToken)` |
| `FindUsagesToolTests.cs` | `PaginationTestTargetsUsages.cs` | `IUsagesTarget.Ping` via test fixture | ✓ WIRED | Tests reference `ILSpy.Mcp.TestTargets.Pagination.Usages.IUsagesTarget` namespace at lines 110, 126, 142, 161, 181, 201, 218, 236 |
| `Application/UseCases/ListNamespaceTypesUseCase.cs` | `PaginationEnvelope.cs` | PaginationEnvelope.AppendFooter | ✗ NOT_WIRED | File deleted by 10-05 worktree merge; `DecompileNamespaceUseCase.cs` present instead with no PaginationEnvelope call |
| `FindImplementorsUseCase.cs` | `PaginationEnvelope.cs` | PaginationEnvelope.AppendFooter | ✓ WIRED | Line 117 |
| `FindDependenciesUseCase.cs` | `PaginationEnvelope.cs` | PaginationEnvelope.AppendFooter | ✓ WIRED | Line 116 |
| `FindInstantiationsUseCase.cs` | `PaginationEnvelope.cs` | PaginationEnvelope.AppendFooter | ✓ WIRED | Line 117 |
| `FindExtensionMethodsUseCase.cs` | `PaginationEnvelope.cs` | PaginationEnvelope.AppendFooter | ✓ WIRED | Line 92 |
| `FindCompilerGeneratedTypesUseCase.cs` | `PaginationEnvelope.cs` | PaginationEnvelope.AppendFooter | ✓ WIRED | Line 113 |
| `ILSpyCrossReferenceService.cs` | `CrossReferenceResult.cs` | constructs DependencyResult with DefiningAssembly | ✓ WIRED | Lines 499-500 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `FindUsagesUseCase.cs` | `results` (UsageResult list) | `ICrossReferenceService.FindUsagesAsync` | Yes — real IL scan | ✓ FLOWING |
| `FindUsagesUseCase.cs` | `MethodSignature` field | `FormatMethodSignature` (FQN upgrade in ILSpyCrossReferenceService) | Yes — populated with FQN return type and parameter types | ✓ FLOWING (previously HOLLOW, now fixed by displaying `result.MethodSignature` at line 105-106) |
| `FindImplementorsUseCase.cs` | sorted, paged results | IsDirect/TypeFullName from CrossRef | Yes — real type analysis | ✓ FLOWING |
| `FindDependenciesUseCase.cs` | `DefiningAssembly` | `ResolveDefiningAssembly` helper | Yes — real assembly walk | ✓ FLOWING |

### Behavioral Spot-Checks

Step 7b: SKIPPED — behavioral verification requires running the MCP server against real assemblies. Build compilation (0 errors) confirms structural correctness.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PAGE-02 | 10-01 through 10-05 | All `find_*` tools implement pagination contract | ✓ SATISFIED | All 6 `find_*` tools (find_usages, find_implementors, find_dependencies, find_instantiations, find_extension_methods, find_compiler_generated_types) have maxResults/offset parameters, ceiling rejection, stable sort, and PaginationEnvelope.AppendFooter |
| OUTPUT-01 | 10-01 | `find_usages` matches include declaring type FQN, containing method signature (FQN), IL offset | ✓ SATISFIED | `FindUsagesUseCase.cs` line 105-106: appends FQN MethodSignature; `FindUsages_Enrichment_ShowsFqnMethodSignature` test asserts `System.Void` appears in output |
| OUTPUT-02 | 10-02 | `find_dependencies` matches grouped by kind with FQN names and defining assembly | ✓ SATISFIED | Flat layout `[Kind] Member [DefiningAssembly]`, `DefiningAssembly` domain field, `ResolveDefiningAssembly` helper, 3 enrichment tests |
| OUTPUT-03 | 10-03 | `find_implementors` matches include full type name, direct-vs-transitive relationship marker | ✓ SATISFIED | `[direct]`/`[transitive]` per-line markers, stable sort, 3 enrichment tests |
| OUTPUT-04 | 10-04 | `find_instantiations` matches include containing type FQN, method signature, IL offset | ✓ SATISFIED | `result.MethodSignature` appended (FQN via FormatMethodSignature upgrade), IL offset, enrichment test |

**Note:** The remaining gap (Truth 2, `ListNamespaceTypesUseCase`) is a 10-01 plan must-have that proves shape compatibility between the PaginationEnvelope helper and the Phase 9 reference implementation. It does NOT block PAGE-02 or any OUTPUT-* requirement. All 5 phase requirements are satisfied.

**Roadmap Success Criteria:**

| SC | Truth | Status |
|----|-------|--------|
| SC1: Agent calling `find_*` tools can pass (maxResults, offset) and receive (truncated, total) | All 6 `find_*` tools support it | ✓ SATISFIED |
| SC2: `find_usages` match tells agent declaring type, method signature, IL offset | MethodSignature displayed in output (line 105-106) | ✓ SATISFIED |
| SC3: `find_dependencies` result shows kind grouping, FQN names, defining assembly | ✓ Delivered | ✓ SATISFIED |
| SC4: `find_implementors` match states direct vs transitive | ✓ Delivered | ✓ SATISFIED |
| SC5: `find_instantiations` match tells agent containing type FQN, method signature, IL offset | ✓ Delivered (FQN signature visible) | ✓ SATISFIED |

All 5 roadmap success criteria are satisfied. The remaining gap is a 10-01 plan must-have (ListNamespaceTypesUseCase retrofit proof) — a shape-compatibility test artifact, not a roadmap deliverable.

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| `Application/UseCases/DecompileNamespaceUseCase.cs` | No PaginationEnvelope.AppendFooter; old maxTypes=200 hard-cap pattern; tool still registered as `decompile_namespace` | ⚠️ Warning | Phase 9 CLEAN-02 deliverable lost to merge regression. Breaks Truth 2 of 10-01 plan. Does NOT block any of the 5 phase requirements (PAGE-02, OUTPUT-01..04). |

### Human Verification Required

None — all failures are structural/code-level and verified programmatically.

### Gaps Summary

**Root cause: Merge regression in commit 70388b4.** The 10-05 executor worktree was created before Phase 9 (CLEAN-02 rename) and before Plan 10-01's Task 1 (ListNamespaceTypesUseCase retrofit). When the 10-05 worktree was merged via `ddea6a2` ("merge executor worktree"), it introduced `DecompileNamespaceUseCase.cs` as the baseline and the merge deleted `ListNamespaceTypesUseCase.cs`. The subsequent 10-01 work (commit `e955eea`) restored the `find_usages` changes but did not restore `ListNamespaceTypesUseCase.cs`.

**Scope of remaining gap:** Truth 2 (ListNamespaceTypesUseCase retrofit) is a 10-01 plan must-have that was intended to prove PaginationEnvelope shape compatibility against the Phase 9 canonical reference. It is NOT one of the 5 phase requirements (PAGE-02, OUTPUT-01..04). All 5 roadmap success criteria for Phase 10 are now met. The gap is a plan-internal completeness issue.

**To close the remaining gap:**

Option A (preferred): Restore `ListNamespaceTypesUseCase.cs` from git history:
```
git show 004c6b5:Application/UseCases/ListNamespaceTypesUseCase.cs > Application/UseCases/ListNamespaceTypesUseCase.cs
```
Then also restore `ListNamespaceTypesTool.cs`, remove `DecompileNamespaceTool.cs`, remove `AnalyzeReferencesTool.cs`, and re-verify `ListNamespaceTypesToolTests.Pagination_*` tests pass.

Option B (override): Accept this deviation since all 5 roadmap success criteria (PAGE-02, OUTPUT-01..04) are satisfied. The ListNamespaceTypesUseCase retrofit was a proof-of-compatibility artifact, not a user-visible feature. Add an override to the VERIFICATION.md frontmatter.

---

_Verified: 2026-04-10T08:00:00Z_
_Verifier: Claude (gsd-verifier)_
