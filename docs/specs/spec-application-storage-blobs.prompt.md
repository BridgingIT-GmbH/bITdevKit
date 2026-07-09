---
status: draft
---

# Execution Prompts: Blob Storage

These prompts transform [spec-application-storage-blobs.md](spec-application-storage-blobs.md) into bounded implementation work for AI coding agents.

The goal is controlled incremental implementation, not a single-pass feature build. Run one prompt at a time, review the result, run the checkpoints, and continue only after human approval.

Hard repository constraint: do not create new projects. Add files only inside existing projects that already match the target layer. Do not add new `.csproj` files, solution entries, package projects, sample projects, or test projects unless a human explicitly approves that change later.

## Architecture Analysis Prompt

```text
Do not implement anything.

Read docs/specs/spec-application-storage-blobs.md completely.
Read the existing document storage implementation and storage registration patterns before proposing code structure.
Inspect existing projects and choose existing project locations only. Do not create new projects.

Produce an architecture analysis for Blob Storage with:

- subsystem decomposition
- existing project/file locations to use
- dependency boundaries between Common, Application, Infrastructure, Presentation, and tests
- exact implementation sequence
- runtime invariants that must not drift
- persistence and transaction risks
- lease/concurrency risks
- retry/timeout risks
- telemetry risks
- test strategy by layer
- ambiguous areas that require human review

Do not modify files.
Do not scaffold classes.
Do not create projects.
Do not implement providers.

End with:

- Build/test checkpoint: not run, analysis only
- Review checkpoint: list decisions that need human approval before implementation begins
```

## Shared Governance Instructions

Copy these instructions into every implementation prompt.

```text
Before implementing, re-read docs/specs/spec-application-storage-blobs.md and the relevant source files.

The specification is the behavioral source of truth. This prompt only bounds the work.

Follow these rules:

- Do not create new projects.
- Do not add new .csproj files or solution entries.
- Use existing projects and existing repository patterns.
- Keep Application abstractions provider-neutral.
- Do not let EF Core, Azure SDK, or provider-specific types leak into Application abstractions.
- Do not implement the whole feature in one pass.
- Do not add dashboard pages, REST APIs, auth/security features, multi-tenancy, backup/restore, range downloads, or resumable transfers.
- Do not add placeholder providers or speculative abstractions.
- Do not create unused interfaces.
- Do not add future-proof extension points unless the current spec requires them.
- Use Result-native behavior for expected failures.
- Preserve stream-first upload/download semantics.
- Do not dispose caller-provided upload streams.
- Do not download content for list, properties, exists, or property update operations.
- Use ContentType and ContentTypeExtensions from src/Common.Utilities/ContentTypes for content type handling.
- Use SHA-256 content hash format sha256:<lowercase-64-character-hex>.
- Enforce MaxBlobSize before committing content.
- Add focused tests for every behavior implemented in this phase.
- Keep XML docs on new public classes, properties, constructors, and methods, including examples where repository conventions require them.

At the end of the phase run:

- dotnet build --nologo /p:UseSharedCompilation=false
- dotnet test --nologo /p:UseSharedCompilation=false

If full test execution is impractical, run the most relevant existing test projects and state exactly what was not run.

End with a review summary:

- files changed
- behavior implemented
- tests added
- commands run and results
- remaining risks
```

## Prompt 1 — Foundation Models And Errors

```text
Implement only the provider-neutral Blob Storage foundation models and typed errors.

Implementation focus:

- Add BlobKey.
- Add BlobInfo.
- Add BlobDownload.
- Add BlobUpload.
- Add BlobOverwriteMode.
- Add BlobPropertiesUpdate.
- Add BlobQuery.
- Add BlobPage.
- Add BlobStoreOptions.
- Add BlobStoreProviderCapabilities.
- Add typed Result errors required by the spec, including validation, not-found, query too broad, page size exceeded, invalid continuation token, conflict, lease, serialization, provider, size limit, integrity, and timeout errors.
- Use existing Result and error conventions.
- Use existing PropertyBag and ContentType utilities.

Implementation exclusions:

- Do not implement IBlobStoreClient.
- Do not implement providers.
- Do not implement registration.
- Do not implement health checks.
- Do not implement retry, timeout, metrics, or logging behaviors.
- Do not create new projects.

Required tests:

- Model construction and property defaults.
- BlobKey validation expectations where local validation helpers already exist or are added in this phase.
- ContentType public model shape uses ContentType?, not string.
- BlobDownload disposes only the returned content stream.
- Typed errors expose expected data.

Behavioral guarantees:

- Blob models are provider-neutral.
- Provider-specific SDK types do not appear in public Application abstractions.
- Upload streams are not owned by BlobUpload.
- Download streams are owned by BlobDownload.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run relevant unit tests, then dotnet test --nologo /p:UseSharedCompilation=false if feasible.
- Review public API shape before continuing.
```

## Prompt 2 — Hashing, Size Limits, Validation, And Query Builder

```text
Implement shared runtime helpers without implementing real storage providers.

Implementation focus:

- Add SHA-256 content hash helper using existing src/Common.Utilities/HashHelper if suitable.
- Produce hashes as sha256:<lowercase-64-character-hex>.
- Validate ExpectedContentHash format.
- Add MaxBlobSize enforcement helpers for known-length and streaming uploads.
- Add BlobValidator for BlobKey, BlobUpload, and BlobPropertiesUpdate.
- Add BlobQueryValidator.
- Add BlobQueries and BlobQueryBuilder.
- Add continuation token model and serializer.
- Add query hash calculation for container, prefix, and allow-full-scan only.

Implementation exclusions:

- Do not implement IBlobStoreClient.
- Do not implement provider upload/download.
- Do not implement EF Core entities.
- Do not implement Azure Blob code.
- Do not create new projects.

Required tests:

- SHA-256 helper returns lowercase sha256-prefixed hash.
- Hash excludes metadata and depends only on content bytes.
- ExpectedContentHash accepts only the required format.
- Known-length upload over MaxBlobSize fails before provider work.
- Unknown-length upload can be counted while streaming and fails after exceeding MaxBlobSize.
- Query validator resolves DefaultTake and enforces MaxTake.
- Full scan requires global and query-level approval.
- Continuation token is opaque and query-bound.
- Continuation token cannot be reused for a different logical query.
- Builder throws for invalid local arguments and does not bypass validator rules.

Behavioral guarantees:

- Helpers do not buffer full blobs solely to hash.
- Continuation tokens do not expose raw provider tokens as public contract.
- Query validation returns Result failures for expected invalid input.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run unit tests covering helpers, validators, builder, and continuation tokens.
- Review whether any helper belongs in Common.Utilities before continuing.
```

## Prompt 3 — Runtime Client, Provider Contract, Registration, Factory, And Health Check

```text
Implement the provider-neutral runtime shell and dependency injection setup.

Implementation focus:

- Add IBlobStoreClient.
- Add IBlobStoreProvider.
- Add BlobStoreClient that validates and delegates.
- Add BlobStorageOptions.
- Add BlobStorageBuilderContext and fluent AddBlobStorage registration.
- Add named client registration metadata.
- Add IBlobStoreClientFactory.
- Register named clients without creating new projects.
- Ensure duplicate client names fail deterministically.
- Add aggregate health check named BlobStorage.
- Health check probes every registered client with a non-mutating ExistsResultAsync probe.
- Health check data must render checked/failed client names as readable strings, not System.String[].

Implementation exclusions:

- Do not implement EF Core provider.
- Do not implement Azure Blob provider.
- Do not implement InMemory provider beyond minimal test doubles if needed.
- Do not implement logging/metrics/retry/timeout behaviors.
- Do not add dashboard pages or APIs.
- Do not create new projects.

Required tests:

- AddBlobStorage registers feature options.
- Disabled Blob Storage does not register runtime clients or health checks.
- Named client can be resolved by IBlobStoreClientFactory.
- Unknown client fails deterministically.
- Duplicate client names fail.
- GetRegistrations returns names, provider names, and capabilities.
- BlobStoreClient validates before provider invocation.
- Aggregate health check calls every configured client.
- Missing health probe blob is healthy.
- Provider failure makes aggregate health check unhealthy and identifies failed client names.

Behavioral guarantees:

- Application abstractions remain provider-neutral.
- Factory returns the configured client by store name only.
- Health check is aggregate and non-mutating.
- No provider-specific dependencies leak into runtime core.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run DI, factory, client, and health-check tests.
- Review fluent API naming before continuing.
```

## Prompt 4 — InMemory Provider And Contract Test Harness

```text
Implement the InMemory provider and establish shared contract tests.

Implementation focus:

- Add InMemory provider in an existing appropriate project.
- Store content in memory.
- Clone uploaded content.
- Return a new readable MemoryStream for every download.
- Preserve BlobInfo, ContentType, ContentHash, ETag if implemented, timestamps, and PropertyBag.
- Support prefix listing.
- Support approved full scans.
- Support deterministic ordering by Name.
- Support keyset continuation tokens.
- Enforce MaxBlobSize.
- Verify ExpectedContentHash.
- Delete idempotently.
- Create a provider-neutral shared contract test harness that can later run against EF Core and Azure Blob providers.

Implementation exclusions:

- Do not implement EF Core provider.
- Do not implement Azure Blob provider.
- Do not implement retry/timeout/metrics/logging behaviors unless needed by existing registration tests.
- Do not create new projects.

Required tests:

- Upload success returns BlobInfo.
- Upload clones content.
- Download content equals uploaded content.
- Download returns a new stream instance.
- Internal content cannot be mutated through returned streams.
- Missing download returns BlobStoreNotFoundError.
- Get properties does not expose content.
- Update properties changes content type and PropertyBag without rewriting content semantics.
- Exists true/false.
- Delete existing and missing blobs succeeds.
- Prefix listing returns matching blobs only.
- Listing returns BlobInfo only.
- Full scan requires approval.
- Continuation token paging works and is query-bound.
- MaxBlobSize and ExpectedContentHash are enforced without partial commit.
- No resumable or range APIs exist.

Behavioral guarantees:

- InMemory behavior matches the public contract, not a simplified alternate contract.
- InMemory is suitable as the first contract-test provider.
- Listing and properties operations never return content streams.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run InMemory provider tests and shared contract tests.
- Review shared contract test shape before additional providers use it.
```

## Prompt 5 — Client Behaviors: Logging, Metrics, Retry, Timeout

```text
Implement client behaviors around IBlobStoreClient.

Implementation focus:

- Add LoggingBlobStoreClientBehavior.
- Add MetricsBlobStoreClientBehavior.
- Add RetryBlobStoreClientBehavior and options.
- Add TimeoutBlobStoreClientBehavior and options.
- Wire behaviors into AddBlobStorage fluent registration.
- Preserve behavior ordering: first registered behavior is outermost.
- Ensure behaviors work per named client.

Implementation exclusions:

- Do not implement new providers.
- Do not change provider contracts unless tests prove a contract gap.
- Do not buffer download streams.
- Do not dispose caller-provided upload streams.
- Do not emit high-cardinality metrics labels.
- Do not create new projects.

Required tests:

- Behavior registration wraps named clients in order.
- Logging behavior does not log blob content, raw continuation tokens, or full property values.
- Metrics behavior emits operation count, duration, failures, bytes, list item count, retries, timeouts, and size-limit failures where applicable.
- Metrics labels do not include full blob names, raw continuation tokens, arbitrary property values, unbounded messages, user identity, or tenant identity.
- Retry behavior retries transient provider failures.
- Retry behavior does not retry validation, not-found, conflict, concurrency, size-limit, integrity, unsupported-query, or caller-cancellation failures.
- Retry behavior rewinds seekable upload streams before retry.
- Retry behavior does not retry non-seekable uploads unless replay safety is explicitly guaranteed by the provider.
- Timeout behavior uses linked cancellation and maps operation timeout to BlobStoreTimeoutError.
- Timeout behavior does not mask caller cancellation.

Behavioral guarantees:

- Behaviors preserve Result-native semantics.
- Behaviors are provider-neutral decorators.
- Retry never creates duplicate or partial content.
- Metrics remain low-cardinality.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run behavior and DI tests.
- Review runtime semantics before persistence implementation continues.
```

## Prompt 6 — EF Core Storage Model And Context Contract

```text
Implement only the EF Core blob storage model and context contract.

Implementation focus:

- Add IBlobStoreContext with DbSet<StorageBlob> and DbSet<StorageBlobChunk>.
- Add StorageBlob entity.
- Add StorageBlobChunk entity.
- Add EF Core entity configuration in existing EF infrastructure project.
- Add indexes required by the spec.
- Add registration guard requiring TContext to implement IBlobStoreContext.
- Ensure the library does not add consuming-application migrations.

Implementation exclusions:

- Do not implement EF Core upload/download operations yet.
- Do not implement leases yet except entity fields needed by the model.
- Do not implement Azure Blob provider.
- Do not create new projects.
- Do not add application-specific migrations.

Required tests:

- EF registration requires IBlobStoreContext.
- Entity model contains required columns.
- StorageBlob has unique key support for ContainerHash and NameHash.
- StorageBlobChunk has unique BlobId and Index support.
- ContentTypeMimeType stores MIME strings, not ContentType enum in public API.
- EF singleton-safe client path resolves scoped DbContext per operation if registration shell exists.

Behavioral guarantees:

- EF storage schema supports chunked content.
- Public Application models do not reference EF Core.
- Migrations remain the responsibility of consuming applications.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run EF model and registration tests.
- Review schema shape and indexes before implementing writes.
```

## Prompt 7 — EF Core Upload, Download, Hashing, Size Limits, And Leases

```text
Implement EF Core upload and download only.

Implementation focus:

- Implement EF Core provider upload with transactions.
- Acquire internal write lease for upload.
- Respect Overwrite and FailIfExists.
- Delete old chunks on overwrite.
- Stream upload content into configured chunk sizes.
- Count bytes while streaming and enforce MaxBlobSize.
- Calculate SHA-256 while streaming.
- Verify ExpectedContentHash.
- Commit length, content hash, ETag, timestamps, content type, and PropertyBag.
- Roll back on failure and avoid partial content.
- Release or expire lease according to spec semantics.
- Implement download with ordered chunk streaming.
- Return BlobDownload with BlobInfo.

Implementation exclusions:

- Do not implement EF listing/properties/exists/delete in this phase.
- Do not implement Azure Blob provider.
- Do not add public lease APIs.
- Do not implement range or resumable downloads.
- Do not create new projects.

Required tests:

- Upload stores content in chunks.
- Upload does not load entire content into memory.
- Upload overwrite replaces old chunks.
- Upload FailIfExists returns conflict.
- Upload stores SHA-256 content hash.
- Upload enforces MaxBlobSize without committing partial chunks.
- Upload validates ExpectedContentHash without committing partial chunks.
- Upload acquires write lease.
- Expired lease can be recovered.
- Failed upload rolls back chunks and metadata.
- Download streams chunks in order.
- Download missing blob returns BlobStoreNotFoundError.
- Caller disposes returned download stream.

Behavioral guarantees:

- Upload is atomic from caller perspective.
- EF content is chunked, not inline-only.
- EF write concurrency is lease-protected.
- Upload stream is not disposed by the provider.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run EF upload/download/lease tests and shared contract tests that are expected to pass for implemented operations.
- Review transaction and lease behavior before implementing delete/update operations.
```

## Prompt 8 — EF Core Properties, Exists, Delete, Listing, And Continuation Tokens

```text
Complete EF Core provider operations.

Implementation focus:

- Implement GetPropertiesResultAsync without querying chunks.
- Implement UpdatePropertiesResultAsync with write lease and optional IfMatchETag.
- Implement ExistsResultAsync without loading chunks.
- Implement idempotent DeleteResultAsync with write lease and chunk cleanup.
- Implement prefix listing without loading chunks.
- Implement approved full container scan.
- Implement keyset continuation token using last blob name.
- Project only BlobInfo fields for list operations.

Implementation exclusions:

- Do not implement Azure Blob provider.
- Do not implement range downloads.
- Do not expose public lease APIs.
- Do not create new projects.

Required tests:

- Get properties does not query chunk content.
- Update properties does not rewrite chunks.
- Update missing blob returns not found.
- IfMatchETag mismatch returns concurrency failure.
- ETag changes after property update.
- Exists does not query chunk content.
- Delete removes blob and chunks.
- Delete missing blob succeeds.
- Delete acquires write lease.
- Prefix listing returns matching blobs only.
- Full scan requires approval.
- Listing is deterministic.
- Listing returns no content streams.
- Keyset paging works.
- Continuation token is query-bound.

Behavioral guarantees:

- Metadata operations do not download content.
- Delete is idempotent.
- EF provider satisfies shared contract tests.
- Full scans require both global and query approval.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run full EF provider tests and shared contract tests for EF.
- Review query performance and chunk avoidance before Azure implementation.
```

## Prompt 9 — Azure Blob Provider

```text
Implement Azure Blob provider in the existing infrastructure project that owns Azure storage integrations.

Implementation focus:

- Add Azure Blob provider registration through WithAzureBlobClient.
- Map BlobKey.Container and BlobKey.Name to Azure container and blob name.
- Use ContentType.MimeType() for native HTTP content type.
- Convert PropertyBag to and from Azure metadata.
- Upload using native streaming APIs.
- Respect Overwrite and FailIfExists using native conditional behavior where possible.
- Calculate SHA-256 while reading upload content.
- Enforce MaxBlobSize.
- Verify ExpectedContentHash.
- Persist devkit SHA-256 hash as metadata.
- Download using native readable stream APIs.
- Get properties/head without downloading content.
- Update properties/metadata and honor IfMatchETag where supported.
- Exists without downloading content.
- Delete idempotently.
- List with prefix and approved full scans using Azure continuation tokens.
- Map provider failures to typed Result errors.

Implementation exclusions:

- Do not implement Azure Table, Cosmos, or FileSystem providers.
- Do not expose raw Azure continuation tokens as public contract.
- Do not add auth/security features.
- Do not implement presigned URLs.
- Do not create new projects.

Required tests:

- Registration resolves named Azure client.
- Upload uses native blob upload abstraction or test double.
- Upload stores SHA-256 hash metadata.
- Upload enforces MaxBlobSize.
- FailIfExists maps to native conditional upload.
- Download returns readable stream.
- Get properties does not download content.
- Metadata maps to and from PropertyBag.
- ContentType maps through ContentTypeExtensions.
- ETag maps correctly.
- IfMatchETag maps to conditional update.
- Prefix listing uses native prefix.
- Full scan requires approval.
- Azure continuation token is wrapped in opaque devkit token.
- Delete missing blob succeeds.
- Provider request failures become Result failures.

Behavioral guarantees:

- Azure SDK types do not leak into Application abstractions.
- Native provider details stay inside the Azure provider.
- Public continuation tokens remain opaque and query-bound.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run Azure provider tests and shared contract tests using test doubles/emulator strategy already used in the repo.
- Review provider exception mapping before hardening.
```

## Prompt 10 — Cross-Provider Contract Completion

```text
Complete and normalize shared contract tests across all implemented providers.

Implementation focus:

- Ensure the same provider-neutral contract tests run against InMemory, EF Core, and Azure Blob providers where feasible.
- Mark provider-specific limitations only when the specification allows them.
- Remove duplicate tests that should be shared.
- Ensure each provider passes common upload, download, properties, exists, delete, list, hash, size, content type, continuation token, and full-scan behavior tests.

Implementation exclusions:

- Do not add new feature behavior.
- Do not relax the spec to make tests pass.
- Do not create new projects.
- Do not add dashboard/API tests.

Required tests:

- Shared contract test suite covers all specification acceptance criteria that apply to every provider.
- Provider-specific tests cover only provider-specific mechanics.
- No provider downloads content for list/properties/exists/update properties.
- No provider exposes range or resumable APIs.

Behavioral guarantees:

- Provider behavior is consistent across implementations.
- Tests protect runtime semantics, not implementation accidents.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run all storage-related unit and integration tests.
- Run dotnet test --nologo /p:UseSharedCompilation=false if feasible.
- Review test names for behavioral clarity.
```

## Prompt 11 — Documentation, Examples, And Integration Wiring

```text
Update documentation and examples after runtime behavior is implemented and tested.

Implementation focus:

- Update feature docs to match final APIs.
- Add concise usage examples for AddBlobStorage, named clients, upload, expected hash, download, properties, listing, full scan, and delete.
- Wire Blob Storage into an existing example application only if the specification or human reviewer asks for it.
- Keep examples minimal and aligned with tested behavior.

Implementation exclusions:

- Do not add new projects.
- Do not create a dashboard page.
- Do not add REST APIs.
- Do not introduce untested sample-only APIs.
- Do not change core behavior while updating docs unless a test fails and the spec requires the fix.

Required tests:

- Build succeeds after docs/example code changes.
- If executable examples or sample app wiring are changed, run the affected sample build/tests.
- Verify snippets compile when they live in source files.

Behavioral guarantees:

- Docs describe implemented behavior, not planned behavior.
- Examples do not bypass named client factory, Result-native flow, content type utilities, or stream ownership rules.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run affected tests.
- Review docs for stale API names.
```

## Final Hardening Prompt

```text
Perform final hardening for Blob Storage.

Implementation focus:

- Review all public APIs for naming, XML docs, examples, and consistency with the specification.
- Review Result error mapping for expected failures.
- Review stream ownership and disposal.
- Review MaxBlobSize enforcement paths.
- Review SHA-256 calculation paths.
- Review retry safety for upload streams.
- Review timeout/cancellation distinction.
- Review telemetry labels for cardinality.
- Review EF transaction and lease behavior.
- Review Azure provider exception mapping.
- Remove dead code, unused abstractions, and speculative extension points.
- Ensure no new projects were added.

Implementation exclusions:

- Do not add new features.
- Do not change public API shape unless required to fix a spec mismatch.
- Do not add dashboard pages, REST APIs, auth/security, multi-tenancy, backup/restore, range, or resumable transfer support.

Required tests:

- Full build.
- Full test suite if feasible.
- Storage-specific test suite.
- Any provider-specific integration tests that are available in the existing repo.

Behavioral guarantees:

- The implementation matches the specification.
- Runtime behavior is protected by tests.
- Provider-specific mechanics do not leak into provider-neutral layers.
- No placeholders or speculative systems remain.

Validation checkpoint:

- Run dotnet build --nologo /p:UseSharedCompilation=false.
- Run dotnet test --nologo /p:UseSharedCompilation=false.
- Produce final review summary with files changed, tests run, known risks, and explicit confirmation that no new projects were created.
```

## Optional Fleet Prompt

Use fleet execution only after prompts 1 through 5 are complete and reviewed. Do not run fleet agents against the same files at the same time.

```text
Coordinate multiple agents for Blob Storage implementation.

Global rules:

- Do not create new projects.
- Do not modify shared public API files concurrently.
- Do not change specification behavior without human approval.
- Do not implement more than the assigned slice.
- Rebase or refresh context before editing.
- Each agent must run build/tests for its slice and report changed files.

Safe parallel lanes after foundation/runtime review:

Lane A: EF Core model and provider implementation
- Own existing EF infrastructure files and EF provider tests.
- Do not edit Azure provider files.
- Do not edit shared public API files unless coordinated.

Lane B: Azure Blob provider implementation
- Own existing Azure infrastructure files and Azure provider tests.
- Do not edit EF provider files.
- Do not edit shared public API files unless coordinated.

Lane C: Behavior and observability tests
- Own logging, metrics, retry, timeout behavior files and tests.
- Do not edit provider mechanics.

Lane D: Documentation and examples
- Start only after API shape is stable.
- Do not change runtime behavior.

Merge order:

1. Shared public API and validators
2. Runtime registration/factory/health
3. InMemory and shared contract tests
4. Behaviors
5. EF provider
6. Azure provider
7. Cross-provider contract normalization
8. Docs and final hardening
```

## Recommended Execution Order

1. Architecture Analysis Prompt
2. Prompt 1 — Foundation Models And Errors
3. Prompt 2 — Hashing, Size Limits, Validation, And Query Builder
4. Prompt 3 — Runtime Client, Provider Contract, Registration, Factory, And Health Check
5. Prompt 4 — InMemory Provider And Contract Test Harness
6. Prompt 5 — Client Behaviors: Logging, Metrics, Retry, Timeout
7. Prompt 6 — EF Core Storage Model And Context Contract
8. Prompt 7 — EF Core Upload, Download, Hashing, Size Limits, And Leases
9. Prompt 8 — EF Core Properties, Exists, Delete, Listing, And Continuation Tokens
10. Prompt 9 — Azure Blob Provider
11. Prompt 10 — Cross-Provider Contract Completion
12. Prompt 11 — Documentation, Examples, And Integration Wiring
13. Final Hardening Prompt

Human review is required after every numbered prompt. Do not continue to the next phase until the previous phase builds, tests pass or failures are understood, and architecture drift has been reviewed.
