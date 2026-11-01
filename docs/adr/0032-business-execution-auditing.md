# ADR-0032: Record finite business executions through explicit contributors

## Status

Accepted.

## Date

2026-10-30

## Context

Diagnostic logs and property history answer different questions from a business execution trail. A request can reuse an existing customer, reject before a handler, recover from failed attempts, or return successfully without changing state. Deferred work can outlive its initiating request. Stored evidence must remain interpretable after live actors/resources or optional source-history rows disappear.

The feature must work in a plain .NET service, compose with existing behaviors without automatically enabling them, and preserve business behavior when auditing fails. Export needs strict source verification and byte/output completion rules without adding a second NDJSON transport implementation.

## Decision

Put injection/scope/value/worker-link contracts in Common. Implement the recorder, privacy, versioned documents, memory storage, integrity, queries, extraction and diagnostics in `Application.Auditing`. Domain/Common source adapters depend on contracts, and each source owns its explicit selection. Keep EF persistence in Infrastructure and HTTP/dashboard/history in the web package. CEF is an opt-in formatter in `Application.Auditing/Formatters`, with no separate project or package. General Data Protection libraries are allowed in core for protected worker links; ASP.NET hosting/framework and source-feature references are not.

Publish one immutable document after a finite root closes. Retain actual action occurrences with parent/sequence/attempt facts. Separate root outcome, recovered child failures, capture completeness, source persistence evidence and audit delivery. Manual instrumentation provides business vocabulary where it is known. Source summaries do not manufacture property deltas or external delivery/commit facts.

Worker entry clears inherited context before scoped services resolve. Only copied, protected, feature/operation-bound handoff metadata can establish trusted initiating identity/causation. Timer/inbound work without such evidence records its actual service execution without inventing a user. A durable wait or idle worker retains no live audit scope.

Optional ChangeHistory remains the source of diffs. Observable surviving source membership after commit permits copied values. Rollback discards staged evidence; unconfirmed evidence freezes as Unknown/reference-only without old/new snapshots. Current read exposure can further restrict retained values.

Use ReportOnly capture/delivery failure behavior. Keep finite root/action/value/query/queue/export budgets, safe failure counters and explicit receipts. Buffering belongs to the provider, which distinguishes volatile admission from confirmed audit-store presence. No business/audit atomic transaction, durable queue handoff or memory fallback after EF failure is implied.

Use the three audit data tables and singleton technical control row decided in [ADR-0031](0031-audit-store-publication-and-control-state.md). Root graphs remain independent of live actor/resource/source rows. Versioned original readers preserve unknown authenticated content; HMAC keys remain external and historical key IDs remain resolvable.

Apply optional trusted host read scope outside all client-controlled filter groups before provider selection/counting. Missing/failed selected scope closes access. No selected resolver means explicitly unrestricted data within ordinary host access. Operational global counts/declarations and numeric publication positions are hidden for restricted/unknown scope.

Keep application extraction separate from formatting and HTTP. Verify complete original sources, apply current exposure, and format one whole graph once. Reuse shared raw pre-encoded NDJSON and terminal output/cleanup outcomes. Protect continuation/checkpoint progress under separate host Data Protection purposes and bind it to store/epoch/selection/profile/principal/scope/exposure/expiry.

In-band NDJSON completion is prospective. Publish usable progress only through confirmed application completion or access-scoped companion metadata after expected flush/cleanup and final source validation. Client byte/count/digest verification is a separate requirement. Retry/deduplicate from previously verified progress; server emission is not recipient acknowledgment.

Discover dashboard review through the existing global dashboard access system in either registration order. Native form downloads stream attachments and poll bounded metadata without a browser Blob/result buffer or file spool. Standalone resource history uses host-owned routes and needs neither Dashboard nor public audit routes.

## Alternatives

- Enable every behavior from `AddAuditing`. Rejected because source selection, reads, worker boundaries and intentional omissions belong to the owning feature/application.
- Update mutable start/progress rows throughout a business operation. Rejected because in-progress state is not a finalized immutable document, and writes would couple recording to execution latency.
- Infer commits/deltas/delivery from successful methods. Rejected because those facts can be unobservable, deferred or rolled back.
- Retain request services/ambient collectors for workers. Rejected because their lifetimes and executing identity differ.
- Implement another JSON-line transport. Rejected because the shared writer/reader/HTTP executor already own framing, original bytes and terminal cleanup outcomes.
- Treat hashes/HMAC or passing tests as compliance proof. Rejected because deletion, rollback, key custody, operating controls and recipient processing require separate evidence.

## Consequences

Applications must identify important entry points and select meaningful contributors. The feature can expose declared/observed omissions but cannot prove every business action was instrumented. ReportOnly behavior can preserve business availability while losing audit evidence.

Durable relational publication serializes per store. Limits are configurable bounds, not throughput claims; deployment capacity needs representative measurement. Buffered admission can be lost on abrupt termination. Cumulative failures, retention/capacity losses and incomplete exports remain visible rather than being relabeled success.

Entity classes own EF mappings, and clients enrich their contexts through IAuditingDbContext. The feature provides no concrete context or migration API. Hosts own migrations, access/tenant constraints, clocks, external HMAC/token keys, routing, backup/restore and retention policy. Compatible restart can retain progress; changed position meaning requires operator epoch rotation. Retained source authentication does not authenticate a restricted export projection or detect deletion.

Public extension contracts and options are documented and available to hosts. Original-document bookkeeping remains assembly-controlled to prevent independent reassignment or disclosure through restricted projections.

## References

- [Application auditing guide](../features-application-auditing.md)
- [Auditing specification](../specs/spec-application-auditing.md)
- [NDJSON streaming specification](../specs/spec-presentation-ndjson-streaming.md)
- [ADR-0031: publication/control state](0031-audit-store-publication-and-control-state.md)
