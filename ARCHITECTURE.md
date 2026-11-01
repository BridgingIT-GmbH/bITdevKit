# Auditing boundaries

Auditing follows the repository's Clean Architecture boundaries. Common exposes `IAuditContext`, `IAuditScope`, immutable descriptors/values and source/worker contracts. `Application.Auditing` supplies recording, memory storage, privacy, versioned source verification, queries, bounded extraction and diagnostics without referencing EF, HTTP, source features or ASP.NET hosting. Its exact Data Protection library dependency protects copied handoff metadata in plain .NET hosts.

Each source feature explicitly selects its own audit behavior. Repository/Active Entity/Common adapters depend on contracts rather than the auditing implementation. Optional ChangeHistory supplies confirmed source evidence; successful methods do not imply commits or deltas.

Infrastructure owns the EF provider for SQL Server, PostgreSQL and SQLite. Its three data tables and singleton technical control table preserve complete-graph publication and durable store/epoch/lifecycle/retention state. Entity classes own EF mappings, and clients enrich their contexts through IAuditingDbContext. The feature provides no concrete context or migration API. Hosts own migrations and keys. The audit transaction is independent of the business transaction.

`Presentation.Web.Auditing` supplies optional read routes, automatically discovered dashboard review and host-owned resource-history rendering. Trusted row scope is composed before selection/counting. Restricted metadata hides process-wide operational counts and numeric publication positions.

Application export owns verification/selection/whole-record progress. Formatters own permitted projection/framing. The web adapter reuses shared pre-encoded raw NDJSON and terminal writer/cleanup outcomes. Usable progress requires confirmed output and final source validation, followed by independent client byte/count/digest verification. The opt-in CEF formatter lives in `Application.Auditing/Formatters` and supplies no capture or SIEM transport.

See [the feature guide](docs/features-application-auditing.md), [ADR-0031](docs/adr/0031-audit-store-publication-and-control-state.md), and [ADR-0032](docs/adr/0032-business-execution-auditing.md).
