# ADR-0041: Validate Every Hot Cache Reuse Authoritatively

## Status

Accepted. Recorded 2026-11-10. Representation and admission defaults are design-selected within this accepted direction.

## Context

Repeated persistent reads can transfer and decode the same payload even when the full dataset exceeds RAM. A local representation must not hide acknowledged invalidation or replacement on another instance.

## Decision

Keep optional Binary, Decoded, or BinaryAndDecoded retention in the collection engine, off by default. Every attempted reuse calls authoritative provider validation for its exact version and snapshot. Changed versions take the ordinary read path; missing state is a miss; authority failure remains failure. There is no timed trust window or stale local fallback.

Use finite parent/collection budgets, bounded second-access evidence, version-specific coalesced promotion, reader pins, and incremental local eviction. Share existing immutable InMemory buffer ownership without a duplicate ordinary resident charge. Decoded retention requires the read-only contract and a conservative estimate.

## Alternatives considered

- Trust local values for a short TTL or invalidation broadcast window. This saves validation I/O but permits stale reads after an acknowledged remote mutation.
- Add another cache provider layer. This makes registration familiar but splits authority and obscures which provider owns validity and publication.
- Always re-read and decode payloads. This keeps a smaller engine but loses the explicitly requested bounded acceleration option.

## Consequences

Hot reuse can save payload I/O and decoding while metadata, reader, or sliding work remains measurable. Local retention adds bookkeeping and memory ownership that require separate diagnostics. Evicting a local copy leaves the provider entry intact. Disabled retention preserves the ordinary baseline without hot preflight calls.

## References

- [Hot-entry contracts](../features-application-caching.md#opt-into-hot-retention)
- [Feature and typed-result boundaries](0039-independent-advanced-application-caching.md)