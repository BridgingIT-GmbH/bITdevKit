// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Application.Storage;
using BridgingIT.DevKit.Application.UnitTests;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Application")]
public sealed class FileStorageBehaviorTests
{
    [Fact]
    public async Task MetricsBehavior_WithOperations_EmitsExpectedLowCardinalityMetrics()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        using var recorder = new RecordingMetrics();
        var metrics = new MetricsFileStorageBehavior(
            new ScriptedFileStorageProvider("secret-location")
            {
                Read = _ => Result<Stream>.Success(new MemoryStream([1, 2, 3, 4])),
                ListFiles = _ => Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(
                    (["secret/path/a.txt", "secret/path/b.txt"], "raw-continuation-token")),
                Exists = _ => Result.Failure()
                    .WithError(new FileSystemError("secret failure message", "secret/path/missing.txt"))
            },
            meterFactory);

        // Act
        await metrics.ReadFileAsync("secret/path/file.txt");
        await metrics.ListFilesAsync("secret/path", "*.txt", recursive: true, continuationToken: "raw-continuation-token");
        await metrics.FileExistsAsync("secret/path/missing.txt");

        // Assert
        recorder.CounterSum("filestorage_operations").ShouldBe(3);
        recorder.HistogramCount("filestorage_operation_duration").ShouldBe(3);
        recorder.CounterSum("filestorage_operation_failures").ShouldBe(1);
        recorder.CounterSum("filestorage_bytes").ShouldBe(4);
        recorder.CounterSum("filestorage_items").ShouldBe(2);
        recorder.AllTagKeys.ShouldBeSubsetOf(["operation", "location", "provider"]);
        recorder.AllTagValues.ShouldContain("secret_location");
        recorder.AllTagValues.ShouldNotContain("secret/path/file.txt");
        recorder.AllTagValues.ShouldNotContain("secret/path/missing.txt");
        recorder.AllTagValues.ShouldNotContain("raw-continuation-token");
        recorder.AllTagValues.ShouldNotContain("secret failure message");
    }

    [Fact]
    public void CreateProvider_WithMetricsBehavior_ShouldResolveDecoratedProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddFileStorage(factory => factory
            .RegisterProvider("files", storage => storage
                .UseInMemory("files")
                .WithMetrics()));

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var provider = serviceProvider.GetRequiredService<IFileStorageProviderFactory>()
            .CreateProvider("files");

        // Assert
        provider.ShouldBeOfType<MetricsFileStorageBehavior>();
    }

    private sealed class ScriptedFileStorageProvider(string locationName) : BaseFileStorageProvider(locationName)
    {
        public Func<string, Result> Exists { get; init; }

        public Func<string, Result<Stream>> Read { get; init; }

        public Func<string, Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFiles { get; init; }

        public override Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Exists?.Invoke(path) ?? Result.Success());

        public override Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Read?.Invoke(path) ?? Result<Stream>.Success(new MemoryStream()));

        public override Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
            string path,
            string searchPattern,
            bool recursive,
            string continuationToken = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(this.ListFiles?.Invoke(path) ??
                Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(([], null)));
    }

    private sealed class RecordingMetrics : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<long>> counters = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ConcurrentBag<double>> histograms = new(StringComparer.Ordinal);
        private readonly ConcurrentBag<string> tagKeys = [];
        private readonly ConcurrentBag<string> tagValues = [];

        public RecordingMetrics()
        {
            this.listener.InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, Metrics.MeterName, StringComparison.Ordinal) &&
                    instrument.Name.StartsWith("filestorage_", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            this.listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                this.counters.GetOrAdd(instrument.Name, _ => []).Add(measurement);
                this.RecordTags(tags);
            });
            this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                this.histograms.GetOrAdd(instrument.Name, _ => []).Add(measurement);
                this.RecordTags(tags);
            });
            this.listener.Start();
        }

        public IReadOnlyCollection<string> AllTagKeys => this.tagKeys.ToArray();

        public IReadOnlyCollection<string> AllTagValues => this.tagValues.ToArray();

        public long CounterSum(string series) =>
            this.counters.TryGetValue(series, out var values) ? values.Sum() : 0;

        public int HistogramCount(string series) =>
            this.histograms.TryGetValue(series, out var values) ? values.Count : 0;

        public void Dispose() => this.listener.Dispose();

        private void RecordTags(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            foreach (var tag in tags)
            {
                this.tagKeys.Add(tag.Key);
                this.tagValues.Add(tag.Value?.ToString() ?? string.Empty);
            }
        }
    }
}
