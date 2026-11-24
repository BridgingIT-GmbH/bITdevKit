# ADR-0040: Keep Cache Publication and Resource Guarantees in One Provider

## Status

Accepted. Recorded 2026-11-10. This records the provider, publication, resource, and native recovery contracts.

## Context

Validity depends on payload identity, expiration, metadata selection, ownership, and invalidation fences. Splitting that state across stores would require cross-store publication and recovery protocols and add provider calls to ordinary reads.

## Decision

Each advanced collection selects one complete InMemory, Entity Framework, or native filesystem provider. That provider owns payloads, indexed metadata, reader state, source ownership, fences, conditional publication, and recovery in its backing store.

The engine owns portable collection policies and source execution. EF uses the selected database directly. The filesystem provider uses native persistence/authority helpers directly. Do not compose cache payloads over Blob Storage or configure independent metadata and payload providers.

### Publication and owned work

Managed source loading captures publication fences before source access. Providers atomically check ownership, expected version, and invalidation state at publication. Snapshot versions identify acquired or confirmed stored generations; conflicts never retry unconditionally. Manual get/source/set cannot claim a pre-source fence it did not acquire.

Default node-local loading and explicit shared-store ownership have different scopes. A central store alone does not coalesce source work across nodes. Shared-store authority failure or uncertainty starts no new source work and never downgrades to node-local ownership. Loaders perform repeatable reads/computations, not business mutations; nested cache source work is rejected to prevent ownership cycles.

Binary delivery uses store-then-open. A loader writes once into bounded selected-store staging, publication is confirmed, then each caller acquires an independent snapshot. Failed, rejected, or indeterminate publication does not deliver an uncached binary stream. Integrity completion is separate from destination success, and partial ranges establish affected-frame integrity only.

Cache-owned buffers, scopes, staging, reader pins, and admissions remain accounted until actual work ends. A caller timeout or ignored cancellation does not prove native termination or release those resources. Persistent stored capacity and process resident/transient capacity remain independent finite constraints. Read-only typed sharing avoids compulsory follower clones; approved decoded retention still needs conservative graph accounting.

### Failure domains

Source policy state is process-local by dependency alias. Sequential attempts and followers share one owned deadline and maximum attempt budget. Source failure/open rejection does not create negative markers or open the cache-provider breaker. Provider validation, ownership, admission, and publication failures do not become source dependency failures.

The optional persistent-provider breaker counts infrastructure failures and permits one nonmutating readiness probe after cooldown. It never replays mutations or consumed streams. Eligible node-local typed unretained completion remains distinct from shared-store authority and confirmed binary delivery.

### Provider-owned persistence and consumer deployment

EF keeps normalized metadata/index/coordination tables and bounded immutable payload chunks in one database. Annotated entities and IApplicationCachingContext enrich consumer contexts. Consumers scaffold/deploy their own engine-specific migrations and establish readiness; the runtime package ships no migrations/deployment SQL and does not migrate databases in cache requests. The payload generation supplies the entry token; real collection counters and operation concurrency revisions remain.

The filesystem provider uses a dedicated native sharded journal/index/manifest format and existing authority/durability primitives. Uncertain committed state cannot become an older readable generation or a timeout-based ownership takeover. Explicit offline recovery requires an inspected StoreId and externally established termination/fencing of every participant on every host, including drained server replay. Recovery persists a new fence/generation before cleanup. Unverifiable state stays unavailable or requires a separately selected disposable-cache reset.

Reset still requires a valid checked root identity/profile. An unidentifiable root is not adopted or reset by guessing metadata; operators select a new dedicated empty root and rewarm, or manage disposal independently outside these APIs.

### Measurement and qualification

Measure complete paths with their required validity, source ownership, serialization, integrity, Result handling, and durability costs. Report actual successful entries, operations, source invocations/joins, latency, allocations, resource accounts, provider I/O, errors, environment, and run variation where supported by the measurement.

Qualify each database engine and Windows/Linux Local, SMB, and NFS profile separately. A dry run, local unit test, generated diagram, or retained-object getter does not certify cross-host recovery, power-loss persistence, larger-than-RAM scale, or a different provider workload. No universal numeric provider ranking is part of this decision.

## Alternatives considered

- Store payloads in Blob Storage and metadata elsewhere. This permits separate scaling but adds cross-store commit, cleanup, failure reconciliation, and latency.
- Hide all providers behind minimal get/set. This is a small API but cannot express required snapshots, conditional publication, indexed invalidation, or shared ownership truthfully.
- Fan out one consumable source stream or add temporary uncached payload storage. This avoids confirmed cache publication but couples readers or introduces another storage/lifetime contract outside the chosen provider.
- Roll back uncertain metadata or steal timed-out authority. This restores availability at the cost of acknowledged invalidation and exclusive ownership guarantees.
- Ship runtime migrations or auto-create tables during requests. This conflicts with consumer context, naming, engine, deployment, and readiness ownership.

## Consequences

Native providers can enforce publication conditions where their authority lives. Each provider must implement a larger complete contract and prove its capabilities. Moving a collection to another provider is a configuration/deployment choice for disposable data, not an automatic durable-data migration.

Applications own source transaction/invalidation delivery and copy shared typed results before mutation. Hosts own EF deployment and native maintenance windows. Fail-closed recovery can reduce availability or discard disposable data. Finite admission can reject work, and process limits multiply across nodes. Complete-path measurements take more infrastructure and some deployment profiles remain explicitly unqualified.

## References

- [Provider reference](../features-application-caching.md#providers-and-deployment)
- [Existing EF migration decision](0007-entity-framework-core-code-first-migrations.md)
- [Filesystem storage composition](0037-filesystem-storage-provider-composition.md)