---
title: Repository or ActiveEntity
---

# Repository or ActiveEntity

`bITdevKit` supports repositories and ActiveEntity. Repositories expose persistence through an injected dependency. ActiveEntity exposes entity-scoped persistence methods backed by a configured provider.

## Choose Repository when

- the application layer must control query composition
- different persistence implementations need separate contracts
- workflows span multiple entity types
- operations require database-specific APIs

For details, see [Domain Repositories](reference/features-domain-repositories.md).

## Choose ActiveEntity when

- operations belong to one entity type
- one configured provider can serve that entity type
- the entity API should expose common persistence operations directly
- generated specifications or the query DSL cover the required queries

For details, see [ActiveEntity](reference/features-domain-activeentity.md).

## Comparison

| Concern | Repository | ActiveEntity |
| --- | --- | --- |
| Dependency | Injected repository interface | Static entity methods backed by a provider |
| Query composition | Repository methods and specifications | Entity methods, generated specifications, and query DSL |
| Persistence contracts | Supports separate interfaces and implementations | Uses one configured provider per entity type |
| Cross-entity operations | Can be expressed through application services and repository contracts | Belong in an application service, repository, or database API |
| Shared capabilities | Typed IDs, domain events, specifications, paging, and EF Core mapping | Typed IDs, domain events, specifications, paging, and EF Core mapping |

## Decision rule

Choose `Repository` when the application layer needs explicit persistence dependencies or database-specific contracts.

Choose `ActiveEntity` for entity-scoped operations that fit one provider. A module can use both patterns.
