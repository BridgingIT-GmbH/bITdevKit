---
goal: 'Add FilterModel queries for blob names, document keys, and storage Dimensions'
version: '1.4'
date_created: '2026-11-23'
last_updated: '2026-11-23'
owner: 'bITdevKit maintainers'
status: 'Completed'
tags: ['feature', 'filtering', 'storage', 'blobs', 'documents', 'dimensions', 'maintenance-api']
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Native blob and document queries use `BlobQuery`, `DocumentQuery`, and `DocumentCountQuery`. Those types support typed Dimensions and storage-specific key constraints. The corresponding REST searches accept the same native query types.

The `FilterModel` listing overloads and their REST maintenance routes combine key and Dimension criteria, choose key ordering, and retrieve metadata a page at a time with an opaque continuation token.

## 1. Requirements & Constraints

- **REQ-001** Add `ListPageAsync(BlobFilterScope scope, FilterModel filter, string continuationToken, CancellationToken cancellationToken = default)` returning `Task<Result<BlobPage>>` to `IBlobStoreClient` and `BlobStoreClient`. Add `ListPageAsync(DocumentFilterScope scope, FilterModel filter, string continuationToken, CancellationToken cancellationToken = default)` returning `Task<Result<DocumentInfoPage>>` to `IDocumentStoreClient<T>`, `DocumentStoreClient<T>`, `IDocumentStoreClientAccessor`, and `DocumentStoreClientAccessor<T>`. These overloads require a third argument so the native one- and two-argument calls still bind to their existing signatures. Pass `null` for the first page. Model `DocumentInfoPage` after `DocumentKeyPage` with `DocumentInfo` items. Include public XML examples for the first and next pages.
- **REQ-002** Add `BlobFilterScope(string Container, string Prefix = null, bool AllowFullScan = false)` and `DocumentFilterScope(string PartitionKey = null, string RowKeyPrefix = null, bool AllowFullScan = false)`. Resolve the named store or document client before evaluating the model. Bind blob queries to exactly one container and document queries to exactly one registered document type. If supplied, apply `Prefix`, `PartitionKey`, and `RowKeyPrefix` as mandatory constraints outside the criteria tree. Require a partition for `RowKeyPrefix`.
- **REQ-003** Accept the fields and operators below. Match the key field names and the `Dimensions.` prefix with ordinal case sensitivity. Resolve the entire suffix after `Dimensions.` as a single logical Dimension name under the existing case-insensitive name rules. Reject unknown fields, `Properties.*`, payload paths, and internal metadata as filter criteria and ordering fields.
- **REQ-004** Support nested groups that mix keys and Dimensions. Match the existing operational `FilterModelQueryExtensions` group behavior. A criterion with `logic: "or"` continues its current OR run. An `and` or the end of the group closes that run. Join the runs with AND. For example, `[A(or), B(and), C(and)]` means `(A OR B) AND C`. A group has no field or operand and has nonempty `filters`. A leaf has a field and no nested filters. Reject a null model, malformed criteria, unsupported enum values, custom filters, specifications, includes, hierarchy, and `NoTracking = false`. Cap each complete request at 16 predicates, 16 explicit groups, and four nested group levels. Apply the limits while walking the input.
- **REQ-005** Use the existing feature-owned Dimension predicate factories and provider capability checks. REST comparisons use the native `value` envelope, for example `{"type":"integer","value":2026}`. Direct .NET callers can pass the corresponding `int`, `decimal`, `bool`, `string`, or UTC `DateTime`. Decode envelopes using the existing feature-owned scalar rules. Validate before querying. Preserve type identity, exact decimals, ordinal Dimension string equality, and explicit null versus a missing Dimension. Omit `value` for `isnull` and `isnotnull`. A comparison with an untyped null is invalid.
- **REQ-006** Accept `FilterModel.Page` values of `0` and `1` for compatibility with the shared model. A missing token starts the query and a token resumes it. Resolve an omitted or zero `PageSize` to `min(DefaultTake, MaxTake, 1000)` from the named storage client's options and the query limit. Accept explicit sizes from 1 through `min(MaxTake, 1000)`. Reject other page values and sizes. Seek past the decoded key tuple in SQL and fetch one more row than the effective page size. Emit a token from the last returned row if an extra row exists. Return `items`, `continuationToken`, and `hasMore`. Perform one bounded page query without an offset or total count.
- **REQ-007** Default blob order to `Name` ascending and document order to `PartitionKey`, then `RowKey`, ascending. Accept explicit `asc` and `desc` for sortable fields. Reject duplicate order fields. Append missing logical key components in ascending order. Break collation ties with the existing exact-key hash columns in ascending order. Use `NameHash` for blobs and `PartitionKeyHash`, then `RowKeyHash`, for documents. Store the complete ordered tuple in the token. Match the seek comparisons to the SQL ordering, including the collation and direction of each term. Keep `Dimensions.*` available as predicates.
- **REQ-008** Use a versioned FilterModel continuation token carried as the optional `continuationToken` property in the REST request body alongside `filters`, `pageSize`, and `orderings`. Reuse `OpaqueContinuationTokenCodec` and the configured `IContinuationTokenProtector`. Use the distinct purposes `blob-filter-search` and `document-filter-search`. Validate an explicit operation and version in the payload for both protection modes. Bind the token to the provider, named client, document type where applicable, and a SHA-256 hash of the normalized scope, scan approval, typed criteria, ordering, and effective page size. Hash a structured representation with the native canonical Dimension value encoding. Use logical Dimension names and values in the hash. Repeat the original query settings on every page. Return the feature's invalid-continuation error for malformed, mismatched, or cross-operation tokens before database I/O.
- **SEC-001** Honor the native full-scan policy on every page. A valid explicit blob prefix or document partition scopes the entire query. Without one, require a positive key or indexed Dimension anchor in every logical branch or apply the existing full-scan consent and options checks. Derive scope and authorization from the current request. Preserve native blob listing visibility. For each document page, read the configured client clock once and apply that timestamp as the expiration cutoff. Use the same rule with protected and unprotected tokens. Keep the expiration cutoff out of the token and its query hash.
- **SEC-002** Place the new POST routes on the opt-in maintenance groups. Inherit their configured paths, authorization settings, policies, and roles. Resolve clients through the existing factories. Parameterize predicates and keep operand values out of diagnostics labels and error text. Select metadata for the requested page. Exclude blob bytes and document body columns from the query.
- **CON-001** Add methods and routes. Preserve native signatures, query JSON, response types, token format, and existing registrations. Use default interface implementations that return the existing feature-specific query-not-supported error for third-party clients and providers until they implement the new contract. Keep dashboard, CLI, and MCP entry points on their current APIs.
- **CON-002** Limit first-release support to the Dimension-capable EF profiles for SQL Server, PostgreSQL, and SQLite. This provider requirement also applies to key-only `FilterModel` requests. Return the existing unsupported-query result before listing on any other built-in provider. Apply each EF profile's current Dimension operator checks, including SQLite's decimal equality restriction. Execute filtering, seeking, ordering, and paging in the store. Keep storage schemas and packages unchanged.

| Feature | Field | Filter operators | Ordering |
| --- | --- | --- | --- |
| Blob | `Container` | `eq`, `neq` against the selected container |  |
| Blob | `Name` | `eq`, `neq`, `startswith` | `asc`, `desc` |
| Document | `PartitionKey`, `RowKey` | `eq`, `neq`, `startswith` | `asc`, `desc` |
| Both | `Dimensions.<name>` | `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `isnull`, `isnotnull` within the provider's existing type and operator rules |  |

For key values, require nonempty strings that pass the existing key length checks. Use existing hash plus logical identity semantics for exact equality. Implement `neq` as its complement. Use the provider's existing prefix semantics for `startswith`. A key anchor is `Name eq` or `Name startswith` inside the selected blob container, or `PartitionKey eq` inside the selected document type. An unscoped `RowKey` predicate, `neq`, and a container predicate do not establish an anchor. Use each native Dimension leaf's `HasPositiveAnchor` result. An AND group is anchored when at least one child anchors it. An OR group is anchored when every child anchors it.

Use the common `isnull` and `isnotnull` operators to match present Dimensions with explicit null or a non-null value, respectively. Use the native Dimensions filter for the additional `Exists` and `Missing` predicates.

## 2. Implementation Steps

### Implementation Phase 1

GOAL-001 Define the filter contract, validation, and scope policy. Complete when application tests prove the input contract and existing implementations still compile.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `BlobFilterScope` and `BlobFilterQuery` under `src/Application.Storage/Blobs/Queries/`. Add `DocumentFilterScope` and `DocumentFilterQuery` under `src/Application.Storage/Documents/`. Normalize each request into a validated query and effective page size. The query contains scope, typed criteria, ordering, and an optional validated seek tuple. Represent a Dimension leaf with the existing feature-owned predicate type. Record anchor coverage as part of validation. Keep the two native Dimension contracts independent. | Yes | 2026-11-23 |
| TASK-002 | Add the client overloads and document page model from REQ-001. Add `IBlobStoreProvider.ListPageAsync(BlobFilterQuery query, int pageSize, CancellationToken cancellationToken)` returning `Task<Result<StoredBlobPage>>`. Add `IDocumentStoreProvider.ListPageAsync(DocumentTypeIdentity type, DocumentFilterQuery query, DateTimeOffset visibilityCutoff, int pageSize, CancellationToken cancellationToken)` returning `Task<Result<DocumentInfoPage>>`. Keep all provider overload arguments required to distinguish the new signatures by arity. Define the provider-side `ContinuationToken` on each of these query results as an encoded seek tuple for client-side wrapping. Add `SupportsFilterModel` to both provider capability descriptors with default `false`. Preserve native methods and defaults. Depends on TASK-001. | Yes | 2026-11-23 |
| TASK-003 | Reuse applicable option, scope, and scan validation from `BlobQueryValidator` and `DocumentQueryValidator`. Implement FilterModel continuation tokens with the shared storage query helper using REQ-008. Check cursor arity and component formats before I/O. Accept persisted blob names that exceed the new filter input limit. Validate the provider seek tuple before issuing a continuation token. Use `BlobFilterQueryTests.cs` and `DocumentFilterQueryTests.cs` under the corresponding `tests/Application.UnitTests/Storage/` feature folders. Cover grouped anchor proofs, value types, paging limits, token binding, and round trips. Depends on TASK-001 and TASK-002. | Yes | 2026-11-23 |

### Implementation Phase 2

GOAL-002 Execute mixed filters through named clients. Depends on Phase 1. Complete when all enabled EF profiles pass mixed-query and scope tests.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Implement the `ListPageAsync` overloads in `BlobStoreClient`, `DocumentStoreClient<T>`, `DocumentStoreClientAccessor<T>`, and the document `ClientBoundary<T>` in `DocumentStorageBuilderContext`. Pass the named blob client identity through `BlobStorageBuilderContext`. Preserve public constructor signatures. Follow the existing named-client token protection pattern. Validate and unwrap incoming tokens before the provider call. Pass the current document page's cutoff from the client clock to the EF provider. Wrap the next seek tuple in a public token. Map stored blob items through `BlobStoredModelMapper`. Forward the overload through built-in decorators and reuse the native listing's logical projection for blob compression and encryption. Verify through both factories. | Yes | 2026-11-23 |
| TASK-005 | Implement the `ListPageAsync` provider overloads in `EntityFrameworkBlobStoreProvider<TContext>` and `EntityFrameworkDocumentStoreProvider<TContext>`. Start from the scoped owner rows. Verify blob container hash and name together. Reuse `ApplyQuery` for document key scope and expiration. Resolve distinct Dimension names once per query. Reuse each Dimension translator's typed predicates and indexed candidate queries. Narrow AND candidates with an exact key or indexed Dimension when available. Evaluate remaining predicates against those candidates. A deferred OR probes Dimensions by owner. A stand-alone anchored OR unions indexed candidate IDs with deduplication. Apply the seek predicate and order from REQ-007, then fetch `pageSize + 1`. Project blob metadata with `BlobInfoProjection` and document metadata with the fields needed by `ToInfo`, including `Properties` and `Dimensions`. Include the internal sort keys in the projection for the next cursor. | Yes | 2026-11-23 |
| TASK-006 | Enable `SupportsFilterModel` in each targeted EF capability builder after TEST-002 proves SQL filtering, seek comparisons, sorting, and bounded fetches on that engine. Verify metadata projections and mixed OR deduplication. Capture execution plans over populated data for the first and later pages of selective mixed key-and-Dimension queries. Leave the flag disabled for providers with failed or pending qualification. Depends on TASK-005. | Yes | 2026-11-23 |

### Implementation Phase 3

GOAL-003 Expose FilterModel queries through maintenance REST endpoints. Depends on Phase 2. Complete when both routes bind the shared request model, enforce maintenance authorization, and publish the tested OpenAPI contracts.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Add `MapFilter` to `src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.Filter.cs`. Register `POST {storeName}/search` from `BlobStorageMaintenanceEndpoints.Map`. Bind `container`, optional `prefix`, and optional `allowFullScan` from the query string. Add `StorageFilterRequest` under `src/Presentation.Web.Storage/Models/` as a storage-specific `FilterModel` subclass with an optional `ContinuationToken` property. Pass scope, model, and token to the new `IBlobStoreClient.ListPageAsync` overload. Map the response through `MapInfo` and return `BlobStorageBlobPageModel`. | Yes | 2026-11-23 |
| TASK-008 | Add `MapFilter` to `src/Presentation.Web.Storage/Documents/DocumentStorageMaintenanceEndpoints.Filter.cs`. Register `POST {clientId}/search` from `DocumentStorageMaintenanceEndpoints.Map`. Bind optional `partitionKey`, `rowKeyPrefix`, and `allowFullScan` from the query string. Bind `StorageFilterRequest` from the JSON body. Pass scope, model, and token to the new `IDocumentStoreClientAccessor.ListPageAsync` overload. Map metadata with `MapInfo` and return a `DocumentStorageDocumentInfoPageModel` with the same page fields as the blob response. Depends on TASK-007 for the request model. | Yes | 2026-11-23 |
| TASK-009 | Give each route a unique `.FilterSearch` operation name under its maintenance name prefix. Return the cursor page on HTTP 200, including empty results. Use the existing error mapping for HTTP 400, including invalid continuations, and unknown-client HTTP 404 responses. Use `.WithFilterSchema(isRequestBody: true)` and a storage-route OpenAPI transformer to add `continuationToken` and describe `value` as a key string or typed Dimension object. Document allowed fields, nested criteria, and page size. Test authorization, both page schemas, and distinct read and maintenance paths. Depends on TASK-007 and TASK-008. | Yes | 2026-11-23 |
| TASK-010 | Add .NET and REST examples to `docs/features-storage-blobs.md` and `docs/features-storage-documents.md`. Document maintenance registration, EF-only support for all `FilterModel` queries, native query availability, configured page-size defaults, and continuation behavior. Explain that each filtered document page reevaluates expiration. Use the request below as the blob example. Add a document example that combines a key and a Dimension. Verify both against the implementation. Depends on TASK-009. | Yes | 2026-11-23 |

The maintenance and read groups share their default path prefixes. The new maintenance routes use `/{storeName}/search` and `/{clientId}/search` relative to their groups. The native searches use `/{storeName}/blobs/search` and `/{clientId}/documents/search`.

With the default blob maintenance path, `POST /_bdk/api/storage/blobs/reports/search?container=invoices` accepts this body. The `Name` prefix bounds the query, and the indexed `Year` condition narrows it further.

```json
{
  "pageSize": 25,
  "filters": [
    { "field": "Name", "operator": "startswith", "value": "issued/" },
    { "field": "Dimensions.Year", "operator": "eq", "value": { "type": "integer", "value": 2026 } }
  ],
  "orderings": [{ "field": "Name", "direction": "asc" }]
}
```

To fetch the next page, repeat the same request with the returned `continuationToken` added to the JSON body. A null token marks the end of the results.

In .NET, call `ListPageAsync(scope, filter, continuationToken: null)` on the blob or document client. Pass the returned token as the third argument to continue. The REST handlers call the same overload.

## 3. Alternatives

- **ALT-001** Adapt `FilterModel` only to `BlobQuery` and `DocumentQuery`. Their Dimension filters cannot express a single OR over key fields and Dimensions.
- **ALT-002** Use numbered pages with offsets and exact totals. The database would count all matches and skip more rows on deeper pages. Continuation tokens support sequential traversal with a bounded fetch.

## 4. Dependencies

- **DEP-001** Existing provider-specific Dimension profiles, name resolvers, indexed predicates, and scalar encodings.
- **DEP-002** `FilterModel`, the operational search grouping convention, `WithFilterSchema`, `OpaqueContinuationTokenCodec`, and `IContinuationTokenProtector` from the existing shared packages.
- **DEP-003** The opt-in Blob and Document Storage maintenance groups, existing client factories, and EF provider tests against SQLite, PostgreSQL, and SQL Server.

## 5. Files

- **FILE-001** `src/Application.Storage/Blobs/` and `src/Application.Storage/Documents/` hold the filter request, client and provider contracts, validators, client implementations, and decorator forwarding.
- **FILE-002** `src/Infrastructure.EntityFramework/Storage/Blobs/` and `src/Infrastructure.EntityFramework/Storage/Documents/` hold the EF filter queries and native Dimension translator integrations.
- **FILE-003** `src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints*.cs` and `src/Presentation.Web.Storage/Documents/DocumentStorageMaintenanceEndpoints*.cs` hold the new endpoint mappings.
- **FILE-004** Update the blob and document guides listed in TASK-010 and the test projects listed below.

## 6. Testing

- **TEST-001** In `tests/Application.UnitTests/`, exercise the new overloads through named clients. Compile existing calls such as `ListPageAsync(null, default)` and verify they still select the native client method. Cover grouping, scan approval for mixed OR, unsupported fields, typed operands, invalid ordering, invalid page values, and cancellation. Verify that key-only and mixed requests return the unsupported result on non-EF providers before listing. Verify that zero `PageSize` honors a custom `DefaultTake`, that the omitted size is capped at 1000 when `DefaultTake` and `MaxTake` are higher, and that an explicit size above the limit fails. Verify token round trips and reject native tokens, tokens from the other storage feature, wrong provider or client, changed document type or scope, changed filters or ordering, and changed effective page size. In protection mode reject tampered and unsigned tokens.
- **TEST-002** Add `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Dimensions/StorageFilterQueryProofTests.cs`. Reuse the SQLite, SQL Server, and PostgreSQL setup in `StorageDimensionsDatabaseProofTests`. Seed owners across containers, document types, and partitions. Traverse mixed AND and OR results across pages in both sort directions. Include explicit composite document ordering with mixed directions and keys that compare equal under the database collation. Assert that each match appears once, the final and empty pages return null tokens, and deleting the last returned item does not invalidate a cursor. Let a matching document after the first page's cursor expire, then verify that both protected and unprotected continuations omit it. Capture SQL execution plans over populated data for first and later pages of selective mixed queries, including OR. Verify seek predicates, bounded fetch, and indexed Dimension comparisons. Check for broad scans or sorts on those selective plans and record the findings for each engine. Verify that generated SQL omits offsets, total counts, and payload columns. Include the provider-specific decimal cases. Verify that a selective key combined with a deferred OR probes indexed Dimensions without a separate union of broad branch results. Verify SQLite pagination across a stored blob name longer than the filter input limit.
- **TEST-003** In `tests/Presentation.UnitTests/Web/Storage/StorageFilterEndpointsTests.cs`, test both maintenance POST routes using the shared JSON aliases and typed Dimension envelopes. Fetch the next page by echoing the token in the body, including a token for a maximum-length Unicode blob name. Assert 200 metadata and continuation fields, 400 invalid continuations and unsupported criteria or providers, 404 unknown client, and 401 or 403 for unauthorized callers. Register read and maintenance groups together with their default prefixes and verify each route reaches its own handler. Register only read endpoints and assert the maintenance search routes are absent. Verify OpenAPI and authorization under custom prefixes. Re-run `StorageDimensionsEndpointsTests` to preserve the native contract.
- **TEST-004** Run the affected application, infrastructure, and presentation test projects sequentially with `--filter 'FullyQualifiedName~FilterQuery|FullyQualifiedName~StorageFilterEndpoints|FullyQualifiedName~StorageDimensions'`. Run `git diff --check`. Record each real-engine run separately from SQLite and report unexecuted engine checks as pending.

### Qualification on 2026-11-23

`StorageFilterQueryProofTests` passed on SQLite, PostgreSQL 16, and SQL Server 2022. The fixture seeded 1,200 distractor owners per feature. It captured first and later mixed OR pages and selective mixed AND plans.

- SQLite used the typed value indexes and owner lookups. The captured OR plans used a temporary B-tree for ordering the result.
- PostgreSQL used owner index lookups. It scanned the small Dimension tables in this fixture.
- SQL Server used index seeks for both owner and Dimension rows in the captured plans.

The application selection passed 164 tests. The presentation storage selection passed 246 tests, and the four new endpoint tests passed again after the OpenAPI operator list was narrowed.

## 7. Risks & Assumptions

- **RISK-001** A mixed OR can degenerate into a broad scan or multiply owner rows. Prove branch eligibility before provider I/O and reuse indexed Dimension candidate queries within each branch. Verify traversal over overlapping matches.
- **RISK-002** A cursor across requests reflects live data. Inserts, deletes, expiration, and Dimension updates between pages can change membership. Keyset paging establishes the next position.
- **RISK-003** The page limit bounds the returned rows. Index selection and sorting determine how much work the database does. Compare selective mixed-query plans for first and later pages on each EF profile. Qualify sort directions, collation ties, and Dimension equality against native queries for the same inputs.
- **ASSUMPTION-001** The initial contract covers names, keys, and indexed Dimensions. The result is metadata. Content remains available through the existing item reads, and Properties remain descriptive metadata under ADR-0044.

## 8. Related Specifications / Further Reading

- [Filtering](../docs/features-filtering.md#operational-service-searches)
- [Blob Storage](../docs/features-storage-blobs.md#queryable-dimensions)
- [Document Storage](../docs/features-storage-documents.md#queryable-dimensions)
- [Independent Storage Dimensions](../docs/adr/0044-independent-storage-dimensions.md)
- [EF document keyset paging](../docs/adr/0036-keyset-and-filtered-indexes-for-ef-documents.md)
