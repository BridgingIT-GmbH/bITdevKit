// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal static class ProfilingRecordSnapshot
{
    public static OperationProfilingRecord Copy(OperationProfilingRecord record, OperationProfilingOptions options)
    {
        if (record is null || record.Id == Guid.Empty || record.NodeId == Guid.Empty || !Key(record.Key, options) || !Key(record.Kind, options)
            || record.StartedUtc.Offset != TimeSpan.Zero || record.CompletedUtc.Offset != TimeSpan.Zero || record.Node.ProcessStartedUtc.Offset != TimeSpan.Zero
            || record.Node.Identity.Key?.Length != 8 || !record.Node.Identity.Key.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9')
            || record.Node.ProcessId <= 0 || record.Duration < TimeSpan.Zero || !Enum.IsDefined(record.Outcome) || record.Quality is null)
        {
            throw new ArgumentException("A bounded completed profiling root with UTC timestamps and node identity is required.");
        }

        CheckText(record.DisplayName, options.MaxStringLength);
        CheckText(record.CorrelationId, options.MaxStringLength);
        CheckText(record.Node.HostName, 128);
        CheckText(record.Node.DisplayName, 128);
        CheckText(record.Node.ApplicationVersion, 128);
        CheckFailure(record.Failure, options);
        var dimensions = CopyList(record.Dimensions, options.MaxMetadataEntries, d => CheckDimension(d, options));
        var measurements = CopyList(record.Measurements, options.MaxMetadataEntries, m => CheckMeasurement(m, options));
        var sources = CopyList(record.Sources, options.MaxMetadataEntries, source =>
        {
            if (source is null || !Key(source.Kind, options) || source.SchemaVersion <= 0)
            {
                throw new ArgumentException("Invalid profiling source schema.");
            }

            var fields = CopyList(source.Fields, options.MaxMetadataEntries, d => CheckDimension(d, options));
            CheckUnique(fields.Select(field => field.Key));
            return source with { Fields = fields };
        });
        var summaries = CopyList(record.Segments, options.MaxSegmentPaths, summary => CopySummary(summary, record, options));
        var wall = CopyList(record.WallTime, options.MaxSegmentPaths + 2, bucket =>
        {
            if (bucket is null || !Enum.IsDefined(bucket.Kind) || bucket.Duration < TimeSpan.Zero
                || bucket.Kind == ProfilingWallTimeKind.Segment && !Key(bucket.Key, options)
                || bucket.Kind != ProfilingWallTimeKind.Segment && bucket.Key is not null)
            {
                throw new ArgumentException("Invalid profiling wall-time bucket.");
            }

            return bucket;
        });
        var coverage = TimeSpan.Zero;
        var wallIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bucket in wall)
        {
            coverage += bucket.Duration;
            var identity = bucket.Kind == ProfilingWallTimeKind.Segment ? "segment:" + ProfilingKeyComparer.Canonicalize(bucket.Key) : bucket.Kind.ToString();
            if (!wallIdentities.Add(identity))
            {
                throw new ArgumentException("A profiling wall-time classification must be unique.");
            }
        }

        if (coverage != record.Duration)
        {
            throw new ArgumentException("Profiling wall-time buckets must cover the observed root duration exactly.");
        }

        var owned = record with { Dimensions = dimensions, Measurements = measurements, Sources = sources, Segments = summaries, WallTime = wall };
        CheckHttp(owned.Http, options);
        CheckUnique(dimensions.Select(d => d.Key));
        CheckUnique(measurements.Select(m => m.Key));
        CheckUnique(sources.Select(source => source.Kind));
        CheckUnique(summaries.Select(summary => summary.Path.ComparisonKey));
        var paths = summaries.Select(summary => summary.Path.ComparisonKey).ToHashSet(StringComparer.Ordinal);
        if (summaries.Any(summary => summary.ParentPath is not null && !paths.Contains(summary.ParentPath.ComparisonKey)))
        {
            throw new ArgumentException("A nested profiling summary requires its owning parent summary.");
        }

        var charge = Estimate(owned);
        if (charge > options.MaxRecordBytes || record.EstimatedPayloadBytes > options.MaxRecordBytes)
        {
            throw new ArgumentException("The profiling root exceeds its payload bound.");
        }

        return owned with { EstimatedPayloadBytes = Math.Max(charge, record.EstimatedPayloadBytes) };
    }

    private static ProfilingSegmentSummary CopySummary(ProfilingSegmentSummary summary, OperationProfilingRecord root, OperationProfilingOptions options)
    {
        if (summary is null || summary.OperationId != root.Id || summary.NodeId != root.NodeId || !Key(summary.Key, options) || summary.Path is null
            || summary.Path.Components.Count > options.MaxSegmentDepth || summary.Path.Components.Any(c => !Key(c, options)))
        {
            throw new ArgumentException("Invalid profiling summary ownership or path.");
        }

        if (!ProfilingKeyComparer.Instance.Equals(summary.Key, summary.Path.Components[^1])
            || (summary.Path.Components.Count == 1 ? summary.ParentPath is not null
                : summary.ParentPath is null || !summary.ParentPath.Equals(new ProfilingSegmentPath(summary.Path.Components.Take(summary.Path.Components.Count - 1).ToArray()))))
        {
            throw new ArgumentException("A profiling summary must have the exact structured parent and leaf key.");
        }

        CheckStatistics(summary.Statistics);
        CheckText(summary.DisplayName, options.MaxStringLength);
        var outcomes = CopyList(summary.Outcomes, 4, outcome =>
        {
            if (outcome is null || !Enum.IsDefined(outcome.Outcome))
            {
                throw new ArgumentException("Invalid profiling invocation outcome.");
            }

            CheckStatistics(outcome.Statistics);
            return outcome;
        });
        var dimensions = CopyList(summary.Dimensions, options.MaxMetadataEntries, dimension =>
        {
            if (dimension is null || !Key(dimension.Key, options) || dimension.SampleCount < 0 || !dimension.Mixed && dimension.Value is null)
            {
                throw new ArgumentException("Invalid profiling summary dimension.");
            }

            CheckValue(dimension.Value, options);
            return dimension;
        });
        var measurements = CopyList(summary.Measurements, options.MaxMetadataEntries, measurement =>
        {
            if (measurement is null || !Key(measurement.Key, options) || !ProfilingValueValidator.IsKey(measurement.Unit, options.MaxUnitLength)
                || !Enum.IsDefined(measurement.Aggregation) || measurement.SampleCount < 0 || measurement.LastCompletedUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Invalid profiling summary measurement.");
            }

            CheckValue(measurement.Value, options, numeric: true);
            CheckValue(measurement.Sum, options, numeric: true);
            return measurement with { Outcomes = CopyList(measurement.Outcomes, 4, outcome =>
            {
                if (outcome is null || !Enum.IsDefined(outcome.Outcome) || outcome.SampleCount < 0 || outcome.LastCompletedUtc.Offset != TimeSpan.Zero)
                {
                    throw new ArgumentException("Invalid profiling outcome reducer.");
                }

                CheckValue(outcome.Value, options, numeric: true);
                CheckValue(outcome.Sum, options, numeric: true);
                return outcome;
            }) };
        });
        var failures = CopyList(summary.Failures, options.MaxFailureCategories, failure =>
        {
            if (failure is null || failure.Count <= 0 || failure.FirstObservedUtc.Offset != TimeSpan.Zero || failure.LastObservedUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Invalid profiling failure summary.");
            }

            CheckFailure(failure.Failure, options);
            return failure;
        });
        CheckUnique(dimensions.Select(d => d.Key));
        CheckUnique(measurements.Select(m => m.Key));
        if (outcomes.Select(outcome => outcome.Outcome).Distinct().Count() != outcomes.Count)
        {
            throw new ArgumentException("A profiling summary cannot repeat an outcome bucket.");
        }

        return summary with { Outcomes = outcomes, Dimensions = dimensions, Measurements = measurements, Failures = failures };
    }

    private static IReadOnlyList<T> CopyList<T>(IReadOnlyList<T> values, int maximum, Func<T, T> copy)
    {
        var count = values?.Count ?? -1;
        if (count < 0 || count > maximum)
        {
            throw new ArgumentException("A profiling collection exceeds its entry bound.");
        }

        var result = new T[count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = copy(values[index]);
        }

        return Array.AsReadOnly(result);
    }

    private static ProfilingDimension CheckDimension(ProfilingDimension dimension, OperationProfilingOptions options)
    {
        if (dimension is null || !Key(dimension.Key, options) || dimension.Value is null)
        {
            throw new ArgumentException("Invalid profiling dimension.");
        }

        CheckValue(dimension.Value, options);
        return dimension;
    }

    private static ProfilingMeasurement CheckMeasurement(ProfilingMeasurement measurement, OperationProfilingOptions options)
    {
        if (measurement is null || !Key(measurement.Key, options) || !ProfilingValueValidator.IsKey(measurement.Unit, options.MaxUnitLength)
            || measurement.Value is null || !Enum.IsDefined(measurement.Aggregation))
        {
            throw new ArgumentException("Invalid profiling measurement.");
        }

        CheckValue(measurement.Value, options, numeric: true);
        return measurement;
    }

    private static bool Key(string value, OperationProfilingOptions options) => ProfilingValueValidator.IsKey(value, options.MaxKeyLength);
    private static void CheckValue(ProfilingValue value, OperationProfilingOptions options, bool numeric = false)
    {
        if (value is not null && (numeric && value.Type is not (ProfilingValueType.Int64 or ProfilingValueType.Decimal or ProfilingValueType.Double)
            || value.Type == ProfilingValueType.String && (value.Scalar.Length > options.MaxStringLength || !ProfilingValueValidator.IsUnicode(value.Scalar))))
        {
            throw new ArgumentException("Invalid profiling scalar.");
        }
    }

    private static void CheckText(string value, int maximum)
    {
        if (value is not null && (value.Length > maximum || !ProfilingValueValidator.IsUnicode(value)))
        {
            throw new ArgumentException("Invalid profiling text.");
        }
    }

    private static void CheckFailure(ProfilingFailureDescriptor failure, OperationProfilingOptions options)
    {
        if (failure is null)
        {
            return;
        }

        if (!Key(failure.Source, options) || !Key(failure.Code, options))
        {
            throw new ArgumentException("Invalid profiling failure category.");
        }

        CheckText(failure.ExceptionType, options.MaxStringLength);
        CheckText(failure.Message, options.MaxFailureLength);
    }

    private static void CheckStatistics(ProfilingDurationStatistics statistics)
    {
        if (statistics is null || statistics.Count < 0 || statistics.TotalDuration < TimeSpan.Zero || statistics.TotalSelfDuration < TimeSpan.Zero
            || statistics.MinimumDuration < TimeSpan.Zero || statistics.MaximumDuration < statistics.MinimumDuration)
        {
            throw new ArgumentException("Invalid profiling timing statistics.");
        }
    }

    private static void CheckUnique(IEnumerable<string> keys)
    {
        var names = new HashSet<string>(ProfilingKeyComparer.Instance);
        if (keys.Any(key => !names.Add(key)))
        {
            throw new ArgumentException("Profiling names must be unique under the canonical comparer.");
        }
    }

    private static void CheckHttp(HttpRequestProfilingMetadata http, OperationProfilingOptions options)
    {
        if (http is null)
        {
            return;
        }

        CheckText(http.Method, options.MaxKeyLength);
        CheckText(http.Path, options.MaxStringLength);
        CheckText(http.Route, options.MaxStringLength);
        CheckText(http.ApplicationRequestId, options.MaxStringLength);
        CheckText(http.CorrelationId, 128);
        CheckText(http.QueryString, 4096);
        CheckText(http.Scheme, options.MaxKeyLength);
        CheckText(http.Host, options.MaxStringLength);
        CheckText(http.Protocol, options.MaxKeyLength);
        CheckText(http.RequestContentType, options.MaxStringLength);
        CheckText(http.ResponseContentType, options.MaxStringLength);
        if (http.StatusCode is < 100 or > 599 || http.RequestBytes < 0 || http.ResponseBytes < 0 || http.DeclaredRequestBytes < 0
            || http.DeclaredResponseBytes < 0 || http.ActiveSelectedRequestsAtEntry < 1
            || !Enum.IsDefined(http.RequestBytesQuality) || !Enum.IsDefined(http.ResponseBytesQuality)
            || http.SamplingStrategyKey is not null && !Key(http.SamplingStrategyKey, options)
            || http.SamplingConfigurationKey is not null && !Key(http.SamplingConfigurationKey, options)
            || http.SamplingInclusionProbability.HasValue && (!double.IsFinite(http.SamplingInclusionProbability.Value)
                || http.SamplingInclusionProbability.Value is < 0 or > 1))
        {
            throw new ArgumentException("Invalid bounded HTTP profiling metadata.");
        }
    }

    private static long Estimate(OperationProfilingRecord root) => 2048 + Text(root.Key, root.Kind, root.DisplayName, root.CorrelationId)
        + root.Dimensions.Sum(d => 128 + Text(d.Key) + ProfilingValueValidator.Charge(d.Value))
        + root.Measurements.Sum(m => 160 + Text(m.Key, m.Unit) + ProfilingValueValidator.Charge(m.Value))
        + root.Sources.Sum(s => 192 + Text(s.Kind) + s.Fields.Sum(d => 128 + Text(d.Key) + ProfilingValueValidator.Charge(d.Value)))
        + (root.Http is null ? 0 : 384 + Text(root.Http.Method, root.Http.Path, root.Http.Route, root.Http.ApplicationRequestId, root.Http.SamplingStrategyKey, root.Http.SamplingConfigurationKey,
            root.Http.CorrelationId, root.Http.QueryString, root.Http.Scheme, root.Http.Host, root.Http.Protocol, root.Http.RequestContentType, root.Http.ResponseContentType))
        + FailureCharge(root.Failure)
        + root.Segments.Sum(s => 512 + Text(s.DisplayName) + s.Path.Components.Sum(ProfilingValueValidator.Charge)
            + s.Dimensions.Sum(d => 192 + Text(d.Key) + ProfilingValueValidator.Charge(d.Value))
            + s.Measurements.Sum(m => 2048 + Text(m.Key, m.Unit))
            + s.Failures.Sum(f => 256 + Text(f.Failure.Source, f.Failure.Code, f.Failure.ExceptionType, f.Failure.Message)));

    private static long Text(params string[] values) => values.Sum(ProfilingValueValidator.Charge);
    private static long FailureCharge(ProfilingFailureDescriptor value) => value is null ? 0 : 192 + Text(value.Source, value.Code, value.ExceptionType, value.Message);
}
