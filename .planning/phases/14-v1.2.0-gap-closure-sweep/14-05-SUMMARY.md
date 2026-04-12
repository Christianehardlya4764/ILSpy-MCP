---
phase: 14-v1.2.0-gap-closure-sweep
plan: 05
subsystem: disassembly
tags: [IL, resolveDeep, IL-03, gap-closure]
requires:
  - 14-04 (canonical truncation footer already emits via use case)
provides:
  - resolveDeep opt-in flag on disassemble_type and disassemble_method
  - ExpandMemberDefinitions wiring in ReflectionDisassembler
affects:
  - Domain/Services/IDisassemblyService.cs
  - Infrastructure/Decompiler/ILSpyDisassemblyService.cs
  - Application/UseCases/DisassembleTypeUseCase.cs
  - Application/UseCases/DisassembleMethodUseCase.cs
  - Transport/Mcp/Tools/DisassembleTypeTool.cs
  - Transport/Mcp/Tools/DisassembleMethodTool.cs
tech-stack:
  added: []
  patterns:
    - Opt-in boolean flag threaded end-to-end across five layers (MCP attribute -> tool -> use case -> service interface -> ReflectionDisassembler setting)
    - Default-false preserves existing behavior for all callers
key-files:
  created: []
  modified:
    - Domain/Services/IDisassemblyService.cs
    - Infrastructure/Decompiler/ILSpyDisassemblyService.cs
    - Application/UseCases/DisassembleTypeUseCase.cs
    - Application/UseCases/DisassembleMethodUseCase.cs
    - Transport/Mcp/Tools/DisassembleTypeTool.cs
    - Transport/Mcp/Tools/DisassembleMethodTool.cs
    - Tests/Tools/DisassembleTypeToolTests.cs
    - Tests/Tools/DisassembleMethodToolTests.cs
    - Tests/Tools/NativeDllGuardTests.cs
decisions:
  - "resolveDeep maps to ReflectionDisassembler.ExpandMemberDefinitions (single property toggle in ICSharpCode.Decompiler 10.0) rather than composing multiple settings -- closest semantic match to IL-03 spec"
  - "Positional CancellationToken.None test call sites converted to named argument (cancellationToken:) rather than inserting a false placeholder for resolveDeep -- intent-preserving and future-proof"
requirements-completed:
  - IL-03
metrics:
  duration: ~15 min
  completed: 2026-04-12
---

# Phase 14 Plan 05: resolveDeep Flag on Disassemble Tools (IL-03) Summary

Opt-in `resolveDeep` boolean parameter threaded end-to-end across the disassemble stack (MCP attribute -> tool -> use case -> service interface -> ILSpyDisassemblyService -> ReflectionDisassembler.ExpandMemberDefinitions). Default `false` preserves existing output byte-for-byte; `true` emits expanded type signatures and generics for deeper inline resolution.

## What Was Built

1. **Domain/Services/IDisassemblyService.cs** — added `bool resolveDeep = false` parameter to both `DisassembleTypeAsync` and `DisassembleMethodAsync` (after `showTokens`, before `cancellationToken`). XML doc comments document the new parameter.

2. **Infrastructure/Decompiler/ILSpyDisassemblyService.cs** — matched interface signatures on both implementations. In each method, the `ReflectionDisassembler` initializer now sets `ExpandMemberDefinitions = resolveDeep`, alongside existing `ShowMetadataTokens` and (for methods) `ShowRawRVAOffsetAndBytes`.

3. **Application/UseCases/DisassembleTypeUseCase.cs** and **DisassembleMethodUseCase.cs** — added `bool resolveDeep = false` to `ExecuteAsync`, passed through to the service call. 14-04 pagination footer logic untouched.

4. **Transport/Mcp/Tools/DisassembleTypeTool.cs** and **DisassembleMethodTool.cs** — exposed `resolveDeep` as a new MCP parameter with `[Description]` attribute describing the deeper-resolution trade-off (larger output vs abbreviated form). Tool forwards flag to use case.

5. **Test adjustments** — three test files (`DisassembleTypeToolTests`, `DisassembleMethodToolTests`, `NativeDllGuardTests`) used positional `CancellationToken.None` at the tail of `ExecuteAsync`. Adding `resolveDeep` after `showTokens` shifted positions so those call sites now had to disambiguate via named argument (`cancellationToken:`). No test logic changed.

## Verification Evidence

- `dotnet build ILSpy.Mcp.sln` — 0 errors, 0 warnings
- `dotnet test --filter "FullyQualifiedName~Disassemble"` — 17 passed, 0 failed, 0 skipped
- `grep -c "resolveDeep" Domain/Services/IDisassemblyService.cs` — 4 matches (XML doc + parameter on both methods)
- `grep -c "resolveDeep" Infrastructure/Decompiler/ILSpyDisassemblyService.cs` — 4 matches (parameter + setter on both methods)
- `grep -cE "ExpandMemberDefinitions\s*=\s*resolveDeep" Infrastructure/Decompiler/ILSpyDisassemblyService.cs` — 2 matches (one per method, both wired)
- Both tool files contain the `[Description("When true, expand full type signatures…")]` attribute on the MCP parameter

## Acceptance Criteria

- [x] Interface and both implementations accept `resolveDeep`
- [x] Both use cases pass `resolveDeep` to the service call
- [x] Both MCP tools declare `resolveDeep` with `[Description]` attribute
- [x] `ExpandMemberDefinitions = resolveDeep` wired on both ReflectionDisassembler initializers (not a dead parameter)
- [x] Existing tests pass unchanged (default `false` preserves behavior)
- [x] `dotnet build` exits 0

## Deviations from Plan

**[Rule 3 - Blocking Issue] Test call sites using positional CancellationToken.None forced named-argument migration.**
- **Found during:** Task 2 build.
- **Issue:** Three test files (`DisassembleTypeToolTests.cs`, `DisassembleMethodToolTests.cs`, `NativeDllGuardTests.cs`) had call sites where the trailing `CancellationToken.None` was positional. Inserting `bool resolveDeep = false` one position before `CancellationToken` shifted the arg index so the compiler tried to bind `CancellationToken.None` to the new `bool resolveDeep` parameter.
- **Fix:** Switched each affected call site to `cancellationToken: CancellationToken.None` named argument. Plan explicitly anticipated this: "If any test breaks because it uses all positional args including `cancellationToken`, update the test to use named arguments."
- **Files modified:** `Tests/Tools/DisassembleTypeToolTests.cs` (3 sites), `Tests/Tools/DisassembleMethodToolTests.cs` (5 sites), `Tests/Tools/NativeDllGuardTests.cs` (1 site).
- **Commit:** 7a3c5d0.

No Rule 1 bug fixes, no Rule 2 missing-critical additions, no Rule 4 architectural decisions required.

## Authentication Gates

None.

## Threat Flags

None — `resolveDeep` is a boolean MCP input validated by the .NET type system. No new trust boundaries, no new surface. T-14-05-01 (DoS via larger output) is mitigated by the plan 14-04 truncation footer applied on top of the service output, which is unchanged by this plan.

## Commits

| Task | Commit  | Message                                                                                  |
| ---- | ------- | ---------------------------------------------------------------------------------------- |
| 1    | 08f0918 | feat(14-05): add resolveDeep parameter to IDisassemblyService and ILSpyDisassemblyService |
| 2    | 7a3c5d0 | feat(14-05): thread resolveDeep through use cases and MCP tools                          |

## Known Stubs

None. `resolveDeep=true` enables a real ReflectionDisassembler setting (`ExpandMemberDefinitions`) — not a placeholder.

## Success Criteria

**ROADMAP SC #6:** `disassemble_type` and `disassemble_method` expose a `resolveDeep` boolean flag that toggles full parameter-signature/generics resolution — **met**. IL-03 satisfied.

## Self-Check: PASSED

- FOUND: Domain/Services/IDisassemblyService.cs (resolveDeep on both methods)
- FOUND: Infrastructure/Decompiler/ILSpyDisassemblyService.cs (resolveDeep + ExpandMemberDefinitions on both initializers)
- FOUND: Application/UseCases/DisassembleTypeUseCase.cs (resolveDeep threaded through)
- FOUND: Application/UseCases/DisassembleMethodUseCase.cs (resolveDeep threaded through)
- FOUND: Transport/Mcp/Tools/DisassembleTypeTool.cs (resolveDeep MCP parameter)
- FOUND: Transport/Mcp/Tools/DisassembleMethodTool.cs (resolveDeep MCP parameter)
- FOUND commit: 08f0918 (Task 1)
- FOUND commit: 7a3c5d0 (Task 2)
- Build: 0 errors, 0 warnings
- Tests: 17 passed / 0 failed / 0 skipped (Disassemble filter)
