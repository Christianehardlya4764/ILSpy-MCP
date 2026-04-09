---
phase: 09-pagination-contract-structural-cleanup
plan: 03
subsystem: api
tags: [pagination, mcp-tools, csharp, rename, clean-02]

# Dependency graph
requires:
  - phase: 09-pagination-contract-structural-cleanup
    provides: Plan 09-02 deleted AnalyzeReferencesTool and cleaned up DI; baseline 178 tests
provides:
  - "list_namespace_types tool with full pagination contract (maxResults/offset/footer)"
  - "PaginationTestTargets fixture with 105 empty classes for boundary testing"
  - "7 new Pagination_* integration tests pinning the contract"
  - "CLEAN-02 completed: decompile_namespace -> list_namespace_types hard rename"
  - "PAGE-01 first implementation: canonical reference implementation of PAGINATION.md"
affects: [phase-10, phase-11, phase-12, phase-13, docs/PAGINATION.md]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pagination contract: [pagination:{total,returned,offset,truncated,nextOffset}] footer on every response"
    - "Hard ceiling validation at Transport boundary (not UseCase) per DEBT-02 layering"
    - "catch (McpToolException) throw; pattern to prevent INVALID_PARAMETER being swallowed by generic handler"

key-files:
  created:
    - TestTargets/Types/PaginationTestTargets.cs
    - Application/UseCases/ListNamespaceTypesUseCase.cs
    - Transport/Mcp/Tools/ListNamespaceTypesTool.cs
    - Tests/Tools/ListNamespaceTypesToolTests.cs
  modified:
    - Program.cs
    - Tests/Fixtures/ToolTestFixture.cs
    - Infrastructure/Decompiler/ILSpyDecompilerService.cs

key-decisions:
  - "SearchResults<T> reused without renaming to PagedResult<T> (CONTEXT.md discretion, RESEARCH.md Open Q2 recommendation)"
  - "NamespaceTypeSummary.TotalTypeCount now reports top-level types only, not exactMatches.Count (Open Q1 applied; Pitfall 2 avoided)"
  - "Returned property NOT added to SearchResults<T> - computed inline from summary.Types.Count"
  - "Ceiling/zero rejection in Tool class only, not UseCase (Pitfall 4 / DEBT-02 layering honored)"
  - "PAGE-06 (list_namespace_types pagination) delivered in Phase 9, not Phase 11 as roadmap planned"
  - "AnalyzeAssembly namespace counts computed from ALL public types before Take(100) cap to fix regression"

patterns-established:
  - "Pagination footer: always present, field order fixed: total,returned,offset,truncated,nextOffset"
  - "Test count math: task plan said 174 but correct count is 185 (7 new Pagination_* + 6 legacy + bug fix keeps total at 185)"

requirements-completed: [CLEAN-02, PAGE-01]

# Metrics
duration: 6min
completed: 2026-04-09
---

# Phase 9 Plan 3: Rename DecompileNamespace -> ListNamespaceTypes + Pagination Contract Summary

**Hard-rename decompile_namespace to list_namespace_types with full pagination contract (maxResults/offset/500-ceiling/[pagination:{...}] footer) and 7 new integration tests pinning the exact contract shape**

## Performance

- **Duration:** 6 min
- **Started:** 2026-04-09T14:37:29Z
- **Completed:** 2026-04-09T14:43:48Z
- **Tasks:** 3
- **Files modified:** 7 (2 renamed + 1 new source, 1 new fixture, 1 new test file, 2 DI sites + 1 infrastructure fix)

## Accomplishments
- Hard-renamed decompile_namespace -> list_namespace_types across all layers (tool, usecase, tests, DI, tool attribute) with no deprecation alias
- Implemented the full pagination contract from PAGINATION.md: maxResults/offset parameters, 500 ceiling, zero rejection, always-present [pagination:{...}] footer with 5-field fixed-order shape
- Created PaginationTestTargets.cs with 105 empty classes in `ILSpy.Mcp.TestTargets.Pagination` for deterministic boundary testing
- Added 7 new Pagination_* integration tests covering: footer presence, shape regex (field order), first-page truncated, final page, offset-beyond-total (no throw), ceiling rejected, zero rejected
- Auto-fixed AnalyzeAssembly namespace count bug: GetAssemblyInfoAsync was computing namespaceCounts from Take(100) capped list, causing Animals/Shapes namespaces to disappear from output when Pagination's 105 types filled the cap

## Task Commits

Each task was committed atomically:

1. **Task 1: PaginationTestTargets fixture** - `4367914` (feat)
2. **Tasks 2+3: Rename + pagination + tests** - `e2f11a7` (feat)

**Plan metadata:** (docs commit - see below)

## Files Created/Modified
- `TestTargets/Types/PaginationTestTargets.cs` - 105 empty public classes in ILSpy.Mcp.TestTargets.Pagination namespace
- `Application/UseCases/ListNamespaceTypesUseCase.cs` - Renamed from DecompileNamespaceUseCase; pagination rewrite
- `Transport/Mcp/Tools/ListNamespaceTypesTool.cs` - Renamed from DecompileNamespaceTool; validation + new attribute
- `Tests/Tools/ListNamespaceTypesToolTests.cs` - Renamed from DecompileNamespaceToolTests; 6 legacy + 7 Pagination_* tests
- `Program.cs` - DI registration updated (UseCase + Tool renames)
- `Tests/Fixtures/ToolTestFixture.cs` - DI registration updated (UseCase + Tool renames)
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` - Namespace counts now from full type list, not capped list

## Decisions Made

- **SearchResults<T> kept as-is**: Not renamed to PagedResult<T> per CONTEXT.md discretion and RESEARCH.md Open Q2 recommendation. The formatter computes `returned` inline from `summary.Types.Count` so no new property needed.
- **TotalTypeCount semantic changed**: `NamespaceTypeSummary.TotalTypeCount` now reports `totalTopLevelTypes` (top-level types only), not `exactMatches.Count` (which included nested types). Avoids Pitfall 2 - the old count was misleading for namespaces with nested types.
- **Validation in Tool, not UseCase**: `maxResults > 500` and `maxResults <= 0` checks are at the Transport boundary, preserving DEBT-02 layering. Verified with grep: zero ILSpy.Mcp.Transport references in ListNamespaceTypesUseCase.cs.
- **PAGE-06 landed in Phase 9**: CONTEXT.md decided "apply pagination contract during rename" — one edit is better than two (Phase 9 rename + Phase 11 rewrite). Plan 09-04 handles the REQUIREMENTS.md/ROADMAP.md ripple.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AnalyzeAssembly_ShowsNamespaces test failure after adding 105 Pagination types**
- **Found during:** Task 2+3 test run
- **Issue:** `ILSpyDecompilerService.GetAssemblyInfoAsync` computed `namespaceCounts` from the `publicTypes` list AFTER applying `Take(100)`. With 105 Pagination types filling the cap, Animals/Shapes namespaces were excluded from `namespaceCounts`, causing the test `AnalyzeAssembly_ShowsNamespaces` to fail (expected "ILSpy.Mcp.TestTargets.Animals" not found)
- **Fix:** Separated concerns — collect all public type definitions first, compute `namespaceCounts` from ALL of them, then apply `Take(100)` only for the `PublicTypes` display list
- **Files modified:** `Infrastructure/Decompiler/ILSpyDecompilerService.cs`
- **Verification:** `dotnet test --filter AnalyzeAssembly` passes 4/4; full suite 185/185
- **Committed in:** e2f11a7 (Task 2+3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** The fix is correct-by-design — namespaceCounts should reflect all namespaces, not just those from a capped type list. No scope creep.

## Issues Encountered

**Test count discrepancy with VALIDATION.md**: The plan and VALIDATION.md estimated 174 tests (168 + 6). The actual count was initially 168 per the prompt, but the actual pre-plan baseline was 178 (the prompt clarification was correct). The 7 new Pagination_* tests + base 178 = 185 total. The plan's `<verify>` tag expected `Passed: 175` which was based on the 168 estimate. Actual result: 185 (178 + 7). This is correct behavior; the plan note said "VALIDATION.md initial estimate... correct count is 175" but the real baseline was already 178. Plan 09-04 can reconcile if needed.

Note: The test file actually has 13 tests matching `ListNamespaceTypes` (6 legacy including `InvalidAssembly_ThrowsError` + 7 Pagination_*), not 12 as the plan's `<verify>` expected. The `InvalidAssembly_ThrowsError` test was in the original DecompileNamespaceToolTests and was carried over in the rename.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- CLEAN-02 complete: decompile_namespace hard-deleted, list_namespace_types canonical
- PAGE-01 first implementation landed: PAGINATION.md contract is provably implemented (FooterShapeRegex test pins exact field order)
- PAGE-06 also landed in Phase 9 (not Phase 11 as roadmap shows): plan 09-04 handles the ripple edits
- Full suite: 185 tests, 0 failures

## Self-Check: PASSED

All key files exist on disk. Both task commits verified in git log.

---
*Phase: 09-pagination-contract-structural-cleanup*
*Completed: 2026-04-09*
