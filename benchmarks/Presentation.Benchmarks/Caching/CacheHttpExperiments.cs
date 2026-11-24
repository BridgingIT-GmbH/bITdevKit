// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Benchmarks.Caching;

using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Measures actual loopback HTTP resource transfers through the cache endpoints, requester and fully validated binary snapshots.</summary>
/// <remarks>Allocation is process-wide and includes client, Kestrel and cache. It is not a claim about cache-only overhead or production authentication.</remarks>
/// <example><code>await CacheHttpExperiments.RunAsync(CacheHttpExperimentOptions.Parse(args));</code></example>
public static class CacheHttpExperiments
{
    /// <summary>Runs authorization/conditional-resource correctness gates, warm-up and independent complete-transfer rounds, then writes raw JSON evidence.</summary>
    /// <param name="options">The validated bounded experiment profile.</param>
    public static async Task RunAsync(CacheHttpExperimentOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddAuthentication("benchmark").AddScheme<AuthenticationSchemeOptions, LocalAuthentication>("benchmark", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddApplicationCaching().WithOptions(o => o.With(x => x.Namespace, "http-benchmark")
                .With(x => x.Limits.MaxTransfers, Math.Max(8, options.Concurrency)))
            .WithBinaryContract("raw").WithBinaryCollection("reports", c => c.With(x => x.Limits.MaxTransfers, Math.Max(4, options.Concurrency)))
            .WithBinaryCollection("hidden", c => c.WithoutEndpoints())
            .WithEndpoints(endpoint => endpoint.WithPath("/cache").WithCachePolicy(CacheHttpCachePolicy.Revalidate));
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEndpoints();
        var payload = new byte[options.PayloadBytes];
        for (var index = 0; index < payload.Length; index++) { payload[index] = (byte)(index % 251); }

        var cache = app.Services.GetRequiredService<IApplicationCache>().GetBinaryCollection("reports");
        using (var seed = new MemoryStream(payload, writable: false))
        {
            var stored = await cache.SetAsync(new("one"), seed);
            if (stored.IsFailure || stored.Value.StorageOutcome != CacheStorageOutcome.Stored) { throw new InvalidDataException("The HTTP benchmark seed was not published."); }
        }

        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
            using var client = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = options.Concurrency }) { BaseAddress = new(address) };
            await VerifyBoundaryAsync(client, payload);
            for (var index = 0; index < 32; index++) { await TransferAsync(client, payload); }

            var rounds = new List<object>();
            for (var round = 0; round < options.Rounds; round++)
            {
                var samples = new double[checked(options.Concurrency * options.Iterations)];
                var firstBytes = new double[samples.Length];
                var allocations = GC.GetTotalAllocatedBytes(precise: true);
                var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                var started = Stopwatch.GetTimestamp();
                await Task.WhenAll(Enumerable.Range(0, options.Concurrency).Select(async worker =>
                {
                    for (var iteration = 0; iteration < options.Iterations; iteration++)
                    {
                        var sample = await TransferAsync(client, payload);
                        var position = worker * options.Iterations + iteration;
                        firstBytes[position] = sample.FirstByte;
                        samples[position] = sample.Complete;
                    }
                }));
                var elapsed = Stopwatch.GetElapsedTime(started);
                var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocations;
                Array.Sort(samples);
                Array.Sort(firstBytes);
                rounds.Add(new
                {
                    Round = round + 1, Requests = samples.Length, SuccessfulEntries = samples.Length, Failures = 0, Missing = 0,
                    Bytes = (long)samples.Length * payload.Length, Seconds = elapsed.TotalSeconds,
                    OperationsPerSecond = samples.Length / elapsed.TotalSeconds, SuccessfulEntriesPerSecond = samples.Length / elapsed.TotalSeconds,
                    ProcessAllocatedBytes = allocated, ProcessAllocatedBytesPerEntry = (double)allocated / samples.Length,
                    CompleteMilliseconds = new { P50 = Percentile(samples, 0.50), P95 = Percentile(samples, 0.95), P99 = Percentile(samples, 0.99) },
                    FirstByteMilliseconds = new { P50 = Percentile(firstBytes, 0.50), P95 = Percentile(firstBytes, 0.95), P99 = Percentile(firstBytes, 0.99) },
                    Gen0 = GC.CollectionCount(0) - collections[0], Gen1 = GC.CollectionCount(1) - collections[1], Gen2 = GC.CollectionCount(2) - collections[2]
                });
            }

            var resources = app.Services.GetRequiredService<CacheRuntimeResources>();
            var output = new
            {
                ObservedAt = DateTimeOffset.UtcNow, Profile = "InMemory/real-Kestrel/loopback/full-validated-binary-transfer", Options = options,
                Runtime = RuntimeInformation.FrameworkDescription, OperatingSystem = RuntimeInformation.OSDescription,
                ProcessorCount = Environment.ProcessorCount, ServerGc = System.Runtime.GCSettings.IsServerGC,
                AllocationScope = "whole process including HTTP client and server; no cache-only subtraction", Integrity = "every byte checked against deterministic payload",
                Limits = app.Services.GetRequiredService<CacheCatalog>().Options.Limits,
                CollectionLimits = app.Services.GetRequiredService<CacheCatalog>().Collections["reports"].Options.Limits,
                ResidentBytes = resources.Resident.Used, TransientBytesAfter = resources.Transient.Used,
                WorkingSetBytes = Environment.WorkingSet, Rounds = rounds
            };
            var path = Path.GetFullPath(options.Output);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var json = JsonSerializer.Serialize(output, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
            Console.WriteLine(json);
        }
        finally { await app.StopAsync(); }
    }

    private static async Task VerifyBoundaryAsync(HttpClient client, byte[] payload)
    {
        using (var anonymous = await client.GetAsync("/cache/reports/entries?key=one"))
        {
            if (anonymous.StatusCode != HttpStatusCode.Unauthorized) { throw new InvalidDataException("Anonymous access was not denied."); }
        }

        client.DefaultRequestHeaders.Add("X-Cache-Benchmark", "local-loopback-fixture");
        using var first = await client.GetAsync("/cache/reports/entries?key=one");
        first.EnsureSuccessStatusCode();
        if (!(await first.Content.ReadAsByteArrayAsync()).AsSpan().SequenceEqual(payload) || first.Headers.ETag is null) { throw new InvalidDataException("The initial HTTP snapshot did not validate."); }

        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/cache/reports/entries?key=one");
        conditional.Headers.IfNoneMatch.Add(first.Headers.ETag);
        using var unchanged = await client.SendAsync(conditional);
        if (unchanged.StatusCode != HttpStatusCode.NotModified) { throw new InvalidDataException("Metadata-only conditional GET failed."); }

        using var stale = new HttpRequestMessage(HttpMethod.Put, "/cache/reports/entries?key=one") { Content = new ByteArrayContent(payload) };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"stale-generation\"");
        stale.Content.Headers.ContentType = new("application/octet-stream");
        using var rejected = await client.SendAsync(stale);
        if (rejected.StatusCode != HttpStatusCode.PreconditionFailed) { throw new InvalidDataException("The stale conditional write was not rejected."); }

        using var hidden = await client.GetAsync("/cache/hidden/entries?key=one");
        if (hidden.StatusCode != HttpStatusCode.BadRequest) { throw new InvalidDataException("Collection opt-out was not enforced."); }
    }

    private static async Task<(double FirstByte, double Complete)> TransferAsync(HttpClient client, byte[] expected)
    {
        var started = Stopwatch.GetTimestamp();
        using var response = await client.GetAsync("/cache/reports/entries?key=one", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[Math.Min(65536, expected.Length)];
        var position = 0;
        var firstByte = 0d;
        while (true)
        {
            var count = await stream.ReadAsync(buffer);
            if (count == 0) { break; }

            if (position == 0) { firstByte = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }

            if (position + count > expected.Length || !buffer.AsSpan(0, count).SequenceEqual(expected.AsSpan(position, count))) { throw new InvalidDataException("The HTTP transfer was truncated or corrupt."); }

            position += count;
        }

        if (position != expected.Length) { throw new InvalidDataException("The HTTP transfer ended before the complete payload."); }

        return (firstByte, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static double Percentile(double[] sorted, double percentile) => sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];

    private sealed class LocalAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (this.Request.Headers["X-Cache-Benchmark"] != "local-loopback-fixture") { return Task.FromResult(AuthenticateResult.NoResult()); }

            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "local-benchmark")], "benchmark"));
            return Task.FromResult(AuthenticateResult.Success(new(principal, "benchmark")));
        }
    }
}