---
goal: Align EF Document Storage row IDs with Blob Storage UUIDv7 keys
version: 1.0
date_created: 2026-11-25
last_updated: 2026-11-25
owner: bITdevKit maintainers
status: 'Completed'
tags: [refactor, storage, documents, entity-framework, uuidv7]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

PR #155 uses application-generated UUIDv7 values for EF Blob Storage row IDs and stores them as lowercase `char(32)` text. EF Document Storage already uses `Guid` for `StorageDocument.Id` and `StorageDocumentDimension.DocumentId`. It delegates ID generation and storage to EF Core and the database provider. Adopt the blob pattern for both document columns on fresh relational schemas.

## 1. Requirements & Constraints

- **REQ-001**. Keep the CLR type `Guid` on `StorageDocument.Id` and `StorageDocumentDimension.DocumentId`. Initialize new `StorageDocument` instances through `StorageDocument.CreateId()`, which returns `Guid.CreateVersion7()`. Configure the owner key as `ValueGeneratedNever()` and preserve any ID explicitly supplied by the caller.
- **REQ-002**. EF discovers the mapping from `[EntityTypeConfiguration]` on both entities. One converter writes `id.ToString("N")` and reads `Guid.ParseExact(value, "N")`. Both relational properties use `char(32)`, maximum length 32, non-Unicode, and fixed length. The child key stays `(DocumentId, NameId)`. Keep the `long` dictionary ID and the existing cascade rules and indexes.
- **REQ-003**. Both columns inherit the database default collation unless the host calls `UseDocumentRowIdCollation(this ModelBuilder modelBuilder, string providerName)`. With this call, use `Latin1_General_100_BIN2` for `Microsoft.EntityFrameworkCore.SqlServer`, `C` for `Npgsql.EntityFrameworkCore.PostgreSQL`, and `BINARY` for `Microsoft.EntityFrameworkCore.Sqlite`. The keys then sort by UUIDv7 timestamp across distinct milliseconds. GUIDs generated in the same millisecond can appear in any order within that millisecond.
- **REQ-004**. Keep logical identity based on document type, partition key, and row key. Preserve ETags, paging and filter tokens, public result ordering, dimension query semantics, and the retention sort by expiration then row ID. The database comparison for equal-expiration row IDs follows the new mapping.
- **REQ-005**. EF Cosmos contexts that call `ExcludeDocumentDimensions()` keep their existing Cosmos GUID and JSON ID mapping. Keep the native Cosmos provider and its persistence model independent of these relational changes.
- **CON-001**. Target fresh schemas. Use the example host's existing local development database recreation and fresh databases for tests and benchmarks. Keep the mapping and collation APIs in `Infrastructure.EntityFramework` and follow `AGENTS.md` for C# visibility and XML examples.
- **PAT-001**. Share key property mapping and provider collation lookup with Blob Storage. Keep the existing blob configuration types, extension signature, and generated schema behavior.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001. Match the blob key mapping and pass TEST-001 and TEST-002.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add public `StorageRowIdConfiguration.Configure(PropertyBuilder<Guid> property)` in FILE-001. Move the converter and four facets from `BlobRowIdConfiguration.ConfigureProperty` there and return the configured builder. Add `GetBytewiseCollation(string providerName)` as an assembly-scoped method in the same class, returning the REQ-003 collation or `null`. Delegate from the three blob configurations and `UseBlobRowIdCollation` in FILE-002. Preserve the blob owner `ValueGeneratedNever()` call. | ✅ | 2026-11-25 |
| TASK-002 | After TASK-001, add `DocumentRowIdConfiguration.Owner` and `.Dimension` in FILE-003 as public `IEntityTypeConfiguration<StorageDocument>` and `IEntityTypeConfiguration<StorageDocumentDimension>` classes. Call the shared `Configure` method for `Id` and `DocumentId`. Chain `ValueGeneratedNever()` on `Id`. Add the corresponding attributes to FILE-004. In `StorageDocument`, set `Id` to `CreateId()` in the initializer, add the documented static factory, and update the `Id` XML example. Update the child property documentation. In FILE-005, remove the obsolete generator comment above `Create` at the current line 757. Its initializer runs for single and batch inserts. | ✅ | 2026-11-25 |
| TASK-003 | After TASK-002, add and document `UseDocumentRowIdCollation` in FILE-006. Follow the blob extension's null check and chaining behavior. Resolve the name before touching the model. For a recognized provider, apply the collation to `StorageDocument.Id` and `StorageDocumentDimension.DocumentId`. Keep this call independent of `ApplyDocumentStoreConfiguration()`, which controls the filtered expiration index. | ✅ | 2026-11-25 |
| TASK-004 | After TASK-003, update `ExcludeDocumentDimensions()` in FILE-006. Keep the two existing `Ignore` calls. On `StorageDocument.Id`, clear the converter and the relational column type, maximum length, Unicode, and fixed-length facets using mutable property metadata. Keep the native Cosmos GUID mapping and application-assigned ID. | ✅ | 2026-11-25 |
| TASK-005 | After TASK-004, implement the model checks in TEST-001 and TEST-002 in FILE-007 and FILE-008 and run both commands. | ✅ | 2026-11-25 |

### Implementation Phase 2

- GOAL-002. Prove that fresh databases persist and join lowercase UUIDv7 keys, and that the regular provider paths use them. This phase depends on Phase 1.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | In FILE-009, rename `UpsertResultAsync_GeneratesDistinctRowIdsInInsertOrder` in the base and four overrides to `UpsertResultAsync_GeneratesDistinctUuidVersion7RowIds`. Assert `Guid.Version == 7`, non-empty IDs, and uniqueness for both single inserts and `UpsertManyAsync` with multiple new rows. Overwrite a row and assert its persisted `Id` is unchanged. Remove the old `AssertRowIdsInInsertOrder` hook and its SQL Server `SqlGuid` and PostgreSQL overrides. TEST-003 covers ordering with fixed IDs. | ✅ | 2026-11-25 |
| TASK-007 | Add `DocumentRowIdStorageProofTests.ExplicitCollation_PersistsAndJoinsDocumentKeysInTimestampOrder` in FILE-010, following `BlobRowIdStorageProofTests`. Implement TEST-003 with the existing Testcontainers SQL Server and PostgreSQL fixtures and an in-memory SQLite connection. | ✅ | 2026-11-25 |
| TASK-008 | In FILE-011, make `DocumentBenchmarkTemplate.AddRow` write `id.ToString("N")` when the raw template's `Id` column is `string` and pass `Guid` through when the template is from the historical `DOC_BASELINE` model. Keep `ConcurrencyVersion` a `Guid`. In current-model runs, replace `SqlServerSequential` with `UuidVersion7` and seed the ordered case using `Guid.CreateVersion7(origin.AddMilliseconds(row))`. Call the collation extension in both current SQL Server document benchmark contexts. Add `_uuidv7` before `DOC_BENCH_DB_SUFFIX` for current-model database names so a row-count match cannot reuse a native GUID schema. Preserve the existing generator, parameter names, and database names inside `DOC_BASELINE`. | ✅ | 2026-11-25 |

### Implementation Phase 3

- GOAL-003. Document the new schema, pass TEST-004 and TEST-005, and record the providers and builds verified. This phase depends on Phase 2.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-009 | Update the provider description in FILE-012 to describe annotations, UUIDv7 generation, lowercase fixed-width storage, and the optional collation for fresh schemas. Add an `[Unreleased]` entry to FILE-013 and a dated Document Storage follow-up in FILE-014. Attribute the existing benchmark measurements to the blob workload. Mark the provider-native key decision in FILE-015 as superseded. | ✅ | 2026-11-25 |
| TASK-010 | Run the integration filter in TEST-004 and verify the default-collation provider paths alongside the explicit-collation proof in FILE-010. | ✅ | 2026-11-25 |
| TASK-011 | After TASK-010, verify the benchmark changes in FILE-011 and the annotation-only WeatherFiesta registration using TEST-005. Record build and test results in this plan. | ✅ | 2026-11-25 |

## 3. Alternatives

- **ALT-001**. Keep native relational GUID columns and switch the generator. SQL Server compares the last six bytes first, so native GUID sort would put UUIDv7's timestamp behind the random suffix.
- **ALT-002**. Use big-endian `binary(16)` keys. This saves space per key and would establish a second persisted format for storage row IDs. The PR #155 text format keeps the IDs readable in SQL and supports one mapping across both features.

## 4. Dependencies

- **DEP-001**. Base the work on merged PR #155 (`a706e34e5`) and use the SDK from `global.json` and existing EF Core references from `Directory.Packages.props`.
- **DEP-002**. Use xUnit, Shouldly, SQLite, and the existing SQL Server 2022 and PostgreSQL 16 Testcontainers fixtures. The existing live EF Cosmos contract fixture requires its emulator. Run the build and test commands sequentially within the worktree.

## 5. Files

- **FILE-001**. `src/Infrastructure.EntityFramework/Storage/StorageRowIdConfiguration.cs` (new shared mapping).
- **FILE-002**. `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/BlobRowIdConfiguration.cs` and `src/Infrastructure.EntityFramework/Storage/Blobs/BlobRowIdModelBuilderExtensions.cs`.
- **FILE-003**. `src/Infrastructure.EntityFramework/Storage/Documents/Entities/DocumentRowIdConfiguration.cs` (new annotation configurations).
- **FILE-004**. `src/Infrastructure.EntityFramework/Storage/Documents/Entities/StorageDocument.cs` (`Id` at line 47) and `src/Infrastructure.EntityFramework/Storage/Documents/Entities/StorageDocumentDimension.cs` (`DocumentId` at line 20).
- **FILE-005**. `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs` (`Create` at line 758 and `SweepExpiredAsync` at line 674).
- **FILE-006**. `src/Infrastructure.EntityFramework/Storage/Documents/DocumentStoreModelBuilderExtensions.cs` (`ApplyDocumentStoreConfiguration` at line 37 and `ExcludeDocumentDimensions` at line 54).
- **FILE-007**. `tests/Infrastructure.UnitTests/EntityFramework/Storage/Documents/DocumentDimensionsModelTests.cs` and `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStorageModelTests.cs`.
- **FILE-008**. `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Dimensions/StorageDimensionsIsolatedModelProofTests.cs` and its `StorageDimensionsSchemaProofTests.cs` sibling.
- **FILE-009**. `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProviderTestsBase.cs` and its `EntityFrameworkSqlServerDocumentStoreProviderTests.cs`, `EntityFrameworkPostgresDocumentStoreProviderTests.cs`, `EntityFrameworkSqliteDocumentStoreProviderTests.cs`, and `EntityFrameworkCosmosDocumentStoreProviderTests.cs` siblings.
- **FILE-010**. `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Documents/DocumentRowIdStorageProofTests.cs` (new).
- **FILE-011**. `benchmarks/Application.Benchmarks/DocumentStorageEfSqlServerBenchmarkEnvironment.cs` (`DocumentBenchmarkIdStrategy`, both contexts, `DatabaseName`) and `benchmarks/Application.Benchmarks/DocumentStorageEfSqlServerLargeTableBenchmarks.cs` (`CreateId`, benchmark parameters, and `DocumentBenchmarkTemplate.AddRow`).
- **FILE-012**. `docs/features-storage-documents.md` (Entity Framework section, current lines 646–656).
- **FILE-013**. `CHANGELOG.md` (`[Unreleased]`).
- **FILE-014**. `docs/adr/0033-time-ordered-ef-blob-row-ids.md` (dated follow-up).
- **FILE-015**. `docs/adr/0034-sequential-ef-document-row-ids.md` (superseded decision).

## 6. Testing

- **TEST-001**. Extend FILE-007 with an annotation-only SQLite model check for documents. Assert the `Guid` CLR type, `string` provider type, `char(32)` mapping and facets on both columns, default collation metadata, owner `ValueGenerated.Never`, and unchanged composite PK and owner FK cascade. Assert that a supplied version-4 GUID survives conversion and that the default constructor yields a version-7 GUID. Run the existing blob model checks after the extraction. Command `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter 'FullyQualifiedName~DocumentDimensions|FullyQualifiedName~EntityFrameworkDocumentProperties|FullyQualifiedName~Blob|FullyQualifiedName~StorageDimensions'`.
- **TEST-002**. Extend the isolated model tests in FILE-008 to assert automatic mapping for all three relational providers. Extend the existing Cosmos schema proof in FILE-008 to compare its document-only model using `ExcludeDocumentDimensions()` with a conventional GUID-key Cosmos model. Verify that the JSON ID mapping matches and that the Dimension entities are absent. Command `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~StorageDimensionsIsolatedModelProofTests|FullyQualifiedName~StorageDimensionsSchemaProofTests'`.
- **TEST-003**. On each engine create an explicit-collation schema with two documents and a dimension on each. Insert `01890f3e-4c01-7000-8000-000000000000` before `01890f3e-4c00-7000-8000-ffffffffffff`. Assert raw and EF `ORDER BY Id` return the older key first. Read both key columns through a raw data reader and assert lowercase `N` strings and equal PK/FK values. Check a parameterized owner-child EF join and, on SQL Server, confirm the key parameter is `char(32)` and the join has no key-column cast or conversion. Delete one owner through the provider and assert exactly its child is removed.
- **TEST-004**. Run `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~EntityFrameworkDocumentStoreProviderTests|FullyQualifiedName~EntityFrameworkSqlServerDocumentStore|FullyQualifiedName~EntityFrameworkPostgresDocumentStore|FullyQualifiedName~EntityFrameworkSqliteDocumentStore|FullyQualifiedName~EntityFrameworkCosmosDocumentStore|FullyQualifiedName~DocumentRowIdStorageProofTests|FullyQualifiedName~StorageDimensions|FullyQualifiedName~StorageFilterQueryProofTests|FullyQualifiedName~BlobRowIdStorageProofTests'`. Review single and batch inserts, duplicate-key conflicts, dimension changes within one batch, dimension paging and filters, retention, and concurrency. Record live Cosmos separately if its fixture skips.
- **TEST-005**. Run `dotnet build benchmarks/Application.Benchmarks/Application.Benchmarks.csproj` and exercise a SQL Server document benchmark seed with a fresh isolated database. Check bulk copy writes canonically formatted IDs and the ordered seed sorts by the timestamp prefix. Build `examples/WeatherFiesta/WeatherFiesta.Presentation.Web.Server/WeatherFiesta.Presentation.Web.Server.csproj` and run `git diff --check`.

The targeted infrastructure unit run passed 262 tests. The provider integration run passed 200 cases. The final eight-test schema and model run passed with the conventional Cosmos GUID mapping verified in the existing model fixture. The live Cosmos fixture skipped 23 tests because its emulator is disabled.

The benchmark and WeatherFiesta server builds succeeded. A SQL Server smoke run through `DocumentStorageEfSqlServerLargeTableStore.GetAsync(1000, DocumentBenchmarkIdStrategy.UuidVersion7)` read back all 1,000 bulk-seeded IDs as lowercase UUIDv7 text in timestamp order.

## 7. Risks & Assumptions

- **RISK-001**. On SQL Server and PostgreSQL, the owner visibility and Dimension value indexes widen when a native 16-byte GUID becomes text. Keep the existing index definitions and measure storage or throughput separately before claiming any gain for Document Storage.
- **RISK-002**. Setting a bytewise collation on only one column can change joins or index use. Configure them as a pair and verify real joins on each engine.
- **ASSUMPTION-001**. Only the example apps use EF Document Storage. Their local development databases can be recreated, so the change targets fresh schemas.

## 8. Related Specifications / Further Reading

- [PR #155: Blob Storage EF GUID keys](https://bridgingitgruppe.ghe.com/bit-intern/bITdevKit/pull/155)
- [EF blob key refactor plan](pln-refactor-ef-blob-guid-keys-1.md)
- [ADR-0033: Time-ordered EF blob row IDs](../docs/adr/0033-time-ordered-ef-blob-row-ids.md)
- [ADR-0036: Keyset paging and expiration indexes for EF documents](../docs/adr/0036-keyset-and-filtered-indexes-for-ef-documents.md)
- [ADR-0044: Independent Storage Dimensions](../docs/adr/0044-independent-storage-dimensions.md)
- [Document Storage provider guide](../docs/features-storage-documents.md#entity-framework)
- [Storage Dimensions specification](../docs/specs/spec-storage-dimensions.md)
- [UUIDv7 specification](https://datatracker.ietf.org/doc/html/rfc9562#section-5.7)
