// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Application.Storage;

[UnitTest("Application")]
[Collection(ProcessWideStateTestCollection.Name)]
public sealed class MetricsDocumentStoreClientBehaviorTests
{
    private static readonly DocumentKey Key = new("people", "42");

    [Fact]
    public async Task UpsertAsync_WithContent_ReportsLogicalLengthAndRecordsWriteRate()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);

        // Act
        var result = await sut.UpsertAsync(Key, new Person { Name = new string('a', 4096) });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentLength.ShouldBeGreaterThan(4096);
        recorder.CounterSum("document.items", "upsert").ShouldBe(1);
        recorder.CounterSum("document.bytes", "upsert").ShouldBe(result.Value.ContentLength);
        var rates = recorder.HistogramValues("document.bytes_per_second", "upsert");
        rates.Count.ShouldBe(1);
        rates.Single().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetAsync_WithStoredDocument_ReportsSameLengthAndRecordsReadRate()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);
        var written = await sut.UpsertAsync(Key, new Person { Name = "Ada" });

        // Act
        var result = await sut.GetAsync(Key);

        // Assert
        result.Value.ContentLength.ShouldBe(written.Value.ContentLength);
        recorder.CounterSum("document.items", "get").ShouldBe(1);
        recorder.CounterSum("document.bytes", "get").ShouldBe(written.Value.ContentLength);
        recorder.HistogramValues("document.bytes_per_second", "get").Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpsertManyAsync_WithTwoDocuments_RecordsSummedBytesAndOneRate()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);

        // Act
        var result = await sut.UpsertManyAsync(
        [
            new DocumentWrite<Person>(new("people", "1"), new Person { Name = "Ada" }),
            new DocumentWrite<Person>(new("people", "2"), new Person { Name = "Grace" })
        ]);

        // Assert
        recorder.CounterSum("document.items", "upsert-many").ShouldBe(2);
        recorder.CounterSum("document.bytes", "upsert-many").ShouldBe(result.Value.Items.Sum(x => x.ContentLength));
        recorder.HistogramValues("document.bytes_per_second", "upsert-many").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetManyAsync_WithTwoDocuments_RecordsSummedBytesAndOneReadRate()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);
        var first = await sut.UpsertAsync(new("people", "1"), new Person { Name = "Ada" });
        var second = await sut.UpsertAsync(new("people", "2"), new Person { Name = "Grace" });

        // Act
        var result = await sut.GetManyAsync([new("people", "1"), new("people", "2"), new("people", "missing")]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        recorder.CounterSum("document.items", "get-many").ShouldBe(2);
        recorder.CounterSum("document.bytes", "get-many").ShouldBe(first.Value.ContentLength + second.Value.ContentLength);
        recorder.HistogramValues("document.bytes_per_second", "get-many").Count.ShouldBe(1);
    }

    [Fact]
    public async Task FindPageAsync_WithDocuments_RecordsSummedBytesAndOneRate()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);
        await sut.UpsertAsync(new("people", "1"), new Person { Name = "Ada" });
        await sut.UpsertAsync(new("people", "2"), new Person { Name = "Grace" });

        // Act
        var result = await sut.FindPageAsync(new DocumentQuery { AllowFullScan = true, Take = 10 });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        recorder.CounterSum("document.items", "find").ShouldBe(2);
        recorder.CounterSum("document.bytes", "find").ShouldBe(result.Value.Items.Sum(x => x.ContentLength));
        recorder.HistogramValues("document.bytes_per_second", "find").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WithMissingDocument_CountsFailureAndRecordsNoBytes()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);

        // Act
        var result = await sut.GetAsync(Key);

        // Assert
        result.IsFailure.ShouldBeTrue();
        recorder.CounterSum("document.operations", "get").ShouldBe(1);
        recorder.CounterSum("document.operation.failures", "get").ShouldBe(1);
        recorder.CounterSum("document.items", "get").ShouldBe(0);
        recorder.CounterSum("document.bytes", "get").ShouldBe(0);
        recorder.HistogramValues("document.bytes_per_second", "get").ShouldBeEmpty();
        recorder.HistogramValues("document.operation.duration", "get").Count.ShouldBe(1);
    }

    [Fact]
    public async Task AllOperations_AreCountedWithOperationTag()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);
        await sut.UpsertAsync(Key, new Person { Name = "Ada" });

        // Act
        await sut.ListPageAsync(new DocumentQuery { AllowFullScan = true, Take = 10 });
        await sut.CountAsync(new DocumentCountQuery { AllowFullScan = true });
        await sut.ExistsAsync(Key);
        await sut.UpdatePropertiesAsync(new DocumentPropertiesUpdate(Key));
        await sut.DeleteManyAsync([new DocumentDelete(Key)]);
        await sut.DeleteAsync(Key);

        // Assert
        foreach (var operation in new[] { "list", "count", "exists", "update-properties", "delete-many", "delete" })
        {
            recorder.CounterSum("document.operations", operation).ShouldBe(1, operation);
        }

        recorder.HistogramValues("document.bytes_per_second", "list").ShouldBeEmpty();
        recorder.HistogramValues("document.bytes_per_second", "update-properties").ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertManyAsync_WhenBatchStopsAtFailedKey_CountsFailure()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var sut = CreateSut(meterFactory);
        await sut.UpsertAsync(new("people", "1"), new Person { Name = "Ada" });

        // Act
        var result = await sut.UpsertManyAsync(
        [
            new DocumentWrite<Person>(new("people", "1"), new Person(), new DocumentWriteOptions { CreateOnly = true })
        ]);

        // Assert
        result.Value.FailedKey.ShouldNotBeNull();
        recorder.CounterSum("document.operation.failures", "upsert-many").ShouldBe(1);
        recorder.CounterSum("document.items", "upsert-many").ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_WithoutMetricsService_DoesNotThrow()
    {
        // Arrange
        var sut = new MetricsDocumentStoreClientBehavior<Person>(
            new DocumentStoreClient<Person>(new InMemoryDocumentStoreProvider()));

        // Act
        var result = await sut.GetAsync(Key);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    private static MetricsDocumentStoreClientBehavior<Person> CreateSut(IMeterFactory meterFactory) =>
        new(
            new DocumentStoreClient<Person>(new InMemoryDocumentStoreProvider(), options: new() { AllowFullScans = true }),
            new MetricsService(meterFactory));

    public sealed class Person
    {
        public string Name { get; set; }
    }

    private sealed class RecordingMetrics : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly ConcurrentBag<(string Series, string Operation, double Value)> samples = [];

        public RecordingMetrics()
        {
            this.listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal) &&
                    instrument.Name.StartsWith("document.", StringComparison.Ordinal))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            this.listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
                this.samples.Add((instrument.Name, Operation(tags), measurement)));
            this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
                this.samples.Add((instrument.Name, Operation(tags), measurement)));
            this.listener.Start();
        }

        public long CounterSum(string series, string operation) =>
            (long)this.samples.Where(x => x.Series == series && x.Operation == operation).Sum(x => x.Value);

        public IReadOnlyList<double> HistogramValues(string series, string operation) =>
            [.. this.samples.Where(x => x.Series == series && x.Operation == operation).Select(x => x.Value)];

        public void Dispose() => this.listener.Dispose();

        private static string Operation(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "operation")
                {
                    return tag.Value?.ToString();
                }
            }

            return null;
        }
    }
}
