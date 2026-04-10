---
phase: 12-il-token-resolution-search-enrichment-truncation-reporting
fixed_at: 2026-04-10T12:15:00Z
review_path: .planning/phases/12-il-token-resolution-search-enrichment-truncation-reporting/12-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 12: Code Review Fix Report

**Fixed at:** 2026-04-10T12:15:00Z
**Source review:** .planning/phases/12-il-token-resolution-search-enrichment-truncation-reporting/12-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4
- Fixed: 4
- Skipped: 0

## Fixed Issues

### WR-01: TruncateSource uses byte index into a UTF-16 string -- line count for the truncated segment is wrong on multi-byte input

**Files modified:** `Application/Pagination/TruncationEnvelope.cs`
**Commit:** 2fb37ec
**Applied fix:** Renamed parameter from `maxBytes` to `maxChars` to accurately reflect the unit (char count, not byte count). Added logic to snap the cut point back to the last newline boundary before truncating, preventing mid-line and mid-CRLF cuts that caused `CountLines` to over-count. Updated doc-comment to match.

### WR-02: EnrichWithSurroundingIL cache key collides for overloaded methods -- wrong IL window attached to a result

**Files modified:** `Infrastructure/Decompiler/ILSpySearchService.cs`
**Commit:** 65c5c3d
**Applied fix:** Changed cache key from `"{DeclaringType}.{MethodName}"` to use `result.MethodSignature` (which includes parameter types and is unique per overload), falling back to the original key format only when `MethodSignature` is null.

### WR-03: DisassembleMethodAsync swallows the overload list and re-throws MethodNotFoundException without the disambiguation message

**Files modified:** `Domain/Errors/MethodNotFoundException.cs`, `Infrastructure/Decompiler/ILSpyDisassemblyService.cs`
**Commit:** 3106f2b
**Applied fix:** Added a new constructor overload to `MethodNotFoundException` accepting a `detail` string parameter that is appended to the base error message. Updated the throw site in `ILSpyDisassemblyService.DisassembleMethodAsync` to pass the built `overloads` string with disambiguation instructions, so the error message now includes the list of available overloads.

### WR-04: ApplyDeepResolution string/object lookahead regex fails to replace at end-of-line

**Files modified:** `Infrastructure/Decompiler/ILSpyDisassemblyService.cs`
**Commit:** fe4e1f3
**Applied fix:** Added `\r\n` to the positive lookahead character class in the `string` and `object` regex patterns, matching the robustness of the other type expansion patterns that use `\W`. This ensures matches when a trailing `\r` is present after `Split('\n')` on Windows-style line endings.

---

_Fixed: 2026-04-10T12:15:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
