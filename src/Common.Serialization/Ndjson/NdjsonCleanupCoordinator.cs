// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

internal static class NdjsonCleanupCoordinator
{
    internal sealed record Outcome(bool Completed, string ReasonCode = null, NdjsonStreamFailureStage Stage = NdjsonStreamFailureStage.Cleanup);

    internal static async Task<Outcome> FinishAsync<T>(IAsyncEnumerator<T> source, Task<bool> pendingMove, CancellationTokenSource producerCancellation, TimeSpan timeout, TimeProvider timeProvider, Action<T> discard = null, ILogger logger = null)
    {
        try
        {
            producerCancellation.Cancel();
        }
        catch (Exception)
        {
            NdjsonDiagnostics.Report(logger, "stream_cleanup_failed");
        }

        var cleanup = SettleAsync();
        try
        {
            return await cleanup.WaitAsync(timeout, timeProvider).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _ = ObserveAsync(cleanup, logger);
            return new Outcome(false, "stream_cleanup_timeout");
        }

        async Task<Outcome> SettleAsync()
        {
            string sourceFailure = null;
            try
            {
                if (pendingMove is not null)
                {
                    try
                    {
                        if (await pendingMove.ConfigureAwait(false))
                        {
                            discard?.Invoke(source.Current);
                        }
                    }
                    catch (OperationCanceledException) when (producerCancellation.IsCancellationRequested)
                    {
                    }
                    catch (Exception)
                    {
                        sourceFailure = "stream_source_failed";
                    }
                }

                if (source is not null)
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }

                return new Outcome(true, sourceFailure, NdjsonStreamFailureStage.Source);
            }
            catch (Exception)
            {
                NdjsonDiagnostics.Report(logger, "stream_cleanup_failed");
                return new Outcome(false, sourceFailure ?? "stream_cleanup_failed", sourceFailure is null ? NdjsonStreamFailureStage.Cleanup : NdjsonStreamFailureStage.Source);
            }
            finally
            {
                producerCancellation.Dispose();
            }
        }
    }

    internal static async Task ObserveAsync(Task task, ILogger logger = null, string reasonCode = "stream_cleanup_failed")
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            NdjsonDiagnostics.Report(logger, reasonCode);
        }
    }
}
