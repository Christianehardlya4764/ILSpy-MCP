# Phase 10: Find-Tool Pagination & Match Enrichment - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 10-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-10
**Phase:** 10-find-tool-pagination-match-enrichment
**Areas discussed:** Method signature format, Match layout under pagination, Defining-assembly resolution depth

---

## Gray Area Scope Selection

The agent surfaced 4 candidate gray areas:
1. Envelope helper timing (extract first vs copy-paste sweep)
2. Method signature format (short / FQN / C# style)
3. Implementors/dependencies output layout under pagination
4. Defining-assembly resolution depth

**User directive:** "Use your own best discretion. ask me only the 2-3 most critical ambiguous questions."

The agent dropped Area 1 (envelope helper timing) — Phase 9's CONTEXT.md explicitly said "extract the helper when the second tool in Phase 10 shows the copy-paste pain," so the decision is already locked by prior phase context. Remaining 3 areas were asked as separate questions.

---

## Method signature format

| Option | Description | Selected |
|--------|-------------|----------|
| Fully-qualified (Recommended) | `System.Void ProcessRequest(Microsoft.AspNetCore.Http.HttpContext context)` — unambiguous across assemblies, agent can correlate with other tools without guessing, no name collisions. Cost: wider lines. | ✓ |
| Short type names (current) | `Void ProcessRequest(HttpContext context)` — what the service produces today. Terse and readable, but `HttpContext` could be the ASP.NET one or a custom type with the same simple name. | |
| C# keywords + short names | `void ProcessRequest(HttpContext context)` — most human-readable, but no disambiguation vs the current format. | |

**User's choice:** Fully-qualified (Recommended)
**Notes:** No custom notes from the user. Pick matches the lazy-agent principle — unambiguous identifiers beat terseness for machine consumption.

---

## Match layout under pagination (find_implementors + find_dependencies)

| Option | Description | Selected |
|--------|-------------|----------|
| Flat + per-line markers (Recommended) | Each match self-describing: `[direct] [Class] MyNs.MyImpl` or `[MethodCall] System.String.Concat`. Page boundaries are clean, agent can re-group client-side. No orphaned section headers. Matches the "lazy agent" principle. | ✓ |
| Keep section headers | Emit 'Direct:' / 'Transitive:' (or 'MethodCall:' / 'FieldAccess:') once at the start of each section. If a page cuts through, later pages arrive with no header context — agent must remember which section they're reading. | |
| Headers repeated per page | Every page emits a header for each kind that appears on it. Preserves grouping within a page, but adds noise and the headers are not the parseable contract. | |

**User's choice:** Flat + per-line markers (Recommended)
**Notes:** No custom notes. The decision applies to both `find_implementors` (direct/transitive marker) AND `find_dependencies` (kind marker) — treated as a single design decision because both tools face the same pagination-vs-grouping conflict.

---

## Defining-assembly resolution depth (find_dependencies)

| Option | Description | Selected |
|--------|-------------|----------|
| Immediate AssemblyReference (Recommended) | Read `TypeReference.ResolutionScope.AssemblyReference.Name` from the MetadataReader. Cheap, no extra assembly loads, accurate at the binding level (e.g. `System.Runtime`). Agent uses `resolve_type` if they need the "real" defining assembly after type-forwards. | |
| Deep via ICrossAssemblyService | Load the resolution-scope assembly, walk type-forwards to the terminal assembly (e.g. `System.Runtime` → `System.Private.CoreLib`). Most accurate for the end consumer. Cost: cross-assembly service integration, slower, can fail if referenced assemblies aren't present. | ✓ (modified) |
| resolveDeep opt-in flag | Default shallow; expose a `resolveDeep=false` parameter. Agents who want type-forward resolution can pay the cost. Two code paths to test. | |

**User's choice:** Option 2 (Deep via ICrossAssemblyService), MODIFIED — "with info messages if referenced assemblies aren't present"
**Notes:** User's modification carries three implications the CONTEXT locks in:
1. Deep resolution is the default (no opt-in flag) — the tool always tries to chase type-forwards.
2. Missing referenced assemblies do NOT error the tool call — fail-soft.
3. Missing referenced assemblies ARE surfaced inline as info notes — the agent must be able to detect degraded resolution.

This locked the shape of the `DependencyResult` extension: `DefiningAssembly` (best-effort name) + `ResolutionNote` (nullable, present only when degraded).

---

## Claude's Discretion (no user questions)

The agent made the following calls without asking the user, based on the explicit directive to minimize questions:

- **PaginationEnvelope helper extraction timing** — extract in Plan 10-01 as the reference plan (Phase 9's deferred-list note resolved this)
- **Plan split strategy** — wave-1 reference plan (10-01) + wave-2 parallel sweep (10-02..10-05)
- **Stable sort keys per tool** — documented per tool in CONTEXT D-07
- **Test fixture strategy** — extend `PaginationTestTargets.cs` with scenario sub-namespaces per find_* tool
- **Tool-level `[Description]` rewrites** — NOT done in Phase 10 (Phase 13 owns DESC-01/02)
- **`ICrossReferenceService` signature changes** — NO — pagination stays Application-layer per D-06
- **Parameter `[Description]` verbatim phrasings** — copied from Phase 9's `list_namespace_types`

Additional gray areas left to the planner (not escalated to user) are enumerated in the CONTEXT's "Claude's Discretion" subsection.

---

## Deferred Ideas

- `SearchResults<T>` → `PagedResult<T>` rename — still deferred from Phase 9
- PaginationEnvelope extended helpers — extract only what Phase 10's 6 tools actually need
- Deep defining-assembly resolution for non-`find_dependencies` tools — out of scope
- `find_type_hierarchy` pagination — out of PAGE-02
- `search_strings` / `search_constants` footer retrofit — Phase 12
- Cross-request caching for paginated scans — PROJECT.md defers this globally
