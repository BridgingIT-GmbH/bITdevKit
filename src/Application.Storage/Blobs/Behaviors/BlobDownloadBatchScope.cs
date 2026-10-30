// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections.Concurrent;

/// <summary>
/// Carries the key set of a bulk download to the innermost provider call, so a provider that implements
/// <see cref="IBlobStoreBatchDownloadProvider" /> can read the keys in batches while each key still
/// travels through the complete client behavior pipeline on its own.
/// </summary>
/// <example>
/// <code>
/// await using var scope = BlobDownloadBatchScope.Begin(keys, maxConcurrentBatches: 4, cancellationToken);
/// var result = await blobs.DownloadAsync(keys[0], cancellationToken);
/// </code>
/// </example>
public sealed class BlobDownloadBatchScope : IAsyncDisposable
{
    private static readonly AsyncLocal<BlobDownloadBatchScope> CurrentScope = new();

    private readonly BlobKey[] keys;
    private readonly int maxConcurrentBatches;
    private readonly CancellationTokenSource cancellation;
    private readonly SemaphoreSlim batchLimit;
    private readonly ConcurrentDictionary<IBlobStoreBatchDownloadProvider, ProviderState> states = new();
    private readonly BlobDownloadBatchScope previous;
    private int disposed;

    private BlobDownloadBatchScope(
        IEnumerable<BlobKey> keys,
        int maxConcurrentBatches,
        BlobDownloadBatchScope previous,
        CancellationToken cancellationToken)
    {
        this.keys = keys.Distinct().ToArray();
        this.maxConcurrentBatches = Math.Max(1, maxConcurrentBatches);
        this.batchLimit = new SemaphoreSlim(this.maxConcurrentBatches);
        this.previous = previous;
        this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    /// <summary>
    /// Gets the scope active for the current asynchronous flow, or <c>null</c> when there is none.
    /// </summary>
    /// <example>
    /// <code>
    /// var active = BlobDownloadBatchScope.Current is not null;
    /// </code>
    /// </example>
    public static BlobDownloadBatchScope Current => CurrentScope.Value;

    /// <summary>
    /// Begins a scope for the given keys and makes it the current scope until it is disposed.
    /// </summary>
    /// <param name="keys">The keys of the bulk download.</param>
    /// <param name="maxConcurrentBatches">The maximum number of batches read at the same time.</param>
    /// <param name="cancellationToken">The token used to cancel pending batches.</param>
    /// <returns>The new scope.</returns>
    /// <example>
    /// <code>
    /// await using var scope = BlobDownloadBatchScope.Begin(keys, 4, cancellationToken);
    /// </code>
    /// </example>
    public static BlobDownloadBatchScope Begin(
        IEnumerable<BlobKey> keys,
        int maxConcurrentBatches,
        CancellationToken cancellationToken)
    {
        var scope = new BlobDownloadBatchScope(keys, maxConcurrentBatches, CurrentScope.Value, cancellationToken);
        CurrentScope.Value = scope;

        return scope;
    }

    /// <summary>
    /// Downloads one key, taking it from a batched provider read when the key is part of the scope.
    /// </summary>
    /// <param name="provider">The provider used for the single-read fallback.</param>
    /// <param name="batchProvider">The batch capability of the same provider.</param>
    /// <param name="key">The key to download.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The download result of the key.</returns>
    /// <example>
    /// <code>
    /// var result = await scope.DownloadAsync(provider, batchProvider, key, cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result<BlobDownload>> DownloadAsync(
        IBlobStoreProvider provider,
        IBlobStoreBatchDownloadProvider batchProvider,
        BlobKey key,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref this.disposed) == 0 && this.TryGetGroup(batchProvider, key, out var state, out var index))
        {
            this.StartBatches(state, index);

            var batch = await state.Groups[index].Load.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (batch.TryRemove(key, out var result))
            {
                return result;
            }
        }

        // not part of the scope, not satisfiable in a batch, or already consumed (duplicate key or retry)
        return await provider.DownloadAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the scope, cancels pending batches and disposes downloads nobody consumed.
    /// </summary>
    /// <returns>A task that completes when the scope is cleaned up.</returns>
    /// <example>
    /// <code>
    /// await scope.DisposeAsync();
    /// </code>
    /// </example>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        CurrentScope.Value = this.previous;
        await this.cancellation.CancelAsync().ConfigureAwait(false);

        foreach (var state in this.states.Values)
        {
            foreach (var group in state.Groups.Where(g => g.Load.IsValueCreated))
            {
                try
                {
                    var batch = await group.Load.Value.ConfigureAwait(false);
                    foreach (var result in batch.Values.Where(r => r.IsSuccess))
                    {
                        await result.Value.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // the batch was abandoned
                }
            }
        }

        this.batchLimit.Dispose();
        this.cancellation.Dispose();
    }

    private bool TryGetGroup(
        IBlobStoreBatchDownloadProvider batchProvider,
        BlobKey key,
        out ProviderState state,
        out int index)
    {
        state = this.states.GetOrAdd(batchProvider, this.CreateState);

        return state.KeyToGroup.TryGetValue(key, out index);
    }

    private ProviderState CreateState(IBlobStoreBatchDownloadProvider batchProvider)
    {
        var batchSize = Math.Max(1, batchProvider.MaxDownloadBatchSize);
        var groups = this.keys
            .Chunk(batchSize)
            .Select(chunk => new BatchGroup(chunk, new Lazy<Task<ConcurrentDictionary<BlobKey, Result<BlobDownload>>>>(
                () => Task.Run(() => this.LoadAsync(batchProvider, chunk), CancellationToken.None))))
            .ToArray();
        var keyToGroup = new Dictionary<BlobKey, int>();
        for (var index = 0; index < groups.Length; index++)
        {
            foreach (var key in groups[index].Keys)
            {
                keyToGroup[key] = index;
            }
        }

        return new ProviderState(groups, keyToGroup);
    }

    // Starts the requested batch plus the following ones, bounded by the allowed number of concurrent batches.
    private void StartBatches(ProviderState state, int index)
    {
        var end = Math.Min(state.Groups.Length, index + this.maxConcurrentBatches);
        for (var i = index; i < end; i++)
        {
            _ = state.Groups[i].Load.Value;
        }
    }

    private async Task<ConcurrentDictionary<BlobKey, Result<BlobDownload>>> LoadAsync(
        IBlobStoreBatchDownloadProvider batchProvider,
        BlobKey[] batchKeys)
    {
        var batch = new ConcurrentDictionary<BlobKey, Result<BlobDownload>>();
        await this.batchLimit.WaitAsync(this.cancellation.Token).ConfigureAwait(false);
        try
        {
            var results = await batchProvider.DownloadManyAsync(batchKeys, this.cancellation.Token).ConfigureAwait(false);
            foreach (var (key, result) in results)
            {
                batch[key] = result;
            }
        }
        catch (OperationCanceledException) when (this.cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // a failed batch only costs the optimization, the keys are read one by one instead
            batch.Clear();
        }
        finally
        {
            this.batchLimit.Release();
        }

        return batch;
    }

    private sealed record BatchGroup(BlobKey[] Keys, Lazy<Task<ConcurrentDictionary<BlobKey, Result<BlobDownload>>>> Load);

    private sealed record ProviderState(BatchGroup[] Groups, Dictionary<BlobKey, int> KeyToGroup);
}
