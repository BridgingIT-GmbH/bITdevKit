// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>Encodes bounded NDJSON records once and streams ordered sequences.</summary>
/// <example><code>await new NdjsonWriter().WriteAsync(values, stream, ct);</code></example>
public sealed class NdjsonWriter
{
    private readonly JsonSerializerOptions json;
    private readonly NdjsonWriterOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger logger;

    /// <summary>Captures compact JSON settings, limits, a controllable clock, and optional safe diagnostics.</summary>
    /// <example><code>var writer = new NdjsonWriter(options: new() { MaxRecordBytes = 65536 });</code></example>
    public NdjsonWriter(JsonSerializerOptions jsonOptions = null, NdjsonWriterOptions options = null, TimeProvider timeProvider = null, ILogger logger = null)
    {
        this.json = new JsonSerializerOptions(jsonOptions ?? DefaultJsonSerializerOptions.Create()) { WriteIndented = false };
        this.json.Converters.Insert(0, new NdjsonEnvelopeJsonConverter());
        this.options = (options ?? new NdjsonWriterOptions()).Capture();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.logger = logger;
    }

    /// <summary>Encodes one validated LF-delimited record without destination I/O.</summary>
    /// <example><code>using var frame = await writer.EncodeRecordAsync(value, ct);</code></example>
    public Task<NdjsonEncodedRecord> EncodeRecordAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = new NdjsonRecordBuffer(this.options.MaxRecordBytes);
        try
        {
            using (var jsonWriter = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = this.json.Encoder, Indented = false }))
            {
                JsonSerializer.Serialize(jsonWriter, value, this.json);
                jsonWriter.Flush();
            }

            var length = buffer.Length;
            NdjsonRecordValidation.Validate(buffer.Memory.Span);
            cancellationToken.ThrowIfCancellationRequested();
            buffer.AppendReceived("\n"u8);
            return Task.FromResult(new NdjsonEncodedRecord(buffer, length, 1));
        }
        catch (Exception error)
        {
            buffer.Dispose();
            if (error is NdjsonStreamException or OperationCanceledException)
            {
                throw;
            }

            throw new NdjsonStreamException("stream_encoding_failed");
        }
    }

    /// <summary>Writes the original bytes without flushing or disposing the caller's lease.</summary>
    /// <example><code>await writer.WriteEncodedRecordAsync(frame, stream, ct);</code></example>
    public async Task WriteEncodedRecordAsync(NdjsonEncodedRecord record, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(destination);
        if (record.JsonBytes.Length > this.options.MaxRecordBytes)
        {
            throw new NdjsonStreamException("stream_record_too_large");
        }

        await destination.WriteAsync(record.FramedBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Encodes, writes, and releases a single record without flushing.</summary>
    /// <example><code>await writer.WriteRecordAsync(value, stream, ct); await stream.FlushAsync(ct);</code></example>
    public async Task WriteRecordAsync<T>(T value, Stream destination, CancellationToken cancellationToken = default)
    {
        using var record = await this.EncodeRecordAsync(value, cancellationToken).ConfigureAwait(false);
        await this.WriteEncodedRecordAsync(record, destination, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Streams an asynchronous source, leaving the destination open.</summary>
    /// <example><code>await writer.WriteAsync(values, stream, ct);</code></example>
    public Task WriteAsync<T>(IAsyncEnumerable<T> items, Stream destination, CancellationToken cancellationToken = default) => this.WriteAsync(items, destination, null, cancellationToken);

    /// <summary>Streams a bounded synchronous source, leaving the destination open.</summary>
    /// <example><code>await writer.WriteAsync(new[] { 1, 2 }, stream, ct);</code></example>
    public Task WriteAsync<T>(IEnumerable<T> items, Stream destination, CancellationToken cancellationToken = default) => this.WriteAsync(Adapt(items), destination, null, cancellationToken);

    /// <summary>Streams a synchronous source and reports one terminal outcome.</summary>
    /// <example><code>await writer.WriteAsync(values, stream, observer, ct);</code></example>
    public Task WriteAsync<T>(IEnumerable<T> items, Stream destination, INdjsonStreamObserver observer, CancellationToken cancellationToken = default) => this.WriteAsync(Adapt(items), destination, observer, cancellationToken);

    /// <summary>Streams an asynchronous source with bounded batching and terminal notification.</summary>
    /// <example><code>await writer.WriteAsync(values, stream, observer, ct);</code></example>
    public async Task WriteAsync<T>(IAsyncEnumerable<T> items, Stream destination, INdjsonStreamObserver observer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(destination);
        var producer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var session = new NdjsonOutputSession(destination, this.options, this.timeProvider, cancellationToken);
        IAsyncEnumerator<T> source = null;
        Task<bool> pending = null;
        var stage = NdjsonStreamFailureStage.Source;
        try
        {
            source = items.GetAsyncEnumerator(producer.Token);
            while (true)
            {
                stage = NdjsonStreamFailureStage.Source;
                pending = source.MoveNextAsync().AsTask();
                if (session.HasBatch && session.BatchDeadline.IsCompleted)
                {
                    await session.FlushBatchAsync().ConfigureAwait(false);
                }

                while (!pending.IsCompleted && session.HasBatch)
                {
                    var selected = await Task.WhenAny(pending, session.BatchDeadline).WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (selected != pending)
                    {
                        await session.FlushBatchAsync().ConfigureAwait(false);
                    }
                }

                var hasValue = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
                pending = null;
                if (!hasValue)
                {
                    session.SourceCompleted = true;
                    break;
                }

                stage = NdjsonStreamFailureStage.Encoding;
                var record = await this.EncodeRecordAsync(source.Current, cancellationToken).ConfigureAwait(false);
                await session.EmitOwnedAsync(record).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            session.Cancel();
        }
        catch (Exception error)
        {
            session.Fail(stage, (error as NdjsonStreamException)?.ReasonCode ?? (stage == NdjsonStreamFailureStage.Source ? "stream_source_failed" : "stream_encoding_failed"));
        }

        session.StopBatchTimer();
        var cleanupStarted = this.timeProvider.GetTimestamp();
        var cleanup = await NdjsonCleanupCoordinator.FinishAsync(source, pending, producer, this.options.CleanupTimeout, this.timeProvider, logger: this.logger).ConfigureAwait(false);
        session.CleanupCompleted = cleanup.Completed;
        if (cleanup.ReasonCode is not null)
        {
            session.Fail(cleanup.Stage, cleanup.ReasonCode);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            session.Cancel();
        }

        if (!session.HasFailure && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await session.FinishOutputAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                session.Cancel();
            }
            catch (NdjsonStreamException)
            {
            }
        }

        var release = session.ReleaseAsync();
        try
        {
            var remaining = this.options.CleanupTimeout - this.timeProvider.GetElapsedTime(cleanupStarted);
            if (!release.IsCompleted && remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException();
            }

            await release.WaitAsync(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromTicks(1), this.timeProvider).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            session.CleanupCompleted = false;
            session.Fail(NdjsonStreamFailureStage.Cleanup, "stream_cleanup_timeout");
            _ = NdjsonCleanupCoordinator.ObserveAsync(release, this.logger);
        }

        var outcome = session.Outcome();
        NdjsonDiagnostics.Finished(this.logger, outcome);
        await NdjsonOutputSession.NotifyAsync(observer, outcome, this.options.ObserverTimeout, this.timeProvider, logger: this.logger).ConfigureAwait(false);
        if (outcome.Status == NdjsonStreamStatus.Failed)
        {
            throw new NdjsonStreamException(outcome.ReasonCode);
        }

        if (outcome.Status == NdjsonStreamStatus.Canceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    internal static async IAsyncEnumerable<T> Adapt<T>(IEnumerable<T> items, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await Task.CompletedTask.ConfigureAwait(false);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
