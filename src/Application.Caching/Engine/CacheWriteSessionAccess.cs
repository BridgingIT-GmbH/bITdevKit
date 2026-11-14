// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

// Session disposal waits for actual in-progress native publication, including a timed-out
// acknowledgement. A caller leaving cannot abort/dispose resources still used by native I/O.
/// <summary>Applies bounded native I/O accounting to one write session through actual publication and cleanup.</summary>
/// <example><code>var stage = await CacheWriteSessionAccess.BeginAsync(access, request, binding, resources, tasks, token);</code></example>
public sealed class CacheWriteSessionAccess : ICacheProviderWriteSession
{
    private readonly Lock gate = new();
    private readonly ICacheProviderWriteSession inner;
    private readonly CacheProviderAccess access;
    private readonly CacheCollectionBinding binding;
    private readonly CacheCollectionResources resources;
    private readonly CacheRuntimeTasks tasks;
    private readonly TaskCompletionSource cleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int occupied;
    private bool disposing;
    private bool disposed;

    private CacheWriteSessionAccess(ICacheProviderWriteSession inner, CacheProviderAccess access,
        CacheCollectionBinding binding, CacheCollectionResources resources, CacheRuntimeTasks tasks)
    {
        this.inner = inner;
        this.access = access;
        this.binding = binding;
        this.resources = resources;
        this.tasks = tasks;
        this.Stream = new CacheProviderIoStream(inner.Stream, this.NativeAdmission, binding.Provider.Options.AttemptTimeout,
            infrastructureObserver: this.InfrastructureObserver, observations: this.access.Observations);
    }

    /// <inheritdoc />
    public Stream Stream { get; }

    /// <inheritdoc />
    public string OperationId => this.inner.OperationId;

    /// <inheritdoc />
    public CacheEntryVersion Version => this.inner.Version;

    private ICacheProviderInfrastructureObserver InfrastructureObserver => this.binding.Provider.Options.Breaker.Enabled && this.binding.Provider.Capabilities.Persistent ?
        this.access.Provider as ICacheProviderInfrastructureObserver : null;

    private CacheAdmission NativeAdmission => StringComparer.OrdinalIgnoreCase.Equals(this.binding.Provider.Kind.Alias, "InMemory") ?
        null : this.resources.ProviderIO;

    /// <summary>Begins an admitted native write session and wraps its stream and cleanup without releasing timed-out native occupancy early.</summary>
    public static async ValueTask<Result<ICacheProviderWriteSession>> BeginAsync(CacheProviderAccess access,
        CacheProviderWriteRequest request, CacheCollectionBinding binding, CacheCollectionResources resources,
        CacheRuntimeTasks tasks, CancellationToken cancellationToken)
    {
        var result = await access.CallAsync(token => access.Provider.BeginWriteAsync(request, token), cancellationToken,
            session => session.DisposeAsync()).ConfigureAwait(false);
        return result.IsSuccess ? Result<ICacheProviderWriteSession>.Success(new CacheWriteSessionAccess(result.Value, access, binding, resources, tasks)) : result;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Stream>> OpenStagedReadAsync(CancellationToken cancellationToken = default)
    {
        this.Enter();
        var result = await this.access.CallAsync(async token =>
        {
            try { return await this.inner.OpenStagedReadAsync(token).ConfigureAwait(false); }
            finally { this.Exit(); }
        }, cancellationToken, stream => stream.DisposeAsync(), notStarted: this.ExitAsync, observeInfrastructureOutcome: true).ConfigureAwait(false);
        return result.IsSuccess ? Result<Stream>.Success(new OwnedStream(new CacheProviderIoStream(result.Value,
            this.NativeAdmission, this.binding.Provider.Options.AttemptTimeout, infrastructureObserver: this.InfrastructureObserver, observations: this.access.Observations), () => result.Value.DisposeAsync())) : result;
    }

    /// <inheritdoc />
    public async ValueTask<Result<CacheWriteOutcome>> PublishAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken = default)
    {
        this.Enter();
        var started = 0;
        var result = await this.access.CallAsync(async token =>
        {
            try
            {
                Volatile.Write(ref started, 1);
                return await this.inner.PublishAsync(metadata, token).ConfigureAwait(false);
            }
            finally { this.Exit(); }
        }, cancellationToken, notStarted: this.ExitAsync, timeoutCode: CacheErrorCode.Indeterminate, observeInfrastructureOutcome: true).ConfigureAwait(false);
        if ((result.IsFailure && result.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.Indeterminate)) ||
            (result.IsSuccess && result.Value.StorageOutcome == CacheStorageOutcome.Indeterminate))
        {
            if (Volatile.Read(ref started) != 1)
            {
                return Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Publication could not start within provider admission/readiness limits."));
            }

            // Query the original operation once. This never repeats its stream or mutation.
            var reconciled = await this.access.CallAsync(token => this.access.Provider.ReconcileAsync(this.OperationId, token),
                cancellationToken).ConfigureAwait(false);
            if (reconciled.IsSuccess && ((reconciled.Value.StorageOutcome == CacheStorageOutcome.Stored && reconciled.Value.Version == this.Version) ||
                reconciled.Value.StorageOutcome is CacheStorageOutcome.Failed or CacheStorageOutcome.Conflict or CacheStorageOutcome.Rejected))
            {
                return reconciled.Value.StorageOutcome == CacheStorageOutcome.Stored ?
                    Result<CacheWriteOutcome>.Success(reconciled.Value with { Metadata = reconciled.Value.Metadata ?? metadata }) : reconciled;
            }

            return Result<CacheWriteOutcome>.Success(new(CacheStorageOutcome.Indeterminate));
        }

        return result;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (this.gate)
        {
            this.disposing = true;
            if (this.disposed || this.occupied != 0)
            {
                return new(this.cleanup.Task);
            }

            this.disposed = true;
        }

        this.tasks.Track(this.CleanupAsync(), "write-session-cleanup");
        return new(this.cleanup.Task);
    }

    private void Enter()
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposing, this);
            this.occupied++;
        }
    }

    private ValueTask ExitAsync()
    {
        this.Exit();
        return ValueTask.CompletedTask;
    }

    private void Exit()
    {
        var release = false;
        lock (this.gate)
        {
            this.occupied--;
            if (this.occupied == 0 && this.disposing && !this.disposed)
            {
                release = this.disposed = true;
            }
        }

        if (release)
        {
            this.tasks.Track(this.CleanupAsync(), "write-session-cleanup");
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            await this.inner.DisposeAsync().ConfigureAwait(false);
            this.cleanup.TrySetResult();
        }
        catch (Exception exception)
        {
            this.cleanup.TrySetException(exception);
            throw;
        }
    }
}