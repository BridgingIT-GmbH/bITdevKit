# ADR-0044: Add Feature-Owned Storage Dimensions

## Status

Accepted. Recorded 2026-11-16. The final decision pass has settled the design, comparison semantics, limits, and qualification targets in the [specification](../specs/spec-storage-dimensions.md). Runtime implementation and real-engine performance qualification remain pending.

## Context

Blob Storage and Document Storage already persist JSON-based `Properties`. A separate [Properties alignment plan](../../plan/pln-refactor-storage-properties-alignment-1.md) first standardizes scalar persistence, preserve/replace/clear behavior, and private transform-metadata separation. Dimensions must retain that resulting descriptive/content metadata contract. Applications also need explicitly supplied, queryable dimensions whose names are not known before runtime.

The features remain independent. DevKit configures tables through EF annotations and supports SQL Server, PostgreSQL, and SQLite, while cloud/filesystem providers do not necessarily offer the same query operations. Many records with several dimensions require attention to index width, maintenance, dictionary contention, and bounded querying.

## Decision

After the prerequisite alignment, add optional `Dimensions` alongside `Properties`. Dimensions does not automatically promote/copy properties, query their JSON, restrict their types, or change their aligned stored format. A name may occur independently in both collections. Properties-only operations perform no dimension-table work.

Give each feature its own typed Dimensions collection/value/query contracts, validation, capability model, dictionary/value entities, translator, and tests. Existing general-purpose Common utilities may be reused. Introduce no shared Dimensions engine, base entity, query AST, name dictionary, or cross-feature dependency.

Keep the public dimension types to `int`, `decimal`, `bool`, `string`, and UTC `DateTime`, plus explicit null. Internally, store integer values, Boolean `0`/`1`, and UTC ticks in `long? IntegralValue`, distinguished by `ValueType`. Use `DecimalValue` and `StringValue` for the remaining types. Declare three ordinary value search indexes rather than five.

Decimals use `[Precision(18, 6)]` and reject overflow or precision loss before I/O. SQL Server/PostgreSQL target indexed decimal equality and ranges; SQLite initially targets verified indexed equality only. String values use case-sensitive ordinal equality, with significant spaces and Unicode normalization forms. Accept at most 256 UTF-8 bytes and index their uppercase hexadecimal representation in `[MaxLength(512)]`, `[Unicode(false)]` string columns, retaining original text in snapshots. String ordering is outside the initial profile.

Names match `[A-Za-z_][A-Za-z0-9_.:-]{0,127}` and canonicalize ASCII letters to uppercase for identity and `NameHash`. Preserve supplied spelling in snapshots, reject duplicate canonical names, and keep existing Properties naming unchanged. Dictionary scope columns retain the existing EF container/type identity and 256-character bounds.

On content replacement, omitted/null Dimensions preserves the existing collection, empty clears it, and non-empty replaces it. Dedicated Dimensions updates require an explicit collection. New insertions with omitted/empty Dimensions register no names and create no values.

Each EF feature has one dimension-name dictionary and one dimension-value table. The dictionary owns generated `long Id`, scope, original name, and 32-byte scope/name hashes. Name hashes are called `NameHash`; canonicalization happens in memory and no normalized-name column is persisted. Scoped hash lookup plus logical identity verification resolves a compact `NameId`. The value table stores owner ID, `NameId`, logical type, and the three nullable slots; it repeats no name strings or hashes.

Automatically register names supplied in a dimension write and index every accepted dimension on a capable backend. Names need no application registration or per-name migration. Scope dictionaries by the provider's existing persisted namespace, not by a conflicting client-name identity. Resolve name races safely, cache positive mappings only after commit, and batch lookups. Retain immutable dictionary entries without ID reuse or per-value reference-count writes.

Do not impose a dictionary-name quota. Applications manage cardinality, and retained dictionary storage may grow without a fixed ceiling. Bound individual collections/queries to 64 Dimensions, 16 predicates, 16 groups, and four group levels. Initially cap each provider instance's positive cache at 4,096 entries, new-name registration concurrency at four mutations, and registration race resolution at three attempts. Existing-name writes need no registration permit or dictionary-count check.

The database generates each dictionary's `long Id` using its EF numeric-key mapping, expressed through `[Key]` and `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]`. The EF provider resolves/inserts names and attaches the resulting `NameId` foreign keys in the owning transaction. Applications supply names/values only; they configure no ID generator, dictionary seeds, name registrations, triggers, or stored procedures. Normal EF migrations create the initial schema, and new dimension names thereafter require data changes only.

Configure tables, keys, relationships, bounds, and ordinary composite indexes using standard annotations and EF conventions. Use `(OwnerId, NameId)` as each value primary key and `(NameId, ValueType, SearchValue, OwnerId)` for each typed search index. Dictionary primary/unique indexes are additional. Do not require filtered indexes, includes, SQL computed columns, custom conventions, or a host model-builder extension.

Add a separate nullable Dimensions snapshot on each parent for optimized metadata reads and mutation comparison. Existing Properties JSON remains untouched. Commit the Dimensions snapshot, any name registration, and changed value rows with the owning mutation. Update only the delta and preserve Properties/content during dimension-only updates. Reuse the whole-item ETag established by PR #124: Dimensions mutations advance the same owner version as content, Properties, and expiration. Preserve exact supplied conditions, atomic create-only behavior, conflict handling for deleted/stale owners, committed-version responses, and guarded compensation; no independent dimension ETag or unconditional conflict retry is introduced. No Dimensions means no name/value rows or extra dictionary/value access on ordinary paths.

Initially support Dimensions persistence and queries only in the EF provider on SQL Server, PostgreSQL, and SQLite, with verified per-engine operator capabilities. All other providers, including InMemory, Azure, filesystem, Cosmos, and unqualified EF engines, advertise both storage/query support as false and reject explicit dimension inputs, updates, and filters before I/O. Their Properties and key-query operations remain unchanged. No non-EF dimension fields, evaluator, implicit property scan, cloud shadow catalog, or filesystem index is introduced. Keep existing logical-key ordering, paging, expiration, concurrency, and content-integrity semantics.

Implement as an additive feature; breaking changes needed for new Dimensions/context adoption are permitted. No Dimensions backfill or compatibility reader is needed. Existing Properties require neither rewriting nor conversion into indexed values.

## Alternatives considered

- **Refactor JSON Properties into queryable metadata.** Rejected because the user chose to keep simple Properties intact and supply Dimensions explicitly.
- **Computed columns or fixed business-dimension columns.** Efficient for known paths but require schema changes as new names appear.
- **Five typed search columns/indexes.** Keeps one physical slot per public type but repeats every value row across five indexes. Three search slots preserve logical types with fewer maintained search indexes.
- **Name hash repeated in every value key.** Fixed-width lookup, but duplicates a 32-byte digest across many rows/indexes. A dictionary adds name-registration work while allowing compact `NameId` references on the high-volume path.
- **Separate value tables per scalar type.** Narrow rows but more entities/lifecycle paths. Use one value table per feature with three nullable slots.
- **Filtered indexes or a shared feature engine.** Can reduce some costs, but conflict with the annotations-only and independent-feature requirements.
- **Filter after paging or scan JSON Properties.** Produces wrong results/counts or changes the Properties contract. Rejected.

## Consequences

Properties retain the behavior established by the prerequisite alignment. Applications opt into searchable Dimensions per record, with runtime names and explicit type/operator capabilities. Compact name IDs and three slots reduce repeated search-key material relative to the earlier five-index proposal, but no speedup or capacity is established until measured.

There are two new tables per feature, three value search indexes plus value/dictionary key indexes, and a separate parent snapshot. All three ordinary search indexes still contain all value rows, including null entries. Name registration introduces concurrency, cache invalidation, and application-managed dictionary growth. Snapshots and values duplicate some data and must commit together. Direct SQL can bypass type/scope validation.

The schema/semantics proof precedes runtime translation. Qualify each feature at up to one million owners with eight Dimensions per populated owner, measuring latency, allocations, storage/index bytes, and concurrent work against the specification's reference targets. Those targets are not measured guarantees. Existing logical-key result ordering can still cause sorting after value lookup. Validate populated workloads and real-engine plans; fewer indexes alone do not prove query scalability.

## References

- [Dimensions specification](../specs/spec-storage-dimensions.md)
- [Properties alignment implementation plan](../../plan/pln-refactor-storage-properties-alignment-1.md)
- [EF code-first migrations](0007-entity-framework-core-code-first-migrations.md)
- [Document keyset and expiration indexes](0036-keyset-and-filtered-indexes-for-ef-documents.md)
- [Filesystem provider composition](0037-filesystem-storage-provider-composition.md)

- [ETag conditional updates, PR #124](https://bridgingitgruppe.ghe.com/bit-intern/bITdevKit/pull/124)

## Cosmos mapping exception

Accepted during implementation: EF Cosmos contexts exclude `StorageDocument.DimensionValues` and `StorageDocument.DimensionsJson` with `modelBuilder.ExcludeDocumentDimensions()` in their existing Cosmos-specific `OnModelCreating` configuration. This preserves the unsupported Dimensions boundary without discovering relational dimension entities. SQL Server, PostgreSQL, and SQLite continue to use annotations alone; no Dimensions model-builder call is required for them. This exception does not enable Dimensions on Cosmos or change the separate native Cosmos provider.
