// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Owns one bounded staging payload and permits one conditional publication attempt before disposal.</summary>
/// <example><code>await using var session = staged; var outcome = await session.PublishAsync(metadata);</code></example>
public sealed class InMemoryCacheWriteSession : ICacheProviderWriteSession
{
    private readonly Lock gate = new();
    private readonly InMemoryCacheStoreProvider owner;
    private readonly CacheAdmissionLease admission;
    private bool attempted;
    private bool published;
    private bool disposed;

    /// <summary>Owns one bounded staging payload and permits one conditional publication attempt before disposal.</summary>
    /// <param name="owner">The native provider owning publication and cleanup.</param>
    /// <param name="state">The declared native publication state.</param>
    /// <param name="request">The original conditional write request.</param>
    /// <param name="payload">The owned bounded chunked payload.</param>
    /// <param name="admission">Staging admission retained until actual disposal.</param>
    public InMemoryCacheWriteSession(InMemoryCacheStoreProvider owner, InMemoryCachePublicationState state,
        CacheProviderWriteRequest request, InMemoryCachePayload payload, CacheAdmissionLease admission)
    {
        this.owner = owner;
        this.State = state;
        this.Request = request;
        this.Payload = payload;
        this.admission = admission;
        this.OperationId = request.OperationId ?? Guid.NewGuid().ToString("N");
        this.Version = CacheEntryVersion.New();
    }

    /// <inheritdoc />
    public Stream Stream => this.Payload.Writer;

    /// <inheritdoc />
    public string OperationId { get; }

    /// <inheritdoc />
    public CacheEntryVersion Version { get; }

    /// <summary>Gets the native collection publication authority and resource hierarchy.</summary>
    public InMemoryCachePublicationState State { get; }

    /// <summary>Gets the original conditional request and captured publication fence.</summary>
    public CacheProviderWriteRequest Request { get; }

    /// <summary>Gets the staged payload whose ownership transfers only on confirmed publication.</summary>
    public InMemoryCachePayload Payload { get; }

    /// <summary>Gets unpublished payload storage charges; confirmed publication transfers them to retained storage.</summary>
    public long StagingCharge => Volatile.Read(ref this.published) ? 0 : this.Payload.StorageCharge;

    /// <inheritdoc />
    public async ValueTask<Result<Stream>> OpenStagedReadAsync(CancellationToken cancellationToken = default)
    {
        var admission = await this.State.Readers.AcquireAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result<Stream>.Failure(admission);
        }

        lock (this.gate)
        {
            if (this.disposed || this.attempted)
            {
                admission.Value.Dispose();
                return Result<Stream>.Failure(new CacheError(CacheErrorCode.Contract, "The staging session no longer permits readers."));
            }

            try
            {
                return Result<Stream>.Success(new OwnedStream(this.Payload.OpenRead(), admission.Value.Dispose));
            }
            catch (CacheCapacityException)
            {
                admission.Value.Dispose();
                return Result<Stream>.Failure(new CacheError(CacheErrorCode.Capacity, "Staging snapshot capacity was exhausted."));
            }
            catch
            {
                admission.Value.Dispose();
                throw;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> PublishAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            if (this.disposed || this.attempted)
            {
                return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(
                    new CacheError(CacheErrorCode.Contract, "A staging session permits one publication attempt.")));
            }

            this.attempted = true;
            try
            {
                this.Payload.Freeze();
            }
            catch (InvalidOperationException)
            {
                return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(
                    new CacheError(CacheErrorCode.Contract, "A failed or prematurely closed staging destination cannot publish.")));
            }

            Result<CacheWriteOutcome> result;
            try
            {
                result = this.owner.Commit(this, metadata, cancellationToken);
            }
            catch (CacheCapacityException)
            {
                return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Capacity, "Native publication index admission was rejected.")));
            }

            if (result.IsSuccess && result.Value.StorageOutcome == CacheStorageOutcome.Stored)
            {
                this.published = true;
                this.Payload.MarkPublished();
                if (result.Value.Metadata.IsNotFound)
                {
                    this.Payload.Release();
                }
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return ValueTask.CompletedTask;
            }

            this.disposed = true;
            if (!this.published)
            {
                this.Payload.Release();
            }

            this.admission.Dispose();
        }

        this.owner.SessionEnded(this);
        return ValueTask.CompletedTask;
    }
}