---
goal: 'Apply optional ETag conditions consistently to blob and document content and metadata updates'
version: '1.0'
date_created: '2026-11-16'
last_updated: '2026-11-16'
owner: 'bITdevKit maintainers'
status: 'Completed'
tags: ['feature', 'storage', 'blobs', 'documents', 'concurrency', 'bug']
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Implement the update semantics agreed in the design discussion for Blob Storage and Document Storage. An optional `IfMatchETag` enables optimistic concurrency for the whole stored item, including its content, properties, content type where applicable, and expiration. Omitting the condition preserves unconditional writes. Dashboard edits use the version originally displayed and expose a separate Force overwrite action.

The implementation is complete. Scope includes Application Storage contracts, behaviors and helpers; Infrastructure providers; Presentation Storage endpoints and dashboards; provider tests; and feature documentation. Domain models, File Storage contracts, independent queryable-properties design, package upgrades, and storage-provider consolidation are outside scope.

Source references identify the inspected checkout on 2026-11-16. Method and type names are authoritative when line numbers move. Implementation was authorized on 2026-11-16. All tasks are complete; final verification is recorded in Section 6.

## 1. Requirements & Constraints

- **REQ-001**: Add optional `string IfMatchETag { get; init; }` to `BlobUpload`. Keep `DocumentWriteOptions.IfMatchETag`, `DocumentPropertiesUpdate.IfMatchETag`, `BlobPropertiesUpdate.IfMatchETag`, and exact-delete options optional. Do not introduce a global requirement to supply ETags.
- **REQ-002**: Treat null, empty, and whitespace-only conditions as omitted, matching existing provider checks. Omitted conditions preserve existing unconditional upload/upsert and metadata-update behavior. Metadata-only operations still require an existing item. Creation remains possible through unconditional content upload/upsert.
- **REQ-003**: A nonempty condition refers to one previously observed version at the complete storage identity. Compare it atomically with publication of the update. A mismatch rejects the update without changing the current item. A conditional content write against a missing item returns the storage feature's conflict error and never inserts an item. A conditional metadata update against a missing item must fail without inserting; preserve its existing not-found result where applicable.
- **REQ-004**: One ETag represents the entire item. Every successful provider-owned content or metadata mutation invalidates the previous ETag, even when the submitted value equals the stored value. Content changes invalidate older metadata edits, and metadata changes invalidate older content edits. Never use the content hash as the concurrency token. Deletes do not return a replacement ETag.
- **REQ-005**: Return the ETag of the version committed by the operation. Do not return the pre-update token or adopt an intervening writer's token from an unconditional read after the write. Other returned metadata must describe the same committed version.
- **REQ-006**: Reject create-only combined with a nonempty `IfMatchETag` before mutation. For blobs, create-only means `BlobOverwriteMode.FailIfExists`; for documents it means `DocumentWriteOptions.CreateOnly`. Preserve physical-record existence semantics, including expired records awaiting retention.
- **REQ-007**: Preserve the caller's condition through compression, encryption, content-type detection, byte/text/object/file uploads, bulk document writes, and copy/move destination writes. Add optional `IfMatchETag` to `BlobCopyOptions` and `DocumentTransferOptions`; it always refers to the destination. `BlobMoveOptions.Copy` carries the blob destination condition.
- **REQ-008**: Keep move source deletion conditional on the source version that was copied. A failed destination condition prevents source deletion. A changed source remains intact and the existing transfer result reports the failed deletion. Preserve same-client/same-key existence-checked no-op behavior.
- **REQ-009**: Make blob prefix deletion pass each enumerated `BlobInfo.ETag` into `BlobDeleteOptions`. Add optional conditional input to the blob maintenance DELETE endpoint. Preserve exact-delete idempotency for an already absent item and unconditional deletion when the caller supplies no condition.
- **REQ-010**: Dashboard content and metadata edits send the originally displayed ETag by default. New items use create-only. Force overwrite is an explicit update choice that omits the ETag. An ordinary dashboard edit with a missing token is invalid; it must not silently become an unconditional write. Do not fetch a new token immediately before submitting an existing draft.
- **REQ-011**: Dashboard ETag conflicts preserve the draft, selected upload file, original token, and item identity. Explain that the stored item changed. Offer Reload current item and Force overwrite. Reload replaces the draft only after explicit discard confirmation; Force overwrite submits the retained draft without a condition. Never automatically retry a stale edit with a refreshed ETag.
- **REQ-012**: Fix Azure Table conditional creation and stale returned ETags, Cosmos conditional upsert resurrection, Azure Blob metadata compensation, and the identified deletion paths. Keep the dedicated Azure Blob document provider.
- **REQ-013**: Azure multi-request blob metadata updates may fail with partial changes. Compensation must use only the last ETag confirmed as written by this operation, chaining subsequent compensation responses. If that version no longer matches, stop and return `BlobStorePartialUpdateError`. Never read the newest ETag to gain permission to restore an older snapshot. Do not add exclusive Azure leases or a cross-provider transaction protocol for this change.
- **REQ-014**: Keep ETag conflicts and partial-update failures out of automatic retries. Preserve the existing prohibition on replaying indeterminate filesystem mutations. A lost write acknowledgement does not authorize obtaining a fresh ETag and retrying or restoring.
- **SEC-001**: ETags do not replace authorization or identity validation. Preserve dashboard authorization, explicit antiforgery handling, named-store isolation, document-type isolation, exact key validation, and structured logging without secrets.
- **CON-001**: Reuse `Result`, existing conflict/validation/not-found/partial-update errors, cancellation, stream-size/hash checks, EF concurrency and leases, filesystem authority, and Azure native conditions. Keep storage orchestration in Application and persistence in Infrastructure. No new NuGet dependency or persistent-format migration is required.
- **CON-002**: Treat ETags as opaque tokens. Preserve existing Cosmos quoted/unquoted handling. Do not accept the wildcard `*` as a concrete version token; reject it at blob/document mutation boundaries so Azure wildcard semantics cannot bypass the agreed version check.
- **CON-003**: Keep upload streams caller-owned and stream-first. Enforce a condition at the final publication point after staging. A preflight existence/ETag read is insufficient. Rejected updates must not replace content, properties, expiration, hashes, or the current ETag.
- **CON-004**: Builds and test commands run sequentially in the same checkout. Use the repository-resolved SDK and package versions. Record unavailable external environments and skipped tests explicitly; local tests do not qualify SMB/NFS or cloud deployments.
- **CON-005**: Preserve unrelated working-tree changes, including the queryable-properties specification and ADR present during planning. Do not alter their storage ownership model or combine this work with their implementation.
- **PAT-001**: Use xUnit, Shouldly, and NSubstitute with behavioral assertions. Use deterministic barriers or SDK request interception for interleavings, not timing sleeps. Follow existing public XML documentation and examples.

The write decision table is normative:

| Condition | Target state | Result |
| --- | --- | --- |
| No ETag, normal content upload/upsert | Present | Replace unconditionally and return the committed new ETag |
| No ETag, normal content upload/upsert | Absent | Create and return its ETag |
| Matching ETag | Present | Apply the requested content/metadata mutation and return its new ETag |
| Stale ETag | Present | Conflict; current item remains unchanged |
| Supplied ETag, content upload/upsert | Absent | Conflict; target remains absent |
| Supplied ETag, metadata update | Absent | Failure; target remains absent |
| Create-only, no ETag | Physical item present | Conflict, including an expired physical item |
| Create-only, nonempty ETag | Any | Validation failure before mutation |

## 2. Implementation Steps

Execute phases in numerical order. Each phase declares dependencies and completion criteria. Task dependencies override parallel eligibility. Tasks sharing a file run sequentially; all build/test commands run sequentially regardless of phase. This plan does not require parallel agents.

### Implementation Phase 1

- **GOAL-001**: Define and propagate optional conditions across Application Storage APIs.
- **DEP-101**: No preceding implementation phase. Recheck `git status --porcelain` and preserve unrelated changes.
- **VAL-101**: Application Storage compiles; every listed upload constructor forwards `IfMatchETag`; invalid condition combinations fail before provider I/O.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add documented optional `IfMatchETag` to `src/Application.Storage/Blobs/Models/BlobUpload.cs:92`. Update upload XML contracts in `src/Application.Storage/Blobs/IBlobStoreClient.cs` and `IBlobStoreProvider.cs`, and clarify whole-item version semantics in `src/Application.Storage/Documents/Models/DocumentModels.cs`. Extend `src/Application.Storage/Blobs/Validation/BlobValidator.cs` with create-only/ETag and wildcard checks plus validation of delete options. Add public static `DocumentWriteValidator` in new `src/Application.Storage/Documents/Validation/DocumentWriteValidator.cs`, with `Validate` overloads for write, metadata, and delete options returning existing feature validation errors. Call these validators from clients and every built-in provider mutation entry point so direct provider calls cannot bypass the rules. In `DocumentStoreClient<T>.PrepareWriteAsync` at `src/Application.Storage/Documents/DocumentStoreClient.cs:322`, validate write options before serialization, including bulk preparation. | ✅ | 2026-11-16 |
| TASK-002 | Depends on TASK-001. Add optional `IfMatchETag` with XML examples to `BlobBytesUploadOptions`, `BlobTextUploadOptions`, and `BlobObjectUploadOptions` under `src/Application.Storage/Blobs/Content/`, `src/Application.Storage/Blobs/Files/BlobFileUploadOptions.cs`, `src/Application.Storage/Blobs/Transfers/BlobCopyOptions.cs`, and `DocumentTransferOptions` in `src/Application.Storage/Documents/Extensions/DocumentStoreClientExtensions.cs:34`. Do not add a second destination condition to `BlobMoveOptions`; use its existing `Copy` options. | ✅ | 2026-11-16 |
| TASK-003 | Depends on TASK-002. Forward the unchanged condition in every `new BlobUpload` inside `BlobBytesStorageExtensions.UploadBytesAsync`, `BlobContentStorageExtensions.UploadTextAsync`/`UploadObjectAsync`, `BlobFileStorageExtensions.UploadFileAsync`, and `BlobTransferStorageExtensions.CopyToAsync`. Forward it from document `CopyAsync` into `DocumentWriteOptions`. Validate mutually exclusive destination options before reading/copying source content. Preserve source-conditional deletion and same-key move behavior. | ✅ | 2026-11-16 |
| TASK-004 | Depends on TASK-001. Forward `upload.IfMatchETag` in `UploadAsync` in `src/Application.Storage/Blobs/Behaviors/CompressionBlobStoreClientBehavior.cs`, `EncryptionBlobStoreClientBehavior.cs`, and `ContentTypeDetectionBlobStoreClientBehavior.cs`. Audit `BlobBehaviorTransform.cs` mappings and document bulk preparation for unchanged condition propagation. Keep existing stream ownership, transform metadata, size, and checksum behavior. | ✅ | 2026-11-16 |
| TASK-005 | Depends on TASK-003 and TASK-004. Extend model, convenience, content, file, transform, and document bulk tests listed in Section 5. Assert behavioral stale-write rejection through each helper and transform composition, not only copied property values. Verify omitted conditions retain unconditional replacement. | ✅ | 2026-11-16 |

### Implementation Phase 2

- **GOAL-002**: Enforce blob content conditions atomically in in-memory, EF, and filesystem providers.
- **DEP-201**: Phase 1 completed. Provider tasks are independent of each other; their tests run sequentially.
- **VAL-201**: Each provider rejects stale and missing-target conditional content writes, rejects an old token after a metadata update, and allows unconditional replacement.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | In `src/Application.Storage/Blobs/InMemory/InMemoryBlobStoreContext.cs`, extend `TryStore` with a conditional overload while retaining its existing public signature as an unconditional forwarding overload. Check existence and the supplied ETag inside the same `syncRoot` lock that publishes the replacement. Update `InMemoryBlobStoreProvider.UploadAsync` in `src/Application.Storage/Blobs/InMemory/InMemoryBlobStoreProvider.cs:75` to pass the condition and return `BlobStoreConflictError` for a missing or mismatched version. Generate/store a new ETag only for a successful replacement. | ✅ | 2026-11-16 |
| TASK-007 | In `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs:161`, reject a supplied condition when the target is absent. For an existing target, compare against the caller's ETag and retain the existing lease acquisition CAS using `ConcurrencyVersion`. After successful acquisition/reload, confirm the expected ETag before `DeleteChunksAsync` or any replacement. Map an intervening version change during lease acquisition to conflict for a conditional request; preserve genuine lease-busy behavior. Keep transactions, chunk flushing, snapshot restoration, and final concurrency checks. Capture the mutation lease/version for cleanup, and coordinate local nonrelational mutations across provider instances through the protected shared gate in the nongeneric public provider base. Never restore an old snapshot over a version this operation did not acquire. | ✅ | 2026-11-16 |
| TASK-008 | In `src/Infrastructure.FileSystem/Blobs/FileSystemBlobStoreProvider.cs:43`, stage bytes as today, acquire key authority, read the current record, then compare `upload.IfMatchETag` before `Publish`. Keep comparison and publication within the same authority scope. On conflict, dispose staging without changing the selected manifest. Exercise both Durable and Buffered modes without changing their format or qualification claims. | ✅ | 2026-11-16 |
| TASK-009 | Depends on TASK-006 through TASK-008. Extend `tests/Application.UnitTests/Storage/Blobs/BlobStoreProviderContractTests.cs` with the scenarios in TEST-001 through TEST-005. Run through `InMemoryBlobStoreProviderContractTests`, EF contract wrappers, and `FileSystemBlobContractTests`/`FileSystemBufferedBlobContractTests`. Add EF contention and filesystem staged-upload interleavings in their provider-specific regression suites. | ✅ | 2026-11-16 |

### Implementation Phase 3

- **GOAL-003**: Enforce Azure blob upload conditions and make metadata compensation safe.
- **DEP-301**: Phases 1 and 2 completed. TASK-011 follows TASK-010 because both change the backend transport types.
- **VAL-301**: Final block commits include the original condition; native-backend tests prove rollback cannot overwrite a competing writer; returned ETags belong to committed mutations.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Extend protected `AzureBlobUploadRequest` in `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreBackend.cs:410` with an optional trailing condition to avoid breaking its existing constructor calls. Forward it from `AzureBlobStoreProvider.UploadAsync` in `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreProvider.cs:125`. In backend `UploadAsync`, put `IfMatch = new ETag(request.IfMatchETag)` on the final `CommitBlockListAsync` request. Keep `IfNoneMatch = ETag.All` for create-only and no condition for unconditional writes. Reject invalid combinations before staging. Map conditional missing-target and precondition failures to `BlobStoreConflictError`. | ✅ | 2026-11-16 |
| TASK-011 | Depends on TASK-010. Rework backend `UpdatePropertiesAsync` and `RestoreAsync` at `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreBackend.cs:174` and `:296`. Track the ETag from each acknowledged header/metadata mutation. Chain forward and compensating requests using only this tracked ETag. Remove the fresh-ETag read from compensation. Stop on a failed compensation condition, preserve the competing writer, and report both operation and restoration errors through `BlobStorePartialUpdateError`. If acknowledgement is ambiguous and the new version cannot be established, do not guess or obtain a fresh token to restore. Keep non-cancelable cleanup only for safe, conditional compensation. Preserve the current no-retry handling for this failure. | ✅ | 2026-11-16 |
| TASK-012 | Depends on TASK-011. Add a private version-bound property snapshot helper in `AzureBlobStoreBackend.cs`: read properties with the supplied condition when present, then read tags conditional on the returned property ETag. Delegate ordinary `GetPropertiesAsync` to it without an initial condition. Extend `AzureBlobUploadResult` with an optional trailing `AzureBlobProperties CommittedProperties = null` and populate it from the committed request metadata/tags, copied length/hash, and SDK commit ETag/LastModified response. Resolve native creation time through a property read conditional on that commit ETag; if a competing write/delete prevents that optional read, keep `CreatedAt` null rather than borrowing another version's timestamps. `BlobInfo.CreatedAt` is already nullable. Make `AzureBlobStoreProvider.UploadAsync` use this committed snapshot instead of an unconditional reread. Build successful metadata-update results from the original snapshot, requested values, and last acknowledged metadata response ETag/time. Setting tags does not advance the native ETag. Never substitute a later writer's ETag. | ✅ | 2026-11-16 |
| TASK-013 | Depends on TASK-012. Update `tests/Infrastructure.IntegrationTests/Azure.Storage/Blobs/RecordingAzureBlobStoreBackend.cs` for the request/result fields and shared contracts. Add `AzureBlobStoreBackendConcurrencyTests.cs` beside it using a `BlobServiceClient` with deterministic SDK HTTP transport interception. Exercise the real private backend through the public provider, rather than implementing compensation in the recording fake. Intercept after headers and after metadata, insert a competing version, and assert every subsequent forward/rollback request retains its expected condition. Verify the pinned SDK sends conditional tag headers and fail without dropping conditions if an endpoint rejects them. Run the Azurite contract wrapper separately. | ✅ | 2026-11-16 |

### Implementation Phase 4

- **GOAL-004**: Correct document provider conditions and prove whole-item concurrency across all document providers.
- **DEP-401**: Phase 3 completed. TASK-014 and TASK-015 are independent; TASK-016 follows both.
- **VAL-401**: Shared document concurrency contracts pass for in-memory, filesystem, EF SQL Server/PostgreSQL/SQLite, Azure Blob, Azure Table, and Cosmos in available environments. Missing external environments remain reported verification gaps.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | Fix `UpsertAsync` and `UpdatePropertiesAsync` in `src/Infrastructure.Azure.Storage/Documents/AzureTableDocumentStoreProvider.cs:155`. A supplied content-write ETag with no physical entity returns conflict before `AddEntityAsync`. Distinguish a failed physical read from not-found; never treat an unavailable read as absence. Existing conditional writes use native replace conditions and map 404/412 to conflict for content writes. Capture new ETags from Add/Update response headers and map the locally committed entity with that ETag instead of using a stale entity or unconditional post-write `GetEntityAsync`. Apply the same response-token rule to metadata updates at `:207`. | ✅ | 2026-11-16 |
| TASK-015 | Correct the explicit-ETag overload `UpsertItemAsync(item, partitionKeyValue, ifMatchETag, cancellationToken)` in `src/Infrastructure.Azure.Cosmos/Provider/CosmosSqlProvider.cs:443`: when a condition is nonempty, call SDK `ReplaceItemAsync` with the ID from the provider's existing `idProperty`, partition key, and exact header ETag. Never let this conditional path fall back to SDK upsert/create. Preserve its public signature, `SetOpaqueETag`, request-charge logging, cancellation, and the separate unconditional overload. Update its XML contract in `ICosmosSqlProvider.cs`. In `CosmosDocumentStoreProvider.UpsertAsync` and `UpdatePropertiesAsync`, use that replacement path for tagged writes and map deletion during conditional replacement to `DocumentStoreConflictError`. Preserve unconditional caller behavior and quoted/unquoted token support. | ✅ | 2026-11-16 |
| TASK-016 | Depends on TASK-014 and TASK-015. Add `tests/Shared/Storage/DocumentStoreConcurrencyContractTests.cs`, an abstract raw-provider contract suite with provider creation and isolated test identities. Link it into Application.UnitTests, Infrastructure.UnitTests, and Infrastructure.IntegrationTests using explicit `Compile Include` entries, following the existing linked blob-contract pattern. Add wrappers named `InMemoryDocumentStoreConcurrencyContractTests`, `FileSystemDocumentStoreConcurrencyContractTests`, `EntityFrameworkDocumentStoreConcurrencyContractTests`, `AzureBlobDocumentStoreConcurrencyContractTests`, `AzureTableDocumentStoreConcurrencyContractTests`, and `CosmosDocumentStoreConcurrencyContractTests` in the corresponding directories listed in Section 5. Reuse existing contexts and fixtures rather than introducing a second registration system. Exercise TEST-001 through TEST-006 for every provider. | ✅ | 2026-11-16 |
| TASK-017 | Depends on TASK-016. Add deterministic Table transport tests for missing-target conditional saves and fresh response ETags, and Cosmos transport tests for delete/TTL removal between read and replacement. Use mocked `TableServiceClient`/`TableClient` or configured SDK HTTP transport and Cosmos `HttpClientFactory` interception, preserving the actual SDK write verb. Add cases to `tests/Infrastructure.IntegrationTests/Azure.Storage/Documents/AzureTableDocumentStoreConcurrencyTests.cs` and `tests/Infrastructure.IntegrationTests/Azure.Cosmos/Storage/Documents/CosmosDocumentStoreConcurrencyTests.cs`. Keep native-service checks separate from deterministic transport proofs. Confirm existing in-memory, EF, filesystem, and Azure Blob document writes already preserve the original condition, including optimized EF bulk writes. | ✅ | 2026-11-16 |

### Implementation Phase 5

- **GOAL-005**: Carry observed versions through destructive helpers and HTTP boundaries.
- **DEP-501**: Phase 4 completed. TASK-018 and TASK-019 are independent.
- **VAL-501**: A replacement made after enumeration survives prefix deletion, and maintenance DELETE accepts and enforces an optional caller condition.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-018 | In `DeleteByPrefixAsync` in `src/Application.Storage/Blobs/Transfers/BlobTransferStorageExtensions.cs:298`, pass `new BlobDeleteOptions { IfMatchETag = item.Value.ETag }`. Retain bounds, dry-run, ordered failure reporting, and continue-on-error semantics. Verify `BlobDeletePrefixMaintenanceJob` continues to use this helper and does not reintroduce unconditional deletion. Preserve the existing observed-tag behavior of document `DeleteByQueryAsync`. | ✅ | 2026-11-16 |
| TASK-019 | Add optional `[FromHeader(Name = "If-Match")] string ifMatchETag` to the maintenance DELETE mapping and propagate it through private `DeleteAsync` into `BlobDeleteOptions` in `src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.cs:117`. Preserve requests without the header. Continue accepting the existing body `IfMatchETag` for property PATCH. Return 409 for version conflicts and 400 for invalid conditions. Add endpoint tests in `tests/Presentation.UnitTests/Web/Storage/BlobStorageEndpointsTests.cs` for matching, stale, omitted, and invalid tokens. | ✅ | 2026-11-16 |

### Implementation Phase 6

- **GOAL-006**: Make dashboard edits conditional by default and retain drafts on conflict.
- **DEP-601**: Phase 5 completed. TASK-021 and TASK-022 depend on TASK-020. TASK-023 follows both.
- **VAL-601**: Ordinary dashboard edits send the displayed token, Force overwrite omits it, and stale saves preserve drafts. Desktop and narrow-browser flows satisfy TEST-012.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-020 | Update `MapFailure` and save/upload handlers in both `src/Presentation.Web.Storage/Documents/Dashboard/DashboardEndpoints.cs` and `src/Presentation.Web.Storage/Blobs/Dashboard/DashboardEndpoints.cs`. Preserve typed failures and cancellation in `src/Application.Storage/Documents/DocumentStoreClientAccessor.cs` so the dashboard receives the original error. Map `DocumentStoreConflictError` explicitly to 409, since it currently reaches the 500 fallback, and `DocumentStoreInvalidQueryError` to 400. Introduce an explicit `forceOverwrite` form flag for content/metadata updates. New mode always uses create-only; normal edit requires a nonempty original `etag`; force mode explicitly sets the condition to null. Reject force plus create-only. Keep exact deletes tied to the displayed ETag and do not add a force-delete action. Preserve authorization and antiforgery validation. | ✅ | 2026-11-16 |
| TASK-021 | Depends on TASK-020. In blob dashboard `Pages/Index.cshtml` and `Pages/Content.cshtml`, add a Replace content action for an existing row/details snapshot that carries store, container, name, and displayed ETag into the upload dialog. Keep that identity fixed for the edit. Default a general new upload to create-only instead of the current checked unconditional-overwrite checkbox. Expose Force overwrite explicitly when editing an existing blob. Forward the captured ETag to content and expiration/property saves. Reset force/token state when opening a different item or creating a new item. | ✅ | 2026-11-16 |
| TASK-022 | Depends on TASK-020. In document dashboard `Pages/Index.cshtml` and `Pages/Content.cshtml`, retain the ETag captured with the displayed document and add the explicit force action for its content/properties/expiration save. On 409, keep JSON, properties, expiration, token, and identity unchanged. Apply the equivalent behavior to blob metadata and content forms, retaining the selected file. Show the server conflict message with Reload current item and Force overwrite. Reload requires explicit confirmation before replacing a dirty draft; it reopens the current snapshot or reports deletion. Refresh of the background listing must not silently rebase an open editor. Disable automatic refresh while an editor is dirty using existing dashboard helpers. | ✅ | 2026-11-16 |
| TASK-023 | Depends on TASK-021 and TASK-022. Extend `tests/Presentation.UnitTests/Web/Storage/BlobStorageDashboardTests.cs` and `DocumentStorageDashboardTests.cs` with behavioral endpoint/rendered-form cases. Add browser coverage for the actual JavaScript interactions in TEST-012. Reuse dashboard dialog/state/refresh helpers, read the dashboard-pages skill during implementation, and inspect desktop plus a 390px viewport. Keep fixture setup and execution evidence outside `docs/`. Stop any hosts started for verification and confirm their ports are released. | ✅ | 2026-11-16 |

### Implementation Phase 7

- **GOAL-007**: Document the contract and complete focused verification.
- **DEP-701**: Phases 1 through 6 completed. Documentation edits may be prepared earlier, but must be checked against the final implementation.
- **VAL-701**: Required local checks pass, external checks have explicit results or blockers, and feature docs describe both optional concurrency and the Azure partial-update exception accurately.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-024 | Update `docs/features-storage-blobs.md`, `docs/features-storage-documents.md`, `src/Application.Storage/README.md`, `src/Infrastructure.Azure.Storage/README.md`, `src/Infrastructure.Azure.Cosmos/README.md`, and `src/Infrastructure.FileSystem/README.md` where they describe these APIs. Include conditional content/metadata examples, unconditional omission, complete item identity, returned token ownership, destination transfer conditions, conditional deletion, dashboard conflict actions, and safe partial Azure compensation. Preserve the existing feature-document spine. Use fictional models and routes; do not put repository application names, links, screenshots, or execution evidence in documentation. | ✅ | 2026-11-16 |
| TASK-025 | Depends on TASK-024. Run the sequential checks in Section 6, review touched XML documentation and examples, check local Markdown links and `git diff --check`, and inspect `git status --porcelain`. Record exact commands, passed/failed/skipped counts, and external-environment limitations outside `docs/`. Do not claim cloud, file-share, or power-loss verification from local/fake tests. Update this plan's task completion cells and status only for work actually completed. | ✅ | 2026-11-16 |

## 3. Alternatives

- **ALT-001**: Require ETags for every library mutation. Rejected because callers must retain the option to disable concurrency checking by omitting `IfMatchETag`.
- **ALT-002**: Read the newest ETag immediately before saving, or automatically retry a stale draft with it. Rejected because this authorizes overwriting changes the editor never observed.
- **ALT-003**: Check the ETag before staging and then publish unconditionally. Rejected because another writer can change the item between comparison and publication.
- **ALT-004**: Use `ExpectedContentHash` as the concurrency check. Rejected because it validates incoming bytes and does not identify the currently stored item version or its metadata.
- **ALT-005**: Keep Cosmos SDK upsert with `IfMatchEtag` for conditional replacement. Rejected because Cosmos ignores that condition when the request becomes a create.
- **ALT-006**: Restore Azure metadata against a freshly read current ETag. Rejected because compensation could overwrite another writer. Guarded partial failure is the agreed outcome when restoration cannot be completed safely.
- **ALT-007**: Introduce exclusive Azure leases, distributed transactions, or a new shared persistence abstraction to guarantee atomic multi-request metadata updates. Deferred because the accepted scope permits explicit partial-update errors and retains existing provider boundaries.
- **ALT-008**: Consolidate the dedicated Azure Blob document provider into an `IBlobStoreClient` adapter after adding conditional uploads. Deferred; this plan fixes semantics without changing provider ownership or query/resource behavior.

## 4. Dependencies

- **DEP-001**: `global.json`, `Directory.Build.props`, project files, and `Directory.Packages.props` define current versions. At inspection they select SDK 10.0.404, .NET 10 runtime projects, Azure.Storage.Blobs 12.30.1, Azure.Data.Tables 12.13.0, and Microsoft.Azure.Cosmos 3.64.0. Do not upgrade packages as part of this plan.
- **DEP-002**: Existing `BlobStoreConflictError`, `DocumentStoreConflictError`, feature validation/not-found errors, `BlobStorePartialUpdateError`, and indeterminate-operation errors remain the public error model. Document errors live in `src/Common.Results/Errors/Builder/Errors.Storage.cs`; blob errors live in `src/Application.Storage/Blobs/BlobStoreErrors.cs`.
- **DEP-003**: EF conditional publication depends on the current `ConcurrencyVersion` token, lease acquisition/reload, transactional chunk changes, and safe compensation. Filesystem publication depends on the current per-key authority protocol, not File Storage APIs.
- **DEP-004**: Native Azure tag ETag conditions require a compatible service API. The pinned SDK forwards these conditions; the Blob service documents support from version 2025-11-05. Test transport headers and native behavior. An incompatible emulator/service is a reported limitation or failure; never strip the condition to make a test pass.
- **DEP-005**: Existing infrastructure integration fixtures can start multiple Docker services even for a focused filter. Inspect `tests/Infrastructure.IntegrationTests/TestEnvironmentFixture.cs` and `Azure.Cosmos/Storage/Documents/CosmosDocumentStoreTestEnvironmentCollection.cs` before executing. Deterministic transport tests do not replace native-service tests.
- **DEP-006**: Existing filesystem deployment qualification remains separate. This change must not silently mark pending SMB/NFS, mixed-host, reconnect, restart, or power-loss gates complete.

## 5. Files

Paths below are relative to the repository root. Existing files are modification targets; files explicitly labeled new are creation targets. Tasks in Section 2 determine which changes are necessary within each file.

- **FILE-001**: `src/Application.Storage/Blobs/Models/BlobUpload.cs`, `IBlobStoreClient.cs`, `IBlobStoreProvider.cs`, `Validation/BlobValidator.cs`, and `Models/BlobStoreProviderCapabilities.cs` under the same blob root. Clarify that `SupportsETag` covers conditional content and metadata mutations for built-in providers.
- **FILE-002**: `src/Application.Storage/Blobs/Content/BlobBytesUploadOptions.cs`, `BlobTextUploadOptions.cs`, `BlobObjectUploadOptions.cs`, `BlobBytesStorageExtensions.cs`, and `BlobContentStorageExtensions.cs`.
- **FILE-003**: `src/Application.Storage/Blobs/Files/BlobFileUploadOptions.cs`, `BlobFileStorageExtensions.cs`; `src/Application.Storage/Blobs/Transfers/BlobCopyOptions.cs`, `BlobMoveOptions.cs`, and `BlobTransferStorageExtensions.cs`.
- **FILE-004**: `src/Application.Storage/Blobs/Behaviors/CompressionBlobStoreClientBehavior.cs`, `EncryptionBlobStoreClientBehavior.cs`, `ContentTypeDetectionBlobStoreClientBehavior.cs`, `BlobBehaviorTransform.cs`, and `RetryBlobStoreClientBehavior.cs`. Retry changes are limited to preserving non-retryable conflict/partial outcomes.
- **FILE-005**: `src/Application.Storage/Documents/Models/DocumentModels.cs`, `DocumentStoreClient.cs`, `IDocumentStoreClient.cs`, `DocumentStoreClientAccessor.cs`, `Extensions/DocumentStoreClientExtensions.cs`, and `Behaviors/RetryDocumentStoreClientBehavior.cs`; new `src/Application.Storage/Documents/Validation/DocumentWriteValidator.cs`.
- **FILE-006**: `src/Application.Storage/Blobs/InMemory/InMemoryBlobStoreContext.cs`, `InMemoryBlobStoreProvider.cs`; `src/Application.Storage/Documents/InMemory/InMemoryDocumentStoreProvider.cs`. Preserve lock-based document conditions and add direct-provider validation where required.
- **FILE-007**: `src/Infrastructure.EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProvider.cs`; `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs`. Retain correct existing document behavior and verify optimized bulk paths.
- **FILE-008**: `src/Infrastructure.FileSystem/Blobs/FileSystemBlobStoreProvider.cs`; `src/Infrastructure.FileSystem/Documents/FileSystemDocumentStoreProvider.cs`. Preserve direct-provider document conditions and key authority.
- **FILE-009**: `src/Infrastructure.Azure.Storage/Blobs/AzureBlobStoreProvider.cs`, `AzureBlobStoreBackend.cs`; `src/Infrastructure.Azure.Storage/Documents/AzureTableDocumentStoreProvider.cs`, `AzureBlobDocumentStoreProvider.cs`.
- **FILE-010**: `src/Infrastructure.Azure.Cosmos/Provider/CosmosSqlProvider.cs`, `ICosmosSqlProvider.cs`; `src/Infrastructure.Azure.Cosmos/Storage/Documents/CosmosDocumentStoreProvider.cs`.
- **FILE-011**: `src/Presentation.Web.Storage/Blobs/BlobStorageMaintenanceEndpoints.cs`; blob and document dashboard `DashboardEndpoints.cs`, `Pages/Index.cshtml`, and `Pages/Content.cshtml` under `src/Presentation.Web.Storage/Blobs/Dashboard/` and `Documents/Dashboard/`.
- **FILE-012**: `tests/Application.UnitTests/Storage/Blobs/BlobStoreProviderContractTests.cs`, `BlobStorageModelTests.cs`, `BlobStorageConvenienceExtensionsTests.cs`, `BlobContentStorageExtensionsTests.cs`, `BlobFileStorageExtensionsTests.cs`, `BlobStoreTransformBehaviorTests.cs`, and `BlobStoreClientBehaviorTests.cs`.
- **FILE-013**: `tests/Application.UnitTests/Storage/Documents/DocumentStoreClientTests.cs`, `DocumentStoreClientBulkWriteTests.cs`, and `DocumentStoreClientAccessorTests.cs`; `tests/Application.IntegrationTests/Storage/Documents/InMemoryDocumentStoreProviderTests.cs`.
- **FILE-014**: New `tests/Shared/Storage/DocumentStoreConcurrencyContractTests.cs`; modification of `tests/Application.UnitTests/Application.UnitTests.csproj`, `tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj`, and `tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj` to link it.
- **FILE-015**: New wrappers `tests/Application.UnitTests/Storage/Documents/InMemory/InMemoryDocumentStoreConcurrencyContractTests.cs`, `tests/Infrastructure.UnitTests/FileSystem/FileSystemDocumentStoreConcurrencyContractTests.cs`, `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreConcurrencyContractTests.cs`, `tests/Infrastructure.IntegrationTests/Azure.Storage/Documents/AzureBlobDocumentStoreConcurrencyContractTests.cs`, `AzureTableDocumentStoreConcurrencyContractTests.cs` beside it, and `tests/Infrastructure.IntegrationTests/Azure.Cosmos/Storage/Documents/CosmosDocumentStoreConcurrencyContractTests.cs`.
- **FILE-016**: `tests/Infrastructure.UnitTests/EntityFramework/Storage/Blobs/EntityFrameworkBlobStoreProviderContractTests.cs`, `EntityFrameworkBlobStoreProviderUploadDownloadTests.cs`; `tests/Infrastructure.UnitTests/FileSystem/FileSystemProviderTests.cs`, `FileSystemRegressionTests.cs`, and `FileSystemReviewRegressionTests.cs`.
- **FILE-017**: `tests/Infrastructure.IntegrationTests/Azure.Storage/Blobs/RecordingAzureBlobStoreBackend.cs`, `AzureBlobStoreProviderContractTests.cs`, `AzureBlobStoreProviderAzuriteContractTests.cs`, and new `AzureBlobStoreBackendConcurrencyTests.cs` beside them. Add new `tests/Infrastructure.IntegrationTests/Azure.Storage/Documents/AzureTableDocumentStoreConcurrencyTests.cs` and `tests/Infrastructure.IntegrationTests/Azure.Cosmos/Storage/Documents/CosmosDocumentStoreConcurrencyTests.cs`. Extend `tests/Infrastructure.IntegrationTests/Azure.Cosmos/Provider/CosmosSqlProviderTests.cs` for the corrected explicit-ETag overload.
- **FILE-018**: `tests/Presentation.UnitTests/Web/Storage/BlobStorageEndpointsTests.cs`, `BlobStorageDashboardTests.cs`, and `DocumentStorageDashboardTests.cs`.
- **FILE-019**: `docs/features-storage-blobs.md`, `docs/features-storage-documents.md`; package READMEs listed in TASK-024. Preserve other specifications and ADRs.
- **FILE-020**: This plan records progress only after implementation. Browser scripts/captures and verification logs belong in a task-specific directory outside `docs/`, such as `.audit/storage-conditional-updates/`.

## 6. Testing

- **TEST-001**: Shared blob/document contracts create version A, replace with matching ETag A to produce B, then reject another content save using A. Read back B and assert content, properties, expiration, hash, and ETag are unchanged by the rejected save.
- **TEST-002**: Update metadata with ETag A to produce B, then reject a content save with A. Separately replace content to produce B, then reject a metadata update with A. Verify matching metadata saves return a new token that permits the next conditional save.
- **TEST-003**: Read A, physically delete it, then attempt a conditional content save with A. Assert conflict and absence. Recreate the same key as B and retry A; assert B is unchanged. Also submit a token from a different item and verify rejection.
- **TEST-004**: Two writers begin from A. Pause one after staging/read and commit the other. Resume the paused writer and assert at most one succeeds with A and the loser cannot overwrite content or metadata. Use provider-specific synchronization at actual publication/SDK boundaries. Retention/delete between read and commit must not allow conditional recreation.
- **TEST-005**: Omitted, empty, and whitespace conditions preserve unconditional content overwrite. Create-only still rejects physical existing/expired records. Create-only plus a nonempty ETag and wildcard `*` fail validation without mutation. Successful operations return metadata and an ETag from their own commit, even if another writer changes the item immediately afterward.
- **TEST-006**: Parameterize document contracts over all providers listed in VAL-401. Verify single and bulk write conditions, transform-enabled clients, expired physical records, and reuse of the token returned by a metadata save. Exercise EF optimized bulk behavior under contention without treating a batch as an all-or-nothing transaction.
- **TEST-007**: Native Azure backend transport tests fail metadata/tag updates after an acknowledged mutation, with and without a competing writer. With no competing writer, compensation restores prior values using chained operation tokens. With a competing writer, no compensation request may use its ETag; return `BlobStorePartialUpdateError`. Include cancellation, a lost mutation response, failure during compensation, and a failure before the first confirmed mutation. Verify tag writes and restores carry the expected conditional header despite not changing the native ETag themselves.
- **TEST-008**: Table tests assert a missing-target conditional content save does not call AddEntity, then assert the ETag returned by metadata mutation is the response ETag and succeeds on a subsequent conditional save. Interleave another write after commit and before any metadata retrieval to prove its token is not adopted.
- **TEST-009**: Cosmos tests delete or expire the item after provider read and before the SDK write. Assert the conditional request uses replacement, fails without recreation, and maps 404/412 correctly. Repeat for content and metadata and for quoted/unquoted tokens. Verify unconditional no-token writes still create and replace.
- **TEST-010**: Exercise byte/text/object/file uploads, compression plus encryption, copy and move with a matching/stale destination condition, omitted destination conditions, create-only combinations, and source mutation after copy. Failed destination writes must leave source untouched. Verify rejected writes do not invalidate a correct cache entry.
- **TEST-011**: Replace a blob after prefix enumeration but before delete; assert the newer blob survives and appears in existing failure reporting. Maintenance DELETE accepts matching `If-Match`, returns conflict for stale input, and preserves unconditional behavior when omitted. Dashboard document ETag conflicts return 409 instead of 500.
- **TEST-012**: Run two independent browser sessions against a host with named blob and document clients. Open the same item in both; save in session B, then save stale content and stale metadata separately in A. Assert A retains its draft/file/token and displays Reload current item and Force overwrite. Assert Force overwrite uses the retained draft without an ETag; Reload asks before discarding edits and loads a fresh snapshot. Test deletion/recreation, new create-only forms, item switching, modal refresh, and a 390px viewport. Verify these controls by interaction, not only source-string checks.
- **TEST-013**: Confirm retry behaviors never replay conflicts, partial updates, or indeterminate operations with a fresh token. Preserve stream disposal/ownership and current size/integrity/cancellation regression coverage.

Run these commands from the repository root, sequentially, after implementation. Omit `--no-restore` until assets exist. A failed command requires diagnosis before proceeding; no skipped suite counts as a pass.

```powershell
dotnet build bITdevKit.slnx --nologo /p:UseSharedCompilation=false
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Storage" --nologo /p:UseSharedCompilation=false
dotnet test tests/Application.IntegrationTests/Application.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~DocumentStore" --nologo /p:UseSharedCompilation=false
dotnet test tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --no-restore --filter "FullyQualifiedName~EntityFrameworkBlobStore|FullyQualifiedName~FileSystemBlobContractTests|FullyQualifiedName~FileSystemBufferedBlobContractTests|FullyQualifiedName~FileSystemDocumentStoreConcurrencyContractTests|FullyQualifiedName~FileSystemProviderTests|FullyQualifiedName~FileSystemRegressionTests|FullyQualifiedName~FileSystemReviewRegressionTests" --nologo /p:UseSharedCompilation=false
dotnet test tests/Presentation.UnitTests/Presentation.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Storage" --nologo /p:UseSharedCompilation=false
dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~BlobStoreProvider|FullyQualifiedName~DocumentStoreConcurrency|FullyQualifiedName~AzureBlobStoreBackendConcurrencyTests" --nologo /p:UseSharedCompilation=false
git diff --check
git status --porcelain
```

The final infrastructure command requires the fixtures' Docker services. Run deterministic Azure transport tests with their class filters when native services are unavailable, and report which native wrappers remain unverified. The current filesystem suites live in `Infrastructure.UnitTests`; do not use the historical, absent `Infrastructure.FileSystem.Tests` project path. No runtime tests are required merely to create this plan.

Final verification on 2026-11-16:

| Check | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Full solution build | 0 warnings and 0 errors | 0 | 0 |
| Application Storage unit tests | 465 | 0 | 0 |
| Document client integration tests | 10 | 0 | 0 |
| Affected EF and filesystem local suites | 232 | 0 | 1 |
| Storage presentation tests | 119 | 0 | 0 |
| Azure/Cosmos deterministic transport tests | 24 | 0 | 0 |
| Native provider suites: SQL Server, PostgreSQL, SQLite, Azurite, Cosmos emulator | 339 | 0 | 0 |

The total is 1,189 passed, zero failed, and one Linux-only filesystem test skipped on Windows. Independent desktop browser sessions and a 390px viewport verified content/properties conflicts, retained drafts/files/tokens, Force overwrite, confirmed Reload, create-only collisions, dialog reuse, and deletion/recreation. The verification host was stopped and its port released. Markdown links and `git diff --check` pass. Emulator/local execution does not qualify cloud deployments, SMB/NFS, or power-loss behavior. Detailed commands, TRX results, browser scripts/captures, and the interrupted broader filesystem selection are recorded outside the documentation tree in `.audit/storage-conditional-updates/`.

## 7. Risks & Assumptions

- **RISK-001**: A helper or transform that reconstructs `BlobUpload` can silently drop the condition. Audit every construction site and verify stale writes through composed behaviors.
- **RISK-002**: EF lease acquisition reloads state, and fallback snapshot restoration can alter state after a failed upload. Compare against the original caller token after acquisition and prove cleanup cannot overwrite a competing version. The implementation captures the mutation lease/version for compensation and uses bounded process-shared coordination for nonrelational local EF mutations. SQL Server, PostgreSQL, and SQLite retain database transactions; this does not qualify a distributed nonrelational EF blob engine.
- **RISK-003**: Returning an ETag from a post-write read can authorize a later stale save against another writer's version. Use mutation response tokens and keep response metadata internally consistent. Azure uploads may return null for the already-nullable `CreatedAt` when their committed version is replaced before a version-bound timestamp read; document this case rather than reporting a different version's creation time.
- **RISK-004**: Azure metadata compensation is not an atomic transaction. A failed operation can leave partial values, and even successful compensation advances the ETag. Document this exception without claiming a global all-or-nothing guarantee. Never retry a partial result automatically.
- **RISK-005**: Azure tag-only changes made outside these providers do not advance the blob ETag. Whole-item guarantees cover toolkit content/metadata mutation paths, which advance the token before changing provider tags. External tag writers and older tag-condition API versions need explicit deployment limits and native verification.
- **RISK-006**: Cosmos conditional replacement must preserve opaque header quoting and token population from the response. SDK upsert with a condition is insufficient even if ordinary stale-write tests pass.
- **RISK-007**: Dashboard background refresh or modal reuse can accidentally replace the original token while retaining an old draft. Treat identity, content, metadata, and token as one editor snapshot and reset force state on item changes.
- **RISK-008**: The independent queryable-properties specification may change storage ownership later. This plan preserves current `PropertyBag` update behavior and does not implement that design or infer cross-store transactions.
- **ASSUMPTION-001**: Omitting `IfMatchETag` intentionally disables the caller's version check; it does not disable provider locks, leases, integrity checks, cancellation, size limits, or atomic publication mechanisms.
- **ASSUMPTION-002**: ETags are equality tokens, not ordered versions, authorization credentials, or historical-version identifiers. Identity includes the named store and blob key, or named store, document type, and document key.
- **ASSUMPTION-003**: No persisted data migration is needed. Existing provider ETags remain usable; newly added request fields default to null. The existing public in-memory `TryStore` signature and Azure upload-request construction remain usable as described in the tasks.

## 8. Related Specifications / Further Reading

- [Blob Storage feature guide](../docs/features-storage-blobs.md)
- [Document Storage feature guide](../docs/features-storage-documents.md)
- [Document Storage alignment plan](pln-refactor-document-storage-alignment-1.md), historical context; its statement that blob uploads lack conditional replacement is superseded when this plan is implemented.
- [Clean Architecture ADR](../docs/adr/0001-clean-onion-architecture.md)
- [Modular monolith ADR](../docs/adr/0003-modular-monolith-architecture.md)
- [Filesystem provider composition ADR](../docs/adr/0037-filesystem-storage-provider-composition.md)
- [Filesystem provider operating reference](../docs/storage-filesystem-providers.md)
- [Filesystem qualification checklist](../docs/storage-filesystem-qualification.md)
- [Microsoft Cosmos IfMatchEtag semantics](https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.requestoptions.ifmatchetag?view=azure-dotnet), including the create behavior of upsert.
- [Azure Table UpdateEntityAsync](https://learn.microsoft.com/en-us/dotnet/api/azure.data.tables.tableclient.updateentityasync?view=azure-dotnet) and [SDK source](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Data.Tables_12.13.0/sdk/tables/Azure.Data.Tables/src/TableClient.cs).
- [Azure Blob conditional headers](https://learn.microsoft.com/en-us/rest/api/storageservices/specifying-conditional-headers-for-blob-service-operations) and [Set Blob Tags semantics](https://learn.microsoft.com/en-us/rest/api/storageservices/set-blob-tags).
- [Pinned Azure Blob SDK source](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Storage.Blobs_12.30.1/sdk/storage/Azure.Storage.Blobs/src/BlobBaseClient.cs), for native tag-condition forwarding.
