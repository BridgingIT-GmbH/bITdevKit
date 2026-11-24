---
goal: 'Align Blob Storage and Document Storage Properties before adding Dimensions'
version: '1.0'
date_created: '2026-11-16'
last_updated: '2026-11-16'
owner: 'bITdevKit maintainers'
status: 'Completed'
tags: ['refactor', 'storage', 'blobs', 'documents', 'properties']
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-green)

Align application-defined `Properties` across Blob Storage and Document Storage before implementing Dimensions. Use the document feature's null-as-preserve semantics and type-preserving scalar persistence as the baseline, and separate blob transform metadata from application Properties. Both features retain their own implementations and provider boundaries.

This plan tracks the authorized implementation. It covers Application Storage contracts/behaviors, every existing storage provider, affected consumers, tests, and documentation. Dimensions tables, dictionaries, querying, and EF-only capability work are excluded. Breaking APIs and persisted formats are allowed. No EF migration classes, schema-upgrade scripts, data conversion, backfill, legacy readers, or rollout compatibility are required; use newly created development/test storage.

## 1. Requirements & Constraints

- **REQ-001**: Keep `PropertyBag` as the public application-metadata container. Both features use case-insensitive names, preserve supplied spelling, return an empty bag for absent metadata, and return defensive snapshots.
- **REQ-002**: Use this common mutation contract for both features. On insertion, null/omitted or empty Properties mean no properties. On content replacement or metadata-only update, null/omitted Properties preserve the current bag, an explicit empty bag clears it, and a non-empty bag fully replaces it. Preserve an explicit null property value as a present entry. Merge/set/remove helpers remain explicit read-modify-write helpers, not implicit merge semantics for replacement.
- **REQ-003**: Change `BlobUpload.Properties` and `BlobPropertiesUpdate.Properties` defaults to null and accept null in `BlobValidator`. Document inputs already default to null. Never turn null into an empty bag in a wrapper before the owning provider resolves the mutation against current committed state.
- **REQ-004**: Both features support exactly the existing `PropertyBagScalarCodec` scalar domain: null, string, char, bool, byte/sbyte, short/ushort, int/uint, long/ulong, float, double, decimal, Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan, and byte arrays. Reject other objects/collections before provider I/O with each feature's typed validation/serialization error. Do not restrict Properties to Dimensions' five types or normalize property timestamps to Dimensions' UTC-only contract.
- **REQ-005**: Preserve scalar runtime type and value through .NET client/provider round trips, including scalar-looking strings, numeric widths, decimal values, GUIDs, temporal types, and byte arrays. Use `PropertyBagScalarCodec` when persistence requires text. EF blob `PropertiesJson` changes to JSON containing codec-encoded scalar strings, matching the document entity's conceptual format. Do not add a plain-JSON legacy fallback.
- **REQ-006**: Keep public HTTP Properties represented by the existing JSON object contract. Type preservation is guaranteed at the typed .NET storage boundary and in provider persistence, not inferred from untagged HTTP strings. Do not change the general `PropertyBagJsonConverter` or unrelated Common consumers to introduce a new public wire format.
- **REQ-007**: Separate blob transform/provider metadata from application Properties, as Document Storage already does. Compression/encryption metadata, logical content descriptors, key IDs, IVs, and provider bookkeeping must survive property replacement/clear without being application properties. They must not appear in public client metadata, list pages, API responses, property editors, or copy/export application-property bags.
- **REQ-008**: Add provider-facing stored blob models with separate `Properties` and `TransformMetadata`. Give application-layer blob models an internal transport channel for behavior metadata, populated only through the provider adapter and transform pipeline. Keep provider metadata off the public `IBlobStoreClient` surface and preserve each transform layer's metadata during cloning. No new dependency on Document Storage types or service implementations is allowed.
- **REQ-009**: Full content replacement installs fresh transform metadata for the new payload, including clearing old transforms when new content is untransformed. Metadata-only updates preserve transform metadata unless a configured transform explicitly updates its own logical descriptor, for example a logical content type. Preserving null application Properties must never preserve obsolete transform state for replaced bytes.
- **REQ-010**: Apply the same logical Properties contract to InMemory, EF, Azure Blob, Azure Table documents, Cosmos documents, and filesystem providers. Dimensions' EF-only restriction is unrelated. Backend physical metadata limits remain real constraints; return typed failures without truncation or partial publication.
- **REQ-011**: Resolve null-as-preserve inside the provider's existing per-owner lock, transaction/concurrency condition, or filesystem publication authority. A pre-read followed by an unconditional metadata/content write is insufficient. A transform-assisted metadata read-modify-write must also carry the ETag/version of the metadata it read and must not apply an old envelope to a newer payload generation. Preserve create-only/ETag failures, cancellation, and atomic publication. Do not add a new public conditional-upload feature or fold unrelated concurrency fixes into this plan.
- **REQ-012**: Use the same scalar-aware cloning rule on write and read boundaries: clone the bag and copy mutable byte-array values. Do not assume the existing shallow `PropertyBag.Clone()` isolates array contents. Invalid input must not partially modify stored data.
- **REQ-013**: Application Properties no longer carry transform-reserved keys. A user property resembling a former transform key cannot activate or overwrite a transform. Physical Azure/system field names remain managed backend fields outside the application bag.
- **CON-001**: Do not implement Dimensions, property querying/indexes, a name dictionary, or migration/compatibility machinery. Retain separate blob/document contracts and implementations, reusing existing Common codecs/utilities only.
- **CON-002**: Preserve existing content serialization, hashing, encryption algorithms, stream ownership, paging, batching, retention, expiration, and content-type contracts except for forwarding the new private metadata channel. Blob expiration and document expiration APIs are not unified here.
- **CON-003**: Keep annotated EF mappings; add the blob transform-metadata field through entity annotations. Recreate development/test schemas rather than authoring database upgrade migrations. Existing storage data/formats and stale sample fixtures need not remain readable.
- **CON-004**: Keep existing providers, including the Azure Blob document provider. The older document-alignment plan contains broader decisions outside this task; do not revive provider consolidation, DI redesign, or adapter replacement from that plan.
- **GUD-001**: Preserve unrelated working-tree changes. Run top-level builds/tests sequentially. Tests requiring Docker, credentials, or real shares must be reported as unrun when unavailable; local tests do not establish cloud/share qualification.

The baseline behavior table is normative:

| Operation/input | Both blob and document target behavior |
| --- | --- |
| Insert, null/omitted Properties | Empty application bag |
| Insert, empty Properties | Empty application bag |
| Replace content, null/omitted Properties | Preserve the current bag atomically |
| Metadata update, null/omitted Properties | Preserve the current bag atomically |
| Existing item, explicit empty Properties | Clear application properties only |
| Non-empty Properties | Replace the entire application bag |
| Entry value is null | Keep the entry, distinct from a missing key |
| Unsupported scalar value | Typed failure before provider I/O |

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Define the aligned contract and stored/public blob boundary. This phase depends on no other phase; completion means the target models and contract tests describe the table above. Later phases are required before declaring the feature complete.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | Update `src/Application.Storage/Blobs/Models/BlobUpload.cs`, `Models/BlobPropertiesUpdate.cs`, and `Validation/BlobValidator.cs`: nullable/default-null Properties, null-as-preserve validation, scalar validation using the existing codec domain, XML examples for explicit empty clear. Add equivalent scalar/defensive-copy boundary validation in `src/Application.Storage/Documents/DocumentStoreClient.cs` without changing document null defaults. | Yes | 2026-11-16 |
| TASK-002 | Add `StoredBlobUpload`, `StoredBlobInfo`, `StoredBlobDownload`, `StoredBlobPropertiesUpdate`, and `StoredBlobPage` under new `src/Application.Storage/Blobs/Models/StoredBlobModels.cs`. Mirror existing stored metadata/stream/page fields, add separate cloned `Properties` and `TransformMetadata`, and document ownership. Stored upload application Properties may be null to preserve; its TransformMetadata always describes the new payload and defaults to empty. Stored metadata update TransformMetadata is null to preserve or an explicit replacement supplied by a transform behavior. | Yes | 2026-11-16 |
| TASK-003 | Update `src/Application.Storage/Blobs/IBlobStoreProvider.cs` and `IBlobStoreBatchDownloadProvider.cs` to stored models. Add internal `TransformMetadata` carriers to public blob models only where needed by the same-assembly pipeline. Update `BlobStoreClient.ProviderClient` in `BlobStoreClient.cs` to clone/map stored/public models, forward null unchanged, map batch items through the same boundary, and keep private state off serialized public results. Public `IBlobStoreClient` operations retain their application-facing models. | Yes | 2026-11-16 |
| TASK-004 | Add feature-specific normalization/cloning helpers under `src/Application.Storage/Blobs/Properties/` and `src/Application.Storage/Documents/` only as required to implement scalar validation and byte-array copying. Reuse `src/Common.Serialization/PropertyBagScalarCodec.cs`; do not change its general-purpose domain or add a shared storage-property service. Cover contract/default/clone failures in `tests/Application.UnitTests/Storage/Blobs/` and `Storage/Documents/`. | Yes | 2026-11-16 |

### Implementation Phase 2

- GOAL-002: Move blob transform metadata into its private channel and preserve behavior forwarding. Depends on Phase 1. Completion means transformed content remains readable after application Properties replacement/clear, with no transform keys in public bags.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-005 | Refactor `src/Application.Storage/Blobs/Behaviors/BlobBehaviorTransform.cs`, `CompressionBlobStoreClientBehavior.cs`, and `EncryptionBlobStoreClientBehavior.cs`: read/write their metadata through the internal carrier, stop merging reserved transform keys into application Properties, and make `ApplyLogicalInfo` preserve application properties while consuming only its own transform layer. Update property-update preparation so null stays preserve and logical content-type edits retain the correct system descriptors. | Yes | 2026-11-16 |
| TASK-006 | Audit every blob-model initializer and wrapper under `src/Application.Storage/Blobs/Behaviors/`, `Content/`, `Files/`, `Properties/`, and `Transfers/`. Copy null-preserve inputs unchanged; carry private metadata only inside the provider/behavior pipeline. Update `BlobDownloadBatchScope`, cache entries, and metadata clones. Copy/move helpers operate on logical content and application properties and must not transport source IVs/key IDs as destination application properties. | Yes | 2026-11-16 |
| TASK-007 | Extend `tests/Application.UnitTests/Storage/Blobs/BlobStoreTransformBehaviorTests.cs` and behavior/helper tests: compression, encryption, both in established registration order, metadata reads/listings, explicit clear, null preserve, logical content-type edits, cache, and batched downloads. Test a user property whose name matches a former internal key and prove it neither controls transforms nor disappears from the application bag. | Yes | 2026-11-16 |

### Implementation Phase 3

- GOAL-003: Apply the storage contract to InMemory, EF, and filesystem providers. Depends on Phases 1-2. Completion means newly created storage preserves scalar values/types and transform isolation through every mutation path.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-008 | Update `src/Application.Storage/Blobs/InMemory/InMemoryBlobStoreProvider.cs` to stored models and resolve application Properties against the current entry under its existing synchronization. Keep transform metadata as separate copied state. Audit `Documents/InMemory/InMemoryDocumentStoreProvider.cs` for identical scalar validation and byte-array isolation. Preserve document expiration and concurrency behavior. | Yes | 2026-11-16 |
| TASK-009 | Update `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/StorageBlob.cs`: encode/decode Properties with per-value `PropertyBagScalarCodec`, and add a separately annotated `TransformMetadata` JSON field using the same scalar codec. Update `EntityFrameworkBlobStoreProvider.cs` projections, exact/batch reads, upload snapshots, metadata updates, leases/rollback, and result mapping to stored models. Null-preserve resolves from the current row inside the existing publication/concurrency boundary. | Yes | 2026-11-16 |
| TASK-010 | Audit `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs`, `Entities/StorageDocument.cs`, and `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosStorageDocument.cs`: retain their existing codec format and separate transform fields; correct only property-contract gaps in scalar validation, copying, null/empty handling, or bulk paths. Do not change unrelated document model/provider architecture. | Yes | 2026-11-16 |
| TASK-011 | Update `src/Infrastructure.FileSystem/Blobs/FileSystemBlobStoreProvider.cs` and blob fields/serialization under `Internal/FileSystemStore.cs` to store application Properties and transform metadata separately. Resolve preserve/replace/clear under the existing publication authority; do not alter content generations for a properties-only mutation. Audit `Documents/FileSystemDocumentStoreProvider.cs` for the same property behavior. Use a fresh provider record format in fixtures, with no old-format conversion. | Yes | 2026-11-16 |
| TASK-012 | Extend existing InMemory, EF blob model/contract/read tests, EF document provider tests, and filesystem contracts under `tests/Infrastructure.UnitTests/FileSystem/`. Verify explicit clear, null preserve, scalar type fidelity, private metadata, failed conditions, repeated overwrites, cancellation, and batch consistency. Retain bounded read paths and stream lifetime guarantees. | Yes | 2026-11-16 |

### Implementation Phase 4

- GOAL-004: Align cloud persistence and atomic preservation. Depends on Phases 1-3. Completion means provider metadata limits and races return typed failures without publishing partially aligned state.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-013 | Update `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreProvider.cs` and `AzureBlobStoreBackend.cs` to stored models. Use two managed metadata fields, `bdk_properties` and `bdk_transforms`, each containing Base64Url JSON of per-value codec strings, matching the established Azure Blob document encoding pattern. Keep content hash, expiration, tags, and other backend fields outside both application Properties and user control. Remove the old per-application-key metadata encoding path; do not add a dual reader. | Yes | 2026-11-16 |
| TASK-014 | Implement null-preserving Azure publication with staged content plus current metadata/ETag read at commit and a conditional final write. Never re-read the payload stream merely to retry a metadata race. Permit at most three conditional publication attempts for an otherwise unconditional null-preserve write, re-reading current metadata between attempts; exhausted races return the feature's conflict error without changing the prior committed blob. Existing create-only/caller conditions are not weakened or retried with a new condition. Metadata-only updates preserve the separated transform field under their current guarded update/compensation path. | Yes | 2026-11-16 |
| TASK-015 | Audit `src/Infrastructure.Azure.Storage/Documents/AzureBlobDocumentStoreProvider.cs`, `AzureTableDocumentStoreProvider.cs`, and `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProvider.cs`: preserve their separate metadata fields and existing scalar codec. Close null-preserve race/copy gaps only where necessary for this contract, using the backend's atomic conditional mutation and existing typed conflict/partial-update handling. Do not add public concurrency options, replace providers, or change unrelated expiration/query behavior. | Yes | 2026-11-16 |
| TASK-016 | Add focused Azure backend tests and update cloud integration contracts under `tests/Infrastructure.IntegrationTests/Azure.Storage/` and `Azure.Cosmos/Storage/Documents/`. Prove property preservation during a competing metadata update, bounded conflict behavior, unchanged committed state on failure, and clear/replace operations preserving usable transform state. Include encoded metadata limits and Unicode/scalar-looking string keys/values. | Yes | 2026-11-16 |

### Implementation Phase 5

- GOAL-005: Update consumers and documentation for the breaking behavior/format change. Depends on Phases 1-4. Completion means all repository call sites express intended clearing/preservation explicitly, and no obsolete property-transform coupling remains.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-017 | Audit callers of `BlobUpload`, `BlobPropertiesUpdate`, `DocumentWriteOptions`, and `DocumentPropertiesUpdate` under `src/`, `examples/`, `benchmarks/`, and `tests/`. Replace accidental default-empty clearing with preserve, and use explicit empty bags only for intended clear. Update property editors and HTTP/CLI adapters under `src/Presentation.Web.Storage/` and `src/Presentation.Cli/` so omitted/null and explicit empty input stay distinct. Do not add UI workflows beyond preserving existing functionality. | Yes | 2026-11-16 |
| TASK-018 | Update `docs/features-storage-blobs.md`, `docs/features-storage-documents.md`, package READMEs, public XML examples, and storage format fixtures. Describe the shared contract, complete scalar domain, private transform metadata, ordinary HTTP JSON caveat, and no existing-data support. Use fictional examples in documentation; update actual example code outside `docs/`. If a public schema changes, regenerate affected generated artifacts from their source instead of hand-editing them. | Yes | 2026-11-16 |
| TASK-019 | Remove dead helpers, old reserved-key merging, obsolete plain-JSON blob persistence paths, stale tests, and compatibility adapters introduced temporarily during the refactor. Keep the existing Common codec's behavior for unrelated callers unchanged. Update Dimensions references to preserve the aligned Properties baseline; implement no Dimensions APIs/entities as part of this task. | Yes | 2026-11-16 |

### Implementation Phase 6

- GOAL-006: Verify the completed alignment. Depends on Phases 1-5. Completion means focused available tests and a sequential solution build pass, external verification limits are recorded, and no migration/Dimensions code was introduced.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-020 | Run the focused commands in Section 6 sequentially, plus any example tests affected by consumer changes. Inspect integration fixtures before invoking external providers. Use `/p:UseSharedCompilation=false` when needed; restore normally if assets are missing. Record exact commands, outcomes, and external skips. | Yes | 2026-11-16 |
| TASK-021 | Run `dotnet build bITdevKit.slnx --nologo` sequentially after focused checks, then `git diff --check`. Inspect the final diff for accidental Dimensions work, migration classes/scripts, provider consolidation, unrelated Common converter changes, and leftover transform keys in application Properties paths. Validate new public provider models' XML documentation/examples. | Yes | 2026-11-16 |

## 3. Alternatives

- **ALT-001**: Keep blob replacement defaults and make document null inputs clear. Rejected because omitted metadata would erase existing properties during ordinary replacements and expiration-only edits.
- **ALT-002**: Keep natural JSON and infer CLR types from strings. Rejected because a date/GUID/numeric-looking string cannot safely identify the original caller's type.
- **ALT-003**: Restrict Properties to Dimensions' five scalar types. Rejected; Properties remain descriptive metadata with the existing broader scalar codec domain.
- **ALT-004**: Expose a public application `TransformMetadata` bag or continue merging system keys into Properties. Rejected; use provider-facing stored models and an internal pipeline carrier to keep the public bag application-owned.
- **ALT-005**: Introduce a shared blob/document property service or merge provider implementations. Rejected; reuse Common utilities while keeping feature ownership independent.
- **ALT-006**: Read existing properties before an unconditional overwrite and hope they remain current. Rejected; resolve preservation inside an atomic/concurrency boundary.
- **ALT-007**: Add migration/backfill or dual persisted-format readers. Rejected by the user; fresh data/schema adoption is sufficient.

## 4. Dependencies

- **DEP-001**: Existing `PropertyBag`, `PropertyBagScalarCodec`, `Base64UrlHelper`, results, hashing, stream helpers, and scalar types. No new NuGet dependency is required.
- **DEP-002**: Existing provider transaction/lease/ETag mechanisms and typed errors. The Azure backend request may gain internal conditions needed to preserve properties atomically, without adding a public conditional-upload feature.
- **DEP-003**: Existing compression/encryption, cache, batch download, and transfer helpers must migrate together with the provider contracts. No compatibility adapter remains at completion.
- **DEP-004**: Dimensions work starts only after this alignment is implemented and verified; it then preserves the resulting Properties contract rather than the earlier blob-specific defaults/format.
- **DEP-005**: Docker/SQL Server/PostgreSQL and cloud credentials are external test dependencies where fixtures require them. Filesystem local tests do not prove real-share durability.

## 5. Files

- **FILE-001**: `src/Application.Storage/Blobs/Models/BlobUpload.cs`, `BlobPropertiesUpdate.cs`, `BlobInfo.cs`, `BlobDownload.cs`, `BlobPage.cs`, new `StoredBlobModels.cs`, `IBlobStoreProvider.cs`, `IBlobStoreBatchDownloadProvider.cs`, `BlobStoreClient.cs`, and `Validation/BlobValidator.cs` define the changed blob boundary.
- **FILE-002**: `src/Application.Storage/Blobs/Behaviors/`, `Properties/`, `Content/`, `Files/`, `Transfers/`, and `InMemory/` contain affected forwarding, transforms, helper defaults, and in-memory persistence.
- **FILE-003**: `src/Application.Storage/Documents/DocumentStoreClient.cs`, `Models/DocumentModels.cs`, and `InMemory/InMemoryDocumentStoreProvider.cs` define the document validation/copy alignment; existing preserve defaults remain.
- **FILE-004**: `src/Infrastructure.EntityFramework/Storage/Blobs/Entities/StorageBlob.cs` and `EntityFrameworkBlobStoreProvider.cs`; document equivalents under `Storage/Documents/`; `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosStorageDocument.cs` cover persisted scalar/transform mapping.
- **FILE-005**: `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreProvider.cs`, `AzureBlobStoreBackend.cs`, document providers under `Documents/`, and `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProvider.cs` cover cloud adapters and conditions.
- **FILE-006**: `src/Infrastructure.FileSystem/Blobs/FileSystemBlobStoreProvider.cs`, `Documents/FileSystemDocumentStoreProvider.cs`, and `Internal/FileSystemStore.cs` cover separated fields and authority-based publication.
- **FILE-007**: `src/Presentation.Web.Storage/`, `src/Presentation.Cli/`, affected `examples/` and `benchmarks/` callers, and corresponding application/infrastructure/presentation tests require explicit preserve/clear behavior.
- **FILE-008**: `docs/features-storage-blobs.md`, `docs/features-storage-documents.md`, storage package READMEs, and [Dimensions draft](../docs/specs/spec-storage-dimensions.md) describe the aligned baseline. Runtime changes implement Properties alignment only; Dimensions and migration files remain excluded.

## 6. Testing

- **TEST-001**: Use one behavior matrix in separate feature-owned suites: insert absent/empty, replace absent/null/empty/non-empty, metadata-only preserve/clear/replace, explicit null entry, and case-insensitive name access.
- **TEST-002**: Round-trip every codec-supported scalar through each provider path, asserting exact CLR type and value. Include scalar-looking strings, `Guid`, all numeric widths, decimal scale, `DateTime.Kind`, temporal values, null, and byte arrays. Test byte-array mutation isolation both after writes and after reads. Untagged public HTTP strings stay strings.
- **TEST-003**: Reject nested objects/unsupported collections before provider calls. Do not silently replace null with empty in text/bytes/object/file uploads, copy/move, cache, batching, or property helpers.
- **TEST-004**: Transformed blob Properties replace/clear/preserve tests retain content readability, logical metadata, and private transform separation. Verify a former reserved-key name is ordinary application data and never controls encryption/compression. Do not change cryptographic algorithms as part of fixing these tests.
- **TEST-005**: Exercise forced publication races and failed ETags/create-only/cancellation. A null-preserve operation either uses current properties atomically or returns a typed conflict; no stale property bag or partial transformation state becomes visible. Azure bounded attempts do not replay caller streams.
- **TEST-006**: Verify metadata-only updates leave payload bytes and hashes unchanged, avoid payload transfer for blob providers, and preserve existing exact/batch read query bounds and stream disposal. Document providers that persist one whole record may read/replace that record; this refactor adds no partial-document patch API. Empty bags remain empty without allocating new property tables or indexes.
- **TEST-007**: Run `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter "FullyQualifiedName~Storage" --nologo` and `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~Storage" --nologo` sequentially. Blob/document test namespaces are `Storage` rather than their filesystem subdirectory names; do not use a filter that reports success with zero matching tests.
- **TEST-008**: Run `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~FileSystem" --nologo`, then focused EF document integration suites under `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Documents/` using `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~EntityFramework&FullyQualifiedName~DocumentStore" --nologo` when fixture dependencies are available. These tests use the `EntityFramework` namespace without the `Storage.Documents` path suffix. Run any newly added EF blob integration class by its exact fully qualified name. Confirm nonzero executed tests for every filter.
- **TEST-009**: Run cloud integration filters for the touched Azure Blob/Table/Cosmos providers only when their fixtures have credentials/services. Report unrun cases explicitly. Run affected presentation/example tests when their callers change, then the final solution build and whitespace check from TASK-021.

## 7. Risks & Assumptions

- **RISK-001**: Blob provider contracts and stored metadata are breaking changes. Batch adapters, custom providers, and model initializers can lose the internal carrier if missed; compile all consumers and test composed behaviors.
- **RISK-002**: New scalar encoding and separated metadata can exceed backend metadata limits sooner. Fail with the provider's typed size error before publication; do not truncate or silently drop entries.
- **RISK-003**: Azure conditional publication may conflict under contention. Bounded attempts are a preservation mechanism, not permission to weaken caller conditions or replace a newer record during compensation.
- **RISK-004**: Changing blob null defaults can change sample/UI intent. Audit every constructor and body binder, and require explicit empty bags when clearing is intended.
- **RISK-005**: This plan does not harmonize expiration/content-type APIs or unrelated integrity/security behavior. Report discovered independent defects separately rather than expanding the refactoring silently.
- **ASSUMPTION-001**: The user authorized implementation on 2026-11-16. Mark tasks complete only after implementation and appropriate verification.
- **ASSUMPTION-002**: No existing storage data needs preservation. Development/test resources may be recreated; no schema/data migration artifacts or compatibility readers are produced.
- **ASSUMPTION-003**: Properties stay unindexed JSON-based metadata; Dimensions remain a separate later feature with EF-only support initially.

## 8. Related Specifications / Further Reading

- [Dimensions draft](../docs/specs/spec-storage-dimensions.md) and [Dimensions ADR](../docs/adr/0044-independent-storage-dimensions.md).
- [Blob Storage guide](../docs/features-storage-blobs.md) and [Document Storage guide](../docs/features-storage-documents.md).
- [Document paging contract](../docs/specs/spec-application-storage-documents-paging.md), [batched document operations](../docs/adr/0035-batched-ef-document-reads-and-bulk-writes.md), and [filesystem publication boundaries](../docs/adr/0037-filesystem-storage-provider-composition.md).
- [Scalar codec implementation](../src/Common.Serialization/PropertyBagScalarCodec.cs), [blob public validation](../src/Application.Storage/Blobs/Validation/BlobValidator.cs), and [document write preparation](../src/Application.Storage/Documents/DocumentStoreClient.cs).

## 9. Implementation verification

Verified on 2026-11-16 in branch `docs/storage-queryable-properties-spec`. The runtime change covers Properties only. Dimensions remain a design draft. No migrations, data conversion, legacy readers, or Common converter changes were added.

The following commands ran sequentially. Test commands used `--no-build --no-restore --nologo` against the solution build; the initial compilation commands also used `/p:UseSharedCompilation=false`.

| Project / filter | Latest result |
| --- | --- |
| `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter 'FullyQualifiedName~Storage'` | 426 passed |
| `dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter 'FullyQualifiedName~Storage\|FullyQualifiedName~Infrastructure.UnitTests.FileSystem'` | 362 passed, 13 skipped |
| `dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --filter 'FullyQualifiedName~Storage'` | 105 passed |
| `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter '(FullyQualifiedName~EntityFramework&FullyQualifiedName~BlobStoreProviderContractTests)\|FullyQualifiedName~EntityFrameworkSqliteDocumentStoreProviderTests\|FullyQualifiedName~EntityFrameworkSqlServerDocumentStoreProviderTests\|FullyQualifiedName~EntityFrameworkPostgresDocumentStoreProviderTests'` | 202 passed; SQL Server/PostgreSQL containers and SQLite |
| `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~AzureBlobPublicationTests\|FullyQualifiedName~AzureBlobStoreProviderTests\|FullyQualifiedName~AzureBlobStoreProviderContractTests\|FullyQualifiedName~PropertiesServiceTests'` | 90 passed; SDK transport tests, fake backend contracts, and Azurite Blob/Table contracts |
| `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~CosmosDocumentStoreProviderTests'` | 12 passed against the Cosmos SDK emulator, including scalar properties and conditional mutations; 23 unsupported EF Cosmos tests also selected and skipped |

The initial broader infrastructure filter `FullyQualifiedName~Storage|FullyQualifiedName~FileSystem` ran 479 tests: 465 passed, 13 skipped, and one stale assertion expected plaintext inside the new codec JSON. That assertion was corrected and passed in the final 375-test focused rerun. The broader run also covered unrelated filesystem caching tests, so those slow tests were not repeated after the assertion-only correction.

The 13 infrastructure skips are existing Linux-only and SMB/NFS qualification gates. No real-share, independent-host, reconnect, or power-loss qualification was performed. Azure and Cosmos results are emulator/controlled-transport evidence, not a real-cloud deployment or performance benchmark. The new Azurite test clients explicitly use Blob API version `2023-11-03`, because the installed Azurite image rejects the SDK's newest API version; production defaults are unchanged.

Properties survive null replacements and updates; explicit empty bags clear only application metadata. Tests cover every codec scalar type, temporal kind/offset and decimal scale, mutable byte-array isolation, serialized caches, transformed listing/batch reads, former reserved-key names, HTTP omitted/null/empty bags, and failed conditions. Azure publication races retry at most three commits without replaying the source stream. Encoded metadata limits fail before publication.

Implementation clarification for TEST-006: blob metadata-only operations do not read payload content. Document providers retain their existing backend granularity; Table/Cosmos may read or replace a complete stored record while leaving payload bytes and hashes unchanged. Introducing partial-document patch APIs is outside this alignment. Cache keys use a fresh format version; cache properties use scalar-codec text rather than ordinary JSON inference.

Final `dotnet build bITdevKit.slnx --no-restore --nologo /p:UseSharedCompilation=false -v:q` passed with zero warnings and zero errors. `git diff --check` passed. The final diff retains independent feature implementations, adds no Dimensions runtime or migrations, and leaves the general Common scalar codec/JSON converter unchanged. All 21 implementation tasks are complete. No commit or push was made.

## 10. Integration with conditional updates from main

PR #124 (`6a22d2a8003c185f0d9391587e3fe07bbc95f145`) was merged into this branch after the original alignment verification. Its public `BlobUpload.IfMatchETag`, upload helper/copy conditions, whole-item document conditions, HTTP ETag transport, and guarded provider rollback are now required baseline behavior. Earlier exclusions of new public conditional-upload APIs describe the original alignment scope; they must not be used to remove PR #124's implemented APIs.

Merge resolution retains scalar-aware property snapshots and separated transform metadata while forwarding `IfMatchETag` through stored blob uploads, client mapping, transforms, helpers, and every provider. Azure null-preserve publication retries apply only when no caller ETag is supplied; explicit conditions stay fixed. Azure responses describe the committed version, and EF restoration restores both application and private metadata only while the failed mutation still owns its lease/version. The Dimensions specification records the resulting whole-item concurrency requirements for future implementation. Historical verification totals above precede this integration.

Integration verification: the combined solution build passed with zero warnings/errors. Storage filters passed 479 application tests, 415 infrastructure tests with 13 existing platform/share skips, and 293 presentation/HTTP tests. The 334-test integration selection exercised native SQL Server/PostgreSQL/SQLite blob and document concurrency contracts, Cosmos concurrency contracts, and controlled Azure/Azurite cases: 329 passed initially and five transport assertions still assumed the old default-empty bag/read count. Those tests now specify explicit empty Properties when testing commit-only conditions and account for the guarded preservation read. The final 60-test Azure transport/Azurite rerun passed completely; no production changes were needed after the initial integration run. The updated integration project also built with zero warnings/errors. Whitespace and conflict-marker checks passed. Emulator/local evidence does not establish real-share, independent-host, power-loss, or real-cloud qualification.
