// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Globalization;
using System.Text.Json;

public sealed class OperationProfilingMetadataTests
{
    [Theory]
    [InlineData(MeasurementAggregation.Sum, "10", ProfilingValueType.Int64)]
    [InlineData(MeasurementAggregation.Min, "1", ProfilingValueType.Int64)]
    [InlineData(MeasurementAggregation.Max, "7", ProfilingValueType.Int64)]
    [InlineData(MeasurementAggregation.Average, "3.3333333333333333333333333333", ProfilingValueType.Decimal)]
    [InlineData(MeasurementAggregation.Last, "7", ProfilingValueType.Int64)]
    public void Reducers_FoldInvocationReplacementOnce_AndKeepContributingCounts(MeasurementAggregation aggregation, string expected, ProfilingValueType type)
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        foreach (var value in new[] { 1, 2, 7 })
        {
            root.RunSegment("Read", segment =>
            {
                segment.SetMeasurement("items", 99, "count", aggregation);
                segment.SetMeasurement("ITEMS", value, "count", aggregation);
                clock.Advance(TimeSpan.FromMilliseconds(10));
            });
        }

        root.RunSegment("Read", _ => { }); // Missing values are not zero samples.
        root.SetMeasurement("items", 1, "count");
        root.SetMeasurement("ITEMS", 7, "count");
        root.Complete();
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Measurements.ShouldHaveSingleItem().Value.Scalar.ShouldBe("7");
        var measurement = record.Segments.ShouldHaveSingleItem().Measurements.ShouldHaveSingleItem();
        measurement.Value.Scalar.ShouldBe(expected);
        measurement.Value.Type.ShouldBe(type);
        measurement.SampleCount.ShouldBe(3);
        measurement.Key.ShouldBe("items");
        measurement.LastCompletionSequence.ShouldBe(3);
        measurement.LastCompletedUtc.ShouldBe(clock.GetUtcNow());
        measurement.Outcomes.ShouldHaveSingleItem().SampleCount.ShouldBe(3);
        if (aggregation == MeasurementAggregation.Average)
        {
            measurement.Sum.Scalar.ShouldBe("10");
        }
    }

    [Fact]
    public void OutcomeReducers_KeepFailuresSeparate_AndLastUsesCompletionOrder()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");
        var first = root.BeginSegment("Read");
        first.SetMeasurement("items", 1, "count", MeasurementAggregation.Last);
        var second = root.BeginSegment("read");
        second.SetMeasurement("ITEMS", 2, "count", MeasurementAggregation.Last);

        // Act
        clock.Advance(TimeSpan.FromMilliseconds(10));
        second.Complete();
        second.Dispose();
        clock.Advance(TimeSpan.FromMilliseconds(10));
        first.Fail(new ProfilingFailureDescriptor { Source = "Domain", Code = "Rejected" });
        first.Dispose();
        root.Complete();
        root.Dispose();

        // Assert
        var summary = sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem();
        var measurement = summary.Measurements.ShouldHaveSingleItem();
        measurement.Value.Scalar.ShouldBe("1");
        measurement.LastCompletionSequence.ShouldBe(2);
        measurement.LastCompletedUtc.ShouldBe(clock.GetUtcNow());
        measurement.Outcomes.Single(o => o.Outcome == ProfilingSegmentOutcome.Failed).Value.Scalar.ShouldBe("1");
        measurement.Outcomes.Single(o => o.Outcome == ProfilingSegmentOutcome.Completed).Value.Scalar.ShouldBe("2");
        summary.Outcomes.Single(o => o.Outcome == ProfilingSegmentOutcome.Failed).Statistics.TotalDuration.ShouldBe(TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public void NumericValues_PreserveLargeIntegersDecimalAndDouble_AndInvariantCulture()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            root.SetDimension("input", 9007199254740993L);
            root.SetDimension("decimal", 0.1000000000000000000000000001m);
            root.SetDimension("double", 0.1d);
            root.RunSegment("read", s => s.SetDimension("input", 1));
            root.RunSegment("READ", s => s.SetDimension("INPUT", 1));
            root.Complete();
            root.Dispose();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Dimensions.Single(d => d.Key == "input").Value.Scalar.ShouldBe("9007199254740993");
        record.Dimensions.Single(d => d.Key == "decimal").Value.ShouldBe(new ProfilingValue(ProfilingValueType.Decimal, "0.1000000000000000000000000001"));
        record.Dimensions.Single(d => d.Key == "double").Value.Type.ShouldBe(ProfilingValueType.Double);
        record.Segments.ShouldHaveSingleItem().Dimensions.ShouldHaveSingleItem().Mixed.ShouldBeFalse();
        var json = JsonSerializer.Serialize(record);
        json.ShouldContain("9007199254740993");
        json.ShouldContain("Z\"");
        json.ShouldNotContain("+00:00");
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("type")]
    [InlineData("aggregation")]
    public void IncompatibleMeasurement_MarksAggregateUnavailable_WithoutCombining(string conflict)
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        root.RunSegment("Read", s => s.SetMeasurement("items", 1, "count"));
        root.RunSegment("Read", s => s.SetMeasurement("items", conflict == "type" ? (object)1m : 1,
            conflict == "unit" ? "Count" : "count", conflict == "aggregation" ? MeasurementAggregation.Max : MeasurementAggregation.Sum));
        root.Complete();
        root.Dispose();

        // Assert
        var measurement = sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem().Measurements.ShouldHaveSingleItem();
        measurement.Conflicting.ShouldBeTrue();
        measurement.Unavailable.ShouldBeTrue();
        measurement.Value.ShouldBeNull();
        measurement.SampleCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(ProfilingValueType.Int64)]
    [InlineData(ProfilingValueType.Decimal)]
    [InlineData(ProfilingValueType.Double)]
    public void Overflow_MarksReducerUnavailable_InsteadOfWrapping(ProfilingValueType type)
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");
        var maximum = type switch { ProfilingValueType.Int64 => (object)long.MaxValue, ProfilingValueType.Decimal => decimal.MaxValue, _ => double.MaxValue };

        // Act
        root.RunSegment("Read", s => s.SetMeasurement("items", maximum, "count"));
        root.RunSegment("Read", s => s.SetMeasurement("items", maximum, "count"));
        root.Complete();
        root.Dispose();

        // Assert
        var measurement = sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem().Measurements.ShouldHaveSingleItem();
        measurement.Unavailable.ShouldBeTrue();
        measurement.Conflicting.ShouldBeFalse();
        measurement.Value.ShouldBeNull();
        measurement.Sum.ShouldBeNull();
        measurement.SampleCount.ShouldBe(2);
    }

    [Fact]
    public void Dimensions_DistinguishMixedMissingEmptyAndTypes()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        root.RunSegment("Read", s => { s.SetDimension("case", "Load"); s.SetDimension("empty", ""); s.SetDimension("type", 1); s.SetDimension("stable", 1.0m); });
        root.RunSegment("Read", s => { s.SetDimension("CASE", "load"); s.SetDimension("type", 1m); s.SetDimension("stable", 1.00m); });
        root.Complete();
        root.Dispose();

        // Assert
        var dimensions = sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem().Dimensions;
        dimensions.Single(d => d.Key == "case").Mixed.ShouldBeTrue();
        dimensions.Single(d => d.Key == "type").Mixed.ShouldBeTrue();
        var empty = dimensions.Single(d => d.Key == "empty");
        empty.Value.Scalar.ShouldBe("");
        empty.Partial.ShouldBeTrue();
        empty.SampleCount.ShouldBe(1);
        dimensions.Single(d => d.Key == "stable").Mixed.ShouldBeFalse();
        dimensions.Single(d => d.Key == "stable").Partial.ShouldBeFalse();
    }

    [Fact]
    public void FailureCategories_AreBounded_CountOnce_AndKeepFirstSanitizedMessage()
    {
        // Arrange
        var (profiler, sink, clock) = OperationProfilerTests.Create(o => o.MaxFailureCategories = 2);
        using var root = profiler.BeginOperation("root");

        // Act
        foreach (var code in new[] { "A", "A", "B", "C", "D" })
        {
            using var segment = root.BeginSegment("Read");
            segment.Fail(new ProfilingFailureDescriptor { Source = "Domain", Code = code, Message = code + " sample" });
            segment.Fail(new ProfilingFailureDescriptor { Source = "Domain", Code = "Again" });
            clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        root.Complete();
        root.Dispose();

        // Assert
        var summary = sink.Records.ShouldHaveSingleItem().Segments.ShouldHaveSingleItem();
        summary.Statistics.Count.ShouldBe(5);
        summary.Failures.Count.ShouldBe(2);
        summary.Failures.Single(f => f.Failure.Code == "A").Count.ShouldBe(2);
        summary.Failures.Single(f => f.Failure.Code == "A").Failure.Message.ShouldBe("A sample");
        summary.Failures.Single(f => f.Failure.Code == "A").LastObservedUtc.ShouldBeGreaterThan(summary.Failures.Single(f => f.Failure.Code == "A").FirstObservedUtc);
        summary.OtherFailureCount.ShouldBe(2);
    }

    [Fact]
    public void InvalidFailureKeys_AreNotShortenedIntoExistingGroups_AndMessageClippingPreservesUnicode()
    {
        // Arrange
        var (profiler, sink, _) = OperationProfilerTests.Create();
        using var root = profiler.BeginOperation("root");

        // Act
        root.Fail(new ProfilingFailureDescriptor { Source = new string('A', 129), Code = "safe", Message = new string('x', 1023) + "😀" });
        root.SetSource(new ProfilingAdapterMetadata { Kind = "Invalid", Fields = [new() { Key = "text", Value = new(ProfilingValueType.String, "\ud800") }] });
        root.Dispose();

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Failure.Source.ShouldBe("Application");
        record.Failure.Code.ShouldBe("InvalidDescriptor");
        record.Failure.Message.Length.ShouldBe(1023);
        record.Quality.Truncated.ShouldBeTrue();
        record.Quality.RejectedMetadataCount.ShouldBe(2);
        record.Sources.ShouldBeEmpty();
    }

    [Fact]
    public void FailingSanitizer_KeepsObservedFailure_AndDoesNotRetainRawMessage()
    {
        // Arrange
        var policy = Substitute.For<IProfilingSafeErrorPolicy>();
        policy.Classify(Arg.Any<Exception>()).Returns(_ => throw new InvalidOperationException("sanitizer"));
        var options = new ProfilingOptions { Enabled = true, Operations = new() { Enabled = true } };
        var sink = new OperationProfilerTests.CaptureSink();
        var profiler = new OperationProfiler(options, new ProfilingNodeIdentityProvider(), sink, errors: policy);

        // Act
        using (var root = profiler.BeginOperation("root"))
        {
            root.Fail(new InvalidOperationException("secret"));
            root.Complete();
        }

        // Assert
        var record = sink.Records.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(OperationProfilingOutcome.Failed);
        record.Quality.ClassificationFailed.ShouldBeTrue();
        record.Failure.Message.ShouldBeNull();
        record.Failure.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
    }
}
