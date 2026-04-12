---
phase: 15-v1.2.0-audit-iteration-2-gap-closure
reviewed: 2026-04-12T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - Domain/Models/SearchResult.cs
  - Infrastructure/Decompiler/ILSpySearchService.cs
  - Application/UseCases/SearchStringsUseCase.cs
  - Tests/Tools/SearchStringsToolTests.cs
  - README.md
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 15: Code Review Report

**Reviewed:** 2026-04-12
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Phase 15 closes two v1.2.0 audit items: OUTPUT-06 (surrounding IL window for string search hits) and CLEAN-03 (README tool count correction 28 to 27). The implementation adds a two-phase scan in `ILSpySearchService.ScanILForStrings` that first renders every instruction in a method body and then slices a +/-3 window around each regex-matching `ldstr`. A new `SurroundingIL` field is added to `StringSearchResult`, and `SearchStringsUseCase.FormatResults` emits it under each match. A new `EmitsSurroundingILWindow` integration test verifies both the label and at least one rendered `IL_XXXX: ldstr` line.

Overall quality is good: the two-phase structure is clean, the window bounds are safe (`Math.Max` / `Math.Min`), the cap logic (`matchCap = offset + maxResults`) matches the existing constant-scan pattern, and the regex still uses a 1-second match timeout to guard against ReDoS. The README tool count was verified against the `[McpServerToolType]`-annotated classes under `Transport/Mcp/Tools/` — 27 tool files, matching the updated table entry.

Two warnings relate to correctness edges in `RenderInstruction`: UTF-16 surrogate-pair truncation of ldstr display text (the same class of bug fixed in Phase 14 WR-02 for a different path), and a read-past-end risk in the default operand-size fallback when the reader has fewer bytes remaining than the opcode's declared size. Three info-level items cover IL prefix-opcode rendering, unused allocation per method, and a minor test-robustness suggestion.

## Warnings

### WR-01: ldstr display truncation can split UTF-16 surrogate pairs

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:275`
**Issue:** The ldstr render path truncates the literal for display via `value.Substring(0, 64) + "..."`. If character 63 or 64 is the high surrogate of a non-BMP code point (emoji, historic scripts, supplementary CJK), `Substring(0, 64)` splits the surrogate pair and produces an invalid UTF-16 string in the rendered `surrounding IL:` output. This is the same defect Phase 14 WR-02 fixed for the ambiguous-method error path. Since this field is emitted through MCP to LLM clients, producing lone surrogates can corrupt JSON serialisation downstream.

Note: `MatchedValue` is never truncated, so regex matching and the primary match output are unaffected. Only the rendered IL window line is at risk.

**Fix:**
```csharp
case ILOpCode.Ldstr:
{
    int token = reader.ReadInt32();
    var handle = MetadataTokens.UserStringHandle(token & 0x00FFFFFF);
    string value;
    try { value = metadataReader.GetUserString(handle); }
    catch { value = string.Empty; }
    isLdstrHit = true;
    ldstrValue = value;
    string display = TruncateSafe(value, 64);
    return $"{prefix}ldstr \"{display}\"";
}

// Helper that avoids splitting surrogate pairs
private static string TruncateSafe(string s, int maxChars)
{
    if (s.Length <= maxChars) return s;
    int cut = maxChars;
    if (char.IsHighSurrogate(s[cut - 1])) cut--;
    return s.Substring(0, cut) + "...";
}
```

### WR-02: Default operand-size fallback reads without bounds check

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:335-356`
**Issue:** In the `default` branch of `RenderInstruction`, unknown or non-token opcodes are handled by switching on `ILParsingHelper.GetOperandSize(opCode)` and calling `reader.ReadByte/ReadInt16/ReadInt32/ReadInt64` unconditionally. `GetOperandSize` returns `0` for unknown opcodes (`_ => 0` at `ILParsingHelper.cs:148`), which is safe, but any opcode whose declared size exceeds `reader.RemainingBytes` will throw `BadImageFormatException` from `BlobReader`. The outer `catch (Exception ex) when (ex is not OperationCanceledException and not RegexMatchTimeoutException)` at line 82 will swallow it and skip the method — so this is not a crash — but it means malformed or truncated IL silently drops all hits for that method, including any ldstr already collected earlier in the same body because Phase 1 is aborted mid-body.

The existing single-pass constant scanner has the same risk profile but the blast radius is one instruction instead of one method's worth of Phase 1 work. For Phase 15 the behaviour is consistent with the rest of the codebase, so this is a warning rather than critical.

**Fix:** Guard the per-size reads, or wrap Phase 1 so partial instruction lists are still usable by Phase 2:
```csharp
switch (ILParsingHelper.GetOperandSize(opCode))
{
    case 0: return $"{prefix}{opName}";
    case 1 when reader.RemainingBytes >= 1: return $"{prefix}{opName} {reader.ReadByte()}";
    case 2 when reader.RemainingBytes >= 2: return $"{prefix}{opName} {reader.ReadInt16()}";
    case 4 when reader.RemainingBytes >= 4: return $"{prefix}{opName} {reader.ReadInt32()}";
    case 8 when reader.RemainingBytes >= 8: return $"{prefix}{opName} {reader.ReadInt64()}";
    // ... fall through to a "(truncated)" render and stop the loop
    default: return $"{prefix}{opName}";
}
```
Alternatively, leave as-is and add a comment explaining that malformed bodies are intentionally skipped wholesale.

## Info

### IN-01: IL prefix opcodes rendered as standalone instructions

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:244-358`
**Issue:** `Constrained.`, `Volatile.`, `Tail.`, `Unaligned.`, and `Readonly.` are IL prefix opcodes that modify the following instruction. `RenderInstruction` treats them as normal instructions with their own `IL_XXXX:` line, which is cosmetically different from ILSpy/ildasm output where the prefix is attached to the next line (e.g. `constrained. !!T callvirt ...`). Functionally harmless — the window still accurately represents bytecode position — but LLM consumers reading the output may find the split prefixes confusing.
**Fix:** Either accept the divergence (documented in the `SurroundingIL` XML comment) or coalesce prefix opcodes with their following instruction in a second rendering pass.

### IN-02: Phase 1 allocates per-instruction strings even when method has no regex hits

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:189-201`
**Issue:** Every instruction in every scanned method is rendered eagerly into a `List<(int, string, bool, string?)>`, even when no `ldstr` in the method matches the regex (which is the common case). For assemblies with tens of thousands of methods, this materially increases allocation compared to the previous single-pass scanner. Performance is out of v1 review scope, but flagging for future follow-up: a cheaper approach is to scan once collecting only raw `(offset, opCode, operandStart, operandLen)` tuples, then render lazily for the ~N windows that actually hit.
**Fix:** Defer rendering until a regex match is found. Not required for correctness.

### IN-03: Surrounding IL test does not assert window size or ordering

**File:** `Tests/Tools/SearchStringsToolTests.cs:119-134`
**Issue:** `EmitsSurroundingILWindow` only checks that the `surrounding IL:` label is present and that at least one `IL_XXXX: ldstr` line appears in the output. It does not assert that (a) the ldstr line is not the first line of the window when there are earlier instructions in the method, (b) the window contains more than one instruction, or (c) the lines are ordered by IL offset ascending. A regression that renders only the hit line itself (off-by-one on `start`/`end`, or swapping loop direction) would still pass this test.
**Fix:**
```csharp
result.Should().MatchRegex(@"surrounding IL:\s*(\r?\n\s+IL_[0-9A-F]{4}:[^\n]*){2,}");
// Extract the window block and assert offsets are monotonically increasing.
```

---

_Reviewed: 2026-04-12_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
