# ADR-0045: Coordinate bounded blob upload batches

## Status

Accepted, 2026-11-19. The selected failure contract attempts all independent items with bounded concurrency and returns an outcome for every input item.

## Context

The blob client supports streamed individual uploads. The earlier [upload admission and chunk-flushing decision](0030-bounded-blob-upload-admission-and-ef-chunk-flushing.md) deferred `UploadManyAsync` because stream ownership, retries and partial success needed their own contract. Applications now need a public operation for bounded groups, including the measured 100-item upload and content-replacement workloads.

## Decision

Add `IBlobStoreClient.UploadManyAsync`, `BlobUploadBatchOptions`, `BlobUploadOutcome` and `BlobUploadBatchResult`. A public, XML-documented `BlobUploadBatchExecutor` implements coordination for the client, decorators and the default interface method. Existing client implementations can retain their individual upload implementation.

Use four workers by default, allow one through 64, and accept at most 1000 requests per call. Allocate bounded input/outcome metadata, snapshot application properties before scheduling, and call the existing `UploadAsync` pipeline for each item. No payload buffering, background service, durable queue or provider-specific public type is introduced. Existing per-store admission can reduce effective concurrency.

Attempt every valid independent item after an item failure. Preserve input positions in the returned collection without promising execution order. Repeated logical keys and shared stream instances produce validation outcomes for those conflicting items; unrelated items continue. Other validation, ETag, Dimensions, transform, cache and retry behavior remains per item through `UploadAsync`.

A successful outer Result means coordination finished. Callers inspect each outcome, `FailedCount` or `AllSucceeded` to determine upload success. Invalid batch arguments fail before dispatch. Unexpected exceptions from an individual client/decorator become indeterminate mutation outcomes because a commit may already have happened. There is no whole-batch retry.

Caller cancellation stops queued work and is propagated to active uploads. The method throws after active work observes cancellation, without returning a complete outcome collection. Earlier commits are not undone. Callers retain every stream and keep it open until completion or cancellation has been observed.

This is application-level parallel coordination, available with every existing provider. It does not advertise a native provider bulk-write capability, one database transaction, cross-blob atomicity or restartability. An optimized provider bulk path is a separate future change and must preserve this outcome contract.

## Consequences

Applications gain a bounded public upload API while retaining individual streaming and behavior semantics. Partial success is explicit, and results remain easy to associate with inputs. The API can improve group completion time through concurrency, but it does not remove each upload's provider roundtrips. Batch callers must manage multiple open streams and reconcile canceled or indeterminate writes before replay.

The prior deferral of the public API is superseded. Admission, per-item transactions and bounded chunk flushing continue to apply to each upload. Sequential groups and `UploadManyAsync` groups are measured separately; documentation must distinguish their concurrency and commit boundaries.
