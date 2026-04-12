---
phase: 15-v1.2.0-audit-iteration-2-gap-closure
verified: 2026-04-12T00:00:00Z
status: passed
score: 6/6 must-haves verified
overrides_applied: 0
re_verification: null
gaps: []
deferred: []
human_verification: []
---

# Phase 15: v1.2.0 Audit Iteration-2 Gap Closure — Verification Report

**Phase Goal:** Close the two gaps surviving Phase 14 that block v1.2.0 ship — OUTPUT-06 (`search_strings` must emit a window of surrounding IL instructions, not just method FQN + offset) and CLEAN-03 (`README.md` competitor comparison table must reflect the 27-tool runtime surface, not the stale 28).
**Verified:** 2026-04-12
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `StringSearchResult` exposes a surrounding-IL window field populated for every match | VERIFIED | `Domain/Models/SearchResult.cs:47` — `IReadOnlyList<string> SurroundingIL { get; init; } = Array.Empty<string>()` inside `StringSearchResult` record; `ConstantSearchResult` is untouched |
| 2 | `ILSpySearchService.ScanILForStrings` records N=3 instructions before and after each `ldstr` hit, bounded by method body start/end | VERIFIED | `Infrastructure/Decompiler/ILSpySearchService.cs:177` — `private const int SurroundingILWindowSize = 3;` with comment `// Window size: N=3 before/after (OUTPUT-06)`. Two-phase scan at lines 189–235: Phase 1 accumulates `(offset, rendered, isLdstrHit, ldstrValue)` tuples; Phase 2 at lines 204–235 slices `[i - SurroundingILWindowSize .. i + SurroundingILWindowSize]` clamped to body bounds. Assignment at line 233: `SurroundingIL = window` |
| 3 | `SearchStringsUseCase` formatter emits the window under each match as indented IL lines | VERIFIED | `Application/UseCases/SearchStringsUseCase.cs:112–119` — emits `"    surrounding IL:"` header then `"      {ilLine}"` per window entry; `PaginationEnvelope.AppendFooter` call preserved at line 123 |
| 4 | A test against the existing test assembly asserts the surrounding IL window appears in the formatted tool output | VERIFIED | `Tests/Tools/SearchStringsToolTests.cs:120–134` — `EmitsSurroundingILWindow` fact; asserts `Contain("surrounding IL:")`, `MatchRegex(@"surrounding IL:\s*\r?\n\s+IL_[0-9A-F]{4}:")`, and `MatchRegex(@"IL_[0-9A-F]{4}:\s*ldstr")` |
| 5 | `SearchStringsTool` `[Description]` "surrounding IL context" promise is no longer false | VERIFIED | `Transport/Mcp/Tools/SearchStringsTool.cs:28` — description reads "...IL offset, and surrounding IL context." which is now fulfilled by the runtime output |
| 6 | `README.md` competitor comparison table row reads `| **Tools** | 27 |` (not 28) | VERIFIED | Grep finds exactly 1 match at `README.md:1501` — `| **Tools** | 27 | ~10 | ~5 | ~3 | ~3 |`; zero `| **Tools** | 28 |` matches; canonical "**27 tools**" at line 58 preserved |

**Score:** 6/6 truths verified

---

## Required Artifacts

| Artifact | Expected | Level 1 (Exists) | Level 2 (Substantive) | Level 3 (Wired) | Level 4 (Data Flows) | Status |
|----------|----------|-----------------|----------------------|-----------------|---------------------|--------|
| `Domain/Models/SearchResult.cs` | `SurroundingIL` field on `StringSearchResult` | PASS | PASS — `IReadOnlyList<string>` with XML doc and `Array.Empty<string>()` default | PASS — populated by `ILSpySearchService`, consumed by `SearchStringsUseCase` | PASS — real IL bytes from method body scan | VERIFIED |
| `Infrastructure/Decompiler/ILSpySearchService.cs` | Window population on `ldstr` hit during IL scan | PASS | PASS — full two-phase scan with `RenderInstruction` helper (300+ lines of substantive logic) | PASS — assigns `SurroundingIL = window` on `StringSearchResult` construction | PASS — reads real `BlobReader` IL bytes via `MetadataReader` | VERIFIED |
| `Application/UseCases/SearchStringsUseCase.cs` | Window emission in `FormatResults` under each match | PASS | PASS — conditional block emits `"surrounding IL:"` header plus per-line IL output | PASS — reads `result.SurroundingIL.Count` and iterates `result.SurroundingIL` | PASS — data originates from infrastructure scan; no static fallback | VERIFIED |
| `Tests/Tools/SearchStringsToolTests.cs` | Test asserting window appears in tool output | PASS — `EmitsSurroundingILWindow` fact present at line 120 | PASS — 4 assertions, uses `_fixture.TestAssemblyPath` | PASS — invokes `SearchStringsTool.ExecuteAsync` end-to-end | N/A (test artifact) | VERIFIED |
| `README.md` | `| **Tools** | 27 |` in competitor comparison table | PASS | PASS — single targeted edit, no other content changed | N/A (documentation) | N/A | VERIFIED |

---

## Key Link Verification

| From | To | Via | Pattern | Status |
|------|----|-----|---------|--------|
| `ILSpySearchService.ScanILForStrings` | `StringSearchResult.SurroundingIL` | Two-phase scan: accumulate `(offset, rendered)` tuples, slice window `[i-3..i+3]` around ldstr hit | `SurroundingIL\s*=` at line 233 | WIRED |
| `SearchStringsUseCase.FormatResults` | `StringSearchResult.SurroundingIL` | `foreach` over `result.SurroundingIL` with indented emission | `result\.SurroundingIL` at lines 112 and 115 | WIRED |
| `SearchStringsToolTests.EmitsSurroundingILWindow` | `SearchStringsTool` output | Calls `tool.ExecuteAsync`, asserts window markers in returned string | `EmitsSurroundingILWindow` at line 120 | WIRED |
| `README.md` competitor table `| **Tools** |` row | Runtime MCP tool surface (27 tools) | Static doc matches runtime count — grep-verified | `\| \*\*Tools\*\* \| 27 \|` at line 1501 | WIRED |

---

## Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `SearchStringsUseCase.FormatResults` | `result.SurroundingIL` | `ILSpySearchService.ScanILForStrings` → `RenderInstruction` reading live `BlobReader` bytes from `metadataFile.GetMethodBody(rva).GetILReader()` | Yes — reads actual IL bytecode from assembly file; no static fallback or hardcoded empty | FLOWING |

---

## Behavioral Spot-Checks

Build and test executed directly:

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution compiles with 0 errors | `dotnet build ILSpy.Mcp.sln --no-restore -v q` | 0 Error(s), 2 pre-existing TestTargets warnings | PASS |
| All `SearchStrings` tests pass (8/8, including new `EmitsSurroundingILWindow`) | `dotnet test --filter "FullyQualifiedName~SearchStrings" --no-build` | Failed: 0, Passed: 8 | PASS |
| Full suite regression clean | `dotnet test ILSpy.Mcp.sln --no-build` | Failed: 0, Passed: 230, Skipped: 0 | PASS |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| OUTPUT-06 | 15-01-PLAN.md | `search_strings` matches include literal value, containing method FQN, IL offset, and a window of surrounding IL instructions | SATISFIED | `SurroundingIL` field added to domain model; populated in infrastructure with N=3 window; emitted in formatter; `EmitsSurroundingILWindow` test proves round-trip to tool output |
| CLEAN-03 | 15-02-PLAN.md | README.md updated to match 27-tool surface (competitor comparison table row) | SATISFIED | `README.md:1501` reads `| **Tools** | 27 |`; zero `28` occurrences remain in competitor table; canonical `**27 tools**` at line 58 preserved |

**Orphaned requirements check:** REQUIREMENTS.md traceability table maps OUTPUT-06 and CLEAN-03 to Phase 15 and no other Phase 15 requirements exist. No orphaned requirements.

---

## Anti-Patterns Found

No anti-patterns identified. Scanned:
- `Domain/Models/SearchResult.cs` — doc comment references `"IL_XXXX: opcode [operand]"` string literal in XML doc (not a stub marker)
- `Infrastructure/Decompiler/ILSpySearchService.cs` — `RenderInstruction` helper has a substantive switch block with real operand rendering
- `Application/UseCases/SearchStringsUseCase.cs` — formatter emits real field data, no hardcoded empty returns
- `Tests/Tools/SearchStringsToolTests.cs` — assertions are non-trivial regex checks, not placeholder stubs
- `README.md` — single-line change, no surrounding content altered

---

## Human Verification Required

None. All success criteria are mechanically verifiable:

- Domain field presence: grep-verified
- Infrastructure window population: grep-verified + unit tested
- Formatter emission: grep-verified + unit tested
- README table row: grep-verified (exact pattern match at line 1501)
- Tool description honesty: grep-verified + validated by `EmitsSurroundingILWindow` test

---

## Gaps Summary

No gaps. Both OUTPUT-06 and CLEAN-03 are fully closed:

**OUTPUT-06 closure path:** `StringSearchResult.SurroundingIL` (Domain) → `ScanILForStrings` two-phase scan + `RenderInstruction` helper (Infrastructure) → `FormatResults` window emission (Application) → `EmitsSurroundingILWindow` test assertion (Tests). All four layers wired and test-proven.

**CLEAN-03 closure:** Single targeted edit at `README.md:1501` changing `28` to `27`. Grep confirms no stale `28` remains in competitor table and canonical `27 tools` statement at line 58 is unchanged.

**Note on SUMMARY test count discrepancy:** The 15-01-SUMMARY.md claims 235 tests; the actual full suite run produces 230 passing tests. This is a summary inaccuracy — all 230 tests pass with 0 failures. The count difference does not affect goal achievement.

---

_Verified: 2026-04-12_
_Verifier: Claude (gsd-verifier)_
