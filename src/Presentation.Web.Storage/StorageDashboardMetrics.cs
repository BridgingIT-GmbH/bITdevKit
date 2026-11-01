// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

// Only two transfer directions are retained; client names and keys never become history dimensions.
internal sealed class StorageDashboardMetrics : IDisposable
{
    private const int BucketSeconds = 1;
    private const int BucketCount = 900;
    private readonly object gate = new();
    private readonly MeterListener listener = new();
    private readonly SortedDictionary<long, TransferBucket[]> buckets = [];
    private readonly string feature;
    private readonly TimeProvider timeProvider;
    private readonly DateTimeOffset started;
    private bool observed;

    internal StorageDashboardMetrics(string feature, TimeProvider timeProvider = null)
    {
        this.feature = feature;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.started = this.timeProvider.GetUtcNow();
        var prefix = feature == "blobs" ? "blobstorage_" : "document.";
        this.listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == Metrics.MeterName &&
                (instrument.Name == prefix + "bytes" || instrument.Name == prefix + "items" ||
                    instrument.Name == prefix + "bytes_per_second" ||
                    instrument.Name == prefix + (feature == "blobs" ? "operation_duration" : "operation.duration")))
            {
                owner.EnableMeasurementEvents(instrument);
            }
        };
        this.listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => this.Record(instrument.Name, value, tags));
        this.listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => this.Record(instrument.Name, value, tags));
        this.listener.Start();
    }

    internal object Snapshot()
    {
        lock (this.gate)
        {
            var current = this.timeProvider.GetUtcNow().ToUnixTimeSeconds() / BucketSeconds;
            this.Trim(current);
            // Return completed intervals only, so a partial current bucket cannot depress the rate.
            var first = Math.Max(this.started.ToUnixTimeSeconds() / BucketSeconds, current - BucketCount);
            var samples = Enumerable.Range(0, (int)Math.Max(0, current - first)).Select(offset =>
            {
                var bucket = first + offset;
                var values = this.buckets.GetValueOrDefault(bucket) ?? [new TransferBucket(), new TransferBucket()];
                var seconds = Math.Min(BucketSeconds, ((bucket + 1) * BucketSeconds * 1000 - this.started.ToUnixTimeMilliseconds()) / 1000d);
                return new
                {
                    at = DateTimeOffset.FromUnixTimeSeconds((bucket + 1) * BucketSeconds),
                    readMiBPerSecond = values[0].Bytes / seconds / 1048576,
                    writeMiBPerSecond = values[1].Bytes / seconds / 1048576,
                    readItems = values[0].Items,
                    writeItems = values[1].Items,
                    readSpeedMiBPerSecond = values[0].SpeedCount > 0 ? (double?)(values[0].SpeedSum / values[0].SpeedCount / 1048576) : null,
                    writeSpeedMiBPerSecond = values[1].SpeedCount > 0 ? (double?)(values[1].SpeedSum / values[1].SpeedCount / 1048576) : null,
                    readLatencyMs = values[0].DurationCount > 0 ? (double?)(values[0].DurationSum / values[0].DurationCount) : null,
                    writeLatencyMs = values[1].DurationCount > 0 ? (double?)(values[1].DurationSum / values[1].DurationCount) : null
                };
            }).ToArray();
            return new { observed = this.observed, bucketSeconds = BucketSeconds, samples };
        }
    }

    private void Record(string name, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        string operation = null;
        foreach (var tag in tags)
        {
            if (tag.Key == "operation") operation = tag.Value?.ToString();
        }

        var direction = this.feature == "blobs"
            ? operation switch { "download" => 0, "upload" => 1, _ => -1 }
            : operation switch { "get" or "get-many" or "find" => 0, "upsert" or "upsert-many" => 1, _ => -1 };
        if (direction < 0 || !double.IsFinite(value) || value < 0) return;

        lock (this.gate)
        {
            this.observed = true;
            var bucket = this.timeProvider.GetUtcNow().ToUnixTimeSeconds() / BucketSeconds;
            this.Trim(bucket);
            if (!this.buckets.TryGetValue(bucket, out var values)) this.buckets[bucket] = values = [new TransferBucket(), new TransferBucket()];
            var transfer = values[direction];
            if (name.EndsWith("bytes_per_second", StringComparison.Ordinal))
            {
                transfer.SpeedSum += value;
                transfer.SpeedCount++;
            }
            else if (name.EndsWith("duration", StringComparison.Ordinal))
            {
                transfer.DurationSum += value;
                transfer.DurationCount++;
            }
            else if (name.EndsWith("items", StringComparison.Ordinal))
            {
                transfer.Items += value;
            }
            else
            {
                transfer.Bytes += value;
            }
        }
    }

    private void Trim(long current)
    {
        while (this.buckets.Count > 0 && this.buckets.First().Key < current - BucketCount)
        {
            this.buckets.Remove(this.buckets.First().Key);
        }
    }

    private sealed class TransferBucket
    {
        internal double Bytes;
        internal double Items;
        internal double SpeedSum;
        internal long SpeedCount;
        internal double DurationSum;
        internal long DurationCount;
    }

    public void Dispose() => this.listener.Dispose();
}
