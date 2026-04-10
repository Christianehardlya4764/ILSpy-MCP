---
phase: 11
slug: list-get-search-pagination-member-enrichment
status: verified
threats_open: 0
asvs_level: 1
created: 2026-04-10
---

# Phase 11 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| MCP client -> Transport layer | Agent-supplied pagination params (maxResults, offset) are untrusted input | Integer parameters from external AI agent |
| MCP client -> Transport layer | Agent-supplied typeName is untrusted input | String parameter validated by TypeName.Create() value object |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-11-01 | Tampering | ListAssemblyTypesTool, ListEmbeddedResourcesTool, SearchMembersByNameTool | mitigate | Validate maxResults > 0 and <= 500 at Transport boundary; reject with INVALID_PARAMETER error code | closed |
| T-11-02 | Denial of Service | All three list/search tools | mitigate | maxResults cap at 500 prevents unbounded memory allocation; existing ITimeoutService and IConcurrencyLimiter provide timeout and concurrency protection | closed |
| T-11-03 | Information Disclosure | ListAssemblyTypesTool (namespaceFilter) | accept | namespaceFilter is case-insensitive substring match on server-local assemblies; no path traversal risk (assemblyPath validated by AssemblyPath.Create()); tool operates on assemblies the agent already has a path to | closed |
| T-11-04 | Tampering | GetTypeMembersTool | mitigate | Validate maxResults > 0 and <= 500 at Transport boundary; typeName validated by TypeName.Create() value object in use case | closed |
| T-11-05 | Denial of Service | GetTypeMembersUseCase (base type walking) | mitigate | Only walk DirectBaseTypes (one level), not full hierarchy; IConcurrencyLimiter and ITimeoutService prevent runaway execution; pagination cap (max 500 per page) | closed |
| T-11-06 | Information Disclosure | GetTypeMembersUseCase (inherited members) | accept | Exposing inherited member info is explicit requirement (OUTPUT-05); no PII or secrets — .NET metadata from assemblies the agent already has access to | closed |

*Status: open / closed*
*Disposition: mitigate (implementation required) / accept (documented risk) / transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-11-01 | T-11-03 | namespaceFilter operates on assemblies agent already has path to; no path traversal possible due to AssemblyPath.Create() validation | Security audit | 2026-04-10 |
| AR-11-02 | T-11-06 | Exposing inherited member metadata is the explicit goal of OUTPUT-05; assemblies are local, no secrets involved | Security audit | 2026-04-10 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-04-10 | 6 | 6 | 0 | gsd-secure-phase |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-04-10
