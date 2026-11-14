# ADR-0042: Keep Cache Scheduling in Cache-Owned Workers

## Status

Accepted. Recorded 2026-11-10.

## Context

Refresh, warm-up, cleanup, and tracked operation progress need host lifecycle, bounded work, readiness, source scopes, and shutdown accounting. Requiring another scheduling feature would add a dependency and another ownership model to a headless cache.

## Decision

Use two cache-owned hosted workers following existing periodic background, TimeProvider, health, and named database-readiness conventions. Refresh and warm-up are explicit opt-ins with code-registered scoped loaders. Maintenance and operation progress use bounded provider work.

Refresh selects an exact existing active positive version and publishes conditionally. It does not extend a failed version's lifetime. Background work does not establish foreground activity. Pause changes new automatic admission on the current node and does not rewrite immutable startup configuration or revoke all running work.

## Alternatives considered

- Require Jobs or JobScheduling. This supplies a general scheduler but expands dependencies and does not remove cache-specific candidate, validity, backoff, or admission responsibilities.
- Start detached tasks from request handlers. This avoids a worker class but loses controlled startup/readiness, fair bounded sweeps, and host shutdown ownership.

## Consequences

The feature works without Jobs, JobScheduling, or StartupTasks. It must own its finite scheduling, fairness, failure backoff, and stop-budget behavior. A valid old version can remain readable during refresh; an expired one cannot become stale fallback.

## References

- [Worker reference](../features-application-caching.md#refresh-warm-up-and-maintenance)