---
phase: 12-il-token-resolution-search-enrichment-truncation-reporting
reviewed: 2026-04-10T12:02:51Z
depth: standard
files_reviewed: 26
files_reviewed_list:
  - Application/Pagination/TruncationEnvelope.cs
  - Application/UseCases/AnalyzeAssemblyUseCase.cs
  - Application/UseCases/DecompileMethodUseCase.cs
  - Application/UseCases/DecompileNamespaceUseCase.cs
  - Application/UseCases/DecompileTypeUseCase.cs
  - Application/UseCases/DisassembleMethodUseCase.cs
  - Application/UseCases/DisassembleTypeUseCase.cs
  - Application/UseCases/ExportProjectUseCase.cs
  - Application/UseCases/SearchConstantsUseCase.cs
  - Application/UseCases/SearchStringsUseCase.cs
  - Domain/Models/SearchResult.cs
  - Domain/Services/IDisassemblyService.cs
  - Infrastructure/Decompiler/ILSpyDisassemblyService.cs
  - Infrastructure/Decompiler/ILSpySearchService.cs
  - Transport/Mcp/Tools/DisassembleMethodTool.cs
  - Transport/Mcp/Tools/DisassembleTypeTool.cs
  - TestTargets/Types/SearchTargets.cs
  - Tests/Tools/DisassembleMethodToolTests.cs
  - Tests/Tools/DisassembleTypeToolTests.cs
  - Tests/Tools/SearchStringsToolTests.cs
  - Tests/Tools/SearchConstantsToolTests.cs
  - Tests/Tools/DecompileTypeToolTests.cs
  - Tests/Tools/DecompileMethodToolTests.cs
  - Tests/Tools/DecompileNamespaceToolTests.cs
  - Tests/Tools/ExportProjectToolTests.cs
  - Tests/Tools/AnalyzeAssemblyToolTests.cs
findings:
  critical: 0
  warning: 4
  info: 5
  total: 9
status: issues_found
---

# Phase 12: Code Review Report

**Reviewed:** 2026-04-10T12:02:51Z
**Depth:** standard
**Files Reviewed:** 26
**Status:** issues_found

## Summary

Phase 12 adds three features across the codebase: a `resolveDeep` parameter for IL disassembly tools (regex post-processing to expand IL type abbreviations), search output enrichment (method FQN with params, surrounding IL window for string matches), and standardised truncation reporting via `TruncationEnvelope` across all tools.

The implementation is well-structured and the layering is clean. The most significant issues are a byte-count vs line-count mismatch in `TruncationEnvelope.TruncateSource` that causes a misleading `returnedLines` value at boundaries, a cache-key collision risk in `EnrichWithSurroundingIL` that can attach the wrong IL window to a result, and a silent data-loss edge case in the method overload error path. The `ApplyDeepResolution` regex approach also has two pattern-correctness issues. Everything else is lower-severity.

## Warnings

### WR-01: TruncateSource uses byte index into a UTF-16 string — line count for the truncated segment is wrong on multi-byte input

**File:** `Application/Pagination/TruncationEnvelope.cs:79`

**Issue:** `TruncateSource` compares `fullText.Length` (char count) against `maxBytes`, then slices with `fullText[..maxBytes]` (also a char-index slice). For ASCII-only content this is equivalent, but the parameter and the doc-comment say "bytes", which is the unit used by the callers. The real problem is the slice `fullText[..maxBytes]` can cut in the middle of a `\r\n` pair, making `CountLines` over-count by 1 for the truncated segment. Because `maxBytes` is used as a `char` index the slice is also susceptible to cutting inside a surrogate pair and producing an invalid string — though this is unlikely for decompiled C# output.

**Fix:**
```csharp
// Use char count consistently and document it, or convert to bytes first.
// Simplest correct fix — rename parameter, document unit:
public static (string text, int totalLines, int returnedLines, bool truncated) TruncateSource(
    string fullText, int maxChars)
{
    if (fullText.Length <= maxChars)
    {
        int lines = CountLines(fullText);
        return (fullText, lines, lines, false);
    }

    var totalLines = CountLines(fullText);
    // Snap back to last newline boundary to avoid cutting mid-line
    int cutAt = maxChars;
    while (cutAt > 0 && fullText[cutAt - 1] != '\n') cutAt--;
    if (cutAt == 0) cutAt = maxChars; // no newline found, cut at char limit
    var truncated = fullText[..cutAt];
    var returnedLines = CountLines(truncated);
    return (truncated, totalLines, returnedLines, true);
}
```

---

### WR-02: EnrichWithSurroundingIL cache key collides for overloaded methods — wrong IL window attached to a result

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:320`

**Issue:** The cache key is `$"{result.DeclaringType}.{result.MethodName}"`. A type can have overloaded methods with the same name (e.g., `Log(string)` and `Log(string, int)`). Both produce the same cache key. The first method's disassembled IL lines are returned for all subsequent matches in other overloads of the same name. The `FindIndex(l => l.offset == result.ILOffset)` call on a wrong method's line list either silently finds no match (resulting in a result with no IL window) or — in the worst case — finds an instruction at the coincidentally same offset in a different method body, attaching incorrect context.

**Fix:**
```csharp
// Use method signature as cache key, not just type + name
var cacheKey = result.MethodSignature ?? $"{result.DeclaringType}.{result.MethodName}";
```
`result.MethodSignature` already contains `DeclaringType.MethodName(param1, param2)`, which is unique per overload. Also update `CaptureMethodILLines` to accept a signature and match by it, or keep the current approach and also match against the signature when scanning methods.

---

### WR-03: DisassembleMethodAsync swallows the overload list and re-throws MethodNotFoundException without the disambiguation message

**File:** `Infrastructure/Decompiler/ILSpyDisassemblyService.cs:147-155`

**Issue:** When multiple overloads of a method are found, the code builds an `overloads` string listing them, then immediately throws `MethodNotFoundException(methodName, typeName.FullName)` — discarding the built string entirely. The error that reaches the caller contains no information about which overloads exist, making it impossible for an AI agent or user to know how to disambiguate.

```csharp
// The overloads string is built but never used:
var overloads = string.Join(", ", methods.Select(m => { ... }));
throw new MethodNotFoundException(methodName, typeName.FullName);  // overloads is lost
```

**Fix:**
Pass the overload list to the exception or to the error message. If `MethodNotFoundException` supports a detail message, use it:
```csharp
throw new MethodNotFoundException(methodName, typeName.FullName,
    $"Multiple overloads found: {overloads}. Specify parameters to disambiguate.");
```
If `MethodNotFoundException` does not support a detail string, either add an overload to its constructor or throw a derived exception with the disambiguation hint.

---

### WR-04: ApplyDeepResolution `string`/`object` lookahead regex fails to replace at end-of-line

**File:** `Infrastructure/Decompiler/ILSpyDisassemblyService.cs:243-244`

**Issue:** The patterns for `string` and `object` use a positive lookahead `(?=[\s\)\],]|$)`. The `$` in .NET `Regex.Replace` without `RegexOptions.Multiline` matches only end-of-input, not end-of-line. IL output is split on `\n` and processed line-by-line, so `$` never matches for intermediate lines (which still have the `\n` stripped by `Split('\n')` before `TrimEnd`). A signature like `.method ... string` where `string` is the very last token on a line will not be expanded.

By contrast, the other type patterns (`int32`, `float64`, etc.) correctly use `(?=\W|$)` where `\W` matches the newline character when present, so they do not have this bug.

**Fix:**
```csharp
line = Regex.Replace(line, @"(?<=[\s\(\[,])string(?=[\s\)\],\r\n]|$)", "System.String");
line = Regex.Replace(line, @"(?<=[\s\(\[,])object(?=[\s\)\],\r\n]|$)", "System.Object");
```
Or, since lines are already split and trimmed, add `RegexOptions.Multiline` so that `$` matches end-of-line:
```csharp
line = Regex.Replace(line, @"(?<=[\s\(\[,])string(?=[\s\)\],]|$)",
    "System.String", RegexOptions.Multiline);
```

---

## Info

### IN-01: ExportProjectUseCase — truncation logic uses CS file count as a proxy for "exported types", not actual type count

**File:** `Application/UseCases/ExportProjectUseCase.cs:188-189`

**Issue:** `exportedTypes` is set to `result.SourceFiles.Count` (number of `.cs` files) while `totalTypeCount` is the metadata type definition count. These are different units: one `.cs` file can contain multiple types, and some generated files (like `AssemblyInfo.cs`) contain no types. The truncation footer will report misleading values like `exportedTypes=12` vs `totalTypes=47` when the ratio is not 1:1.

**Fix:** Either rename the footer fields to `totalCsFiles`/`exportedCsFiles` to reflect what is actually counted, or count exported types by reading the `.cs` files and counting type declarations (more expensive). Renaming the fields is the simplest accurate fix.

---

### IN-02: ApplyDeepResolution allocates a compiled Regex per call — `Regex.Replace` is called in a hot inner loop

**File:** `Infrastructure/Decompiler/ILSpyDisassemblyService.cs:243-249`

**Issue:** `ApplyDeepResolution` calls `Regex.Replace` multiple times (once for `string`, once for `object`, then once per entry in the `typeExpansions` array) inside a `for` loop over every line of IL output. Each call allocates a new `Regex` object (non-compiled form) or pays an implicit compilation cost. For large type disassemblies with hundreds of lines and 13 patterns, this is unnecessary overhead.

**Fix:** Hoist the compiled regexes to `static readonly` fields on the class. The patterns are fixed at compile time:
```csharp
private static readonly Regex _stringPattern = new(
    @"(?<=[\s\(\[,])string(?=[\s\)\],]|$)", RegexOptions.Compiled);
private static readonly Regex _objectPattern = new(
    @"(?<=[\s\(\[,])object(?=[\s\)\],]|$)", RegexOptions.Compiled);
// etc.
```

---

### IN-03: CaptureMethodILLines does a full linear scan of all type definitions to locate a method — O(n) per result

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:366-403`

**Issue:** For each unique method in the paged result set, `CaptureMethodILLines` iterates over every `TypeDefinition` in the assembly to find the matching type, and then iterates over every method in that type. The `EnrichWithSurroundingIL` cache reduces calls per-method, but if the paged results span many distinct methods (up to `maxResults` distinct methods), this becomes O(maxResults * totalTypes). For large assemblies this is noticeable.

**Fix:** Pass the `ITypeDefinition` directly into `CaptureMethodILLines` rather than re-scanning by name. The type is already available at the scan site in `ScanILForStrings`. Store it in `StringSearchResult` as an internal field (not exposed in the domain model), or add a parallel internal lookup structure.

---

### IN-04: Silent bare `catch` in CaptureMethodILLines hides disassembler exceptions from diagnostics

**File:** `Infrastructure/Decompiler/ILSpySearchService.cs:406-409`

**Issue:** The `catch` block is completely bare — no logging, no type filter, no rethrow path. Exceptions from `ReflectionDisassembler` (invalid metadata, access violations in malformed assemblies) will be silently swallowed, making it impossible to diagnose why the IL window is absent. The comment "If disassembly fails for any reason, return empty" acknowledges this is intentional, but the absence of any diagnostic logging is a maintenance hazard.

**Fix:**
```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogDebug(ex, "Skipping IL window for {Type}.{Method} — disassembly failed",
        result.DeclaringType, result.MethodName);
}
```
`CaptureMethodILLines` is a `static` method, so either make it an instance method (to access `_logger`) or pass `ILogger` as a parameter.

---

### IN-05: SearchStringsToolTests — `SearchStrings_ShowsSurroundingILWindow` depends on `MethodWithContext` having enough surrounding instructions for a window to appear

**File:** `Tests/Tools/SearchStringsToolTests.cs:151-164`

**Issue:** The test asserts `result.Should().Contain("<-- match")` and `result.Should().MatchRegex(@"IL_[0-9A-Fa-f]{4}")`. The IL window is only populated when `CaptureMethodILLines` successfully disassembles the method and `FindIndex` locates the exact offset. If the test assembly is compiled with optimisations that inline `MethodWithContext`, the method may not appear as a standalone entry, or the IL offsets may shift. The test will silently fail to exercise the enrichment path without a clear failure message.

**Fix:** Add an assertion that the result contains at least one `IL_` instruction _before_ or _after_ the match line, not just that the regex matches anywhere in the result (the match line itself already contains `IL_XXXX`). Also consider adding a comment documenting the dependency on `MethodWithContext` being present and non-inlined.

---

_Reviewed: 2026-04-10T12:02:51Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
