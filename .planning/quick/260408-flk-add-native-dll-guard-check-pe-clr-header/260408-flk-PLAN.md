---
quick_id: 260408-flk
description: Add native DLL guard - check PE CLR header before loading assemblies
date: 2026-04-08
tasks: 3
---

# Quick Plan: Native DLL Guard

## Goal
When a user passes a native (non-.NET) DLL to any tool, return a clear error message instead of the cryptic `MetadataFileNotSupportedException: PE file does not contain any managed metadata`.

## Approach
Add a second constructor to `AssemblyLoadException` that accepts a custom message, then catch `MetadataFileNotSupportedException` in all 3 infrastructure services before the generic `Exception` catch. Zero tool-layer changes needed since tools already catch `AssemblyLoadException`.

## Task 1: Add custom message constructor to AssemblyLoadException

**Files:** `Domain/Errors/AssemblyLoadException.cs`
**Action:** Add a second constructor overload that accepts a custom message string:
```csharp
public AssemblyLoadException(string assemblyPath, string message, Exception innerException)
    : base("ASSEMBLY_LOAD_FAILED", message, innerException)
{
    AssemblyPath = assemblyPath;
}
```
**Verify:** Project compiles
**Done:** AssemblyLoadException has two constructors

## Task 2: Add MetadataFileNotSupportedException catch to all 3 services

**Files:**
- `Infrastructure/Decompiler/ILSpyDecompilerService.cs` (6 methods)
- `Infrastructure/Decompiler/ILSpyDisassemblyService.cs` (2 methods)
- `Infrastructure/Decompiler/ILSpyCrossReferenceService.cs` (4 methods)

**Action:** In each method's try-catch, add a `catch (MetadataFileNotSupportedException)` block BEFORE the generic `catch (Exception ex)` block. The catch should:
1. Log at Warning level: "Assembly is not a .NET assembly: {Assembly}"
2. Throw `new AssemblyLoadException(assemblyPath.Value, $"'{assemblyPath.FileName}' is not a .NET assembly. The file does not contain managed (.NET) metadata and cannot be decompiled.", ex)`

Add `using ICSharpCode.Decompiler.Metadata;` if not already present (needed for MetadataFileNotSupportedException).

**Verify:** `dotnet build` succeeds
**Done:** All 12 service methods catch native DLL errors with clear messages

## Task 3: Add test for native DLL error handling

**Files:** Tests directory - find appropriate test file for ILSpyDecompilerService

**Action:** Add a test that verifies passing a native DLL (or any non-.NET PE file) throws AssemblyLoadException with a message containing "not a .NET assembly". Use a known Windows system DLL like kernel32.dll or create a minimal non-.NET test fixture.

**Verify:** `dotnet test` passes including new test
**Done:** Native DLL guard has test coverage
