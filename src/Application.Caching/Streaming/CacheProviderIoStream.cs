// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

// Buffer ownership and prompt caller deadlines belong to the outer transfer/source. Here an
// occupied native read/write retains its admission until the actual call ends, even on cancellation.
/// <summary>Holds bounded provider admission through each actual native read, write, or flush call.</summary>
/// <param name="inner">The native stream; its provider handle or session retains disposal ownership.</param>
/// <param name="admission">The optional provider-call admission; null bypasses admission.</param>
/// <param name="timeout">The maximum wait to obtain provider-call admission.</param>
/// <param name="admit">Whether admission applies immediately or starts after EnableAdmission is called.</param>
/// <param name="infrastructureObserver">Optional enabled provider-circuit observer; it never gates an already acquired stream or replays native I/O.</param>
/// <param name="observations">Optional predeclared collection facts for post-acquisition native failures.</param>
/// <remarks>Native cancellation is forwarded without releasing admission before the native call ends. I/O, access, and database failures become classified CacheTransferException failures.</remarks>
/// <example><code>var stream = new CacheProviderIoStream(nativeStream, resources.ProviderIO, attemptTimeout);</code></example>
public sealed class CacheProviderIoStream(Stream inner, CacheAdmission admission, TimeSpan timeout, bool admit = true,
    ICacheProviderInfrastructureObserver infrastructureObserver = null, CacheObservationSlot observations = null) : Stream
{
    private bool metered = admit;

    /// <summary>Enables provider-call admission for subsequent reads, writes, and flushes.</summary>
    public void EnableAdmission() => this.metered = true;

    /// <inheritdoc />
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc />
    public override bool CanWrite => inner.CanWrite;

    /// <inheritdoc />
    public override bool CanSeek => inner.CanSeek;

    /// <inheritdoc />
    public override long Length => inner.Length;

    /// <inheritdoc />
    public override long Position { get => inner.Position; set => inner.Position = value; }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var lease = await this.AcquireAsync(cancellationToken).ConfigureAwait(false);
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            await this.ObserveAsync(epoch, false, cancellationToken).ConfigureAwait(false);
            return read;
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            await this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, cancellationToken).ConfigureAwait(false);

            throw classified;
        }
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        using var lease = this.AcquireAsync(default).AsTask().GetAwaiter().GetResult();
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            var read = inner.Read(buffer, offset, count);
            this.ObserveAsync(epoch, false, default).GetAwaiter().GetResult();
            return read;
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, default).GetAwaiter().GetResult();

            throw classified;
        }
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        using var lease = this.AcquireAsync(default).AsTask().GetAwaiter().GetResult();
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            var read = inner.Read(buffer);
            this.ObserveAsync(epoch, false, default).GetAwaiter().GetResult();
            return read;
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, default).GetAwaiter().GetResult();

            throw classified;
        }
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var lease = await this.AcquireAsync(cancellationToken).ConfigureAwait(false);
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await this.ObserveAsync(epoch, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            await this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, cancellationToken).ConfigureAwait(false);

            throw classified;
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        using var lease = this.AcquireAsync(default).AsTask().GetAwaiter().GetResult();
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            inner.Write(buffer, offset, count);
            this.ObserveAsync(epoch, false, default).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, default).GetAwaiter().GetResult();

            throw classified;
        }
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        using var lease = this.AcquireAsync(default).AsTask().GetAwaiter().GetResult();
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            inner.Write(buffer);
            this.ObserveAsync(epoch, false, default).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, default).GetAwaiter().GetResult();

            throw classified;
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
        using var lease = this.AcquireAsync(default).AsTask().GetAwaiter().GetResult();
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            inner.Flush();
            this.ObserveAsync(epoch, false, default).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, default).GetAwaiter().GetResult();

            throw classified;
        }
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        using var lease = await this.AcquireAsync(cancellationToken).ConfigureAwait(false);
        var epoch = infrastructureObserver?.CircuitEpoch ?? 0;
        try
        {
            await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
            await this.ObserveAsync(epoch, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            var classified = Classify(exception);
            await this.ObserveAsync(epoch, classified.Code == CacheErrorCode.ProviderUnavailable, cancellationToken).ConfigureAwait(false);

            throw classified;
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    private async ValueTask ObserveAsync(long epoch, bool failure, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) { return; }

        if (failure && this.metered) { observations?.Record(CacheObservationKind.ProviderFailure); }

        if (infrastructureObserver is null) { return; }

        try { await infrastructureObserver.ReportInfrastructureOutcomeAsync(epoch, failure).ConfigureAwait(false); }
        catch { /* Optional observation cannot change the actual native result or disposal ownership. */ }
    }

    private static bool IsNativeFailure(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Data.Common.DbException;

    private static CacheTransferException Classify(Exception exception) => exception as CacheTransferException ?? new(exception switch
    {
        CacheCapacityException or StreamSizeLimitExceededException => CacheErrorCode.Capacity,
        InvalidDataException or EndOfStreamException => CacheErrorCode.Corruption,
        _ => CacheErrorCode.ProviderUnavailable
    });

    // Native stream disposal is owned by the provider handle/session, not this admission wrapper.
    private async ValueTask<CacheAdmissionLease> AcquireAsync(CancellationToken cancellationToken)
    {
        if (!this.metered || admission is null)
        {
            return null;
        }

        var result = await admission.AcquireAsync(timeout, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Value : throw new CacheTransferException(CacheErrorCode.Capacity);
    }
}