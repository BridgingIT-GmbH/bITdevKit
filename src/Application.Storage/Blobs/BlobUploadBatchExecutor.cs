// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Coordinates bounded blob uploads through an existing client's per-item pipeline.</summary>
/// <remarks>Uploads commit independently. The coordinator never buffers, disposes, or retries caller-owned streams.</remarks>
/// <example><code>var result = await BlobUploadBatchExecutor.ExecuteAsync(blobs, uploads, new() { MaxConcurrency = 4 }, cancellationToken);</code></example>
public static class BlobUploadBatchExecutor
{
    /// <summary>Attempts every valid independent upload and retains each outcome at its input position.</summary>
    /// <param name="client">The client whose UploadAsync pipeline handles each request.</param>
    /// <param name="uploads">At most 1000 caller-owned requests with distinct keys and stream instances.</param>
    /// <param name="options">Optional concurrency settings; null selects four workers.</param>
    /// <param name="cancellationToken">Cancels queued and active work without undoing earlier commits.</param>
    /// <returns>A completed outcome collection, or a validation failure for invalid batch arguments.</returns>
    /// <exception cref="OperationCanceledException">Caller cancellation interrupts the batch; no complete outcome collection is returned.</exception>
    /// <remarks>Duplicate keys and shared streams produce item-level validation failures. Unexpected per-item exceptions produce indeterminate outcomes because a provider or decorator may already have committed. The outer Result succeeds when coordination completes, even if individual uploads fail.</remarks>
    /// <example><code>var batch = await BlobUploadBatchExecutor.ExecuteAsync(blobs, uploads, new() { MaxConcurrency = 2 }, cancellationToken);</code></example>
    public static async Task<Result<BlobUploadBatchResult>> ExecuteAsync(IBlobStoreClient client,
        IReadOnlyCollection<BlobUpload> uploads, BlobUploadBatchOptions options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var concurrency = options?.MaxConcurrency ?? 4;
        if (client is null)
        {
            return Result<BlobUploadBatchResult>.Failure(new BlobStoreValidationError("A blob client is required."));
        }

        if (uploads is null || uploads.Count > BlobUploadBatchOptions.MaximumItemCount)
        {
            return Result<BlobUploadBatchResult>.Failure(new BlobStoreValidationError("Supply at most 1000 upload requests."));
        }

        if (concurrency is < 1 or > BlobUploadBatchOptions.MaximumConcurrency)
        {
            return Result<BlobUploadBatchResult>.Failure(new BlobStoreValidationError("Upload concurrency must be between 1 and 64."));
        }

        var inputs = uploads.Take(BlobUploadBatchOptions.MaximumItemCount + 1).ToArray();
        if (inputs.Length > BlobUploadBatchOptions.MaximumItemCount)
        {
            return Result<BlobUploadBatchResult>.Failure(new BlobStoreValidationError("Supply at most 1000 upload requests."));
        }

        var snapshots = new BlobUpload[inputs.Length];
        var outcomes = new BlobUploadOutcome[inputs.Length];
        for (var index = 0; index < inputs.Length; index++)
        {
            try
            {
                snapshots[index] = BlobStoredModelMapper.Snapshot(inputs[index]);
            }
            catch (Exception)
            {
                outcomes[index] = Failure(index, inputs[index]?.Key, new BlobStoreValidationError("Upload metadata could not be snapshotted."));
            }
        }

        foreach (var group in Enumerable.Range(0, inputs.Length).Where(index => snapshots[index]?.Key is not null)
            .GroupBy(index => snapshots[index].Key).Where(group => group.Count() > 1))
        {
            foreach (var index in group)
            {
                outcomes[index] = Failure(index, snapshots[index].Key, new BlobStoreValidationError("Batch upload keys must be distinct."));
            }
        }

        foreach (var group in Enumerable.Range(0, inputs.Length).Where(index => snapshots[index]?.Content is not null)
            .GroupBy(index => snapshots[index].Content, ReferenceEqualityComparer.Instance).Where(group => group.Count() > 1))
        {
            foreach (var index in group)
            {
                outcomes[index] = Failure(index, snapshots[index].Key, new BlobStoreValidationError("Each upload must own a distinct stream instance."));
            }
        }

        await Parallel.ForEachAsync(Enumerable.Range(0, inputs.Length), new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = cancellationToken
        }, async (index, token) =>
        {
            if (outcomes[index] is not null)
            {
                return;
            }

            Result<BlobInfo> result;
            try
            {
                result = await client.UploadAsync(snapshots[index], token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A decorator may throw after the provider commits. Do not imply that replay is safe.
                result = Result<BlobInfo>.Failure(new BlobStoreIndeterminateOperationError("upload", Guid.NewGuid().ToString("N")));
            }

            outcomes[index] = new()
            {
                Index = index,
                Key = snapshots[index]?.Key,
                Result = result
            };
        }).ConfigureAwait(false);

        return Result<BlobUploadBatchResult>.Success(new()
        {
            Items = Array.AsReadOnly(outcomes)
        });
    }

    private static BlobUploadOutcome Failure(int index, BlobKey key, IResultError error) =>
        new()
        {
            Index = index,
            Key = key,
            Result = Result<BlobInfo>.Failure(error)
        };
}
