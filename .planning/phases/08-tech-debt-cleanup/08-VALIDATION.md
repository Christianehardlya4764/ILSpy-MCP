---
phase: 8
slug: tech-debt-cleanup
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-09
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + FluentAssertions 8.9.x |
| **Config file** | None — tests registered via `[Collection("ToolTests")]` + shared `ToolTestFixture` |
| **Quick run command** | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~{TestClass}"` |
| **Full suite command** | `dotnet test ILSpy.Mcp.sln` |
| **Estimated runtime** | ~30-60s full suite |

---

## Sampling Rate

- **After every task commit:** Run targeted filter for the affected test class (e.g. `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~FindDependenciesToolTests"`)
- **After every plan wave:** Run `dotnet test ILSpy.Mcp.sln`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~30s for targeted filter, ~60s for full suite

DEBT-03 is a doc-only plan — validation is grep-based, no test runner involvement. DEBT-04 *is* the phase-gate full suite run, recording evidence artifacts.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 8-01-01 | 01 | 1 | DEBT-02 | unit | N/A (new file, compile-only) | ❌ W0 (new) | ⬜ pending |
| 8-01-02 | 01 | 1 | DEBT-02 | unit | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~ExportProjectToolTests.FailsOnNonEmptyDirectory"` | ✅ | ⬜ pending |
| 8-01-03 | 01 | 1 | DEBT-01, DEBT-02 | unit | `dotnet test ILSpy.Mcp.sln --filter "FullyQualifiedName~FindDependenciesToolTests\|FullyQualifiedName~ExportProjectToolTests"` | ✅ (+ 1 new) | ⬜ pending |
| 8-01-04 | 01 | 1 | DEBT-01, DEBT-02 | integration | `dotnet test ILSpy.Mcp.sln` | ✅ | ⬜ pending |
| 8-02-01 | 02 | 2 | DEBT-03 | static/grep | `for f in 01-01 01-02 02-01 02-02 02-03 06-01; do grep -q "^requirements-completed:" .planning/milestones/v1.0-phases/*/${f}-SUMMARY.md \|\| echo MISSING: ${f}; done` | ✅ | ⬜ pending |
| 8-03-01 | 03 | 3 | DEBT-04 | integration (full suite) | `dotnet test ILSpy.Mcp.sln` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Tests/Tools/FindDependenciesToolTests.cs` — add `FindDependencies_NonExistentMember_ThrowsMemberNotFound` test (new test giving DEBT-01 direct runtime coverage against the `MEMBER_NOT_FOUND` wire code)
- [ ] `Domain/Errors/OutputDirectoryNotEmptyException.cs` — new file (not a test, but a new source artifact the DEBT-02 rewire depends on)

No framework install needed (dotnet 10.0.201, xUnit 2.9.x already present). No new fixtures or test targets needed (reuses `ToolTestFixture`, `_fixture.TestAssemblyPath`, and the existing `CrossRef.DataService` test target).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `Application/UseCases/ExportProjectUseCase.cs` contains zero `using ILSpy.Mcp.Transport.*` directives | DEBT-02 (layering invariant) | Static code-level invariant, not runtime behavior | `grep -c "using ILSpy.Mcp.Transport" Application/UseCases/ExportProjectUseCase.cs` must return `0` |
| Phase 1-6 SUMMARY.md files have `requirements-completed:` frontmatter key | DEBT-03 | Documentation content, not runtime | Grep check in task 8-02-01; confirm `requirements-completed:` count in `.planning/milestones/v1.0-phases/**/SUMMARY.md` equals 16 (10 pre-existing + 6 backfilled) |
| Phase 7 SUMMARY.md files contain a `## Runtime Verification` evidence block with `dotnet test` final counts | DEBT-04 | Documentation artifact captured from a real runtime run | Grep for `## Runtime Verification` in `.planning/milestones/v1.0-phases/07-bulk-operations-documentation/07-0{1,2,3}-SUMMARY.md` — all three present |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (new DEBT-01 test + new domain exception file)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
