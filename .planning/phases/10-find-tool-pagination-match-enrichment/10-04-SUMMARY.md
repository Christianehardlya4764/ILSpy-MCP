---
phase: 10-find-tool-pagination-match-enrichment
plan: 04
subsystem: find-instantiations
tags: [pagination, enrichment, find-tool, output-format]
dependency_graph:
  requires: [10-01]
  provides: [PAGE-02-instantiations, OUTPUT-04]
  affects: [find_instantiations, analyze_references]
tech_stack:
  added: []
  patterns: [PaginationEnvelope, stable-sort, ceiling-rejection]
key_files:
  created:
    - TestTargets/Types/PaginationTestTargetsInstantiations.cs
  modified:
    - Application/UseCases/FindInstantiationsUseCase.cs
    - Transport/Mcp/Tools/FindInstantiationsTool.cs
    - Transport/Mcp/Tools/AnalyzeReferencesTool.cs
    - Tests/Tools/FindInstantiationsToolTests.cs
requirements_completed: [PAGE-02, OUTPUT-04]
decisions:
  - "Named cancellationToken parameter required on all callers after signature change (AnalyzeReferencesTool, pre-existing tests)"
metrics:
  duration: 4m
  completed: 2026-04-10
  tasks: 1
  files: 5
---

# Phase 10 Plan 04: find_instantiations Pagination and Match Enrichment Summary

find_instantiations now returns paginated, stably-sorted results with FQN method signatures visible on every match line, using the PaginationEnvelope contract from Plan 10-01.

## Before/After Match Line Format

**Before:**
```
  ILSpy.Mcp.TestTargets.CrossRef.DataService.DoWork (IL_0023)
```

**After:**
```
  ILSpy.Mcp.TestTargets.CrossRef.DataService.DoWork (IL_0023) System.Void DoWork(System.String)
```

The `MethodSignature` field was already populated at `ILSpyCrossReferenceService.cs:551` and upgraded to FQN form by Plan 10-01's `FormatMethodSignature` change. This plan made it visible in the output (it was previously dropped by `FormatResults`).

## Changes Made

### Application/UseCases/FindInstantiationsUseCase.cs
- Added `maxResults` and `offset` parameters to `ExecuteAsync`
- Stable sort by `(DeclaringType asc, ILOffset asc)` with `StringComparer.Ordinal`
- Pagination via `.Skip(offset).Take(maxResults)`
- Three-branch header (zero results, offset beyond total, showing N-M)
- `PaginationEnvelope.AppendFooter` on every response
- `result.MethodSignature` now displayed on each match line

### Transport/Mcp/Tools/FindInstantiationsTool.cs
- Added `maxResults = 100` and `offset = 0` parameters with `[Description]` phrasings matching Plan 10-01
- Ceiling rejection: `maxResults > 500` and `maxResults <= 0` throw `INVALID_PARAMETER`
- `catch (McpToolException) { throw; }` as first catch clause

### Transport/Mcp/Tools/AnalyzeReferencesTool.cs
- Fixed caller to use `cancellationToken:` named parameter after signature change (Rule 3: blocking issue)

### TestTargets/Types/PaginationTestTargetsInstantiations.cs
- New sibling fixture with `InstantiationsTarget` class and 105 distinct `new InstantiationsTarget()` call sites across 3 caller classes

### Tests/Tools/FindInstantiationsToolTests.cs
- 4 pre-existing functional tests updated to use named `cancellationToken:` parameter
- 7 new Pagination_* tests: FooterPresent, FooterShapeRegex, FirstPageTruncated, FinalPage, OffsetBeyondTotal, CeilingRejected, ZeroMaxResultsRejected
- 1 new enrichment test: FindInstantiations_Enrichment_ShowsFqnMethodSignatureAndIlOffset

## Test Results

- **Before:** 4 FindInstantiations tests
- **After:** 12 FindInstantiations tests (4 existing + 7 pagination + 1 enrichment)
- **Full suite:** 201 passed, 0 failed, 0 skipped

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed AnalyzeReferencesTool caller after signature change**
- **Found during:** Task 1 GREEN phase
- **Issue:** `AnalyzeReferencesTool.cs:54` passed `CancellationToken` as 3rd positional argument to `FindInstantiationsUseCase.ExecuteAsync`, which now expects `int maxResults` in position 3
- **Fix:** Changed to named parameter `cancellationToken: cancellationToken`
- **Files modified:** Transport/Mcp/Tools/AnalyzeReferencesTool.cs
- **Commit:** 2369902

**2. [Rule 3 - Blocking] Fixed pre-existing test callers after tool signature change**
- **Found during:** Task 1 GREEN phase
- **Issue:** 4 pre-existing tests passed `CancellationToken.None` as 3rd positional argument to `FindInstantiationsTool.ExecuteAsync`, which now expects `int maxResults` in position 3
- **Fix:** Changed to named parameter `cancellationToken: CancellationToken.None`
- **Files modified:** Tests/Tools/FindInstantiationsToolTests.cs
- **Commit:** 2369902

## Commits

| Hash | Message |
|------|---------|
| d15b8c4 | test(10-04): Add failing tests for find_instantiations pagination and enrichment |
| 2369902 | feat(10-04): Implement find_instantiations pagination, FQN signature display, and enriched output |

## Self-Check: PASSED

All 5 files verified present. Both commits (d15b8c4, 2369902) verified in history. 201/201 tests green.
