# Pagination Contract

This document defines the single, uniform pagination contract that every list-returning MCP tool
in this project obeys. It is the canonical reference; the skill at
`.claude/skills/mcp-tool-design/SKILL.md` Principle 4 and `README.md` both link here rather than
duplicate the rules.

---

## Parameters

Every paginable tool exposes these two parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | int | `100` | Maximum number of results to return (default: 100) |
| `offset` | int | `0` | Number of results to skip for pagination (default: 0) |

---

## Hard Ceiling

- **`maxResults > 500`** is rejected with:
  ```
  McpToolException("INVALID_PARAMETER", "maxResults cannot exceed 500. Use offset to paginate.")
  ```
- **`maxResults <= 0`** is rejected with:
  ```
  McpToolException("INVALID_PARAMETER", "maxResults must be >= 1.")
  ```

Rejection (not silent clamping) is intentional: agents discover the limit on the first violation
and can self-correct. Silent clamping would silently corrupt the agent's mental model of what it
received.

---

## Response Envelope

Every response from a paginable tool has three sections:

```
[tool-specific natural-language header]

[results body]

[pagination:{"total":N,"returned":N,"offset":N,"truncated":bool,"nextOffset":N|null}]
```

The trailing `[pagination:...]` footer is always present — including zero-match and final-page
responses.

---

## Footer Shape

| Field | Type | Meaning |
|-------|------|---------|
| `total` | int | Total items across all pages (unsliced result size) |
| `returned` | int | Items actually in this response's body |
| `offset` | int | Offset this response was computed at |
| `truncated` | bool | `true` if more items exist beyond this page |
| `nextOffset` | int or null | Valid offset when `truncated=true`; explicit `null` when `truncated=false` |

Field order is **fixed**: `total`, `returned`, `offset`, `truncated`, `nextOffset`. The footer is
single-line, minified JSON wrapped in `[pagination:...]`. Never omit `nextOffset`.

---

## Edge Cases

- **Zero matches:** `total=0`, `returned=0`, `truncated=false`, `nextOffset=null`. The footer is
  still present.
- **Final page:** `truncated=false`, `nextOffset=null`. `returned` may be less than `maxResults`.
- **Offset beyond total:** NOT an error. Tool returns an empty body with `total` populated,
  `truncated=false`, `nextOffset=null`.

---

## Worked Examples

```
// Zero matches
(no matching types found)

[pagination:{"total":0,"returned":0,"offset":0,"truncated":false,"nextOffset":null}]

// Mid-page
...body rows 1-100...

[pagination:{"total":523,"returned":100,"offset":0,"truncated":true,"nextOffset":100}]

// Final page
...body rows 501-523...

[pagination:{"total":523,"returned":23,"offset":500,"truncated":false,"nextOffset":null}]
```

---

## Scope

Which tools implement this contract, and when:

- **Phase 9 (v1.2.0):** `list_namespace_types` is the first and only implementer.
- **Phase 10:** all `find_*` tools (PAGE-02).
- **Phase 11:** `list_assembly_types`, `list_embedded_resources`, `get_type_members`,
  `search_members_by_name` (PAGE-03..05). Note: PAGE-06 (`list_namespace_types`) landed in
  Phase 9 — see plan 09-04 for the roadmap ripple update.
- **Phase 12:** `search_strings` and `search_constants` get the trailing footer added
  (OUTPUT-06, OUTPUT-07 — they already expose `maxResults`/`offset`).
- **Source-returning / bounded-output tools** (PAGE-07, PAGE-08) use a related but simpler
  `(truncated, total_lines)` pattern tracked separately.

Subsequent phases cite this document rather than re-deriving the contract shape.
