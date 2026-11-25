---
goal: Fix two confirmed EF Document Storage defects
version: '1.0'
date_created: 2026-11-25
last_updated: 2026-11-25
owner: bITdevKit maintainers
status: 'Planned'
tags: [storage, documents, entity-framework, reliability, bug]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

The audit of `60410ea38` reproduced both defects on SQL Server, PostgreSQL, and SQLite. An ordinary create-only batch committed two rows and reported zero after losing the commit acknowledgement. A permanent constraint violation made four EF upsert attempts and twelve attempts with the three-attempt client retry policy enabled.

## 1. Requirements & Constraints

- **REQ-001**. EF-03 requires `UpsertManyAsync` to report an indeterminate outcome after a lost commit acknowledgement and stop fallback and automatic replay. Preserve the results of previously confirmed chunks.
- **REQ-002**. EF-07 requires a known permanent EF write failure to surface after one save attempt even with client retries enabled. Retain bounded conflict resolution for unconditional upserts and allow client retry when a known transient failure leaves the write uncommitted.
- **CON-001**. Test the three relational EF providers. Preserve signatures, schema, and the established behavior of providers that continue constructing unclassified document provider errors.
- **CON-002**. Use the SDK and packages pinned by the repo. Run top-level test commands sequentially in each worktree. Use disposable databases and independent reads after fault injection.
- **GUD-001**. Make one commit per defect, with its regression and applicable guide and changelog updates. Add XML documentation with an `<example>` block to new public and protected members.
- **PAT-001**. Keep engine error classification in Infrastructure and retry decisions in the Application behavior. Use the existing indeterminate result type and `ITransientStorageError`.

## 2. Implementation Steps

### Implementation Phase 1

- **GOAL-001**. Give ordinary relational upsert batches an explicit commit boundary and reproduce the acknowledgement failure through the public client.
- **Completion**. TEST-001 and TEST-002 pass on SQL Server, PostgreSQL, and SQLite. The existing confirmed-prefix and Dimension tests also pass.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-001 | EF-03. In FILE-001 `UpsertManyAsync` and `UpsertManyInOneCommitAsync`, start a provider-owned explicit transaction for every relational bulk upsert. Run the explicit transaction under the context's execution strategy and create fresh transaction-local state on each strategy attempt. In FILE-002, rename `CommitDimensionsAsync` to `CommitDocumentMutationAsync` and reuse it for all relational batch commits, including batches without Dimensions. Return `DocumentStoreIndeterminateOperationError` from inside the strategy after commit acknowledgement fails, before any sequential fallback or cache publication. Allow relational sequential fallback only when `SaveChangesAsync` throws `DbUpdateException` before commit and rollback completes successfully. Keep the existing nonrelational path and earlier confirmed chunk results. Add TEST-001 and TEST-002. | | |

### Implementation Phase 2

- **GOAL-002**. Stop retries for permanent EF upsert failures while preserving contention recovery.
- **Depends on**. TASK-001 precedes edits to FILE-001. Complete TASK-006 of the Blob Storage plan to supply FILE-005.
- **Completion**. TEST-003 through TEST-005 pass. The decorated permanent-error path records exactly one save attempt on each engine.

| Task | Description | Completed | Date |
| --- | --- | --- | --- |
| TASK-002 | EF-07. In FILE-001 `UpsertAsync`, keep the existing four-attempt re-read for unconditional writes only after `DbUpdateConcurrencyException` or a unique violation on the document owner key `(TypeHash, PartitionKeyHash, RowKeyHash)` during insert. Use the owner index metadata and native unique violation details to identify that collision. Throw cancellation as before. Return a classified `DocumentStoreProviderError` for other EF upsert failures. For replayable writes, use FILE-005 and require a known uncommitted outcome. Treat conflicts, permanent constraints, and ambiguous transport or commit failures as terminal. In FILE-003 add a `DocumentStoreProviderError(string message, Exception innerException, bool isTransient)` constructor and implement `ITransientStorageError`. Keep the existing constructor signature and its historical retry behavior for other providers. In FILE-004 `IsRetryable`, require every error to explicitly report `IsTransient = true`. Add provider and decorator tests in FILE-006 and FILE-007. | | |

## 3. Alternatives

- **ALT-001**. Retrying the failed bulk batch row by row after any exception can conflict with the rows it already committed. Capture the outcome at the explicit commit boundary.
- **ALT-002**. Disabling all client provider retries would change native providers that rely on the existing default. Preserve the old constructor behavior and classify errors produced by EF upsert.
- **ALT-003**. Treating every EF update failure as another opportunity to re-read the document multiplies permanent errors. Reserve the local loop for resolving owner-row races.

## 4. Dependencies

- **DEP-001**. Use the relational document provider test fixtures and the existing Dimension commit interceptor patterns. Test SQL Server 2022, PostgreSQL 16, and SQLite. Exercise a pooled SQL Server registration and a SQL Server execution strategy with retries enabled for the batch commit regression.
- **DEP-002**. TASK-002 uses `RelationalStorageErrors.IsTransient` from TASK-006 of the [EF Blob Storage defect plan](pln-refactor-ef-blob-reliability-1.md).

## 5. Files

- **FILE-001**. `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.cs`
- **FILE-002**. `src/Infrastructure.EntityFramework/Storage/Documents/EntityFrameworkDocumentStoreProvider.Dimensions.cs`
- **FILE-003**. `src/Common.Results/Errors/Builder/Errors.Storage.cs`
- **FILE-004**. `src/Application.Storage/Documents/Behaviors/RetryDocumentStoreClientBehavior.cs`
- **FILE-005**. `src/Infrastructure.EntityFramework/Storage/RelationalStorageErrors.cs`, supplied by the Blob Storage plan.
- **FILE-006**. `tests/Infrastructure.IntegrationTests/EntityFramework/Storage/Documents/EntityFrameworkDocumentReliabilityTests.cs`, new regression fixture using the adjacent provider contract fixtures as the setup reference.
- **FILE-007**. `tests/Application.UnitTests/Storage/Documents/DocumentDimensionsRetryTests.cs`, existing decorator test fixture.
- **FILE-008**. `docs/features-storage-documents.md` and `CHANGELOG.md`

## 6. Testing

- **TEST-001**. Write two create-only documents without Dimensions through `UpsertManyAsync`. Use `AutoTransactionBehavior.Always` to reproduce on the reference revision. Throw a `TimeoutException` from `TransactionCommittedAsync` after the real commit. Assert `DocumentStoreIndeterminateOperationError`, no sequential writes or decorator replay, and two persisted values read from a fresh context. Repeat with a retrying SQL Server execution strategy and assert the same single commit attempt.
- **TEST-002**. Fail a bulk save before commit and complete its rollback. Verify fallback reports only the confirmed prefix and the correct failed key. Run `StorageDimensionsBatchFailureProofTests` to confirm a later chunk with an indeterminate outcome retains the earlier committed prefix. Verify cancellation before commit propagates and the failed batch leaves no row behind.
- **TEST-003**. Add a permanent check constraint or, on SQLite, a trigger that rejects a synthetic row. Use SQL Server error `547`, PostgreSQL SQLSTATE `23514`, and SQLite `SQLITE_CONSTRAINT`. Count `SavingChangesAsync` calls. Assert that direct and three-attempt decorated unconditional upserts each make one attempt, return a non-transient `DocumentStoreProviderError`, and persist no row.
- **TEST-004**. Inject SQL Server `1205` before an attempted write commits and clear the fault after the first attempt. Assert that an opt-in client retry succeeds and the provider does not multiply attempts. Use a concurrency barrier to confirm that unconditional owner-insert races still converge and conditional conflicts remain terminal.
- **TEST-005**. In the decorator fixture verify one invocation for a classified permanent error and for an indeterminate outcome. Verify bounded retry for a classified transient error and the previous retry count with the legacy constructor. Add a canceled token case with no replay.
- **TEST-006**. Run `dotnet test tests/Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj --filter FullyQualifiedName~EntityFrameworkDocumentReliabilityTests`. Run the SQL Server, PostgreSQL, and SQLite document provider suites and `StorageDimensionsMutationProofTests`. Run `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter FullyQualifiedName~DocumentDimensionsRetryTests`.

## 7. Risks & Assumptions

- **RISK-001**. An exception after commit starts cannot establish whether the server committed the write. Return the indeterminate result and document how to reconcile before replay.
- **RISK-002**. A unique violation from another index can be permanent. Match the existing owner key index before re-entering the EF provider's unconditional upsert loop.
- **RISK-003**. The specification and guide disagree about document page ordering. Resolve that contract separately from the two confirmed defects before changing order or cursor semantics.
- **ASSUMPTION-001**. Non-EF providers continue to control their retry classification through their choice of error constructor or their implementation of `ITransientStorageError`.

## 8. Related Specifications / Further Reading

- [Storage reliability audit plan](pln-process-storage-reliability-audit-1.md)
- [EF Blob Storage defect plan](pln-refactor-ef-blob-reliability-1.md)
- [Document Storage guide](../features-storage-documents.md)
- [Document Storage paging specification](../specs/spec-application-storage-documents-paging.md)
