// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class StreamingExperiments
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string Description = new('a', 960);
    private static readonly ConcurrentDictionary<Guid, SourceState> Sources = new();
    private static readonly string[] Formats =
    [
        "json-buffered",
        "json-streamed",
        "raw",
        "batch16",
        "batch64",
        "envelope",
    ];

    internal static async Task RunAsync(string[] args)
    {
        var output =
            args.Length == 0 ? "docs/validation/streaming-experiments/http.json" : args.Single();
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = "Production" }
        );
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddNdjsonStreaming();
        await using var app = builder.Build();
        foreach (var format in Formats)
        {
            if (format.StartsWith("json-", StringComparison.Ordinal))
            {
                app.MapGet(
                    $"/{format}",
                    async (int count, int delay, CancellationToken token) =>
                    {
                        var source = Produce(count, delay, Description, cancellationToken: token);
                        if (format == "json-streamed")
                        {
                            return Results.Json(source, Json);
                        }

                        var records = new List<StockRecord>();
                        await foreach (var record in source)
                        {
                            records.Add(record);
                        }

                        return Results.Json(records, Json);
                    }
                );
            }
            else
            {
                app.MapGet(
                        $"/{format}",
                        (int count, int delay) =>
                            NdjsonHttpResult.From(Produce(count, delay, Description))
                    )
                    .WithNdjson<StockRecord>(options => Configure(format, options));
            }
        }

        app.MapGet(
                "/paused/{id:guid}",
                (Guid id) =>
                    NdjsonHttpResult.From(
                        Produce(int.MaxValue, 0, new string('a', 65500), Sources[id])
                    )
            )
            .WithNdjson<StockRecord>();
        await app.StartAsync();
        try
        {
            var address = app
                .Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()
                .Addresses.Single();
            using var client = new HttpClient
            {
                BaseAddress = new Uri(address),
                Timeout = TimeSpan.FromSeconds(60),
            };
            var warmup = Stopwatch.StartNew();
            do
            {
                foreach (var format in Formats)
                {
                    await MeasureAsync(client, format, 10000, 0);
                }
            } while (warmup.Elapsed < TimeSpan.FromSeconds(5));

            var measurements = new List<TransferResult>();
            // Rotate ordering between rounds to reduce warm-up and thermal bias.
            for (var round = 0; round < 5; round++)
            {
                foreach (var format in Formats.Skip(round).Concat(Formats.Take(round)))
                {
                    var result = await MeasureAsync(client, format, 20000, 0);
                    measurements.Add(result);
                    Console.WriteLine(
                        $"bulk {format, -14} {result.RecordsPerSecond, 10:F0} records/s, first {result.FirstRecordMs:F2} ms, lag p95 {result.RecordLagP95Ms:F2} ms"
                    );
                }
            }

            // Sparse production exposes the latency cost of a batch that never fills.
            foreach (var format in Formats)
            {
                var result = await MeasureAsync(client, format, 50, 2);
                measurements.Add(result);
                Console.WriteLine(
                    $"paced {format, -13} first {result.FirstRecordMs:F2} ms, lag p95 {result.RecordLagP95Ms:F2} ms"
                );
            }

            var backpressure = new List<BackpressureResult>();
            foreach (var connections in new[] { 1, 8 })
            {
                var result = await MeasurePausedAsync(client, connections);
                backpressure.Add(result);
                Console.WriteLine(
                    $"paused {connections}: source progress {result.AdditionalProducedWhilePaused}, held heap delta {result.HeldHeapDeltaBytes / 1048576d:F2} MiB, cleanup {result.CleanupMs:F2} ms"
                );
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            await File.WriteAllTextAsync(
                output,
                JsonSerializer.Serialize(
                    new
                    {
                        CapturedUtc = DateTimeOffset.UtcNow,
                        Runtime = RuntimeInformation.FrameworkDescription,
                        OS = RuntimeInformation.OSDescription,
                        ProcessorCount = Environment.ProcessorCount,
                        Notes = "HTTP/1.1 loopback, no TLS/compression/proxy, one process contains server and client. Five warm bulk rounds. Paced cases have one observation. Heap uses forced GC outside transfer timing; includes pools and transport. Not a production capacity claim.",
                        Measurements = measurements,
                        Backpressure = backpressure,
                    },
                    new JsonSerializerOptions(Json) { WriteIndented = true }
                )
            );
            Console.WriteLine($"Saved {output}");
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(shutdown.Token);
        }
    }

    private static void Configure(string format, NdjsonEndpointOptions options)
    {
        options.Mode = format == "envelope" ? NdjsonMode.Envelope : NdjsonMode.Raw;
        if (format is "batch16" or "batch64")
        {
            options.BatchByteLimit = format == "batch16" ? 16384 : 65536;
            options.BatchMaxDelay = TimeSpan.FromMilliseconds(format == "batch16" ? 2 : 10);
        }
    }

    private static async Task<TransferResult> MeasureAsync(
        HttpClient client,
        string format,
        int expected,
        int delay
    )
    {
        var started = Stopwatch.GetTimestamp();
        var count = 0;
        var first = 0d;
        var lags = new double[expected];
        var uri = $"/{format}?count={expected}&delay={delay}";
        if (format.StartsWith("json-", StringComparison.Ordinal))
        {
            using var response = await client.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead
            );
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync();
            if (format == "json-buffered")
            {
                foreach (
                    var record in (
                        await JsonSerializer.DeserializeAsync<StockRecord[]>(body, Json)
                    )!
                )
                {
                    Observe(record);
                }
            }
            else
            {
                await foreach (
                    var record in JsonSerializer.DeserializeAsyncEnumerable<StockRecord>(body, Json)
                )
                {
                    Observe(record!);
                }
            }
        }
        else
        {
            var envelope = format == "envelope";
            var opened = await client.GetNdjsonAsync<StockRecord>(
                uri,
                new() { ExpectedMode = envelope ? NdjsonMode.Envelope : NdjsonMode.Raw }
            );
            if (opened.IsFailure)
            {
                throw new InvalidOperationException("Could not open benchmark response.");
            }

            await using var response = opened.Value;
            await foreach (var record in envelope ? response.ReadDataAsync() : response.ReadAsync())
            {
                Observe(record);
            }
        }

        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        if (count != expected)
        {
            throw new InvalidOperationException($"Expected {expected} records, got {count}.");
        }

        Array.Sort(lags);
        return new TransferResult(
            format,
            expected,
            delay,
            expected / elapsed,
            first,
            lags[(int)Math.Ceiling(expected * 0.95) - 1]
        );

        void Observe(StockRecord record)
        {
            if (
                record.Sequence != count
                || record.Quantity != 12
                || record.Description != Description
            )
            {
                throw new InvalidOperationException("The benchmark changed or reordered records.");
            }

            if (count == 0)
            {
                first = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }

            lags[count++] = Stopwatch.GetElapsedTime(record.CreatedAt).TotalMilliseconds;
        }
    }

    private static async Task<BackpressureResult> MeasurePausedAsync(
        HttpClient client,
        int connections
    )
    {
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        var states = new List<SourceState>();
        var responses = new List<NdjsonHttpResponse<StockRecord>>();
        var iterators = new List<IAsyncEnumerator<StockRecord>>();
        try
        {
            for (var index = 0; index < connections; index++)
            {
                var id = Guid.NewGuid();
                var state = new SourceState(id);
                Sources[id] = state;
                states.Add(state);
                var opened = await client.GetNdjsonAsync<StockRecord>($"/paused/{id}");
                if (opened.IsFailure)
                {
                    throw new InvalidOperationException("Could not open paused response.");
                }

                responses.Add(opened.Value);
                var iterator = opened.Value.ReadAsync().GetAsyncEnumerator();
                iterators.Add(iterator);
                if (!await iterator.MoveNextAsync())
                {
                    throw new InvalidOperationException("Missing first record.");
                }
            }

            await Task.Delay(1000);
            var produced = states.Sum(state => Interlocked.Read(ref state.Produced));
            var held = GC.GetTotalMemory(forceFullCollection: true);
            await Task.Delay(1000);
            var additional = states.Sum(state => Interlocked.Read(ref state.Produced)) - produced;
            var later = GC.GetTotalMemory(forceFullCollection: true);
            var started = Stopwatch.GetTimestamp();
            foreach (var response in responses)
            {
                await response.DisposeAsync();
            }

            await Task.WhenAll(states.Select(state => state.Disposed.Task))
                .WaitAsync(TimeSpan.FromSeconds(10));
            return new BackpressureResult(
                connections,
                produced,
                additional,
                held - baseline,
                later - held,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds
            );
        }
        finally
        {
            foreach (var response in responses)
            {
                await response.DisposeAsync();
            }

            foreach (var iterator in iterators)
            {
                await iterator.DisposeAsync();
            }

            foreach (var state in states)
            {
                Sources.TryRemove(state.Id, out _);
            }
        }
    }

    private static async IAsyncEnumerable<StockRecord> Produce(
        int count,
        int delay,
        string description,
        SourceState state = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        try
        {
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (delay != 0)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                if (state is not null)
                {
                    Interlocked.Increment(ref state.Produced);
                }

                yield return new StockRecord(index, description, 12, Stopwatch.GetTimestamp());
            }
        }
        finally
        {
            state?.Disposed.TrySetResult();
        }
    }

    private sealed record StockRecord(
        int Sequence,
        string Description,
        int Quantity,
        long CreatedAt
    );

    private sealed record TransferResult(
        string Format,
        int Count,
        int ProducerDelayMs,
        double RecordsPerSecond,
        double FirstRecordMs,
        double RecordLagP95Ms
    );

    private sealed record BackpressureResult(
        int Connections,
        long ProducedBeforePauseSample,
        long AdditionalProducedWhilePaused,
        long HeldHeapDeltaBytes,
        long PausedHeapChangeBytes,
        double CleanupMs
    );

    private sealed class SourceState(Guid id)
    {
        internal Guid Id { get; } = id;
        internal long Produced;
        internal TaskCompletionSource Disposed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
