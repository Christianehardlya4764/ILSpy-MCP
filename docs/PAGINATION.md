# Pagination Contract

Every list-returning, search-returning, or bounded-output tool in this MCP server
follows the same pagination contract. Agents do not need to specify pagination
parameters unless they intend to page — sensible defaults apply and the response
always includes a trailing `[pagination:{...}]` footer so truncation is visible.

## Parameters

Every paginable tool accepts the following two optional parameters:

| Parameter | Type | Default | Purpose |
|-----------|------|---------|---------|
| `maxResults` | int | 100 | Maximum results to return in one call. Hard ceiling: 500. |
| `offset` | int | 0 | Number of results to skip (for paging through truncated results). |

Tools MUST validate `maxResults <= 500` and `maxResults >= 1`, returning
`INVALID_PARAMETER` on violation. `offset < 0` should be normalized to 0 or
rejected with `INVALID_PARAMETER`.

## Footer Format

Every paginable response ends with a single line:

    [pagination:{"total":N,"returned":M,"offset":O,"truncated":T,"nextOffset":X}]

Field order is LOCKED to: `total, returned, offset, truncated, nextOffset`.

| Field | Type | Meaning |
|-------|------|---------|
| `total` | int | Total matches available across all pages. |
| `returned` | int | Number of matches in this response. |
| `offset` | int | Offset used for this call (echoes the input). |
| `truncated` | bool | True if `offset + returned < total` (more pages exist). |
| `nextOffset` | int \| null | If `truncated`, the offset to use on the next call; otherwise `null`. |

## Worked Example

Calling `find_usages` against a member with 250 total call sites, default
`maxResults=100`, `offset=0`:

```
Usages of MyApp.Service.Process: 250 found (showing 1-100)

  [Call] MyApp.Caller.A.RunOnce (IL_0012)
  [Call] MyApp.Caller.A.RunTwice (IL_0034)
  ...
[pagination:{"total":250,"returned":100,"offset":0,"truncated":true,"nextOffset":100}]
```

To get the next page, call again with `offset=100`. The third page
(`offset=200`) returns the final 50 results with `truncated=false` and
`nextOffset=null`.

## Truncation Footer (Source / Bounded-Output Tools)

Source-returning tools (`decompile_type`, `decompile_method`, `disassemble_type`,
`disassemble_method`) and bounded-output tools (`export_project`,
`analyze_assembly`) emit the SAME `[pagination:{...}]` footer when their output
exceeds the configured cap (`MaxDecompilationSize`, default 1 MB), reusing the
fields with this semantic:

| Field | Source/Bounded-Output Semantic |
|-------|--------------------------------|
| `total` | Total bytes (or total types for `export_project`) of the full output. |
| `returned` | Bytes (or types) actually included in this response. |
| `offset` | Always 0 (these tools don't support offset paging). |
| `truncated` | True if the output was capped. |
| `nextOffset` | Always `null` (re-call with narrower scope, not a higher offset). |

This deliberately reuses the canonical envelope so agents have ONE truncation
detection rule across all tools — never search for free-form
"[Output truncated at N bytes...]" strings.

## Implementation

The footer is emitted by the shared helper
`Application.Pagination.PaginationEnvelope.AppendFooter(StringBuilder, int total, int returned, int offset)`.

Every paginable use case MUST call this helper rather than inlining a footer
block. This guarantees field order, JSON shape, and `truncated`/`nextOffset`
computation stay uniform.

## Reference Implementation

`Application/UseCases/FindUsagesUseCase.cs` — `FormatResults` method — is the
canonical reference for new paginable tools. New tools should mirror its
structure: header line with `(showing N-M)` range, body lines, single
`PaginationEnvelope.AppendFooter` call.
