// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Common;

public static partial class ProfilingEntityMapper
{
    internal static OperationProfilingEntity ToOperationEntity(ProfilingWriteEnvelope envelope, long publication)
    {
        var record = envelope.Record;
        var key = ProfilingOperationComparisons.Canonical(record.Key);
        var kind = ProfilingOperationComparisons.Canonical(record.Kind);
        var route = ProfilingOperationComparisons.Canonical(record.Http?.Route);
        var entity = new OperationProfilingEntity
        {
            Id = record.Id, CanonicalIdBytes = Encoding.ASCII.GetBytes(record.Id.ToString("N")), NodeId = record.NodeId, NodeKey = record.NodeKey,
            WriterId = envelope.Lease.WriterId, WriterGeneration = envelope.Lease.Generation, CompletionSequence = envelope.CompletionSequence,
            CommitWatermark = publication, Key = record.Key, Kind = record.Kind, KeyBytes = key, KindBytes = kind,
            KeyHash = ProfilingOperationComparisons.Hash(key), KindHash = ProfilingOperationComparisons.Hash(kind),
            BaseGroupBytes = ProfilingOperationComparisons.BaseGroup(record.Kind, record.Key),
            StartedUtcTicks = record.StartedUtc.UtcTicks, CompletedUtcTicks = record.CompletedUtc.UtcTicks, DurationTicks = record.Duration.Ticks,
            Outcome = record.Outcome, ParentOperationId = record.ParentOperationId, CorrelationBytes = ProfilingOperationComparisons.Exact(record.CorrelationId),
            ApplicationVersionBytes = ProfilingOperationComparisons.Exact(record.Node.ApplicationVersion),
            HasSegmentFailures = record.Segments.Any(summary => summary.Outcomes.Any(outcome => outcome.Outcome == ProfilingSegmentOutcome.Failed && outcome.Statistics.Count > 0)),
            HttpMethodBytes = ProfilingOperationComparisons.Canonical(record.Http?.Method), HttpMethodGroupBytes = ProfilingOperationComparisons.MethodPart(record.Http?.Method),
            HttpRouteBytes = route, HttpRouteHash = ProfilingOperationComparisons.Hash(route), HttpStatusCode = record.Http?.StatusCode,
            ApplicationRequestIdBytes = ProfilingOperationComparisons.Exact(record.Http?.ApplicationRequestId),
            SamplingStrategyBytes = ProfilingOperationComparisons.Canonical(record.Http?.SamplingStrategyKey), SamplingConfigurationBytes = ProfilingOperationComparisons.Canonical(record.Http?.SamplingConfigurationKey),
            EstimatedPayloadBytes = record.EstimatedPayloadBytes, RecordJson = JsonSerializer.Serialize(record),
        };
        foreach (var dimension in record.Dimensions)
        {
            entity.Dimensions.Add(Dimension(entity.Id, null, new byte[32], dimension.Key, dimension.Value, 1, false, false));
        }

        foreach (var measurement in record.Measurements)
        {
            entity.Measurements.Add(Measurement(entity.Id, null, new byte[32], measurement.Key, measurement.Unit, measurement.Aggregation, measurement.Value, null, 1, 0, record.CompletedUtc, false, false, []));
        }

        foreach (var summary in record.Segments)
        {
            var path = ProfilingOperationComparisons.Exact(summary.Path.ComparisonKey);
            var parent = ProfilingOperationComparisons.Exact(summary.ParentPath?.ComparisonKey);
            var pathHash = ProfilingOperationComparisons.Hash(path);
            var leaf = ProfilingOperationComparisons.Canonical(summary.Key);
            var segment = new OperationProfilingSegmentEntity
            {
                Id = ProfilingOperationComparisons.SummaryId(record.Id, path), OperationId = record.Id, NodeId = record.NodeId,
                Key = summary.Key, KeyBytes = leaf, KeyHash = ProfilingOperationComparisons.Hash(leaf), PathBytes = path, PathHash = pathHash,
                ParentPathBytes = parent, ParentPathHash = ProfilingOperationComparisons.Hash(parent), Count = summary.Statistics.Count,
                TotalDurationTicks = summary.Statistics.TotalDuration.Ticks, TotalSelfDurationTicks = summary.Statistics.TotalSelfDuration.Ticks,
                MinimumDurationTicks = summary.Statistics.MinimumDuration.Ticks, MaximumDurationTicks = summary.Statistics.MaximumDuration.Ticks,
                CompletedCount = Count(summary, ProfilingSegmentOutcome.Completed), FailedCount = Count(summary, ProfilingSegmentOutcome.Failed),
                CanceledCount = Count(summary, ProfilingSegmentOutcome.Canceled), IncompleteCount = Count(summary, ProfilingSegmentOutcome.Incomplete),
                Unavailable = summary.Statistics.Unavailable, SummaryJson = JsonSerializer.Serialize(summary),
            };
            entity.Segments.Add(segment);
            foreach (var dimension in summary.Dimensions)
            {
                entity.Dimensions.Add(Dimension(entity.Id, segment.Id, pathHash, dimension.Key, dimension.Value, dimension.SampleCount, dimension.Mixed, dimension.Partial));
            }

            foreach (var measurement in summary.Measurements)
            {
                entity.Measurements.Add(Measurement(entity.Id, segment.Id, pathHash, measurement.Key, measurement.Unit, measurement.Aggregation, measurement.Value, measurement.Sum,
                    measurement.SampleCount, measurement.LastCompletionSequence, measurement.LastCompletedUtc, measurement.Unavailable, measurement.Conflicting, measurement.Outcomes));
            }
        }

        return entity;
    }

    internal static OperationProfilingRecord ToOperationModel(OperationProfilingEntity entity, OperationProfilingOptions options)
    {
        if (entity.RecordJson is null || entity.RecordJson.Length > (long)options.MaxRecordBytes * 8)
        {
            throw new ArgumentException("Stored operation JSON exceeds its bounded serialized representation.");
        }

        var record = JsonSerializer.Deserialize<OperationProfilingRecord>(entity.RecordJson)
            ?? throw new ArgumentException("Stored operation payload is absent.");
        if (record.Node is null)
        {
            throw new ArgumentException("Stored operation process identity is absent.");
        }

        // Runtime's node identity intentionally hides its ID in ordinary JSON. Restore the explicit
        // operation projection before validating/freezing the graph; no node query or N+1 is needed.
        record = record with { Node = record.Node with { Identity = new ProfilingNodeIdentity(entity.NodeId, entity.NodeKey) } };
        if (record.Id != entity.Id || record.Duration.Ticks != entity.DurationTicks || record.CompletedUtc.UtcTicks != entity.CompletedUtcTicks)
        {
            throw new ArgumentException("Stored operation projections do not match their immutable payload.");
        }

        return ProfilingRecordSnapshot.Copy(record, options);
    }

    private static long Count(ProfilingSegmentSummary summary, ProfilingSegmentOutcome outcome) => summary.Outcomes.FirstOrDefault(value => value.Outcome == outcome)?.Statistics.Count ?? 0;

    private static OperationProfilingDimensionEntity Dimension(Guid operation, Guid? segment, byte[] scope, string key, ProfilingValue value, long count, bool mixed, bool partial)
    {
        var name = ProfilingOperationComparisons.Canonical(key);
        return new()
        {
            Id = Guid.NewGuid(), OperationId = operation, SegmentId = segment, ScopeHash = scope.ToArray(), Key = key, KeyBytes = name, KeyHash = ProfilingOperationComparisons.Hash(name),
            ValueType = value?.Type, ValueScalar = value?.Scalar, ValueBytes = ProfilingOperationComparisons.Exact(value?.Scalar), GroupPartBytes = ProfilingOperationComparisons.ValuePart(value),
            SampleCount = count, Mixed = mixed, Partial = partial,
        };
    }

    private static OperationProfilingMeasurementEntity Measurement(Guid operation, Guid? segment, byte[] scope, string key, string unit, MeasurementAggregation aggregation,
        ProfilingValue value, ProfilingValue sum, long count, long lastSequence, DateTimeOffset lastUtc, bool unavailable, bool conflicting, IReadOnlyList<ProfilingOutcomeMeasurementSummary> outcomes)
    {
        var name = ProfilingOperationComparisons.Canonical(key);
        return new()
        {
            Id = Guid.NewGuid(), OperationId = operation, SegmentId = segment, ScopeHash = scope.ToArray(), Key = key, KeyBytes = name, KeyHash = ProfilingOperationComparisons.Hash(name),
            UnitBytes = ProfilingOperationComparisons.Exact(unit), Aggregation = aggregation, ValueType = value?.Type, ValueScalar = value?.Scalar,
            SumType = sum?.Type, SumScalar = sum?.Scalar, SampleCount = count, LastCompletionSequence = lastSequence, LastCompletedUtcTicks = lastUtc.UtcTicks,
            Unavailable = unavailable, Conflicting = conflicting, OutcomesJson = JsonSerializer.Serialize(outcomes),
        };
    }
}
