# Phase 8: Tech Debt Cleanup - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Four discrete remediation tasks on the v1.0 baseline, delivering a clean starting point for the v1.2 polish work:

1. **DEBT-01** — Normalize `FindDependenciesTool` error code to match other cross-reference tools
2. **DEBT-02** — Remove the `Application → Transport` layer violation in `ExportProjectUseCase`
3. **DEBT-03** — Backfill missing `requirements-completed` frontmatter in Phase 1-6 SUMMARY.md files
4. **DEBT-04** — Runtime-verify Phase 7 tool tests that were previously only code-inspected

Not in scope: new features, tool surface changes (CLEAN-* lives in Phase 9), pagination/enrichment work (Phases 10-12), description rewrites (Phase 13), frontmatter-shape normalization beyond the `requirements-completed` gap.

</domain>

<decisions>
## Implementation Decisions

### DEBT-01 — Error code normalization

- **Target code: `MEMBER_NOT_FOUND`** for `FindDependenciesTool`. Cross-reference tools operate on "members" (methods, fields, properties) as a category; sibling tools `FindUsagesTool` and `GetMemberAttributesTool` already use `MEMBER_NOT_FOUND`. Aligning `FindDependenciesTool` closes the audit finding with the smallest diff and the most semantically correct code.
- **Scope is narrow on purpose.** Do NOT touch `DecompileMethodTool` / `DisassembleMethodTool` — those are method-specific operations where `METHOD_NOT_FOUND` is correct and agent-meaningful. Do NOT touch `AnalyzeReferencesTool` — it is deleted in Phase 9 (CLEAN-01), so any change is wasted work.
- **Domain exception stays.** `MethodNotFoundException` (Domain/Errors/) is kept unchanged — it is a semantic domain concept. Only the wire error code string in the Transport-layer catch handler changes.
- **Test update required.** `FindDependenciesToolTests` has at least one assertion expecting `METHOD_NOT_FOUND`. Update it to expect `MEMBER_NOT_FOUND`. This is a load-bearing part of the change — without it DEBT-04 (runtime verification) fails.

### DEBT-02 — Domain exception for output directory

- **Create `OutputDirectoryNotEmptyException : DomainException`** in `Domain/Errors/` following the `NamespaceNotFoundException` pattern exactly: single sealed class, base-class-carried error code string, structured properties (`OutputDirectory`), message explaining why and what to do. Error code: `DIRECTORY_NOT_EMPTY` (unchanged from current wire contract).
- **Rewire `ExportProjectUseCase`:**
  - Remove `using ILSpy.Mcp.Transport.Mcp.Errors;` (this is the architecture violation).
  - Throw `OutputDirectoryNotEmptyException` instead of `McpToolException("DIRECTORY_NOT_EMPTY", ...)`.
  - Remove the `catch (McpToolException) { throw; }` handler — it becomes dead once the transport exception is gone.
- **Rewire `ExportProjectTool`:**
  - Add a `catch (OutputDirectoryNotEmptyException ex)` clause that maps to `McpToolException("DIRECTORY_NOT_EMPTY", ErrorSanitizer.SanitizePath(ex.Message))`.
  - Remove the dead `catch (McpToolException) { throw; }` clause.
- **ArgumentException at line 46 is OUT OF SCOPE.** The whitespace-check `ArgumentException` is a BCL exception, not a Transport-layer type, so it does not violate the layer boundary. Normalizing it to a value object (e.g. `OutputDirectoryPath.Create`) is scope creep — leave it for a future cleanup if it ever matters.
- **Tests:** `ExportProjectToolTests` asserts on the `DIRECTORY_NOT_EMPTY` error code via `McpToolException`. Re-run after the change and confirm the wire contract is preserved — the error code that reaches agents must be identical.

### DEBT-03 — Frontmatter backfill

- **Field name: `requirements-completed:` (hyphenated).** Matches Phase 7 precedent. Precedent beats documentation wording — the actual frontmatter in the repo is what tools read. If the REQUIREMENTS.md doc wording becomes wrong, that is a documentation fix for a later pass.
- **Scope is minimal: only add the missing field.** Do NOT normalize frontmatter shapes. Phase 1's nested `dependency_graph:` vs Phase 4+'s flat `requires:`/`provides:` is a cosmetic inconsistency the audit did NOT flag; touching it is churn that dilutes the fix and expands the diff for no benefit.
- **Per-plan granularity.** Each SUMMARY.md file gets its own `requirements-completed:` array listing only the requirements that specific plan satisfied. Mapping source (in priority order): (1) the plan's `Accomplishments` / `Task Commits` sections if explicit, (2) the phase's own ROADMAP.md requirements slot cross-referenced against each plan's scope, (3) the v1.0 milestone audit's requirement-to-phase mapping as a sanity check.
- **Files to touch (14 SUMMARY.md files in `.planning/milestones/v1.0-phases/`):** 01-01, 01-02, 02-01, 02-02, 02-03, 03-01, 03-02, 04-01, 04-02, 05-01, 05-02, 06-01, 06-02. Phase 7's three SUMMARY files already have the field — skip them.
- **Ambiguity policy:** If a plan's exact requirement mapping is unclear, list the phase-level requirements on the *first* plan and an empty array on subsequent plans, and note this as a deviation in the execution summary.

### DEBT-04 — Runtime verification of Phase 7 tests

- **`dotnet` is available in this environment** (`dotnet --version` → `10.0.201`). The blocker that prevented runtime verification during Phase 7 execution is gone.
- **Verification command:** Run the full test suite (`dotnet test`), not just Phase 7 filters. Reason: DEBT-01 and DEBT-02 also touch tests in the same run, and a full green bar is the fastest way to prove that (a) Phase 7 tests actually pass at runtime, (b) the DEBT-01/02 changes did not regress anything else. A filtered Phase 7 run is a strict subset and gives weaker evidence.
- **Evidence artifact:** Capture the `dotnet test` final summary (total/passed/failed counts) and record it in each Phase 7 SUMMARY.md file as a `## Runtime Verification` section appended to the existing `## Self-Check: PASSED` block, with date, command, and the pass/fail count. This closes the loop on the "tests verified by code inspection only" note that currently sits in those summaries.
- **Failure policy:** If Phase 7 tool tests (`DecompileNamespaceToolTests`, `ExportProjectToolTests`, any phase 7 plan 3 tests) fail at runtime, the failures are in-scope for this phase — fix them here. Reason: the whole purpose of DEBT-04 is to prove the baseline is green. Punting a failure defeats the purpose. Non-Phase-7 failures surfaced by the full-suite run are also in-scope only if they are caused by DEBT-01 or DEBT-02 changes (which is expected and must be fixed); unrelated pre-existing failures get deferred as a new tech-debt item and noted in the phase summary.
- **Ordering:** DEBT-04 runs LAST in the phase so it also validates the DEBT-01/02/03 changes. Running it first would only verify the pre-Phase-8 baseline, which is less useful.

### Claude's Discretion

- Exact file layout for the new `OutputDirectoryNotEmptyException` (constructor signature, property names) — follow `NamespaceNotFoundException` shape
- Which of the Phase 7 SUMMARY files need the `Runtime Verification` block (probably all three, but only the ones whose tests actually ran)
- Exact sub-ordering of the four DEBT items within the phase plan structure (e.g., DEBT-02 and DEBT-01 can share a single plan since both touch the same Transport/Application boundary; DEBT-03 is a doc-only plan; DEBT-04 is the final validation plan)
- Whether to combine DEBT-01 + DEBT-02 into one plan or keep them separate (small, targeted plans match v1.0 velocity; recommendation is combine since both touch `Transport/Mcp/Tools/Find*Tool.cs`-adjacent areas and both need the same `dotnet test` pass)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Domain/Errors/NamespaceNotFoundException.cs` — exact pattern to copy for `OutputDirectoryNotEmptyException`: sealed class, `DomainException` base, error code passed to base constructor, structured property + message
- `Domain/Errors/DomainException.cs` — base class that carries the error code string
- `Transport/Mcp/Tools/FindUsagesTool.cs` — reference implementation of the `MEMBER_NOT_FOUND` convention that `FindDependenciesTool` needs to match
- `Transport/Mcp/Tools/ExportProjectTool.cs` — already has a layered catch structure; just needs one new `catch` clause added for the new domain exception
- `ErrorSanitizer.SanitizePath(...)` — used by every Transport tool for error message sanitization; reuse for the new catch clause

### Established Patterns
- **Domain exceptions own the error code string** via the `DomainException` base class — Transport tools catch the specific type and map to `McpToolException` with the same code
- **One exception per specific failure** — `TypeNotFoundException`, `MethodNotFoundException`, `NamespaceNotFoundException`, `AssemblyLoadException` are all narrow and purposeful. `OutputDirectoryNotEmptyException` follows the same shape.
- **Transport catches are ordered specific-to-generic** — domain exceptions first, then `TimeoutException`, `OperationCanceledException`, fallback `Exception`
- **Tests live in `Tests/Tools/{ToolName}Tests.cs`** — one class per tool, FluentAssertions, xUnit 2.9.x, integration style against `TestTargets` assembly
- **SUMMARY.md frontmatter keys use `kebab-case`** in Phase 4+ (`requires:`, `provides:`, `affects:`, `tech-stack:`, `key-files:`, `key-decisions:`, `patterns-established:`, `requirements-completed:`, `duration:`, `completed:`). Phase 1 is the outlier with `dependency_graph:` nested shape.

### Integration Points
- `Transport/Mcp/Tools/FindDependenciesTool.cs:47` — single-line error code string change
- `Application/UseCases/ExportProjectUseCase.cs:8,60,141` — remove one `using`, replace one `throw`, remove one dead `catch`
- `Transport/Mcp/Tools/ExportProjectTool.cs:41-44` — remove dead `catch`, add new specific `catch` for the domain exception
- `Domain/Errors/` — new file `OutputDirectoryNotEmptyException.cs`
- `Tests/Tools/FindDependenciesToolTests.cs` — update assertion on error code string
- `.planning/milestones/v1.0-phases/{01..06}/` — 13 SUMMARY.md files get a new `requirements-completed:` frontmatter key
- Test runner: `dotnet test` at repo root

### What NOT to touch
- `MethodNotFoundException` (Domain/Errors/) — it is the correct domain concept, only the wire code for cross-ref tools changes
- `DecompileMethodTool` / `DisassembleMethodTool` — keep `METHOD_NOT_FOUND`; they are method-specific operations
- `AnalyzeReferencesTool` — deleted in Phase 9, do not invest
- The `ArgumentException` at `ExportProjectUseCase.cs:46` — BCL exception, not a layer violation
- Phase 1-6 SUMMARY.md frontmatter shape normalization — not flagged by audit, not in scope
- Phase 7 SUMMARY.md frontmatter — already has `requirements-completed`

</code_context>

<specifics>
## Specific Ideas

- The v1.0 milestone audit at `.planning/milestones/v1.0-MILESTONE-AUDIT.md` line 20-28 lists the four tech debt items verbatim — this is the authoritative source for DEBT-01..04 and the recommended resolution wording ("Create domain-level OutputDirectoryException in Domain/Errors/ and map to McpToolException in ExportProjectTool")
- `FindDependenciesTool` is the *only* tool the audit flagged for error code inconsistency; the broader METHOD_NOT_FOUND vs MEMBER_NOT_FOUND population across the codebase is intentional and stays as-is
- DEBT-04 is ultimately a *credibility* fix, not a correctness fix — Phase 7's SUMMARY files currently say `Self-Check: PASSED` with a parenthetical admitting tests weren't runtime-verified. Closing this with evidence (a captured `dotnet test` summary) is the actual deliverable
- Keep the diff small and the commits atomic — the v1.0 velocity table in STATE.md shows phases of this size landing in 2-6 minutes per plan; Phase 8 should not explode that

</specifics>

<deferred>
## Deferred Ideas

- Full normalization of Phase 1-6 SUMMARY.md frontmatter shapes (nested `dependency_graph:` vs flat `requires:`) — cosmetic, not flagged by audit, defer
- Wider METHOD_NOT_FOUND vs MEMBER_NOT_FOUND audit across all 6 tools that use either code — audit only flagged one, fix just the one
- `OutputDirectoryPath` value object for directory validation (mirroring `AssemblyPath.Create`) — would also encapsulate the whitespace `ArgumentException` case cleanly, but not required to close the layer violation and expands scope
- REQUIREMENTS.md doc wording fix (`requirements_completed` underscored in docs vs `requirements-completed` hyphenated in actual frontmatter) — doc-only fix, not code, defer
- Backfilling VALIDATION.md Nyquist compliance for Phases 1-7 (flagged in the v1.0 audit as "overall: MISSING") — that is a separate validation effort, not part of the tech debt cleanup scope

</deferred>

---

*Phase: 08-tech-debt-cleanup*
*Context gathered: 2026-04-09*
