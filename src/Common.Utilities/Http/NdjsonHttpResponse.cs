// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.CompilerServices;

/// <summary>Owns one response and one exclusive typed, original-byte, or envelope consumption path.</summary>
/// <typeparam name="T">The endpoint data model.</typeparam>
/// <example><code>await using var response = result.Value; await foreach (var item in response.ReadAsync(ct)) Process(item);</code></example>
public sealed class NdjsonHttpResponse<T> : IAsyncDisposable
{
    private readonly HttpResponseMessage response;
    private readonly Stream stream;
    private readonly HttpRequestMessage ownedRequest;
    private readonly NdjsonClientOptions options;
    private readonly CancellationTokenSource lifetime = new();
    private int claimed;
    private int disposed;

    internal NdjsonHttpResponse(HttpResponseMessage response, Stream stream, HttpRequestMessage ownedRequest, NdjsonClientOptions options)
    {
        this.response = response;
        this.stream = stream;
        this.ownedRequest = ownedRequest;
        this.options = options;
    }

    /// <summary>Reads independent raw DTOs, with no implicit input timeout by default.</summary>
    /// <example><code>await foreach (var item in response.ReadAsync(ct)) Process(item);</code></example>
    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.Claim(NdjsonMode.Raw);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.lifetime.Token);
        await using var reader = new NdjsonReader(this.options.JsonOptions, this.options.ReaderOptions).ReadAsync<T>(this.stream, linked.Token).GetAsyncEnumerator();
        try
        {
            while (await this.NextAsync(reader, this.options.RawInactivityTimeout, linked.Token).ConfigureAwait(false)) { yield return reader.Current; }
        }
        finally
        {
            await this.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Reads borrowed original-byte raw frames, valid until the next pull or disposal.</summary>
    /// <example><code>await foreach (var frame in response.ReadRecordsAsync(ct)) hash.AppendData(frame.FramedBytes.Span);</code></example>
    public async IAsyncEnumerable<NdjsonReadRecord> ReadRecordsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.Claim(NdjsonMode.Raw);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.lifetime.Token);
        await using var reader = new NdjsonReader(this.options.JsonOptions, this.options.ReaderOptions).ReadRecordsAsync(this.stream, linked.Token).GetAsyncEnumerator();
        try
        {
            while (await this.NextAsync(reader, this.options.RawInactivityTimeout, linked.Token).ConfigureAwait(false)) { yield return reader.Current; }
        }
        finally
        {
            await this.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Yields heartbeat/data/completion frames, throwing on the pull that parses a terminal error.</summary>
    /// <example><code>await foreach (var frame in response.ReadEnvelopesAsync(ct)) Show(frame.Kind);</code></example>
    public async IAsyncEnumerable<NdjsonEnvelope<T>> ReadEnvelopesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.Claim(NdjsonMode.Envelope);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.lifetime.Token);
        await using var reader = new NdjsonEnvelopeReader(this.options.JsonOptions, this.options.ReaderOptions).ReadAsync<T>(this.stream, linked.Token).GetAsyncEnumerator();
        try
        {
            while (await this.NextAsync(reader, this.options.EnvelopeInactivityTimeout, linked.Token).ConfigureAwait(false))
            {
                var frame = reader.Current;
                if (frame.Kind == NdjsonEnvelopeKind.Completed)
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                    await this.DisposeAsync().ConfigureAwait(false);
                    yield return frame;
                    yield break;
                }

                yield return frame;
            }
        }
        finally
        {
            await this.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Yields only envelope data while preserving heartbeat activity and failure semantics.</summary>
    /// <example><code>await foreach (var item in response.ReadDataAsync(ct)) Process(item);</code></example>
    public async IAsyncEnumerable<T> ReadDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in this.ReadEnvelopesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (frame.Kind == NdjsonEnvelopeKind.Data) { yield return frame.Data; }
        }
    }

    /// <summary>Cancels active input and releases the response and any internally created request.</summary>
    /// <example><code>await response.DisposeAsync();</code></example>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            this.lifetime.Cancel();
            this.response.Dispose();
            this.ownedRequest?.Dispose();
            this.lifetime.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void Claim(NdjsonMode required)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        if (this.options.ExpectedMode != required || Interlocked.CompareExchange(ref this.claimed, 1, 0) != 0)
        {
            throw new InvalidOperationException("The response mode must match one exclusive consumption path.");
        }
    }

    private async Task<bool> NextAsync<TFrame>(IAsyncEnumerator<TFrame> reader, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        var pending = reader.MoveNextAsync().AsTask();
        try
        {
            return timeout is { } interval
                ? await pending.WaitAsync(interval, this.options.TimeProvider, cancellationToken).ConfigureAwait(false)
                : await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await this.DisposeAsync().ConfigureAwait(false);
            await NdjsonCleanupCoordinator.ObserveAsync(pending).ConfigureAwait(false);
            throw new NdjsonStreamException("stream_input_timeout");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await this.DisposeAsync().ConfigureAwait(false);
            await NdjsonCleanupCoordinator.ObserveAsync(pending).ConfigureAwait(false);
            throw;
        }
    }
}
