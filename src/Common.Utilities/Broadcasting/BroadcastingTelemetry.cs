// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;
/// <summary>Defines safe, low-cardinality Broadcasting metric series.</summary>
/// <example><code>BroadcastingMetrics.RecordRegistration(metrics, "registered");</code></example>
public static class BroadcastingMetrics
{
    /// <summary>Records one publication and its target count.</summary>
    public static void RecordPublication(
        IMetricsService metrics,
        Type broadcastType,
        int targetCount
    )
    {
        if (metrics is null)
        {
            return;
        }

        var type = Metrics.NormalizeTypeName(broadcastType);
        metrics.Increment("broadcasting_publish", type);
        metrics.AddCounter(Metrics.Series("broadcasting_targets", type), targetCount);
    }

    /// <summary>Records one immediate node outcome and optional duration.</summary>
    public static void RecordDelivery(
        IMetricsService metrics,
        Type broadcastType,
        BroadcastDeliveryOutcome outcome,
        TimeSpan? duration
    )
    {
        if (metrics is null)
        {
            return;
        }

        var type = Metrics.NormalizeTypeName(broadcastType);
        metrics.Increment("broadcasting_delivery", type, outcome.ToString());
        if (duration is not null)
        {
            metrics.RecordHistogram(
                Metrics.Series("broadcasting_delivery_duration", type, outcome.ToString()),
                duration.Value.TotalMilliseconds,
                "ms"
            );
        }
    }

    /// <summary>Records one receiver decision.</summary>
    public static void RecordReceiver(
        IMetricsService metrics,
        string broadcastType,
        BroadcastDeliveryOutcome outcome
    )
    {
        metrics?.Increment(
            "broadcasting_receiver",
            Metrics.NormalizePart(broadcastType),
            outcome.ToString()
        );
    }

    /// <summary>Records one registration lifecycle event.</summary>
    public static void RecordRegistration(IMetricsService metrics, string outcome) =>
        metrics?.Increment("broadcasting_registration", outcome);

    /// <summary>Records registrations made inactive or removed during stale-node cleanup.</summary>
    /// <param name="metrics">The optional metrics service.</param>
    /// <param name="count">The number of affected registrations.</param>
    /// <example><code>BroadcastingMetrics.RecordStaleRemoval(metrics, expiredCount);</code></example>
    public static void RecordStaleRemoval(IMetricsService metrics, int count)
    {
        if (count > 0)
        {
            metrics?.AddCounter("broadcasting_stale_removal", count);
        }
    }
}
