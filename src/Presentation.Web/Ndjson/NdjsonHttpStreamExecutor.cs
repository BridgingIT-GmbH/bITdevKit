// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

internal static class NdjsonHttpStreamExecutor
{
    internal static async Task ExecuteAsync<TSource, TRecord>(HttpContext context, IAsyncEnumerable<TSource> items, NdjsonStreamingOptions settings, TimeProvider clock, INdjsonStreamObserver observer, Func<TSource, NdjsonWriter, CancellationToken, Task<NdjsonEncodedRecord>> encode, Action<TSource> discard = null)
    {
        var hostStopping = context.RequestServices.GetService(typeof(IHostApplicationLifetime)) is IHostApplicationLifetime lifetime ? lifetime.ApplicationStopping : CancellationToken.None;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, hostStopping);
        using var duration = settings.MaxDuration is { } limit ? new CancellationTokenSource(limit, clock) : new CancellationTokenSource();
        var producer = CancellationTokenSource.CreateLinkedTokenSource(request.Token, duration.Token);
        using var scheduler = new CancellationTokenSource();
        var logger = (context.RequestServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger("BridgingIT.DevKit.Presentation.Web.Ndjson");
        var writer = new NdjsonWriter(settings.JsonOptions, settings.WriterOptions(), clock, logger);
        var session = new NdjsonOutputSession(context.Response.Body, settings.WriterOptions(), clock, producer.Token, settings.WriteTimeout);
        IAsyncEnumerator<TSource> source = null;
        Task<bool> pending = null;
        var stage = NdjsonStreamFailureStage.Source;
        long dataCount = 0;
        var terminalErrorSent = false;
        CancellationTokenSource heartbeatCancellation = null;
        var heartbeat = Task.Delay(Timeout.InfiniteTimeSpan, scheduler.Token);
        long heartbeatActivity = -1;

        context.Response.ContentType = NdjsonDefaults.CONTENT_TYPE;
        context.Response.Headers["X-Ndjson-Mode"] = settings.Mode == NdjsonMode.Raw ? "raw" : "envelope";
        context.Response.Headers.CacheControl = "no-store";
        if (settings.Mode == NdjsonMode.Envelope) { context.Response.Headers["X-Ndjson-Envelope-Version"] = "1"; }

        if (settings.DownloadFileName is { } fileName)
        {
            var disposition = new ContentDispositionHeaderValue("attachment");
            disposition.SetHttpFileName(fileName);
            context.Response.Headers.ContentDisposition = disposition.ToString();
        }

        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        try
        {
            if (settings.Mode == NdjsonMode.Raw)
            {
                await session.StartResponseAsync(context.Response.StartAsync).ConfigureAwait(false);
            }
            else
            {
                await ControlAsync(NdjsonEnvelopeKind.Heartbeat).ConfigureAwait(false);
            }

            source = items.GetAsyncEnumerator(producer.Token);
            while (true)
            {
                stage = NdjsonStreamFailureStage.Source;
                pending ??= source.MoveNextAsync().AsTask();
                if (settings.Mode == NdjsonMode.Envelope && heartbeatActivity != session.LastFlushTimestamp)
                {
                    heartbeatCancellation?.Cancel();
                    heartbeatCancellation?.Dispose();
                    heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(scheduler.Token);
                    var remaining = settings.HeartbeatInterval - clock.GetElapsedTime(session.LastFlushTimestamp);
                    heartbeat = Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, clock, heartbeatCancellation.Token);
                    heartbeatActivity = session.LastFlushTimestamp;
                }

                // Due output deadlines win over an always-ready producer.
                if (session.HasBatch && session.BatchDeadline.IsCompleted)
                {
                    await session.FlushBatchAsync().ConfigureAwait(false);
                    continue;
                }

                if (settings.Mode == NdjsonMode.Envelope && heartbeat.IsCompletedSuccessfully)
                {
                    await ControlAsync(NdjsonEnvelopeKind.Heartbeat).ConfigureAwait(false);
                    heartbeatActivity = -1;
                    continue;
                }

                var selected = await Task.WhenAny(pending, session.BatchDeadline, heartbeat).WaitAsync(producer.Token).ConfigureAwait(false);
                if (selected == pending)
                {
                    var available = await pending.ConfigureAwait(false);
                    pending = null;
                    if (!available)
                    {
                        session.SourceCompleted = true;
                        break;
                    }

                    var current = source.Current;
                    stage = NdjsonStreamFailureStage.Encoding;
                    var frame = await encode(current, writer, producer.Token).ConfigureAwait(false);
                    await session.EmitOwnedAsync(frame).ConfigureAwait(false);
                    dataCount = checked(dataCount + 1);
                }
                else if (selected == heartbeat)
                {
                    await ControlAsync(NdjsonEnvelopeKind.Heartbeat).ConfigureAwait(false);
                    heartbeatActivity = -1;
                }
                else
                {
                    await session.FlushBatchAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (producer.IsCancellationRequested)
        {
            if (request.IsCancellationRequested) { session.Cancel(); }
            else { session.Fail(stage, "stream_duration_exceeded"); }
        }
        catch (Exception error)
        {
            session.Fail(stage, (error as NdjsonStreamException)?.ReasonCode ?? (stage == NdjsonStreamFailureStage.Source ? "stream_source_failed" : "stream_encoding_failed"));
        }

        scheduler.Cancel();
        heartbeatCancellation?.Cancel();
        heartbeatCancellation?.Dispose();
        session.StopBatchTimer();
        var cleanupStarted = clock.GetTimestamp();
        var cleanup = await NdjsonCleanupCoordinator.FinishAsync(source, pending, producer, RemainingCleanup(), clock, discard, logger).ConfigureAwait(false);
        session.CleanupCompleted = cleanup.Completed;
        if (cleanup.ReasonCode is not null) { session.Fail(cleanup.Stage, cleanup.ReasonCode); }

        using var finalOutput = CancellationTokenSource.CreateLinkedTokenSource(request.Token, duration.Token);
        session.OutputCancellation = finalOutput.Token;
        if (request.IsCancellationRequested) { session.Cancel(); }
        else if (duration.IsCancellationRequested) { session.Fail(NdjsonStreamFailureStage.Source, "stream_duration_exceeded"); }

        if (session.HasFailure)
        {
            await TryTerminalErrorAsync().ConfigureAwait(false);
            if (!terminalErrorSent || !cleanup.Completed) { context.Abort(); }
        }
        else if (request.IsCancellationRequested)
        {
            context.Abort();
        }
        else
        {
            try
            {
                if (settings.Mode == NdjsonMode.Envelope)
                {
                    await session.FlushBatchAsync().ConfigureAwait(false);
                    await ControlAsync(NdjsonEnvelopeKind.Completed).ConfigureAwait(false);
                }
                else
                {
                    await session.FinishOutputAsync().ConfigureAwait(false);
                }
            }
            catch (Exception error)
            {
                if (error is OperationCanceledException && request.IsCancellationRequested) { session.Cancel(); }
                else if (error is OperationCanceledException && duration.IsCancellationRequested) { session.Fail(NdjsonStreamFailureStage.Source, "stream_duration_exceeded"); }
                else { session.Fail(NdjsonStreamFailureStage.Encoding, (error as NdjsonStreamException)?.ReasonCode ?? "stream_encoding_failed"); }

                await TryTerminalErrorAsync().ConfigureAwait(false);
                if (!terminalErrorSent) { context.Abort(); }
            }
        }

        var release = session.ReleaseAsync();
        try
        {
            if (!release.IsCompleted && clock.GetElapsedTime(cleanupStarted) >= settings.CleanupTimeout)
            {
                throw new TimeoutException();
            }

            await release.WaitAsync(RemainingCleanup(), clock).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            session.CleanupCompleted = false;
            session.Fail(NdjsonStreamFailureStage.Cleanup, "stream_cleanup_timeout");
            context.Abort();
            _ = NdjsonCleanupCoordinator.ObserveAsync(release, logger);
        }

        var outcome = session.Outcome();
        NdjsonDiagnostics.Finished(logger, outcome);
        await NdjsonOutputSession.NotifyAsync(observer, outcome, settings.ObserverTimeout, clock, hostStopping, logger).ConfigureAwait(false);

        TimeSpan RemainingCleanup()
        {
            var remaining = settings.CleanupTimeout - clock.GetElapsedTime(cleanupStarted);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromTicks(1);
        }

        async Task ControlAsync(NdjsonEnvelopeKind kind)
        {
            var value = new NdjsonEnvelope<TRecord>
            {
                Kind = kind,
                RecordCount = kind == NdjsonEnvelopeKind.Completed ? dataCount : null,
                Error = kind == NdjsonEnvelopeKind.Error ? new NdjsonError(session.ReasonCode ?? "stream_source_failed", "Stream failed.", context.TraceIdentifier) : null
            };
            NdjsonEncodedRecord frame;
            try
            {
                frame = await writer.EncodeRecordAsync(value, session.OutputCancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (session.OutputCancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                session.Fail(NdjsonStreamFailureStage.Encoding, (error as NdjsonStreamException)?.ReasonCode ?? "stream_encoding_failed");
                throw;
            }

            await session.EmitOwnedAsync(frame, immediate: true).ConfigureAwait(false);
        }

        async Task TryTerminalErrorAsync()
        {
            if (terminalErrorSent || settings.Mode != NdjsonMode.Envelope || session.PartialWritePossible || request.IsCancellationRequested || session.ReasonCode == "stream_cleanup_timeout") { return; }

            // A failed producer/duration token cannot suppress a safe error attempt.
            session.OutputCancellation = request.Token;
            try
            {
                await ControlAsync(NdjsonEnvelopeKind.Error).ConfigureAwait(false);
                terminalErrorSent = true;
            }
            catch (Exception)
            {
                context.Abort();
            }
        }
    }
}
