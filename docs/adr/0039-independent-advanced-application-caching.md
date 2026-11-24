# ADR-0039: Keep Advanced Application Caching Independent

## Status

Accepted. Recorded 2026-11-10. This records the accepted feature boundary; deployment qualification is separate.

## Context

Existing `Common.Caching` consumers need a minimal cache abstraction. Advanced caching adds declared collections, metadata indexes, source ownership, streams, and publication conditions. Key/Value Storage owns application state under a different backend and local-read consistency contract.

## Decision

Keep `Application.Caching`, `Common.Caching`, and Key/Value Storage independent in contracts, registration, configuration, storage format, and operational state. Preserve `ICacheProvider`, `AddCaching()`, existing providers, and existing consumers. Do not require a rename or migration when adding either new feature.

Application services call cache collection handles explicitly. HTTP, Dashboard, Console, and MCP access are independently enabled presentation capabilities over common Application operations.

Modules contribute registrations and configuration sections to one host-wide `Application.Caching` startup builder, catalog, runtime, and worker set through repeated `AddApplicationCaching` calls. The catalog validates and freezes after composition, before cache use. Contract, provider, and collection aliases remain globally unique, so modules use qualified names. Global options and defaults belong to host composition; module policies belong to named collections.

Named EF provider registrations bind each module's provider to its own registered `IDbContextFactory<TContext>`. SQL Server context registration can opt into that factory through `WithDbContextFactory()`, sharing native configuration while keeping request-scoped interceptors on scoped contexts. Cache operations own fresh factory contexts and never borrow the request's scoped context.

Use strongly typed fluent setup for ordinary registration. A typed collection can use finite in-memory and JSON defaults without a separate value-contract declaration; custom codecs, approved immutable sharing, and configuration-only collection contracts remain code-registered capabilities. A value contract can snapshot supplied JSON settings with `WithJsonOptions`. The conventional configuration section is `Caching:Application`. Root configuration overloads apply that convention with or without a setup callback. Named dot or colon section paths support module contributions, while explicitly supplied `IConfigurationSection` instances are read verbatim. Inherited resource defaults adapt to their provider and process ceilings; explicit invalid limits fail validation.

Keep shared value codecs in cache-independent Common.Serialization. Typed collection results are read-only and may be shared without cloning. Consumers copy before mutation, including nested mutable collections, and cannot depend on reference identity. Immutable decoded hot retention requires additional trusted approval and conservative graph accounting; the public read-only contract applies with hot retention off too.

## Alternatives considered

- Replace the minimal API with the advanced engine. This reduces the number of cache APIs but changes existing consumers and forces advanced policy/lifecycle costs into small use cases.
- Treat Key/Value Storage as the cache backend contract. This shares get/set terminology but conflates authoritative state with disposable derived values and eventual local reads with strict cache validation.
- Clone every typed result at the feature boundary. This isolates mutation but adds unavoidable work for every follower and needs a deep-copy contract for arbitrary graphs. The accepted read-only contract keeps copying with the consumer that needs mutation.

## Consequences

Hosts can select the appropriate feature per use case and use them together. Some concepts and Common primitives overlap, but that overlap does not create automatic adapters or migration obligations. Future consolidation needs its own compatibility decision.

Applications must model shared answers as immutable or make their own copies. A permitted null or empty value is a positive result; confirmed absence and source failure remain separate outcomes. This choice saves per-follower cloning without promising a universal zero-allocation result path.

## References

- [Feature guide](../features-application-caching.md)
- [Clean Architecture](0001-clean-onion-architecture.md)