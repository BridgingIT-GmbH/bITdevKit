---
goal: Document Storage continuation-token paging and Result-native APIs
version: 1.0
date_created: 2026-06-16
last_updated: 2026-06-16
owner: bITdevKit maintainers
status: 'Implemented'
tags: feature, storage, documents, paging, result
---

# Introduction

![Status: Implemented](https://img.shields.io/badge/status-Implemented-green)

This plan implements the Document Storage continuation paging design from `docs/specs/spec-application-storage-documents-paging.md`. The work removes unbounded public document reads, replaces non-Result document client and provider methods with Result-native operations, adds provider-neutral query/page models and query builders, and updates all document-store providers, behaviors, cache integrations, tests, and examples.

## Implementation Status

Updated 2026-06-16:

- Done: shared query/page/count/options/capability/token/hash/validator models; typed document-store Result errors; Result-native `IDocumentStoreClient<T>` and `IDocumentStoreProvider`; removal of public unbounded document-store APIs; client behaviors; cache integrations; InMemory provider paging; EF/Azure Table/Azure Blob/Cosmos document-store provider Result APIs; Cosmos native feed paging APIs; Azure Table and Azure Blob SDK continuation-token page reads; test migrations and targeted verification.
- Verified: `dotnet build --nologo /p:UseSharedCompilation=false`; application document-storage unit/integration tests; infrastructure document-provider integration filter.
- Pending: none known from the original spec.

## 1. Requirements & Constraints

- **REQ-001**: Replace `IDocumentStoreClient<T>` in `src/Application.Storage/Documents/IDocumentStoreClient.cs` with only `GetResultAsync`, `FindPageResultAsync`, `ListPageResultAsync`, `CountResultAsync`, `ExistsResultAsync`, `UpsertResultAsync`, and `DeleteResultAsync`.
- **REQ-002**: Replace `IDocumentStoreProvider` in `src/Application.Storage/Documents/IDocumentStoreProvider.cs` with the same Result-native operation set plus a `DocumentStoreProviderCapabilities Capabilities` property.
- **REQ-003**: Remove all public unbounded `FindAsync`, `ListAsync`, `CountAsync`, `ExistsAsync`, `UpsertAsync`, and `DeleteAsync` document-store APIs instead of keeping obsolete compatibility shims.
- **REQ-004**: Add `DocumentQuery`, `DocumentCountQuery`, `DocumentPage<T>`, `DocumentKeyPage`, `DocumentQueries`, `DocumentQueryBuilder`, and `DocumentCountQueryBuilder` under `src/Application.Storage/Documents/`.
- **REQ-005**: Add provider capability models under `src/Application.Storage/Documents/`: `DocumentQuerySupport` and `DocumentStoreProviderCapabilities`.
- **REQ-006**: Add `DocumentStoreOptions` under `src/Application.Storage/Documents/` with explicit defaults for `DefaultTake`, `MaxTake`, `AllowFullScans`, and `RejectClientSideFilteredQueries`.
- **REQ-007**: Add typed Result errors for invalid query shape, page size too large, full scan not allowed, unsupported filter, client-side filtering rejected, invalid continuation token, query mismatch, document not found, provider failure, serialization failure, and concurrency failure.
- **REQ-008**: Add opaque continuation token envelope serialization with provider name, token version, query hash, and provider-native token. Do not expose native continuation tokens directly to application code.
- **REQ-009**: Add shared query validation that enforces required document key shapes, `Take > 0`, `Take <= MaxTake`, full-scan opt-in, provider capability support, and query-bound continuation token checks before provider execution.
- **REQ-010**: `GetResultAsync` must return `Result<T>.Success(document)` for an exact existing document and a typed not-found failure for missing exact keys. It must never return `Result<T>.Success(null)`.
- **REQ-011**: `ExistsResultAsync` must return `Result<bool>.Success(true)` or `Result<bool>.Success(false)` and must not use not-found as a failure.
- **REQ-012**: `FindPageResultAsync` must return document payload instances and never return more than `DocumentQuery.Take` items.
- **REQ-013**: `ListPageResultAsync` must return `DocumentKey` values only and must not deserialize or download payload content in any provider.
- **REQ-014**: `CountResultAsync` must not deserialize document payload content.
- **REQ-015**: Full scans require both query-level `AllowFullScan = true` and `DocumentStoreOptions.AllowFullScans = true`.
- **REQ-016**: Client-side filtered provider behavior must fail by default when `DocumentStoreOptions.RejectClientSideFilteredQueries = true`.
- **REQ-017**: Do not use offset/page-number paging or `ResultPaged<T>` for Document Storage paging.
- **REQ-018**: Builders must construct query model instances only; builders must not execute storage operations or duplicate provider-specific validation.
- **REQ-019**: Cancellation must continue to use normal .NET cancellation behavior. Do not convert caller cancellation into Result failures.
- **REQ-020**: Public XML documentation and examples must be updated for every changed public document-store symbol.
- **CON-001**: This is a breaking change. No backward-compatible overloads or `[Obsolete]` transition is required.
- **CON-002**: Application-layer abstractions must stay provider-agnostic. Provider-native paging mechanics must remain in provider projects.
- **CON-003**: `src/Application.Storage` must not reference Infrastructure projects.
- **CON-004**: Use existing `Result` and `Result<T>` types from `BridgingIT.DevKit.Common`.
- **CON-005**: Do not change unrelated file-storage APIs.
- **CON-006**: Do not run multiple top-level `dotnet build` or `dotnet test` commands in parallel in this worktree.
- **PAT-001**: Keep document-store decorators registered through `DocumentStoreBuilderContext<T>` and Scrutor decoration.
- **PAT-002**: Preserve provider naming conventions: EF type/hash columns, Cosmos `CosmosStorageDocument`, Azure Table table naming, and Azure Blob blob names formatted as `<PartitionKey>__<RowKey>`.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Add shared document paging models, options, capabilities, errors, continuation tokens, query hashing, builders, and validators without changing provider implementations.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `src/Application.Storage/Documents/DocumentQuery.cs` with public sealed class properties `DocumentKey? DocumentKey`, `DocumentKeyFilter Filter = DocumentKeyFilter.FullMatch`, `int? Take`, `string? ContinuationToken`, and `bool AllowFullScan`; include XML documentation and usage example. | | |
| TASK-002 | Add `src/Application.Storage/Documents/DocumentCountQuery.cs` with public sealed class properties `DocumentKey? DocumentKey`, `DocumentKeyFilter Filter = DocumentKeyFilter.FullMatch`, and `bool AllowFullScan`; include XML documentation and usage example. | | |
| TASK-003 | Add `src/Application.Storage/Documents/DocumentPage.cs` with public sealed class `DocumentPage<T>` containing `IReadOnlyCollection<T> Items = []`, `string? ContinuationToken`, and `bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken)`. | | |
| TASK-004 | Add `src/Application.Storage/Documents/DocumentKeyPage.cs` with public sealed class `DocumentKeyPage` containing `IReadOnlyCollection<DocumentKey> Items = []`, `string? ContinuationToken`, and `bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken)`. | | |
| TASK-005 | Add `src/Application.Storage/Documents/DocumentQueries.cs` with static methods `Query()` returning `DocumentQueryBuilder.Create()` and `Count()` returning `DocumentCountQueryBuilder.Create()`. | | |
| TASK-006 | Add `src/Application.Storage/Documents/DocumentQueryBuilder.cs` with methods `Create`, `ForKey(DocumentKey)`, `ForKey(string, string)`, `WithFullMatch`, `WithRowKeyPrefix`, `WithRowKeySuffix`, `Take(int)`, `ContinueWith(string)`, `AllowFullScan`, and `Build`; reject null keys, `Take <= 0`, and null or whitespace continuation tokens. | | |
| TASK-007 | Add `src/Application.Storage/Documents/DocumentCountQueryBuilder.cs` with methods `Create`, `ForKey(DocumentKey)`, `ForKey(string, string)`, `WithFullMatch`, `WithRowKeyPrefix`, `WithRowKeySuffix`, `AllowFullScan`, and `Build`; reject null keys only. | | |
| TASK-008 | Add `src/Application.Storage/Documents/DocumentStoreOptions.cs` with defaults `DefaultTake = 100`, `MaxTake = 1000`, `AllowFullScans = false`, and `RejectClientSideFilteredQueries = true`; validate `DefaultTake > 0`, `MaxTake > 0`, and `DefaultTake <= MaxTake`. | | |
| TASK-009 | Add `src/Application.Storage/Documents/DocumentQuerySupport.cs` enum with values `Unsupported`, `SupportedEfficiently`, `SupportedServerSide`, and `SupportedClientSide`. | | |
| TASK-010 | Add `src/Application.Storage/Documents/DocumentStoreProviderCapabilities.cs` with properties `FullMatch`, `RowKeyPrefixMatch`, `RowKeySuffixMatch`, `FullScan`, `KeyListing`, `SupportsContinuationPaging`, `SupportsServerSideCount`, and `SupportsKeyOnlyProjection`. | | |
| TASK-011 | Add typed Result error classes and factory methods in `src/Common.Results/Errors/Builder/Errors.Storage.cs` for all expected Document Storage failures listed in `REQ-007`; keep existing file-storage errors intact. | | |
| TASK-012 | Add `src/Application.Storage/Documents/DocumentContinuationToken.cs` as an internal sealed model containing `Provider`, `Version`, `QueryHash`, and `NativeToken`. | | |
| TASK-013 | Add `src/Application.Storage/Documents/DocumentContinuationTokenSerializer.cs` with Result-native serialize and deserialize methods that Base64Url-encode JSON and return typed Result failures for malformed tokens. | | |
| TASK-014 | Add `src/Application.Storage/Documents/DocumentQueryHash.cs` that computes a stable hash from document type, operation, partition key, row key, filter, take, and allow-full-scan; exclude provider-native token and document payload content. | | |
| TASK-015 | Add `src/Application.Storage/Documents/DocumentQueryValidator.cs` with Result-native validation for `DocumentQuery`, `DocumentCountQuery`, provider capabilities, `DocumentStoreOptions`, and optional decoded continuation token query hash. | | |

### Implementation Phase 2

- GOAL-002: Replace public client and provider APIs and update cross-cutting document-store client behaviors.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-016 | Replace `src/Application.Storage/Documents/IDocumentStoreClient.cs` with the exact Result-native API from the spec and update XML examples to use `DocumentQueries.Query()`, `GetResultAsync`, and `UpsertResultAsync`. | | |
| TASK-017 | Replace `src/Application.Storage/Documents/IDocumentStoreProvider.cs` with the exact Result-native API from the spec and add `DocumentStoreProviderCapabilities Capabilities { get; }`. | | |
| TASK-018 | Replace `src/Application.Storage/Documents/DocumentStoreClient.cs` methods so each method delegates directly to the corresponding provider Result-native method and does not catch expected provider failures. | | |
| TASK-019 | Delete `src/Application.Storage/Documents/DocumentStoreClientResultExtensions.cs` after all Result extension tests are migrated or removed. | | |
| TASK-020 | Update `src/Application.Storage/Documents/Behaviors/LoggingDocumentStoreClientBehavior.cs` to implement the new API, log paged read metadata, log success/failure after inner calls, and never log raw continuation tokens or payload content. | | |
| TASK-021 | Update `src/Application.Storage/Documents/Behaviors/CacheDocumentStoreClientBehavior.cs` to implement the new API, avoid caching paged reads by default, cache only successful exact get results when existing behavior requires it, and invalidate by exact key, partition prefix, and type on writes. | | |
| TASK-022 | Update `src/Application.Storage/Documents/Behaviors/RetryDocumentStoreClientBehavior.cs` to retry only transient exceptions and not retry typed Result validation failures. | | |
| TASK-023 | Update `src/Application.Storage/Documents/Behaviors/TimeoutDocumentStoreClientBehavior.cs` to wrap the new methods and preserve caller cancellation behavior. | | |
| TASK-024 | Update `src/Application.Storage/Documents/Behaviors/ChaosDocumentStoreClientBehavior.cs` to wrap the new methods and return the inner Result values when no injected fault occurs. | | |
| TASK-025 | Update `src/Application.Storage/Documents/DocumentStoreBuilderContext.cs` only if generic constraints or examples need adjustment after the `IDocumentStoreClient<T>` API replacement. | | |

### Implementation Phase 3

- GOAL-003: Implement Result-native continuation paging for the InMemory provider and use it as the first contract-test target.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-026 | Update `src/Application.Storage/Documents/InMemory/InMemoryDocumentStoreContext.cs` to expose deterministic key-ordered query helpers that can return entities and keys without relying on provider-level unordered `HashSet` behavior. | | |
| TASK-027 | Replace `src/Application.Storage/Documents/InMemory/InMemoryDocumentStoreProvider.cs` APIs with Result-native methods and `Capabilities` values: full match, prefix, suffix, full scan, key listing, and continuation paging supported. | | |
| TASK-028 | Implement InMemory continuation tokens as query-bound keyset tokens using the last returned `(PartitionKey, RowKey)` as the native token. | | |
| TASK-029 | Implement `GetResultAsync<T>` in InMemory with exact lookup, single entity cloning, and typed not-found failure. | | |
| TASK-030 | Implement `FindPageResultAsync<T>` in InMemory by validating the query, applying the key/filter/full-scan shape, ordering by partition key then row key, taking the bounded page, cloning returned payloads, and returning a query-bound continuation token only when more keys exist. | | |
| TASK-031 | Implement `ListPageResultAsync<T>` in InMemory by returning keys only from the context and not cloning or materializing payloads. | | |
| TASK-032 | Implement `CountResultAsync<T>` in InMemory by counting matching keys without cloning payloads. | | |
| TASK-033 | Implement `ExistsResultAsync<T>`, `UpsertResultAsync<T>`, batch `UpsertResultAsync<T>`, and `DeleteResultAsync<T>` in InMemory with validation errors returned as Result failures. | | |

### Implementation Phase 4

- GOAL-004: Implement Result-native continuation paging for Entity Framework providers with keyset paging and key-only projections.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-034 | Update `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs` to expose `Capabilities` with full match, prefix, suffix, full scan, server-side count, key listing, key-only projection, and continuation paging support. | | |
| TASK-035 | Add provider constructor overloads or optional parameters to accept `DocumentStoreOptions` while preserving current DI registration call sites. | | |
| TASK-036 | Implement EF continuation tokens as query-bound keyset tokens containing the last returned partition key and row key, not an offset. | | |
| TASK-037 | Implement `GetResultAsync<T>` using `QueryExactDocument`, `AsNoTracking`, and `SingleOrDefaultAsync`; return typed not-found failure when no row exists and deserialize exactly one payload on success. | | |
| TASK-038 | Implement `ListPageResultAsync<T>` using `Select(e => new DocumentKey(e.PartitionKey, e.RowKey))`, ordered keyset predicates, and `Take(effectiveTake + 1)` to detect `HasMore`; do not select `Content`. | | |
| TASK-039 | Implement `FindPageResultAsync<T>` using ordered keyset predicates, `Take(effectiveTake + 1)`, and projection of key plus `Content` only for the returned page; deserialize at most `Take` payloads. | | |
| TASK-040 | Implement `CountResultAsync<T>` with `LongCountAsync` over the filtered query and no `Content` projection. | | |
| TASK-041 | Convert validation, serialization, EF update, timeout, concurrency, and provider exceptions into typed Result failures except caller cancellation. | | |
| TASK-042 | Update `src/Infrastructure.EntityFramework/Storage/Documents/ServiceCollectionExtensions.cs` to pass configured `DocumentStoreOptions` when available. | | |

### Implementation Phase 5

- GOAL-005: Add Cosmos native feed paging support and migrate Cosmos Document Storage to Result-native paging.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-043 | Add `src/Infrastructure.Azure.Cosmos/Provider/CosmosSqlPage.cs` with `IReadOnlyCollection<TItem> Items`, `string? ContinuationToken`, and request metadata needed by logging. | Yes | 2026-06-16 |
| TASK-044 | Extend `src/Infrastructure.Azure.Cosmos/Provider/ICosmosSqlProvider.cs` with `ReadItemsPageResultAsync` and key/projection page APIs that accept expressions, partition key value, max item count, and continuation token; do not use `skip`. | Yes | 2026-06-16 |
| TASK-045 | Implement the new Cosmos provider methods in `src/Infrastructure.Azure.Cosmos/Provider/CosmosSqlProvider.cs` using `FeedIterator`, `QueryRequestOptions.MaxItemCount`, and Cosmos continuation tokens. | Yes | 2026-06-16 |
| TASK-046 | Add projection support for `CosmosStorageDocument` key-only reads so `ListPageResultAsync` does not return or deserialize `Content`. | Yes | 2026-06-16 |
| TASK-047 | Update `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProvider.cs` with `Capabilities` and Result-native methods. | | |
| TASK-048 | Implement Cosmos continuation tokens by wrapping the native Cosmos continuation token in the shared envelope with provider name `cosmos`. | | |
| TASK-049 | Implement Cosmos `CountResultAsync<T>` using server-side count when available; otherwise count projected keys through paged reads without deserializing payload content. | | |
| TASK-050 | Convert Cosmos provider, query, serialization, concurrency, and request failures into typed Result failures except caller cancellation. | | |
| TASK-051 | Update `src/Infrastructure.Azure.Cosmos/Storage/Documents/ServiceCollectionExtensions.cs` and `CosmosDocumentStoreClientBuilderContext<T>` examples only as needed for the new client API. | | |

### Implementation Phase 6

- GOAL-006: Implement Azure Table and Azure Blob provider paging with SDK-native continuation tokens and key-only listing.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-052 | Update `src/Infrastructure.Azure.Storage/Documents/AzureTableDocumentStoreProvider.cs` with `Capabilities` where full match and prefix are supported efficiently, suffix is unsupported, full scan is server-side, key listing is supported efficiently, continuation paging is supported, server-side count is false, and key-only projection is true. | | |
| TASK-053 | Refactor Azure Table query filter creation into private methods shared by find/list/count and return typed query-not-supported failures for suffix filters instead of throwing `NotSupportedException`. | | |
| TASK-054 | Implement Azure Table `ListPageResultAsync<T>` with `QueryAsync<TableEntity>(filter).AsPages(nativeToken, take)` and return keys only. | | |
| TASK-055 | Implement Azure Table `FindPageResultAsync<T>` with `QueryAsync<TableEntity>(filter).AsPages(nativeToken, take)` and convert only returned page entities to documents. | | |
| TASK-056 | Implement Azure Table `CountResultAsync<T>` by enumerating pages and counting keys/entities without converting payloads. | | |
| TASK-057 | Implement Azure Table `GetResultAsync<T>` and `ExistsResultAsync<T>` with exact key lookup where possible. | | |
| TASK-058 | Update `src/Infrastructure.Azure.Storage/Documents/AzureBlobDocumentStoreProvider.cs` with `Capabilities` where full match and prefix are efficient, suffix is client-side, full scan is server-side, key listing is efficient, continuation paging is supported, server-side count is false, and key-only projection is true. | | |
| TASK-059 | Implement Azure Blob `GetResultAsync<T>` with direct blob lookup using `<PartitionKey>__<RowKey>` and deserialize at most one blob. | | |
| TASK-060 | Implement Azure Blob `ListPageResultAsync<T>` using blob listing prefix/full-scan pages and blob-name parsing only; do not download blobs. | | |
| TASK-061 | Implement Azure Blob `FindPageResultAsync<T>` by first paging blob names, then downloading and deserializing only blobs in the returned page. | | |
| TASK-062 | Implement Azure Blob suffix behavior: reject by default with typed client-side filtering error; support optional client-side suffix filtering only when `RejectClientSideFilteredQueries = false`. | | |
| TASK-063 | Implement Azure Blob `CountResultAsync<T>` by counting blob names only and never downloading blob content. | | |
| TASK-064 | Convert Azure request, validation, serialization, and unsupported-query failures into typed Result failures except caller cancellation. | | |
| TASK-065 | Update `src/Infrastructure.Azure.Storage/Documents/ServiceCollectionExtensions.cs` to pass configured `DocumentStoreOptions` when available. | | |

### Implementation Phase 7

- GOAL-007: Update document-store consumers, cache integration, examples, and documentation to the new API.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-066 | Update `src/Application.Storage/Caching/DocumentStoreCache.cs` to use `GetResultAsync`, `UpsertResultAsync`, and `DeleteResultAsync`; treat not-found get as a cache miss and propagate non-not-found Result failures according to existing cache-provider conventions. | | |
| TASK-067 | Update `src/Application.Storage/Caching/DocumentStoreCacheProvider.cs` to use bounded `ListPageResultAsync` loops for key enumeration and prefix invalidation; continue until `HasMore` is false. | | |
| TASK-068 | Update cache provider registrations in `src/Application.Storage/Caching/ServiceCollectionExtensions.cs`, `src/Infrastructure.EntityFramework/Storage/Caching/ServiceCollectionExtensions.cs`, `src/Infrastructure.Azure.Storage/Caching/ServiceCollectionExtensions.cs`, and `src/Infrastructure.Azure.Cosmos/Storage/Caching/ServiceCollectionExtensions.cs` only if constructor signatures change. | | |
| TASK-069 | Update `examples/DinnerFiesta/Modules/Core/Core.Application/Jobs/DinnerSnapshotExportJob.cs` to use `FindPageResultAsync` or `ListPageResultAsync` loops instead of any removed unbounded methods. | | |
| TASK-070 | Update examples in `examples/DinnerFiesta/Modules/Core/Core.Presentation/CoreModule.cs` and provider XML docs to demonstrate the Result-native API. | | |
| TASK-071 | Update `docs/features-storage-documents.md` with exact read, page find, list keys, continue page, count, exists, upsert, delete, full-scan, and error-handling examples. | | |
| TASK-072 | Update `docs/specs/spec-application-storage-documents-paging.md` status from `draft` only if maintainers want the spec to track implementation state; otherwise leave status unchanged. | | |

### Implementation Phase 8

- GOAL-008: Replace tests with provider-neutral contract coverage and provider-specific paging assertions.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-073 | Replace `tests/Application.UnitTests/Storage/Documents/DocumentStoreClientTests.cs` so it verifies each Result-native client method delegates to the matching provider method and returns the exact Result. | | |
| TASK-074 | Delete or replace `tests/Application.UnitTests/Storage/Documents/DocumentStoreClientResultExtensionsTests.cs` because `DocumentStoreClientResultExtensions.cs` is removed. | | |
| TASK-075 | Add `tests/Application.UnitTests/Storage/Documents/DocumentQueryBuilderTests.cs` covering all builder requirements from the spec. | | |
| TASK-076 | Add unit tests for `DocumentQueryValidator`, `DocumentContinuationTokenSerializer`, and `DocumentQueryHash` in `tests/Application.UnitTests/Storage/Documents/`. | | |
| TASK-077 | Add provider-neutral contract test base classes under `tests/Application.IntegrationTests/Storage/Documents/` for exact get, exists, page find, key list, continuation, full scan gating, invalid take, query mismatch, prefix, suffix capability behavior, count, upsert, delete, Result failures, and cancellation propagation. | | |
| TASK-078 | Update `tests/Application.UnitTests/Storage/Documents/InMemory/InMemoryDocumentStoreProviderTests.cs` and `tests/Application.IntegrationTests/Storage/Documents/InMemoryDocumentStoreProviderTests.cs` to run the shared contract plus deterministic in-memory ordering tests. | | |
| TASK-079 | Update `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProviderTestsBase.cs` and SQL Server, SQLite, Postgres, and Cosmos-derived EF tests to run the shared contract and assert EF keyset paging and key-only projections where observable. | | |
| TASK-080 | Update `tests/Infrastructure.IntegrationTests/Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProviderTests.cs` to verify native continuation wrapping, no `skip`, key-only projection, and query-hash mismatch failures. | | |
| TASK-081 | Update `tests/Infrastructure.IntegrationTests/Azure.Storage/Documents/AzureTableDocumentStoreProviderTests.cs` to verify `.AsPages(nativeToken, pageSizeHint)`, one-page reads, prefix support, suffix typed failure, and count without payload conversion. | | |
| TASK-082 | Update `tests/Infrastructure.IntegrationTests/Azure.Storage/Documents/AzureBlobDocumentStoreProviderTests.cs` to verify direct exact lookup, prefix listing, key-only listing without downloads, page-limited downloads for find, suffix default failure, optional suffix behavior, and count without downloads. | | |
| TASK-083 | Update document-store builder-context tests for EF, Azure Storage, and Cosmos so registered clients compile and resolve with the new API. | | |
| TASK-084 | Update document-store cache provider integration tests so cache key enumeration uses paged listing and no removed API calls remain. | | |
| TASK-085 | Run `dotnet build --nologo /p:UseSharedCompilation=false` from the repository root and fix compile errors. | | |
| TASK-086 | Run targeted test projects sequentially: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --nologo --no-build`, then application integration, then each changed infrastructure integration test project as local dependencies allow. | | |

## 3. Alternatives

- **ALT-001**: Keep old unbounded APIs as `[Obsolete]` shims. Rejected because the spec explicitly states no backward compatibility is required and unbounded reads must be removed.
- **ALT-002**: Implement paging with `Skip` and page numbers. Rejected because the spec requires continuation-token paging and EF must use keyset semantics.
- **ALT-003**: Reuse `ResultPaged<T>`. Rejected because the spec explicitly prohibits `ResultPaged<T>` for Document Storage.
- **ALT-004**: Slice provider results in `DocumentStoreClient<T>`. Rejected because providers must use native or efficient paging and `ListPageResultAsync` must not load payloads.
- **ALT-005**: Expose provider-native continuation tokens directly. Rejected because continuation tokens must be opaque, versioned, and query-bound.
- **ALT-006**: Make the fluent builders execute provider calls. Rejected because builders must only construct `DocumentQuery` and `DocumentCountQuery`.

## 4. Dependencies

- **DEP-001**: Existing `BridgingIT.DevKit.Common.Result` and `Result<T>` APIs.
- **DEP-002**: Existing `DocumentKey` and `DocumentKeyFilter` models in `src/Application.Storage/Documents/`.
- **DEP-003**: Existing provider SDKs: EF Core, Azure Cosmos DB SDK, Azure Tables SDK, and Azure Blob Storage SDK.
- **DEP-004**: Existing `ISerializer`, `SystemTextJsonSerializer`, `HashHelper`, and cache provider abstractions.
- **DEP-005**: Existing test stack: xUnit, Shouldly, NSubstitute, Testcontainers/Azurite/Cosmos test fixtures where already used.
- **DEP-006**: Existing DI registration extensions in Application Storage, Infrastructure EntityFramework, Infrastructure Azure Storage, and Infrastructure Azure Cosmos.

## 5. Files

- **FILE-001**: `src/Application.Storage/Documents/IDocumentStoreClient.cs` - replace public client contract.
- **FILE-002**: `src/Application.Storage/Documents/IDocumentStoreProvider.cs` - replace provider contract and add capabilities.
- **FILE-003**: `src/Application.Storage/Documents/DocumentStoreClient.cs` - delegate Result-native methods.
- **FILE-004**: `src/Application.Storage/Documents/DocumentStoreClientResultExtensions.cs` - remove after migration.
- **FILE-005**: `src/Application.Storage/Documents/DocumentQuery.cs` - new query model.
- **FILE-006**: `src/Application.Storage/Documents/DocumentCountQuery.cs` - new count query model.
- **FILE-007**: `src/Application.Storage/Documents/DocumentPage.cs` - new payload page model.
- **FILE-008**: `src/Application.Storage/Documents/DocumentKeyPage.cs` - new key page model.
- **FILE-009**: `src/Application.Storage/Documents/DocumentQueries.cs` - new builder factory.
- **FILE-010**: `src/Application.Storage/Documents/DocumentQueryBuilder.cs` - new document query builder.
- **FILE-011**: `src/Application.Storage/Documents/DocumentCountQueryBuilder.cs` - new count query builder.
- **FILE-012**: `src/Application.Storage/Documents/DocumentStoreOptions.cs` - new paging and query safety options.
- **FILE-013**: `src/Application.Storage/Documents/DocumentQuerySupport.cs` - new capability enum.
- **FILE-014**: `src/Application.Storage/Documents/DocumentStoreProviderCapabilities.cs` - new capability model.
- **FILE-015**: `src/Application.Storage/Documents/DocumentContinuationToken.cs` - new internal token envelope.
- **FILE-016**: `src/Application.Storage/Documents/DocumentContinuationTokenSerializer.cs` - new Result-native token serializer.
- **FILE-017**: `src/Application.Storage/Documents/DocumentQueryHash.cs` - new stable query hash helper.
- **FILE-018**: `src/Application.Storage/Documents/DocumentQueryValidator.cs` - new Result-native validator.
- **FILE-019**: `src/Common.Results/Errors/Builder/Errors.Storage.cs` - add typed Document Storage error factories and classes.
- **FILE-020**: `src/Application.Storage/Documents/Behaviors/*.cs` - update cache, logging, retry, timeout, and chaos decorators.
- **FILE-021**: `src/Application.Storage/Documents/InMemory/*.cs` - implement in-memory paging and deterministic query support.
- **FILE-022**: `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs` - implement EF Result-native paging.
- **FILE-023**: `src/Infrastructure.EntityFramework/Storage/Documents/ServiceCollectionExtensions.cs` - pass options and update XML examples.
- **FILE-024**: `src/Infrastructure.Azure.Cosmos/Provider/ICosmosSqlProvider.cs` - add native page APIs.
- **FILE-025**: `src/Infrastructure.Azure.Cosmos/Provider/CosmosSqlProvider.cs` - implement feed continuation page APIs.
- **FILE-026**: `src/Infrastructure.Azure.Cosmos/Provider/CosmosSqlPage.cs` - new page result model.
- **FILE-027**: `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProvider.cs` - implement Cosmos Result-native paging.
- **FILE-028**: `src/Infrastructure.Azure.Cosmos/Storage/Documents/ServiceCollectionExtensions.cs` - update registration examples if required.
- **FILE-029**: `src/Infrastructure.Azure.Storage/Documents/AzureTableDocumentStoreProvider.cs` - implement Azure Table Result-native paging.
- **FILE-030**: `src/Infrastructure.Azure.Storage/Documents/AzureBlobDocumentStoreProvider.cs` - implement Azure Blob Result-native paging.
- **FILE-031**: `src/Infrastructure.Azure.Storage/Documents/ServiceCollectionExtensions.cs` - pass options and update examples if required.
- **FILE-032**: `src/Application.Storage/Caching/DocumentStoreCache.cs` - migrate exact cache document operations.
- **FILE-033**: `src/Application.Storage/Caching/DocumentStoreCacheProvider.cs` - migrate key enumeration and prefix invalidation to paged listing.
- **FILE-034**: `examples/DinnerFiesta/Modules/Core/Core.Application/Jobs/DinnerSnapshotExportJob.cs` - remove any unbounded read usage.
- **FILE-035**: `docs/features-storage-documents.md` - update user-facing documentation.
- **FILE-036**: `tests/**/Storage/Documents/*.cs` - replace and expand document-store tests.
- **FILE-037**: `tests/**/Storage/Caching/*DocumentStoreCache*.cs` - update cache integration tests.

## 6. Testing

- **TEST-001**: `DocumentStoreClientTests` must verify all Result-native methods delegate to `IDocumentStoreProvider` and preserve Result success/failure values.
- **TEST-002**: `DocumentQueryBuilderTests` must verify full match, prefix, suffix, full scan, take, continuation, count queries, and local argument validation.
- **TEST-003**: `DocumentQueryValidatorTests` must verify invalid shape, invalid take, max take, full scan gating, provider capability failures, client-side filtering rejection, and continuation query mismatch.
- **TEST-004**: `DocumentContinuationTokenSerializerTests` must verify round-trip, malformed token failure, version failure, and query hash mismatch integration with validation.
- **TEST-005**: Shared provider contract tests must verify exact get, missing get typed failure, exists true/false, page find, bounded page sizes, continuation, key listing, no full scan without approval, count, upsert, delete, expected Result failures, and cancellation propagation.
- **TEST-006**: InMemory tests must verify deterministic key ordering, continuation across multiple partitions, prefix, suffix, full scan gating, and no public non-Result methods remain.
- **TEST-007**: EF tests must verify keyset paging, key-only list projection, content projection only for find pages, `LongCountAsync` behavior, prefix/suffix support, full scan gating, and exception-to-Result conversion.
- **TEST-008**: Cosmos tests must verify native continuation token wrapping, no `skip`, key-only projections, page-limited content reads, count strategy, and query-hash mismatch failure.
- **TEST-009**: Azure Table tests must verify SDK `.AsPages` usage, one page per read, prefix support, suffix typed failure, key-only list behavior, and paged count without payload conversion.
- **TEST-010**: Azure Blob tests must verify direct exact lookup, prefix listing, full scan gating, key-only list without downloads, find downloads only returned page blobs, suffix default failure, optional suffix support, and count without downloads.
- **TEST-011**: Cache tests must verify `DocumentStoreCache` exact get miss behavior, successful set/remove, `GetKeysAsync` paged enumeration, and `RemoveStartsWithAsync` deletes all pages.
- **TEST-012**: Verification commands must run sequentially from repository root: `dotnet build --nologo /p:UseSharedCompilation=false`; then targeted `dotnet test` commands for Application unit tests, Application integration tests, and changed Infrastructure integration test projects.

## 7. Risks & Assumptions

- **RISK-001**: Cosmos key-only projection may require SDK-specific query APIs that are not currently present in `ICosmosSqlProvider<TItem>`, so the Cosmos provider may need more infrastructure work than other providers.
- **RISK-002**: Azure Table and Azure Blob SDK paging behavior may return fewer items than requested in a native page. Provider methods must still return at most `Take` items and a valid continuation token.
- **RISK-003**: Existing cache behavior contains unbounded key enumeration. Migrating it to paged loops is required to avoid reintroducing full scans.
- **RISK-004**: Replacing public APIs will create broad compile failures until all providers, behaviors, tests, docs, and examples are migrated in the same feature branch.
- **RISK-005**: Tests that assert no payload loading for key listing may require instrumentation or test doubles around serializers, EF projections, and Azure SDK clients.
- **RISK-006**: EF keyset continuation tokens contain logical keys. They must be opaque at the API boundary and must not contain payload content.
- **RISK-007**: Full scan count and cache-prefix invalidation need explicit option handling so internal maintenance operations do not accidentally bypass safety rules.
- **ASSUMPTION-001**: Backward compatibility is not required because the spec states external client projects are not using Document Storage yet.
- **ASSUMPTION-002**: `DocumentStoreOptions.DefaultTake = 100` and `MaxTake = 1000` are acceptable defaults unless maintainers choose different values before implementation.
- **ASSUMPTION-003**: Existing provider storage schemas remain unchanged; this plan changes query behavior and APIs, not persisted document layout.
- **ASSUMPTION-004**: Delete remains idempotent for providers that currently ignore missing documents, but missing delete failures may be preserved where an existing provider already exposes that behavior.
- **ASSUMPTION-005**: The implementation can add provider constructor options without breaking DI registration helpers by using optional parameters or builder defaults.

## 8. Related Specifications / Further Reading

- [Document Storage Continuation Paging spec](../docs/specs/spec-application-storage-documents-paging.md)
- [Document Storage feature docs](../docs/features-storage-documents.md)
- [Repository architecture](../ARCHITECTURE.md)
- [Agent instructions](../AGENTS.md)
