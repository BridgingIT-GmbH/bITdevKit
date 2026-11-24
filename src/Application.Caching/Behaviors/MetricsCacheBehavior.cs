// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>Observes public operations and owned source units independently, using only bounded declared aliases.</summary>
/// <example><code>collection.WithMetrics(); // listen to BridgingIT.DevKit.Application.Caching</code></example>
public sealed class MetricsCacheBehavior : ICacheCollectionBehavior, ICacheLoadBehavior, ICacheTransferObserver
{
    /// <summary>Gets the meter and activity source name used for opt-in export.</summary>
    public const string InstrumentationName = "BridgingIT.DevKit.Application.Caching";

    private static readonly Meter Meter = new(InstrumentationName);
    private static readonly ActivitySource Activities = new(InstrumentationName);
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("cache.operations", "{operation}");
    private static readonly Counter<long> Transfers = Meter.CreateCounter<long>("cache.source.transfers", "{transfer}");
    private static readonly Counter<long> OwnedSources = Meter.CreateCounter<long>("cache.source.units", "{unit}");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("cache.operation.duration", "s");

    /// <inheritdoc />
    public async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next, CancellationToken cancellationToken = default)
    {
        if (!Operations.Enabled && !Duration.Enabled && !Activities.HasListeners())
        {
            return await next(context, cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        using var activity = Start(context.Collection, context.Provider, context.Operation.ToString());
        var result = await next(context, cancellationToken).ConfigureAwait(false);
        Record(Operations, context.Collection, context.Provider, context.Operation.ToString(), result.IsSuccess, started, activity);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<Result<T>> ExecuteAsync<T>(CacheLoadBehaviorContext context, CacheLoadContinuation<T> next, CancellationToken cancellationToken = default)
    {
        if (!OwnedSources.Enabled && !Duration.Enabled && !Activities.HasListeners())
        {
            return await next(context, cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        // Shared work has independent lifetime; linking a follower's ambient request would give false ownership.
        using var activity = Start(context.Collection, context.Provider, "OwnedSource", true);
        var result = await next(context, cancellationToken).ConfigureAwait(false);
        Record(OwnedSources, context.Collection, context.Provider, "OwnedSource", result.IsSuccess, started, activity);
        return result;
    }

    /// <inheritdoc />
    public ICacheTransferObservationScope BeginTransfer(CacheTransferContext context) => !Transfers.Enabled && !Duration.Enabled && !Activities.HasListeners() ? null :
        new TransferScope(context, Start(context.Collection, context.Provider, "Transfer", true));

    private sealed class TransferScope(CacheTransferContext context, Activity activity) : ICacheTransferObservationScope
    {
        public void Complete(CacheTransferObservation observation)
        {
            try
            {
                var success = observation.Completion.IsSuccess && observation.Completion.Value != CacheReadCompletion.Incomplete;
                var tags = new TagList { { "cache.collection", context.Collection }, { "cache.provider", context.Provider },
                    { "cache.operation", "Transfer" }, { "cache.success", success } };
                Transfers.Add(1, tags);
                Duration.Record(observation.Elapsed.TotalSeconds, tags);
                activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            }
            finally { activity?.Dispose(); }
        }
    }

    private static Activity Start(string collection, string provider, string operation, bool independent = false)
    {
        try
        {
            var activity = independent ? Activities.StartActivity("cache.source", ActivityKind.Internal, default(ActivityContext)) :
                Activities.StartActivity("cache." + operation, ActivityKind.Internal);
            activity?.SetTag("cache.collection", collection).SetTag("cache.provider", provider);
            return activity;
        }
        catch { return null; }
    }

    private static void Record(Counter<long> counter, string collection, string provider, string operation, bool success, long started, Activity activity)
    {
        try
        {
            var tags = new TagList { { "cache.collection", collection }, { "cache.provider", provider }, { "cache.operation", operation }, { "cache.success", success } };
            counter.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
            activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
        catch { /* Exporter failures cannot change an engine outcome. */ }
    }
}