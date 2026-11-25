---
goal: Use Guid for EF blob row keys while preserving ordered text storage
version: 1.0
date_created: 2026-11-25
last_updated: 2026-11-25
owner: bITdevKit maintainers
status: 'Completed'
tags: [refactor, storage, blobs, entity-framework, uuidv7]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

ADR-0033 records the choice of UUIDv7 and its 32-character hexadecimal storage format to put recent writes together in SQL Server's clustered indexes. Its benchmark compared random and time-ordered string IDs. This follow-up makes the persisted column width explicit and uses `Guid` for the corresponding C# values. It assumes a fresh blob schema.

## 1. Requirements & Constraints

- **REQ-001**. Use `Guid` for `StorageBlob.Id`, `StorageBlobChunk.BlobId`, `StorageBlobDimension.BlobId`, and the internal parameters and queries that carry those identifiers. Generate new IDs with `Guid.CreateVersion7()` before insert.
- **REQ-002**. Persist each ID as exactly 32 lowercase hexadecimal characters using the GUID `N` format. Map the relational columns to `char(32)`. SQLite treats this declared type as TEXT affinity.
- **REQ-003**. Guarantee UUIDv7 timestamp ordering across distinct milliseconds on the primary key and both foreign keys when all three columns use the same explicit bytewise collation. Use `Latin1_General_100_BIN2` on SQL Server, `C` on PostgreSQL, and `BINARY` on SQLite. Set the collation on the columns when creating the schema so indexes and joins use the same comparison rules.
- **CON-001**. Apply the conversion and width automatically when a context discovers the three entities. Without an explicit collation, the columns inherit the database default. Host contexts requiring the ordering guarantee call `UseBlobRowIdCollation` in `OnModelCreating` before creating their schema.
- **CON-002**. The feature has no existing consumers or deployed schemas. Change the EF entity members directly and verify with fresh schemas. Preserve the provider-neutral `BlobKey`, client contracts, tokens, names, hashes, lease IDs, and ETags.
- **CON-003**. Preserve relational joins, cascade behavior, hash lookups, dimension query translation, and retention ordering. Preserve the preceding reliability fixes.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001. Map three strongly typed EF keys to the same ordered text representation and pass the provider model tests.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | In FILE-001 through FILE-003 change the owner PK and both FKs from `string` to `Guid`. Change `StorageBlob.CreateId()` to return `Guid.CreateVersion7()`. Keep the existing keys, FK relationships, indexes, and initializer. Remove the old length annotations on the three properties and update their XML examples. | ✅ | 2026-11-25 |
| TASK-002 | With TASK-001, add one shared `ValueConverter<Guid, string>` in FILE-004 with `id => id.ToString("N")` and `text => Guid.ParseExact(text, "N")`. Reuse it in three `IEntityTypeConfiguration<T>` configurations. Apply them from the three entities with `EntityTypeConfigurationAttribute`, following the repository's annotation-based discovery pattern. Apply `HasColumnType("char(32)")`, `HasMaxLength(32)`, `IsUnicode(false)`, and `IsFixedLength()` consistently. Use `ValueGeneratedNever()` on the owner PK to keep explicit application UUIDv7 generation. | ✅ | 2026-11-25 |
| TASK-003 | Add a documented `UseBlobRowIdCollation(this ModelBuilder modelBuilder, string providerName)` extension in FILE-005. Apply the three matching column collations from REQ-003 for `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, and `Microsoft.EntityFrameworkCore.Sqlite`. Return the builder without further configuration for other providers. Use it in the explicit ordering proof in FILE-009 and the SQL Server benchmark context in FILE-011. Keep the standard setup, the WeatherFiesta context in FILE-012, and the automatic mapping model test annotation-based. | ✅ | 2026-11-25 |
| TASK-004 | In FILE-006 change `DeleteChunksAsync`, `WriteChunksAsync`, `BlobMutationLease.BlobId`, and `BlobChunkReadStream` to carry `Guid`. Use the typed PK and FK in compiled reads, lease checks, snapshots, expiry replacement, and chunk scans. In FILE-007 use `Guid` for candidate IDs and predicates and for the owner key in the dimension mutation session. Keep dimension dictionary names and IDs as they are. Confirm the existing generic filter in FILE-008 infers `TId = Guid`. | ✅ | 2026-11-25 |
| TASK-005 | After TASK-001 through TASK-004, run the targeted unit and model tests in TEST-001 and TEST-002. Check the raw SQLite retention query reads the converted keys from its existing model-derived `Id` column. Keep the retention algorithm and public listing order. | ✅ | 2026-11-25 |

### Implementation Phase 2

- GOAL-002. Verify the fresh schemas and normal blob paths on each database and record the decision with its evidence.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | Add TEST-003 to FILE-009 or a dedicated sibling fixture, using the existing SQL Server, PostgreSQL, and SQLite test setup. Cover schema column types, collation, raw hex values, UUIDv7 ordering, and PK-to-FK equality. Run the existing provider contracts and cascade/filter proofs listed in TEST-004. | ✅ | 2026-11-25 |
| TASK-007 | Check FILE-011 and its companion SQL Server large-table benchmark build against the new model. Keep text values at raw SQL and `SqlBulkCopy` boundaries, with `N` formatting applied exactly once. Keep UUID-like strings used in public blob names as names. If measuring speed or fragmentation, run the existing workload on an isolated SQL Server benchmark instance and record separately from ADR-0033's historical results. | ✅ | 2026-11-25 |
| TASK-008 | Update FILE-013 through FILE-015. Add a dated follow-up to ADR-0033 for the new CLR type, fixed text width, and selectable column collations. Preserve the original benchmark evidence and describe the scope of any new measurements. Explain the automatic converter and explicit collation configuration in the blob guide. Record the EF entity type change in the changelog. | ✅ | 2026-11-25 |

## 3. Alternatives

- **ALT-001**. Use SQL Server `uniqueidentifier` with standard UUIDv7. It provides compact, readable values, and its comparison treats the last six bytes as most significant. UUIDv7 puts its timestamp in the first six bytes. A SQL Server-specific sequential GUID generator would satisfy a different ordering requirement.
- **ALT-002**. Store 16 big-endian UUID bytes in SQL Server `binary(16)`. This keeps keys narrower. The decision favors straightforward inspection and copy/paste in SQL.
- **ALT-003**. Keep the entity properties as `string`. This preserves the database representation but keeps formatting and parsing exposed throughout the C# persistence code.

## 4. Dependencies

- **DEP-001**. Use the repository's pinned .NET and EF Core versions. Use a custom converter because the built-in `GuidToStringConverter` writes the hyphenated format.
- **DEP-002**. Use xUnit and Shouldly in existing test projects. SQL Server and PostgreSQL integration runs require the existing Docker fixtures. Run top-level builds and tests sequentially in the worktree.
- **DEP-003**. Preserve the verified reliability changes already present in the shared EF provider and test files.

## 5. Files

- **FILE-001**. `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/StorageBlob.cs`
- **FILE-002**. `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/StorageBlobChunk.cs`
- **FILE-003**. `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/StorageBlobDimension.cs`
- **FILE-004**. `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/BlobRowIdConfiguration.cs` (new)
- **FILE-005**. `src/Infrastructure.EntityFramework/Storage/Blobs/BlobRowIdModelBuilderExtensions.cs` (new)
- **FILE-006**. `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs` and `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Retention.cs` (inspect retention)
- **FILE-007**. `src/Infrastructure.EntityFramework/Storage/Blobs/Dimensions/BlobDimensionQueryTranslator.cs` and `src/Infrastructure.EntityFramework/Storage/Blobs/Dimensions/BlobDimensionNameCache.cs`
- **FILE-008**. `src/Infrastructure.EntityFramework/Storage/EntityFrameworkStorageFilterQuery.cs` and `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.Filter.cs` (verify generic reuse and token shape)
- **FILE-009**. `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Dimensions/StorageDimensionsSchemaProofTests.cs`, `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Dimensions/StorageDimensionsCascadeDeletionProofTests.cs`, and `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Blobs/BlobRowIdStorageProofTests.cs` (new)
- **FILE-010**. `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStorageModelTests.cs`, `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderRelationalReadTests.cs`, and `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderUploadDownloadTests.cs`
- **FILE-011**. `benchmarks/Application.Benchmarks/BlobStorageEfSqlServerReadBenchmarkEnvironment.cs` and `benchmarks/Application.Benchmarks/BlobStorageEfSqlServerLargeTableReadBenchmarks.cs`
- **FILE-012**. `examples/WeatherFiesta/WeatherFiesta.Infrastructure/Modules/Core/EntityFramework/CoreDbContext.cs`
- **FILE-013**. `docs/adr/0033-time-ordered-ef-blob-row-ids.md`
- **FILE-014**. `docs/features-storage-blobs.md`
- **FILE-015**. `CHANGELOG.md`

## 6. Testing

- **TEST-001**. In FILE-010 assert that generated keys are version 7 and that the converter round-trips a fixed UUID and produces exactly its lowercase `N` form. Assert that all three model properties have CLR type `Guid`, provider type `string`, and store type `char(32)`. Assert the composite keys and cascade relationships remain in the model. Use an annotation-only context for the automatic mapping check.
- **TEST-002**. Run `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter 'FullyQualifiedName~Blob|FullyQualifiedName~StorageDimensions'`. Cover the existing single and batch read query-count assertions, streamed chunks, dimension predicates, expired replacement, and lease mutations.
- **TEST-003**. With fresh schemas and explicit column collations, insert `01890f3e-4c01-7000-8000-000000000000` before `01890f3e-4c00-7000-8000-ffffffffffff`. The timestamps are one millisecond apart and sorting by the last six bytes reverses their order, so the SQL Server test detects accidental native GUID storage. Assert raw `ORDER BY Id` and EF `OrderBy(row => row.Id)` put the older ID first. Check raw parent and child IDs against the expected hex strings. Update and delete through the provider to exercise typed key parameters and cascade.
- **TEST-004**. Run `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~EntityFrameworkSqlServerBlobStoreProviderContractTests|FullyQualifiedName~EntityFrameworkPostgresBlobStoreProviderContractTests|FullyQualifiedName~EntityFrameworkSqliteBlobStoreProviderContractTests|FullyQualifiedName~StorageDimensions|FullyQualifiedName~StorageFilterQueryProofTests|FullyQualifiedName~BlobRowIdStorageProofTests'`. Check that representative SQL Server joins compare the mapped key columns directly and take properly sized text key parameters. Verify SQLite's retention query still uses the expiry index.
- **TEST-005**. Run `dotnet build benchmarks/Application.Benchmarks/Application.Benchmarks.csproj`, build the WeatherFiesta server, and run `git diff --check`. Use the pinned SDK selected by `global.json`. If the host resolves the system launcher, put the pinned runtime directory first on `PATH` for build-time OpenAPI generation.

The targeted runs passed 258 infrastructure unit tests and 199 integration tests. The benchmark project and WeatherFiesta server both built successfully. `git diff --check` passed.

## 7. Risks & Assumptions

- **RISK-001**. A native GUID mapping, a mismatched FK converter, or a native GUID SQL parameter can change ordering or introduce SQL conversions. Verify both model metadata and executed SQL against the real database engines.
- **RISK-002**. Hosts can select custom default collations. The explicit configuration in REQ-003 makes the ordering claim independent of those defaults. UUIDv7 IDs generated in the same millisecond can sort in any order within that millisecond.
- **ASSUMPTION-001**. The owner row ID is an EF persistence key. The public blob identity remains its container and name.
- **ASSUMPTION-002**. Test schemas and benchmark databases can be created fresh. The original benchmark establishes the value of ordered insertion. Any throughput or storage improvement attributable specifically to changing the column definition requires a separate measurement.

## 8. Related Specifications / Further Reading

- [ADR-0033. Time-ordered EF blob row IDs](../docs/adr/0033-time-ordered-ef-blob-row-ids.md)
- [Blob Storage reference](../docs/features-storage-blobs.md)
- [UUIDv7 specification](https://datatracker.ietf.org/doc/html/rfc9562#section-5.7)
- [SQL Server GUID comparison](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/compare-guid-uniqueidentifier-values)
- [EF Core value conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [PostgreSQL collations](https://www.postgresql.org/docs/16/collation.html)
- [SQLite text affinity and collations](https://www.sqlite.org/datatype3.html)
