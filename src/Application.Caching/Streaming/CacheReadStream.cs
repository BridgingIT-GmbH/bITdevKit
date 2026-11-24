// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Security.Cryptography;

// Native/decompressor reads target only this stream's owned buffer. A caller can time out and
// return its own pooled destination while ignored native cancellation still occupies our buffer.
/// <summary>Owns a bounded logical read buffer, terminal integrity observation, and transfer cleanup.</summary>
/// <remarks>Only one positioned read or validation operation may run at a time. A canceled asynchronous read stops the caller wait while an ignored native read retains the owned buffer and reservations until it ends.</remarks>
/// <example><code>await using var handle = acquired.Value; await handle.Stream.CopyToAsync(destination); var terminal = await handle.WaitForCompletionAsync();</code></example>
public sealed class CacheReadStream : Stream
{
    private readonly Stream inner;
    private readonly CacheEntryMetadata metadata;
    private readonly Func<bool, CancellationToken, ValueTask<Result>> verify;
    private readonly Func<ValueTask> release;
    private readonly IDisposable reservation;
    private readonly CacheRuntimeTasks tasks;
    private readonly CacheTransferBudget budget;
    private readonly CancellationTokenSource stop = new();
    private readonly IncrementalHash logicalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly TaskCompletionSource<Result<CacheReadCompletion>> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource cleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long? expectedBytes;
    private byte[] buffer;
    private long delivered;
    private CacheTransferObservationSession observation;
    private volatile bool partial;
    private int reading;
    private int stopped;
    private int cleanupStarted;

    /// <summary>Adopts the already admitted stream, buffer, reservation, and release callback for one acquired snapshot.</summary>
    /// <param name="inner">The logical readable representation whose disposal precedes owner release.</param>
    /// <param name="metadata">Immutable metadata used for size and logical-hash validation.</param>
    /// <param name="ownedBuffer">The exclusively owned intermediate native-read buffer.</param>
    /// <param name="reservation">The buffer and transformation reservation retained through actual cleanup.</param>
    /// <param name="verify">The terminal verifier; true requests full-entry verification.</param>
    /// <param name="release">The asynchronous callback releasing the provider snapshot and remaining ownership.</param>
    /// <param name="tasks">The tracker for already admitted read tails and cleanup.</param>
    /// <param name="timeProvider">The transfer deadline clock.</param>
    /// <param name="options">The finite idle timeout and absolute lifetime.</param>
    /// <param name="partial">Whether consumption starts with partial-range integrity semantics.</param>
    /// <param name="expectedBytes">An optional exact byte count required before successful range completion.</param>
    public CacheReadStream(Stream inner, CacheEntryMetadata metadata, byte[] ownedBuffer, IDisposable reservation,
        Func<bool, CancellationToken, ValueTask<Result>> verify, Func<ValueTask> release, CacheRuntimeTasks tasks,
        TimeProvider timeProvider, CacheStreamOptions options, bool partial = false, long? expectedBytes = null)
    {
        this.inner = inner;
        this.metadata = metadata;
        this.buffer = ownedBuffer;
        this.reservation = reservation;
        this.verify = verify;
        this.release = release;
        this.tasks = tasks;
        this.partial = partial;
        this.expectedBytes = expectedBytes;
        this.budget = new(timeProvider, options, () => this.Stop(Failure(CacheErrorCode.CoordinationTimeout,
            "The cache read exceeded its idle or absolute transfer limit.")));
        this.budget.Start();
    }

    /// <inheritdoc />
    public override bool CanRead => Volatile.Read(ref this.stopped) == 0;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override bool CanSeek => this.CanRead && this.inner.CanSeek;

    /// <inheritdoc />
    public override long Length => this.inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => this.inner.Position;
        set => this.Seek(value, SeekOrigin.Begin);
    }

    /// <summary>Gets logical bytes copied into caller buffers, excluding bytes read only into the owned buffer.</summary>
    public long BytesDelivered => Interlocked.Read(ref this.delivered);

    /// <summary>Gets whether range acquisition or a seek has limited integrity to affected stored frames.</summary>
    public bool IsPartial => this.partial;

    /// <summary>Attaches the first transfer-observation session and immediately reports an already established terminal outcome.</summary>
    /// <param name="session">The optional session; null or later attachment attempts have no effect.</param>
    public void Observe(CacheTransferObservationSession session)
    {
        if (session is null) { return; }

        if (Interlocked.CompareExchange(ref this.observation, session, null) is not null) { return; }

        if (this.completion.Task.IsCompletedSuccessfully) { session.Complete(this.completion.Task.Result, this.BytesDelivered); }
    }

    private void Complete(Result<CacheReadCompletion> terminal)
    {
        if (this.completion.TrySetResult(terminal)) { Volatile.Read(ref this.observation)?.Complete(terminal, this.BytesDelivered); }
    }

    /// <summary>Runs an explicit whole-representation verifier under the same read exclusivity and transfer deadlines.</summary>
    /// <param name="validation">The verifier receiving a progress callback and linked cancellation token.</param>
    /// <param name="cancellationToken">Cancellation forwarded with the stream stop and deadline tokens.</param>
    /// <returns>The verifier result; concurrent positioned work is rejected and expired or stopped transfers fail.</returns>
    public async ValueTask<Result> ValidateWholeAsync(Func<Action, CancellationToken, ValueTask<Result>> validation,
        CancellationToken cancellationToken)
    {
        this.BeginRead();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.budget.Token, this.stop.Token);
            var result = await validation(this.budget.Progress, linked.Token).ConfigureAwait(false);
            this.CheckActive();
            return result;
        }
        finally
        {
            Volatile.Write(ref this.reading, 0);
            this.CleanupWhenIdle();
        }
    }

    /// <summary>Waits for the existing terminal observation without reading, draining, or disposing the stream.</summary>
    /// <param name="cancellationToken">Cancels this observation wait without canceling consumption.</param>
    /// <returns>Validated or range completion, incomplete consumption, or the classified terminal failure.</returns>
    public async ValueTask<Result<CacheReadCompletion>> WaitForCompletionAsync(CancellationToken cancellationToken)
    {
        // Observing never reads/drains the consumer's stream.
        return await this.completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        this.BeginRead();
        CancellationTokenSource linked = null;
        var detached = false;
        try
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.budget.Token, this.stop.Token);
            var pending = this.ReadOwnedAsync(Math.Min(destination.Length, this.buffer.Length), linked.Token);
            OwnedRead read;
            if (pending.IsCompletedSuccessfully)
            {
                read = pending.Result;
            }
            else
            {
                var actual = pending.AsTask();
                try
                {
                    read = await actual.WaitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    detached = true;
                    this.Stop(Failure(this.budget.HasExpired ? CacheErrorCode.CoordinationTimeout : CacheErrorCode.Incomplete,
                        "The cache read was interrupted before completion."));
                    this.tasks.Track(this.DrainOwnedAsync(actual, linked), "read-tail");
                    linked = null;
                    throw;
                }
            }

            this.CheckActive();
            if (read.Terminal.HasValue)
            {
                this.Complete(read.Terminal.Value);
                if (read.Terminal.Value.IsFailure)
                {
                    this.Stop(read.Terminal.Value);
                    throw new CacheTransferException(read.Terminal.Value.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.Corruption);
                }
            }

            if (read.Count > 0)
            {
                this.ValidateDelivery(read.Count);
                this.buffer.AsMemory(0, read.Count).CopyTo(destination);
                this.RecordDelivery(read.Count);
            }

            return read.Count;
        }
        catch (OperationCanceledException) when (this.budget.HasExpired)
        {
            throw new CacheTransferException(CacheErrorCode.CoordinationTimeout);
        }
        catch (OperationCanceledException)
        {
            this.Stop(Failure(CacheErrorCode.Incomplete, "The consumer cancelled the cache read."));
            throw;
        }
        catch (Exception exception)
        {
            this.Stop(Failure(Classify(exception), "The cache read failed before verified completion."));
            throw;
        }
        finally
        {
            linked?.Dispose();
            if (!detached)
            {
                Volatile.Write(ref this.reading, 0);
                this.CleanupWhenIdle();
            }
        }
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] destination, int offset, int count) => this.Read(destination.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> destination)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        this.BeginRead();
        try
        {
            var count = this.inner.Read(this.buffer.AsSpan(0, Math.Min(destination.Length, this.buffer.Length)));
            this.CheckActive();
            if (count == 0)
            {
                var terminal = this.VerifyEndAsync(this.stop.Token).AsTask().GetAwaiter().GetResult();
                this.Complete(terminal);
                if (terminal.IsFailure)
                {
                    this.Stop(terminal);
                    throw new CacheTransferException(terminal.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.Corruption);
                }
            }
            else
            {
                this.ValidateDelivery(count);
                this.buffer.AsSpan(0, count).CopyTo(destination);
                this.RecordDelivery(count);
            }

            return count;
        }
        catch (Exception exception)
        {
            this.Stop(Failure(Classify(exception), "The cache read failed before verified completion."));
            throw;
        }
        finally
        {
            Volatile.Write(ref this.reading, 0);
            this.CleanupWhenIdle();
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        this.BeginRead();
        try
        {
            var position = this.inner.Seek(offset, origin);
            this.partial = true;
            this.budget.Progress();
            return position;
        }
        finally
        {
            Volatile.Write(ref this.reading, 0);
            this.CleanupWhenIdle();
        }
    }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        this.Stop(Result<CacheReadCompletion>.Success(CacheReadCompletion.Incomplete));
        GC.SuppressFinalize(this);
        // An ignored pending native read owns cleanup and all reservations until it actually ends.
        return Volatile.Read(ref this.reading) == 0 ? new(this.cleanup.Task) : ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Stop(Result<CacheReadCompletion>.Success(CacheReadCompletion.Incomplete));
        }

        base.Dispose(disposing);
    }

    private void BeginRead()
    {
        if (Interlocked.CompareExchange(ref this.reading, 1, 0) != 0)
        {
            throw new InvalidOperationException("A cache handle permits one positioned read at a time.");
        }

        try
        {
            this.CheckActive();
        }
        catch
        {
            Volatile.Write(ref this.reading, 0);
            this.CleanupWhenIdle();
            throw;
        }
    }

    private void CheckActive()
    {
        if (Volatile.Read(ref this.stopped) != 0 || this.budget.HasExpired)
        {
            throw new CacheTransferException(this.budget.HasExpired ? CacheErrorCode.CoordinationTimeout : CacheErrorCode.Incomplete);
        }
    }

    private void ValidateDelivery(int count)
    {
        var limit = this.expectedBytes ?? (!this.partial ? this.metadata.LogicalSizeBytes : long.MaxValue);
        if (count > limit - this.delivered)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }

    private void RecordDelivery(int count)
    {
        if (!this.partial)
        {
            this.logicalHash.AppendData(this.buffer.AsSpan(0, count));
        }

        Interlocked.Add(ref this.delivered, count);
        this.budget.Progress();
    }

    private async ValueTask<OwnedRead> ReadOwnedAsync(int maximum, CancellationToken cancellationToken)
    {
        var count = await this.inner.ReadAsync(this.buffer.AsMemory(0, maximum), cancellationToken).ConfigureAwait(false);
        return count > 0 ? new(count, null) : new(0, await this.VerifyEndAsync(cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<Result<CacheReadCompletion>> VerifyEndAsync(CancellationToken cancellationToken)
    {
        if (this.completion.Task.IsCompletedSuccessfully)
        {
            return this.completion.Task.Result;
        }

        if (this.expectedBytes.HasValue && this.delivered != this.expectedBytes.Value)
        {
            return Failure(CacheErrorCode.Corruption, "The selected cache range was truncated.");
        }

        if (!this.partial)
        {
            var actual = Convert.ToHexStringLower(this.logicalHash.GetHashAndReset());
            if (this.delivered != this.metadata.LogicalSizeBytes ||
                !StringComparer.OrdinalIgnoreCase.Equals(actual, this.metadata.PayloadHash))
            {
                return Failure(CacheErrorCode.Corruption, "Logical cache payload integrity failed.");
            }
        }

        var verified = await this.verify(!this.partial, cancellationToken).ConfigureAwait(false);
        return verified.IsSuccess
            ? Result<CacheReadCompletion>.Success(this.partial ? CacheReadCompletion.RangeCompleted : CacheReadCompletion.Validated)
            : Result<CacheReadCompletion>.Failure(verified);
    }

    private void Stop(Result<CacheReadCompletion> terminal)
    {
        this.Complete(terminal);
        if (Interlocked.Exchange(ref this.stopped, 1) == 0)
        {
            try { this.stop.Cancel(); }
            catch (AggregateException) { /* Cancellation callback faults do not release occupied work or skip cleanup. */ }
        }

        this.CleanupWhenIdle();
    }

    private async Task DrainOwnedAsync(Task<OwnedRead> actual, CancellationTokenSource linked)
    {
        try
        {
            await actual.ConfigureAwait(false);
        }
        catch
        {
            // The caller already received the controlled transfer failure. Observe the actual tail.
        }
        finally
        {
            linked.Dispose();
            Volatile.Write(ref this.reading, 0);
            this.CleanupWhenIdle();
        }
    }

    private void CleanupWhenIdle()
    {
        if (Volatile.Read(ref this.stopped) != 0 && Volatile.Read(ref this.reading) == 0 &&
            Interlocked.CompareExchange(ref this.cleanupStarted, 1, 0) == 0)
        {
            this.tasks.Track(this.CleanupAsync(), "read-cleanup");
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            try
            {
                await this.inner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await this.release().ConfigureAwait(false);
            }
        }
        finally
        {
            this.buffer = null;
            this.logicalHash.Dispose();
            this.budget.Dispose();
            this.stop.Dispose();
            this.reservation.Dispose();
            this.cleanup.TrySetResult();
        }
    }

    private static CacheErrorCode Classify(Exception exception) => exception switch
    {
        CacheTransferException transfer => transfer.Code,
        StreamSizeLimitExceededException => CacheErrorCode.Capacity,
        InvalidDataException or EndOfStreamException => CacheErrorCode.Corruption,
        _ => CacheErrorCode.ProviderUnavailable
    };

    private static Result<CacheReadCompletion> Failure(CacheErrorCode code, string message) =>
        Result<CacheReadCompletion>.Failure(new CacheError(code, message));

    private readonly record struct OwnedRead(int Count, Result<CacheReadCompletion>? Terminal);
}