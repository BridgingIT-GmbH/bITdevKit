// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using System.Text;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

public sealed class ProfilingOperationEntityMapperTests
{
    /// <summary>Bounded HTTP metadata survives the parent JSON projection without confusing application and transport identifiers.</summary>
    /// <example><code>suite.Mapping_HttpMetadata_PreservesOriginalTargetAndCorrelation();</code></example>
    [Fact]
    public void Mapping_HttpMetadata_PreservesOriginalTargetAndCorrelation()
    {
        var record = CreateRecord() with
        {
            Kind = "HttpRequest", CorrelationId = "application-42",
            Http = new()
            {
                Method = "GET", Path = "/api/products/42", Route = "/api/products/{id}",
                QueryString = "?page=2&token=%5Bredacted%5D", QueryStringTruncated = true,
                Scheme = "https", Host = "example.test:443", Protocol = "HTTP/2",
                CorrelationId = "application-42", ApplicationRequestId = "transport-42",
                RequestContentType = "application/json", ResponseContentType = "text/plain", StatusCode = 200,
            },
        };
        var entity = ProfilingEntityMapper.ToOperationEntity(new() { Lease = new() { WriterId = Guid.NewGuid() }, CompletionSequence = 1, Record = record }, 1);

        var restored = ProfilingEntityMapper.ToOperationModel(entity, new OperationProfilingOptions());

        restored.CorrelationId.ShouldBe(record.CorrelationId);
        restored.Http.ShouldBe(record.Http);
    }

    [Fact]
    public void Mapping_ImmutableGraph_RestoresNodeIdentityAndLosslessTypedValues()
    {
        // Arrange
        var record = CreateRecord();
        ProfilingValue.TryCreate(long.MaxValue, out var integer);
        ProfilingValue.TryCreate(1m, out var number);
        record = record with { Dimensions = [new() { Key = "rows", Value = integer }, new() { Key = "scale", Value = number }] };
        var envelope = new ProfilingWriteEnvelope { Lease = new() { WriterId = Guid.NewGuid(), Generation = 2 }, CompletionSequence = 12, Record = record };

        // Act
        var entity = ProfilingEntityMapper.ToOperationEntity(envelope, 10);
        var model = ProfilingEntityMapper.ToOperationModel(entity, new OperationProfilingOptions());

        // Assert
        model.Id.ShouldBe(record.Id);
        model.Node.Identity.ShouldBe(record.Node.Identity);
        model.Node.ProcessStartedUtc.ShouldBe(record.Node.ProcessStartedUtc);
        model.Dimensions.Single(dimension => dimension.Key == "rows").Value.ShouldBe(integer);
        model.Dimensions.Single(dimension => dimension.Key == "scale").Value.Type.ShouldBe(ProfilingValueType.Decimal);
        entity.Dimensions.Single(dimension => dimension.Key == "rows").ValueScalar.ShouldBe(long.MaxValue.ToString());
        entity.WriterGeneration.ShouldBe(2);
        entity.CompletionSequence.ShouldBe(12);
        entity.CommitWatermark.ShouldBe(10);
        ((ICollection<ProfilingDimension>)model.Dimensions).IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void Mapping_StructuredSummary_UsesDistinctOwnerPathAndTypedMetadataRows()
    {
        // Arrange
        var record = CreateRecord();
        ProfilingValue.TryCreate("sql", out var sql);
        var statistics = new ProfilingDurationStatistics { Count = 2, TotalDuration = TimeSpan.FromMilliseconds(5), MinimumDuration = TimeSpan.FromMilliseconds(2), MaximumDuration = TimeSpan.FromMilliseconds(3) };
        var segment = new ProfilingSegmentSummary
        {
            OperationId = record.Id, NodeId = record.NodeId, Key = "Read", Path = new(["Read"]), Statistics = statistics,
            Outcomes = [new() { Outcome = ProfilingSegmentOutcome.Completed, Statistics = statistics }],
            Dimensions = [new() { Key = "source", Value = sql, SampleCount = 2 }],
        };
        record = record with { Segments = [segment] };

        // Act
        var entity = ProfilingEntityMapper.ToOperationEntity(new() { Lease = new() { WriterId = Guid.NewGuid() }, CompletionSequence = 1, Record = record }, 1);
        var model = ProfilingEntityMapper.ToOperationModel(entity, new OperationProfilingOptions());

        // Assert
        entity.Segments.Single().Count.ShouldBe(2);
        entity.Segments.Single().CompletedCount.ShouldBe(2);
        entity.Dimensions.Single().SegmentId.ShouldBe(entity.Segments.Single().Id);
        entity.Dimensions.Single().ScopeHash.ShouldBe(entity.Segments.Single().PathHash);
        model.Segments.Single().Path.ShouldBe(segment.Path);
        model.Segments.Single().Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(5));
        model.Segments.Single().Dimensions.Single().Value.ShouldBe(sql);
    }

    [Fact]
    public void ComparisonBytes_CanonicalNamesAndOrdinalValues_KeepAllBoundariesAndWhitespace()
    {
        // Arrange/Act
        var upper = ProfilingOperationComparisons.Canonical("Load");
        var lower = ProfilingOperationComparisons.Canonical("load");
        var value = ProfilingOperationComparisons.Exact("Load");
        var otherValue = ProfilingOperationComparisons.Exact("load");

        // Assert
        upper.ShouldBe(lower);
        value.ShouldNotBe(otherValue);
        ProfilingOperationComparisons.Canonical("Load ").ShouldNotBe(upper);
        Encoding.BigEndianUnicode.GetString(ProfilingOperationComparisons.BaseGroup("Service", "Load")).ShouldBe("7:SERVICE4:LOAD");
        ProfilingOperationComparisons.BaseGroup("AB", "C").ShouldNotBe(ProfilingOperationComparisons.BaseGroup("A", "BC"));
    }

    [Fact]
    public void NodeMapping_OperationOnly_HasNoBroadcastCorrelation()
    {
        // Arrange
        var node = new ProfilingNodeIdentityProvider().GetNode();

        // Act
        var entity = ProfilingEntityMapper.ToEntity(null, node);
        var restored = ProfilingEntityMapper.ToModel(entity);

        // Assert
        entity.BroadcastNodeIdentity.ShouldBeNull();
        restored.Correlation.ShouldBeNull();
        restored.Identity.ShouldBe(node.Identity);
        restored.ProcessStartedUtc.ShouldBe(node.ProcessStartedUtc);
    }

    private static OperationProfilingRecord CreateRecord()
    {
        var utc = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
        return new()
        {
            Id = Guid.NewGuid(), Key = "Report", Kind = "Service", Node = new ProfilingNodeIdentityProvider().GetNode(),
            StartedUtc = utc.AddMilliseconds(-10), CompletedUtc = utc, Duration = TimeSpan.FromMilliseconds(10), Outcome = OperationProfilingOutcome.Completed,
            WallTime = [new() { Kind = ProfilingWallTimeKind.OutsideSegments, Duration = TimeSpan.FromMilliseconds(10) }],
        };
    }
}
