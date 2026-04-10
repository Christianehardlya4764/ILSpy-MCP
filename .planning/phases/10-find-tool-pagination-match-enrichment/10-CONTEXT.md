# Phase 10: Find-Tool Pagination & Match Enrichment - Context

**Gathered:** 2026-04-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Every `find_*` tool in scope (6 tools) becomes paginable via the Phase 9 contract **and** returns self-describing match records. Six tools in scope:

1. `find_usages` — PAGE-02 + **OUTPUT-01** (declaring type FQN, containing method signature, IL offset)
2. `find_implementors` — PAGE-02 + **OUTPUT-03** (direct vs transitive marker per match)
3. `find_dependencies` — PAGE-02 + **OUTPUT-02** (kind marker, FQN, **defining assembly**)
4. `find_instantiations` — PAGE-02 + **OUTPUT-04** (containing type FQN, containing method signature, IL offset)
5. `find_extension_methods` — PAGE-02 only (pagination, no new enrichment field)
6. `find_compiler_generated_types` — PAGE-02 only (pagination, no new enrichment field)

**NOT in scope (Phase 11/12/13 owns these):**
- `find_type_hierarchy` — excluded from PAGE-02 by REQUIREMENTS.md and ROADMAP.md success criteria (hierarchies are bounded)
- `list_*`, `get_type_members`, `search_members_by_name` — Phase 11
- `search_strings`, `search_constants` footer retrofit + enrichment — Phase 12
- `disassemble_*` / `decompile_*` / `export_project` / `analyze_assembly` — Phase 12
- Tool description rewrites — Phase 13 (DESC-01/02). Phase 10 only edits `[Description]` to add parameter descriptions; tool-level description stays untouched.

**Carrying forward from Phase 9 (locked — do not revisit):**
- Pagination parameter contract (`maxResults=100`, `offset=0`, ceiling `<=500`, **reject** don't clamp)
- Footer shape and field order: `[pagination:{"total":N,"returned":N,"offset":N,"truncated":bool,"nextOffset":N|null}]`
- Footer is always emitted — zero matches, mid-page, final page
- `SearchResults<T>` reused (not renamed to `PagedResult<T>`)
- Hard parameter changes, no deprecation aliases
- Integration tests with `Pagination_*` naming against `PaginationTestTargets.cs` fixture pattern
- Canonical reference implementation lives in `Application/UseCases/ListNamespaceTypesUseCase.cs`
- `docs/PAGINATION.md` is the parseable contract source of truth

</domain>

<decisions>
## Implementation Decisions

### D-01 — Method signature format: fully-qualified

**User choice (Q1):** Containing-method signatures in `find_usages` and `find_instantiations` output use **fully-qualified type names** for return type and parameters.

- Example: `System.Void ProcessRequest(Microsoft.AspNetCore.Http.HttpContext context)`
- Rationale: unambiguous across assemblies; agent can correlate with `find_dependencies`, `disassemble_method`, and `resolve_type` without guessing. No name collisions on common simple names like `HttpContext` or `Configuration`.
- **Implementation:** Upgrade `FormatMethodSignature(IMethod method)` in `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs:619-623` from `method.ReturnType.Name` / `p.Type.Name` to the FQN form. The existing construction sites (lines 413 and 551) pick up the change automatically.
- **Scope:** Change applies only to `MethodSignature` fields used by the two cross-reference result records. It does NOT ripple to other services or formatters in Phase 10.

### D-02 — Match layout under pagination: flat with per-line markers

**User choice (Q2):** Both `find_implementors` and `find_dependencies` emit a **flat, sorted match list with per-line markers** — no section headers.

- `find_implementors` output:
  ```
  [direct] [Class] MyNamespace.DirectImpl
  [direct] [Class] MyNamespace.AnotherDirect
  [transitive] [Class] MyNamespace.GrandChild
  ```
  Sort key: `(IsDirect descending, TypeFullName ascending)` — direct first (group in place because sort is stable), alpha within each group.

- `find_dependencies` output:
  ```
  [MethodCall] System.String.Concat(System.String, System.String) [System.Private.CoreLib]
  [MethodCall] System.Console.WriteLine(System.String) [System.Private.CoreLib]
  [VirtualCall] Microsoft.Extensions.Logging.ILogger.Log(...) [Microsoft.Extensions.Logging.Abstractions]
  [FieldAccess] System.String.Empty [System.Private.CoreLib]
  [TypeReference] System.Threading.CancellationToken [System.Private.CoreLib]
  ```
  Sort key: `(Kind ascending using enum order, TargetMember ascending)`. Enum order: `MethodCall=0, FieldAccess=1, TypeReference=2, VirtualCall=3` — whatever the existing declaration order is; the planner may re-order if it improves agent UX, but the order must be stable and documented.

- Rationale: page boundaries never orphan a section header; every match is self-describing; agent can re-group client-side if they prefer; matches the lazy-agent principle from the 260410 audit.
- **No section headers.** Phase 9's `list_namespace_types` uses kind-group headers because it only paginates types (no grouping-vs-pagination conflict). Phase 10 tools paginate WITHIN what would be groups — headers cannot survive pagination cleanly, so we drop them.

### D-03 — find_dependencies: deep defining-assembly resolution, fail-soft

**User choice (Q3):** `find_dependencies` performs **deep** defining-assembly resolution via type-forwards, with **transparent fail-soft info notes** when referenced assemblies aren't present at lookup time.

**Resolution chain:**
1. From each MemberReference/MethodDefinition/FieldDefinition encountered during IL scan, walk `TypeReference.ResolutionScope` → `AssemblyReference` to get the declared scope (e.g., `System.Runtime`).
2. Attempt to load the referenced assembly from the **same directory as the assembly being analyzed** using `UniversalAssemblyResolver` (ICSharpCode.Decompiler) or `ICrossAssemblyService` equivalent. No new tool parameter — the lookup root is implicit (`Path.GetDirectoryName(assemblyPath)`).
3. If loaded, chase type-forwards to the terminal assembly (e.g., `System.Runtime` → `System.Private.CoreLib`) and use that name.
4. If the referenced assembly is missing, emit an inline note on the affected match AND fall back to the immediate AssemblyReference name from the metadata scope. **Do not throw. Do not drop the match.**

**Output shape with fail-soft note:**
```
[MethodCall] System.String.Concat(System.String, System.String) [System.Private.CoreLib]
[MethodCall] Microsoft.Extensions.Logging.ILogger.Log(...) [Microsoft.Extensions.Logging.Abstractions] (unresolved: type-forward target assembly not present)
```

The note style is Claude's discretion (inline parenthetical shown above, or a trailing "Notes:" block). Whatever the format, the contract is:
- The match line still appears with a best-effort assembly name
- The unresolved case is visible to the agent (not silently degraded)
- The pagination footer is unaffected (notes are cosmetic, not structured fields)

**Domain model extension:**
Extend `Domain/Models/CrossReferenceResult.cs:58-79` `DependencyResult` record with:
- `DefiningAssembly` (required string) — terminal assembly name when deep resolution succeeded, else immediate AssemblyReference name
- `ResolutionNote` (nullable string) — populated only when deep resolution failed

**Where the deep-resolution logic lives:** Claude's discretion (see Claude's Discretion section).

### D-04 — PaginationEnvelope helper: extract in Plan 10-01

**Phase 9 deferred this:** Phase 9's CONTEXT.md line 199 said "extract the helper when the second tool in Phase 10 shows the copy-paste pain." Phase 10 has 6 tools — the pain is about to become 6× copy-paste. Extract **once**, up front, before any `find_*` tool is touched.

- **Plan 10-01 (reference plan)** extracts the helper AND retrofits `list_namespace_types` onto it AND implements the first `find_*` tool end-to-end. This establishes:
  - The helper shape (contract for Plans 10-02..10-06)
  - Proof the helper is shape-compatible with the canonical reference (retrofit on `list_namespace_types`)
  - The full end-to-end template for a find_* tool (pagination + enrichment + fixture + tests)
- **Helper API (contract, not implementation):**
  ```csharp
  // Append the [pagination:...] footer to an existing StringBuilder.
  // Computes truncated/nextOffset from total/returned/offset.
  PaginationEnvelope.AppendFooter(StringBuilder sb, int total, int returned, int offset);
  ```
  The planner may extend this with convenience helpers (`SliceAndFormat`, header helpers) if it reduces duplication across the 6 tools. Stay minimal — no helper class should grow beyond what Phase 10's 6 tools actually use.
- **Location for the helper:** Claude's discretion (`Application/Services/` near concurrency/timeout vs `Application/Formatting/` new folder vs `Application/Pagination/`). Naming does not affect the contract.

### D-05 — Tool parameter surface (all 6 find_* tools)

All 6 tools gain the exact Phase 9 parameter pair:

- `int maxResults = 100` — `[Description("Maximum number of results to return (default: 100)")]`
- `int offset = 0` — `[Description("Number of results to skip for pagination (default: 0)")]`

Verbatim phrasings reused from Phase 9's `list_namespace_types` — agents see one consistent parameter contract across the paginable tool surface.

Ceiling rejection reuses the exact error code and message from Phase 9:
- `maxResults > 500` → `McpToolException("INVALID_PARAMETER", "maxResults cannot exceed 500. Use offset to paginate.")`
- `maxResults <= 0` → `McpToolException("INVALID_PARAMETER", "maxResults must be >= 1.")`

Tool-level `[Description]` strings are **not** rewritten — Phase 13 owns DESC-01/02. Only parameter-level descriptions are added.

### D-06 — Pagination slicing lives in Application layer

Following the `list_namespace_types` precedent: domain services (`ICrossReferenceService`, `IAssemblyInspectionService`, `IDecompilerService.FindExtensionMethodsAsync`) still return `IReadOnlyList<T>` with all matches. Slicing (`Skip(offset).Take(maxResults)`) happens in the use case, wrapped in `SearchResults<T>` for the formatter.

- No change to `ICrossReferenceService.FindUsagesAsync` / `FindImplementorsAsync` / `FindInstantiationsAsync` signatures. They return full lists.
- `FindDependenciesAsync` MAY change if the deep-resolution work (D-03) pushes the resolver dependency into the service implementation. Signature stays the same either way (returns full `IReadOnlyList<DependencyResult>`).
- `FindExtensionMethodsAsync` is on `IDecompilerService` — signature unchanged.
- `FindCompilerGeneratedTypesAsync` is on `IAssemblyInspectionService` — signature unchanged.

Rationale: (a) matches Phase 9 precedent; (b) keeps domain services pagination-agnostic; (c) minimal blast radius for the scan-path code in `ILSpyCrossReferenceService` (lines 380-564).

### D-07 — Stable sort keys (pagination determinism)

Pagination REQUIRES deterministic order or pages can return the same item twice. Each tool gets a stable sort in the use case (ordinal string comparer):

- `find_usages`: `(DeclaringType asc, ILOffset asc)` — by containing type then location within the type
- `find_instantiations`: `(DeclaringType asc, ILOffset asc)` — same
- `find_implementors`: `(IsDirect desc, TypeFullName asc)` — direct first, alpha within each group (per D-02)
- `find_dependencies`: `(Kind asc, TargetMember asc)` — kind first, alpha within (per D-02)
- `find_extension_methods`: `(containing static class FQN asc, method Name asc, signature asc)`
- `find_compiler_generated_types`: `(ParentType ?? FullName asc, FullName asc)` — parent-grouped when possible

All sorts use `StringComparer.Ordinal` (case-sensitive, culture-invariant) for portability.

### D-08 — Test fixture strategy: one scenario per tool in PaginationTestTargets.cs

Extend `TestTargets/Types/PaginationTestTargets.cs` (Phase 9 fixture, currently 105 top-level classes in `ILSpy.Mcp.TestTargets.Pagination` namespace) with scenario sub-namespaces — one per find_* tool that needs a >100-item threshold:

- `ILSpy.Mcp.TestTargets.Pagination.Usages` — a widely-called member (e.g. a `Log` helper) with ≥105 call sites spread across methods
- `ILSpy.Mcp.TestTargets.Pagination.Implementors` — an interface with ≥105 implementing classes
- `ILSpy.Mcp.TestTargets.Pagination.Dependencies` — a kitchen-sink class depending on ≥105 distinct members
- `ILSpy.Mcp.TestTargets.Pagination.Instantiations` — a type `new`'d ≥105 times
- `ILSpy.Mcp.TestTargets.Pagination.ExtensionMethods` — ≥105 extension methods for a single target type
- `ILSpy.Mcp.TestTargets.Pagination.CompilerGenerated` — ≥105 async methods/lambdas to trigger compiler-generated types

Keep each scenario in ONE file (`PaginationTestTargets.cs` expansion OR sibling files) — Claude's discretion. Each assertion should follow Phase 9's crossover pattern: page 1 returns 100, page 2 returns the remainder, footer fields match exactly.

### D-09 — Plan split strategy

**Wave 1 — Reference plan (serial first, unblocks the rest):**
- **Plan 10-01** — Extract `PaginationEnvelope` helper. Retrofit `list_namespace_types` to use it (proof of shape compatibility). Implement `find_usages` end-to-end (FQN signature change in service, pagination + footer in use case, parameter surface in tool, Pagination_* fixture + tests). This plan establishes the template every subsequent Phase 10 plan copies.

**Wave 2 — Parallel sweep (all unblocked by Plan 10-01):**
- **Plan 10-02** — `find_dependencies` — isolated because it touches a new domain model field (`DefiningAssembly` + `ResolutionNote`) AND adds deep type-forward resolution. Biggest single plan in the phase.
- **Plan 10-03** — `find_implementors` — flat direct/transitive layout per D-02, pagination + footer, tests.
- **Plan 10-04** — `find_instantiations` — FQN signature display (service change already in 10-01), pagination + footer, tests.
- **Plan 10-05** — `find_extension_methods` + `find_compiler_generated_types` — combined plan for the two tools that only need pagination (no enrichment). These touch different services (`IDecompilerService` and `IAssemblyInspectionService`) but their plan shape is identical and small. Combine if the resulting plan stays under ~8 files touched; split if it grows.

Planner may collapse/expand Wave 2 plans based on final file counts. The hard constraint is that Plan 10-01 lands before ANY Wave 2 plan begins (the helper contract must exist).

Phase 9 velocity precedent: 2-6 minute plans, 2-7 files each. Phase 10 should stay in that envelope.

### Claude's Discretion

- **Exact location of the `PaginationEnvelope` helper** — `Application/Services/` vs `Application/Formatting/` vs `Application/Pagination/`. Pick whichever fits the existing folder structure best.
- **Helper API surface beyond `AppendFooter(sb, total, returned, offset)`** — extend only if 2+ tools need the same convenience pattern. Do not over-engineer for hypothetical Phase 11/12 needs.
- **Whether to retrofit `list_namespace_types` onto the extracted helper in Plan 10-01** — retrofitting proves shape compatibility (recommended); not retrofitting minimizes Plan 10-01 diff. Decide based on the retrofit's size; if the retrofit is 5+ lines removed and 1 line added, do it.
- **Where the deep-resolution logic lives (D-03)** — directly inside `ILSpyCrossReferenceService` (localized, no interface change) vs pushed through a new `ICrossAssemblyService.ResolveTerminalAssemblyAsync` method (layered purity, reusable for future phases). Prefer localized for Phase 10 unless a second consumer emerges within the phase.
- **UniversalAssemblyResolver vs manual PEFile walk** — ICSharpCode.Decompiler provides `UniversalAssemblyResolver` which already chases type-forwards given a search path. Prefer it over hand-rolled metadata walks unless it pulls in extra dependencies this phase doesn't want.
- **Inline vs trailing format for fail-soft resolution notes (D-03)** — `[kind] name [asm] (note: ...)` inline OR a `Notes:` block appended before the pagination footer. Inline is denser; trailing is cleaner when multiple notes appear. Pick based on the most common case.
- **Collapsing or splitting Wave 2 plans** — the sketch above has 5 plans (10-01..10-05). Combine or split based on file counts so each plan lands in 2-6 minutes.
- **Whether to rename `SearchResults<T>` → `PagedResult<T>`** — still deferred from Phase 9. Phase 10 doubles the domains using it. If the planner sees 5+ find_* use cases all constructing a `SearchResults<T>` with clearly-mismatched semantics (e.g. "Results" vs "Dependencies"), it may be worth reopening. Otherwise defer again.
- **Fold or keep `VirtualCall` as a distinct kind marker in `find_dependencies`** — the enum distinguishes `VirtualCall` from `MethodCall`. Presenting them distinctly matches the data; folding them reduces noise. Match what helps the agent most in practice — probably keep distinct.
- **Test fixture file layout** — one big `PaginationTestTargets.cs` with 6 sub-namespaces, OR a sibling file per scenario (e.g. `PaginationTestTargets.Usages.cs`). Latter gives cleaner per-plan diffs; former keeps all pagination fixtures in one discoverable place.
- **Exact sort order for `DependencyKind` enum presentation** — existing enum order is `MethodCall, FieldAccess, TypeReference, VirtualCall`. Use that; reorder only if the agent UX clearly benefits.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Pagination contract (locked Phase 9)
- `docs/PAGINATION.md` — canonical parameter/footer contract (Parameters, Hard Ceiling, Response Envelope, Edge Cases, Worked Examples). THIS IS THE PARSEABLE CONTRACT SOURCE OF TRUTH.
- `.claude/skills/mcp-tool-design/SKILL.md` Principle 4 — cross-references PAGINATION.md; do not duplicate rules, link only.
- `.planning/phases/09-pagination-contract-structural-cleanup/09-CONTEXT.md` — prior phase context, reference implementation notes, defer list that includes the `PaginationEnvelope` helper extraction.

### Audit source (motivation for OUTPUT-01..04)
- `.planning/quick/260410-audit-mcp-tools-for-ai-effectiveness-and/260410-AUDIT.md` §2 (Scoping), §3 (Description Quality), §5 (Pagination) — the audit that produced the OUTPUT-01..04 requirements and the "lazy agent" design principle.

### Reference implementation (Phase 9 canonical)
- `Application/UseCases/ListNamespaceTypesUseCase.cs` — canonical slice-then-format-with-footer pattern. Key lines:
  - `:48-53` — parameter surface (`maxResults`, `offset`)
  - `:100-108` — sort and slice in the use case
  - `:119` — invoke FormatOutput
  - `:187-246` — header + body + footer format, including zero-match and offset-beyond-total branches
  - `:229-243` — the footer JSON serialization block that Plan 10-01 extracts into `PaginationEnvelope`
- `Tests/Tools/ListNamespaceTypesToolTests.cs` — `Pagination_*` test naming and assertion style:
  - `:133-245` — FooterPresent, FooterShapeRegex, FirstPageTruncated, FinalPage, OffsetBeyondTotal, CeilingRejected, ZeroMaxResultsRejected
- `TestTargets/Types/PaginationTestTargets.cs` — 105 top-level classes in `ILSpy.Mcp.TestTargets.Pagination` namespace. Header comment documents assertion coupling.
- `Domain/Models/SearchResult.cs:6` — `SearchResults<T>` record reused across all paginated tools.

### Project scope and success criteria
- `.planning/REQUIREMENTS.md` §Pagination (PAGE-02), §Output Richness (OUTPUT-01, OUTPUT-02, OUTPUT-03, OUTPUT-04) — the 5 requirements this phase closes.
- `.planning/ROADMAP.md` Phase 10 — 5 success criteria (pagination-receivable, find_usages enrichment, find_dependencies grouping with defining assembly, find_implementors direct/transitive marker, find_instantiations enrichment).
- `.planning/PROJECT.md` — constraints: no new runtime dependencies; layered architecture (Domain → Infrastructure → Application → Transport); 28-existing-tools stability (now 27 post Phase 9).
- `.planning/phases/08-tech-debt-cleanup/08-CONTEXT.md` — tech-debt baseline that Phase 10 builds on (error code normalization, layered-architecture rules).

### Find_* existing implementations (the code Phase 10 edits)
- `Transport/Mcp/Tools/FindUsagesTool.cs` — thin handler, add parameters
- `Transport/Mcp/Tools/FindImplementorsTool.cs` — thin handler, add parameters
- `Transport/Mcp/Tools/FindDependenciesTool.cs` — thin handler, add parameters
- `Transport/Mcp/Tools/FindInstantiationsTool.cs` — thin handler, add parameters
- `Transport/Mcp/Tools/FindExtensionMethodsTool.cs` — thin handler, add parameters
- `Transport/Mcp/Tools/FindCompilerGeneratedTypesTool.cs` — thin handler, add parameters
- `Application/UseCases/FindUsagesUseCase.cs:74-92` — rewrite FormatResults (show MethodSignature, emit footer)
- `Application/UseCases/FindImplementorsUseCase.cs:72-106` — rewrite FormatResults (flatten direct/transitive, per-line marker)
- `Application/UseCases/FindDependenciesUseCase.cs:74-97` — rewrite FormatResults (flatten groups, per-line marker, defining assembly)
- `Application/UseCases/FindInstantiationsUseCase.cs:72-90` — rewrite FormatResults (show MethodSignature)
- `Application/UseCases/FindExtensionMethodsUseCase.cs` — add slice + footer, no enrichment
- `Application/UseCases/FindCompilerGeneratedTypesUseCase.cs:70-93` — add slice + footer, no enrichment
- `Domain/Models/CrossReferenceResult.cs:6-97` — all four cross-reference result records. `DependencyResult` needs extension (D-03).
- `Domain/Services/ICrossReferenceService.cs` — signatures unchanged (pagination is Application-layer).
- `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs:619-623` — `FormatMethodSignature` upgrade to FullName (D-01)
- `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs:427-502` — `ScanILForDependencies` — add deep defining-assembly resolution (D-03)
- `Infrastructure/Decompiler/ILSpyCrossAssemblyService.cs` — existing directory-loading adapter; see if `UniversalAssemblyResolver` lives nearby and can be reused for D-03

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Domain/Models/SearchResult.cs:6` — `SearchResults<T>`** — record with `Results`, `TotalCount`, `Offset`, `Limit`. Reused across all Phase 10 tools without changes. No rename (Phase 9 decision).
- **`Application/UseCases/ListNamespaceTypesUseCase.cs:229-243`** — the inline footer serialization block is the extraction target for Plan 10-01's `PaginationEnvelope.AppendFooter`.
- **`Domain/Models/CrossReferenceResult.cs`** — all four result records already exist, and three of them already carry the enrichment data Phase 10 needs to display:
  - `UsageResult.DeclaringType` (FQN), `MethodName`, `ILOffset`, `Kind`, `MethodSignature` — `MethodSignature` is populated by the service (verified at `ILSpyCrossReferenceService.cs:413`) but not displayed in output today. Phase 10 just displays it.
  - `InstantiationResult.DeclaringType`, `MethodName`, `ILOffset`, `MethodSignature` — same: populated at `:551`, not displayed. Phase 10 displays it.
  - `ImplementorResult.IsDirect` — populated at `:149-155` and `:181-187`. The current text output already splits Direct/Indirect via SECTION HEADERS; Phase 10 flattens to per-line `[direct]`/`[transitive]` markers.
  - `DependencyResult.TargetMember`, `TargetType`, `Kind` — NO defining assembly field. Phase 10 extends with `DefiningAssembly` + `ResolutionNote`.
- **`Infrastructure/Decompiler/ILSpyCrossReferenceService.cs:619-623` — `FormatMethodSignature(IMethod method)`** — current short-name implementation. Upgrade to use `.FullName` (return type and each parameter type). This one change ripples through both `UsageResult` and `InstantiationResult` because both construction sites call it.
- **`Infrastructure/Decompiler/ILSpyCrossReferenceService.cs:449-477` — `ScanILForDependencies`** — existing MemberReference / MethodDefinition / FieldDefinition metadata walk. Extension point for D-03 — add `ResolutionScope` → `AssemblyReference` walk here plus optional type-forward chase via UniversalAssemblyResolver.
- **`Infrastructure/Decompiler/ILSpyCrossAssemblyService.cs`** — existing directory-based PEFile loader. If Plan 10-02 decides to push deep resolution through the service layer instead of keeping it in `ILSpyCrossReferenceService`, this is where a new `ResolveTerminalAssemblyAsync` method would land.
- **`TestTargets/Types/PaginationTestTargets.cs`** — Phase 9 fixture pattern (105 top-level classes in a dedicated namespace). Phase 10 extends with scenario sub-namespaces per D-08.
- **`Tests/Fixtures/ToolTestFixture.cs:55-58`** — `FindUsagesUseCase` / `FindImplementorsUseCase` / `FindDependenciesUseCase` / `FindInstantiationsUseCase` already DI-registered. No fixture changes for adding pagination tests.
- **`Tests/Tools/ListNamespaceTypesToolTests.cs:131-245`** — the 7 `Pagination_*` facts are the template to copy per find_* tool.

### Established Patterns
- **MCP tools return `Task<string>`** — plain-text envelope, unchanged in Phase 10.
- **Tool / UseCase / Service separation** — tool is thin catch-handler, use case does orchestration + formatting (including pagination slicing + footer emission), domain service does IL scanning (unaware of pagination). This is enforced; D-06 locks it in.
- **`[Description]` parameter phrasings verbatim** — copy from `list_namespace_types` so agents see one consistent parameter contract.
- **xUnit 2.9.x + FluentAssertions, integration-style against `TestTargets`** — no mocking, no test-double layer. Pagination_* tests assert on the footer JSON string contents.
- **Hard parameter changes, no deprecation aliases** — Phase 9 precedent carries forward. Adding `maxResults`/`offset` parameters to existing tools is a signature change; no shim for old callers.
- **Plan atomic commits** — Phase 8 and Phase 9 landed one commit per plan. Follow the precedent.

### Integration Points
- **New files:**
  - `Application/{Services|Formatting|Pagination}/PaginationEnvelope.cs` — the extracted helper (location is Claude's discretion)
  - Possibly `TestTargets/Types/Pagination{Usages|Implementors|Dependencies|Instantiations|ExtensionMethods|CompilerGenerated}Targets.cs` — sibling fixtures (or expand `PaginationTestTargets.cs`)
- **Modified files (per plan):**
  - Plan 10-01: `PaginationEnvelope.cs` (new), `ListNamespaceTypesUseCase.cs` (retrofit), `FindUsagesTool.cs`, `FindUsagesUseCase.cs`, `ILSpyCrossReferenceService.cs` (FormatMethodSignature FQN), `PaginationTestTargets.cs` (Usages scenario), `Tests/Tools/FindUsagesToolTests.cs` (Pagination_* facts + FQN signature assertions)
  - Plan 10-02: `CrossReferenceResult.cs` (extend DependencyResult), `FindDependenciesTool.cs`, `FindDependenciesUseCase.cs`, `ILSpyCrossReferenceService.cs` (ScanILForDependencies deep resolution), possibly `ILSpyCrossAssemblyService.cs` / `ICrossAssemblyService.cs`, `PaginationTestTargets.cs` (Dependencies scenario), `Tests/Tools/FindDependenciesToolTests.cs`
  - Plan 10-03: `FindImplementorsTool.cs`, `FindImplementorsUseCase.cs`, `PaginationTestTargets.cs` (Implementors scenario), `Tests/Tools/FindImplementorsToolTests.cs`
  - Plan 10-04: `FindInstantiationsTool.cs`, `FindInstantiationsUseCase.cs`, `PaginationTestTargets.cs` (Instantiations scenario), `Tests/Tools/FindInstantiationsToolTests.cs`
  - Plan 10-05: `FindExtensionMethodsTool.cs`, `FindExtensionMethodsUseCase.cs`, `FindCompilerGeneratedTypesTool.cs`, `FindCompilerGeneratedTypesUseCase.cs`, `PaginationTestTargets.cs` (ExtensionMethods + CompilerGenerated scenarios), `Tests/Tools/FindExtensionMethods*Tests.cs`, `Tests/Tools/FindCompilerGeneratedTypesToolTests.cs`

### What NOT to touch
- **`find_type_hierarchy`** — NOT in PAGE-02. Leave alone. Hierarchies are bounded (≤20 items per the audit's pagination audit table).
- **`search_strings`, `search_constants`** — Phase 12 retrofits the footer for these. Do not add pagination footer here.
- **`list_assembly_types`, `list_embedded_resources`, `get_type_members`, `search_members_by_name`** — Phase 11.
- **`disassemble_*`, `decompile_*`, `analyze_assembly`, `export_project`** — Phase 12.
- **Tool-level `[Description]` strings** — Phase 13 owns DESC-01/02. Phase 10 only adds parameter-level descriptions for `maxResults`/`offset`.
- **`ICrossReferenceService` method signatures** — stay unchanged. Pagination is Application-layer, D-06.
- **Error code conventions** — inherited from Phase 8 (DEBT-01). `find_dependencies` already returns `MEMBER_NOT_FOUND` consistently; do not change it.
- **`SearchResults<T>` record shape** — do not add `Returned` property mid-phase; Phase 9 left this deferred and Phase 10 inherits the decision.
- **`FindTypeHierarchyTool` / `FindTypeHierarchyUseCase`** — out of scope entirely.

</code_context>

<specifics>
## Specific Ideas

- **User directive (explicit):** "Use your own best discretion. ask me only the 2-3 most critical ambiguous questions." Interpretation: minimize back-and-forth; surface only decisions with real product impact; document all remaining gray areas as Claude's Discretion so the planner has explicit latitude without re-querying.
- **User directive (Q3 modification):** The user chose "Option 2 (deep via ICrossAssemblyService), with info messages if referenced assemblies aren't present." This is a modified deep-resolution choice — NOT a fail-fast or silently-degraded one. The tool must stay functional when assemblies are missing and MUST surface the degraded-resolution case to the agent inline. Losing the match is not acceptable; hiding the degradation is not acceptable.
- **Reference implementation precedent:** Every Phase 10 use case's `ExecuteAsync` + `FormatResults` should visually rhyme with `ListNamespaceTypesUseCase` — fetch full list, stable sort, slice, wrap in `SearchResults<T>`, format header, format body, append footer via helper.
- **Data-already-there observation:** For OUTPUT-01 and OUTPUT-04, the enrichment data (`MethodSignature`) is already populated by the service — Phase 10 is a display-layer change for those fields. This means the FQN upgrade in `FormatMethodSignature` ripples into both tools without touching their use cases beyond the formatting rewrite.
- **Deep-resolution cost containment:** The type-forward chase only runs for `find_dependencies`, not for `find_usages` or `find_instantiations` (which scan within the analyzed assembly and don't need cross-assembly resolution). This limits the performance footprint to one tool.
- **Phase 9 deferred-list carryover:** `PaginationEnvelope` helper extraction, `SearchResults<T>` rename, and `search_strings`/`search_constants` footer retrofit all came from Phase 9's deferred list. Phase 10 acts on the first, keeps deferring the second, and explicitly leaves the third to Phase 12.
- **Plan velocity target:** Phase 9 landed 4 plans in 2-14 minutes each. Phase 10 has similar scope (5-6 plans), target 3-8 minute range per plan.

</specifics>

<deferred>
## Deferred Ideas

- **`SearchResults<T>` → `PagedResult<T>` rename** — carried forward from Phase 9. Phase 10 may reopen if the planner sees domain-semantic mismatch. Not required for any success criterion.
- **`PaginationEnvelope` extended helpers** (e.g. `WriteHeader(sb, toolName, total, range)`, `SliceAndFormat<T>`) — extract only if Phase 10's 6 tools actually need them. Defer aggressive helper expansion to Phase 11/12 when more tools will exercise the helper surface.
- **Deep defining-assembly resolution for non-`find_dependencies` tools** — Phase 10 only does this for `find_dependencies` (per OUTPUT-02). Other tools' enrichment requirements do not include cross-assembly resolution.
- **`find_type_hierarchy` pagination** — explicitly out of PAGE-02. If a future assembly with huge hierarchies surfaces, raise as a new requirement in a later milestone.
- **`search_strings` / `search_constants` footer retrofit** — Phase 12 owns it (alongside OUTPUT-06/07 enrichment). Phase 10 does not touch these tools.
- **Automated tool-count assertion** — Phase 9 noted this as nice-to-have. Phase 10 inherits the defer; 27-tool count verification is still manual.
- **Domain-level pagination push-down** — forcing `ICrossReferenceService` to accept `(maxResults, offset)` and slice at IL-scan time. This would avoid allocating huge result lists for popular members but breaks the Phase 9 precedent of Application-layer slicing AND requires cross-service plumbing. Defer unless memory/time benchmarks demand it.
- **Caching across paginated calls** — Phase 10 re-scans the assembly on every page. If `find_usages` page 2 is called immediately after page 1, the entire IL scan runs again. PROJECT.md explicitly defers cross-request caching ("premature optimization, defer until performance data exists"). Phase 10 inherits that defer.

</deferred>

---

*Phase: 10-find-tool-pagination-match-enrichment*
*Context gathered: 2026-04-10*
