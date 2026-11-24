// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Application.Storage;
using Common;
using DotNet.Testcontainers.Containers;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static readonly int[] RandomReadSizes = [1024, 16 * 1024, 256 * 1024, 1024 * 1024];

    private static int RandomReadClass(int owner) => (owner % 100) switch { < 70 => 0, < 90 => 1, < 99 => 2, _ => 3 };

    private static string RandomReadKey(int owner) => $"item-{owner:D8}";

    private static async Task<int> RunRandomReadWorkloadAsync(IServiceScopeFactory scopes, string engine,
        string feature, int owners, string output, bool smoke, QualificationCommands commands, IContainer container)
    {
        var errors = new ConcurrentQueue<string>();
        var expectedHashes = new byte[owners][];
        var pools = Enumerable.Range(0, 4).Select(kind => Enumerable.Range(0, owners).Where(owner => RandomReadClass(owner) == kind).ToArray()).ToArray();
        var templates = RandomReadSizes.Select((size, kind) =>
        {
            var bytes = new byte[size];
            new Random(DimensionQualificationData.Seed + kind).NextBytes(bytes);
            return bytes;
        }).ToArray();
        var rawBytes = pools.Select((pool, kind) => pool.LongLength * RandomReadSizes[kind]).Sum();
        var disk = await container.ExecAsync(["sh", "-c", engine == "sqlserver" ? "df -Pk /var/opt/mssql/data" : "df -Pk /var/lib/postgresql/data"]);
        if (disk.ExitCode != 0) throw new InvalidOperationException("Cannot verify owned database disk headroom.");
        var diskFields = disk.Stdout.Trim().Split('\n')[^1].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        var availableBytes = long.Parse(diskFields[3], System.Globalization.CultureInfo.InvariantCulture) * 1024;
        var requiredBytes = rawBytes * 3 + 10L * 1024 * 1024 * 1024;
        await WriteAsync(output, "random-read-disk-budget.json", new { AvailableBytes = availableBytes, RequiredBytes = requiredBytes, RawPayloadBytes = rawBytes, Output = disk.Stdout });
        if (availableBytes < requiredBytes) throw new InvalidOperationException("Insufficient database disk headroom for payloads, indexes and logs.");
        await WriteAsync(output, "random-read-definition.json", new
        {
            Engine = engine, Feature = feature, Owners = owners, Seed = DimensionQualificationData.Seed,
            PayloadClasses = pools.Select((pool, kind) => new { Bytes = RandomReadSizes[kind], Records = pool.Length }),
            RawPayloadBytes = rawBytes, Dimensions = false, Properties = false, Readers = new[] { 1, 16 },
            Repetitions = smoke ? 1 : 3, SecondsPerRepetition = smoke ? 3 : 30,
            Cache = "Warm-up with ordinary engine and host caches retained; not cold storage.",
            Boundary = "EF provider through complete payload consumption and SHA256 verification; no HTTP or typed deserialization.",
            BlobChunkBytes = new BlobStoreOptions().ChunkSize,
            Limitation = "All default payloads fit one blob chunk. Large payload classes have fewer records. Duplicate random body templates have unique record prefixes; compression is disabled."
        });

        Console.WriteLine($"Random content fixture: {engine}/{feature}/{owners:N0}, {rawBytes / 1073741824d:F2} GiB payload, no Dimensions.");
        var seedTimer = Stopwatch.StartNew();
        await using (var scope = scopes.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            for (var start = 0; start < owners; start += 100)
            {
                for (var owner = start; owner < Math.Min(start + 100, owners); owner++)
                {
                    var content = templates[RandomReadClass(owner)].ToArray();
                    BinaryPrimitives.WriteInt64LittleEndian(content, owner);
                    var hash = SHA256.HashData(content);
                    expectedHashes[owner] = hash;
                    var contentHash = $"sha256:{Convert.ToHexString(hash)}";
                    if (feature == "blob")
                    {
                        var blob = new StorageBlob
                        {
                            Container = "random-reads", ContainerHash = HashHelper.ComputeSha256("random-reads"),
                            Name = RandomReadKey(owner), NameHash = HashHelper.ComputeSha256(RandomReadKey(owner)),
                            Length = content.Length, ContentHash = contentHash, ETag = Guid.NewGuid().ToString("N")
                        };
                        blob.Chunks.Add(new() { Blob = blob, Index = 0, Content = content, Length = content.Length });
                        context.Add(blob);
                    }
                    else
                    {
                        context.Add(new StorageDocument
                        {
                            Type = "RandomReadRecord", TypeHash = HashHelper.ComputeSha256("RandomReadRecord"),
                            PartitionKey = "records", PartitionKeyHash = HashHelper.ComputeSha256("records"),
                            RowKey = RandomReadKey(owner), RowKeyHash = HashHelper.ComputeSha256(RandomReadKey(owner)),
                            Content = content, ContentHash = contentHash, StoredContentHash = contentHash
                        });
                    }
                }

                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
                if ((start + 100) % 10000 == 0) Console.WriteLine($"Seeded {start + 100:N0}/{owners:N0} in {seedTimer.Elapsed.TotalSeconds:F1}s.");
            }

            var actual = feature == "blob" ? await context.StorageBlobs.LongCountAsync() : await context.StorageDocuments.LongCountAsync();
            if (actual != owners || await context.Set<StorageBlobDimension>().AnyAsync() || await context.Set<StorageDocumentDimension>().AnyAsync())
                throw new InvalidOperationException("Random-read fixture population or empty Dimensions check failed.");
        }

        await RefreshStatisticsAsync(scopes, engine);
        await WriteAsync(output, "random-read-seed.json", new { Records = owners, RawPayloadBytes = rawBytes, Seconds = seedTimer.Elapsed.TotalSeconds, Dimensions = 0 });
        IBlobStoreProvider blobProvider = feature == "blob" ? new EntityFrameworkBlobStoreProvider<DimensionsQualificationContext>(scopes) : null;
        IDocumentStoreProvider documentProvider = feature == "document" ? new EntityFrameworkDocumentStoreProvider<DimensionsQualificationContext>(scopes) : null;
        var random = Enumerable.Range(0, 16).Select(worker => Enumerable.Range(0, 4)
            .Select(kind => new Random(DimensionQualificationData.Seed + worker * 1009 + kind * 7919)).ToArray()).ToArray();
        var visited = Enumerable.Range(0, 4).Select(_ => new ConcurrentDictionary<int, byte>()).ToArray();
        var jobs = RandomReadSizes.Select((size, kind) => new DimensionQualificationJob($"{feature}/random-content/{size}", false, 1, false, async worker =>
        {
            var owner = pools[kind][random[worker][kind].Next(pools[kind].Length)];
            byte[] hash;
            if (blobProvider is not null)
            {
                var download = Require(await blobProvider.DownloadAsync(new("random-reads", RandomReadKey(owner))));
                await using var stream = download.Content;
                using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                long length = 0;
                try
                {
                    int read;
                    while ((read = await stream.ReadAsync(buffer.AsMemory())) != 0)
                    {
                        checksum.AppendData(buffer, 0, read);
                        length += read;
                    }
                }
                finally { ArrayPool<byte>.Shared.Return(buffer); }

                if (length != size || download.Info.Length != size || download.Info.Dimensions.Count != 0)
                    throw new InvalidOperationException("Blob read returned incomplete content or unexpected Dimensions.");
                hash = checksum.GetHashAndReset();
            }
            else
            {
                var document = Require(await documentProvider.GetAsync(new("RandomReadRecord"), new("records", RandomReadKey(owner)), DateTimeOffset.UtcNow));
                if (document.Content.Length != size || document.Dimensions.Count != 0)
                    throw new InvalidOperationException("Document read returned incomplete content or unexpected Dimensions.");
                hash = SHA256.HashData(document.Content);
            }

            if (!hash.AsSpan().SequenceEqual(expectedHashes[owner])) throw new InvalidOperationException("Random content checksum mismatch.");
            visited[kind].TryAdd(owner, 0);
            return 1;
        })).ToArray();

        commands.Enabled = true;
        foreach (var job in jobs) await job.Execute(0);
        commands.Enabled = false;
        if (commands.Text.Any(command => System.Text.Json.JsonSerializer.Serialize(command).Contains("__Storage_BlobDimension", StringComparison.Ordinal) || System.Text.Json.JsonSerializer.Serialize(command).Contains("__Storage_DocumentDimension", StringComparison.Ordinal)))
            throw new InvalidOperationException("A random content read accessed dimension tables.");
        await WriteAsync(output, "random-read-commands.json", commands.Text.ToArray());

        var results = new List<object>();
        foreach (var readers in new[] { 1, 16 })
        {
            for (var run = 1; run <= (smoke ? 1 : 3); run++)
            {
                await LoadAsync(jobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 5), errors, false, readerCount: readers);
                foreach (var keys in visited) keys.Clear();
                var before = await CaptureDatabaseWorkAsync(scopes, engine);
                var timer = Stopwatch.StartNew();
                var cases = await LoadAsync(jobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 30), errors, true, readerCount: readers);
                timer.Stop();
                var record = new
                {
                    Readers = readers, Run = run, DurationSeconds = timer.Elapsed.TotalSeconds,
                    Cases = cases.Select((item, kind) => new
                    {
                        item.Name, PayloadBytes = RandomReadSizes[kind], ClassPopulation = pools[kind].Length,
                        UniqueKeys = visited[kind].Count, item.Operations, item.Failures,
                        item.P50Milliseconds, item.P95Milliseconds, item.P99Milliseconds, item.OverTwoSeconds,
                        VerifiedBytes = (item.Operations - item.Failures) * RandomReadSizes[kind],
                        OperationsPerSecond = item.Operations / timer.Elapsed.TotalSeconds,
                        MiBPerSecond = (item.Operations - item.Failures) * RandomReadSizes[kind] / 1048576d / timer.Elapsed.TotalSeconds,
                        PercentilesHaveAtLeast200Samples = item.Operations - item.Failures >= 200,
                        item.Distribution
                    }).ToArray(),
                    DatabaseWorkBefore = before, DatabaseWorkAfter = await CaptureDatabaseWorkAsync(scopes, engine)
                };
                results.Add(record);
                await WriteAsync(output, $"random-read-{readers}-{run}.json", record);
                Console.WriteLine($"Random reads {readers} readers, repetition {run}: {cases.Sum(item => item.Operations):N0} operations, {cases.Sum(item => item.Failures)} errors.");
                if (!errors.IsEmpty) break;
            }

            if (!errors.IsEmpty) break;
        }

        await WriteAsync(output, "random-read-results.json", new
        {
            Engine = engine, Feature = feature, Owners = owners, Smoke = smoke, OperationChecksPassed = errors.IsEmpty,
            QualificationComplete = false, Results = results, Errors = errors.ToArray(),
            Note = "Warmed random full-content provider reads. Exploratory, no Dimensions and no latency SLA. Histograms above two seconds retain overflow."
        });
        return errors.IsEmpty ? 0 : 1;
    }

    private static async Task<int> RunDimensionUpdateRecheckAsync(IServiceScopeFactory scopes, string engine,
        IReadOnlyList<DimensionQualificationJob> jobs, string output, bool smoke, QualificationCommands commands)
    {
        var errors = new ConcurrentQueue<string>();
        var results = new List<object>();
        for (var run = 1; run <= (smoke ? 1 : 3); run++)
        {
            await LoadAsync(jobs, engine, TimeSpan.FromSeconds(smoke ? 1 : 15), errors, false);
            var before = await CaptureDatabaseWorkAsync(scopes, engine);
            var commandBefore = commands.Capture();
            var timer = Stopwatch.StartNew();
            var cases = await LoadAsync(jobs, engine, TimeSpan.FromSeconds(smoke ? 3 : 120), errors, true);
            timer.Stop();
            var record = new { Run = run, DurationSeconds = timer.Elapsed.TotalSeconds, Cases = cases,
                CommandWork = commands.Capture().Subtract(commandBefore), DatabaseWorkBefore = before,
                DatabaseWorkAfter = await CaptureDatabaseWorkAsync(scopes, engine) };
            results.Add(record);
            await WriteAsync(output, $"update-recheck-{run}.json", record);
            Console.WriteLine($"Dimension update recheck {engine} repetition {run}: {cases.Sum(item => item.Failures)} failures.");
            if (!errors.IsEmpty) break;
        }

        await WriteAsync(output, "update-recheck-results.json", new { Engine = engine, Smoke = smoke, Results = results,
            OperationChecksPassed = errors.IsEmpty, QualificationComplete = false, Errors = errors.ToArray(),
            Note = "Original mixed read/count and dimension writer jobs; targeted repeat, not full qualification. Retain earlier observations." });
        return errors.IsEmpty ? 0 : 1;
    }
}
