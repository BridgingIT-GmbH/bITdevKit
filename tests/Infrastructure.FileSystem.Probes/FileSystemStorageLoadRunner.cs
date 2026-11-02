namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.FileSystem;

internal sealed class PatternReadStream(long length) : Stream
{
    private long position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position { get => this.position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return this.Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        var take = (int)Math.Min(buffer.Length, length - this.position);
        buffer[..take].Fill(0x5a);
        this.position += take;

        return take;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(this.Read(buffer.Span));
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}

internal sealed class LoadDocument
{
    public string Value { get; set; }
}

internal sealed class FileSystemLoadSamples(string name)
{
    internal readonly ConcurrentQueue<double> Latencies = new();
    internal readonly ConcurrentDictionary<string, long> Errors = new();
    internal long Bytes;
    internal long Attempts;
    internal long Conflicts;

    internal object Summarize()
    {
        var samples = this.Latencies.Order().ToArray();
        double? Percentile(double percentile)
        {
            return samples.Length == 0 ? null : samples[(int)Math.Ceiling(percentile * samples.Length) - 1];
        }

        return new
        {
            name,
            attempts = this.Attempts,
            successes = samples.Length,
            expectedConflicts = this.Conflicts,
            verifiedBytes = this.Bytes,
            errors = this.Errors,
            byteScope = "Verified logical/source bytes for this case, not network/disk transfer bytes. Typed cases count model ASCII bytes before JSON/compression.",
            latencyUnit = "microseconds",
            p50 = Percentile(.50),
            p95 = samples.Length >= 100 ? Percentile(.95) : null,
            p99 = samples.Length >= 10000 ? Percentile(.99) : null,
            sampleCount = samples.Length,
            p99OmittedReason = samples.Length < 10000 ? "Fewer than 10000 successful samples in this repetition." : null,
            rawLatenciesMicroseconds = samples
        };
    }
}

internal static class FileSystemStorageLoadRunner
{
    internal static async Task<int> RunAsync(string evidence, string runId, IReadOnlyDictionary<string, string> arguments)
    {
        var seconds = Number(arguments, "--seconds", 120, 1, 3600);
        var warmup = Number(arguments, "--warmup", 30, 0, 300);
        var repetitions = Number(arguments, "--repetitions", 5, 1, 20);
        var concurrency = Number(arguments, "--concurrency", 4, 1, 64);
        var payloadBytes = Number(arguments, "--payload-bytes", 65536, 1, 134217728);
        var documentBytes = Number(arguments, "--document-bytes", Math.Min(payloadBytes, 65536), 1, 1040000);
        var records = Number(arguments, "--records", concurrency, concurrency, 1_000_000);
        var retentionSeconds = Number(arguments, "--retention-convergence-seconds", 60, 1, 3600);
        var durability = arguments.GetValueOrDefault("--durability") ?? "Durable";

        if (!Enum.TryParse<FileSystemStorageDurability>(durability, out var mode) || !Enum.IsDefined(mode))
        {
            throw new InvalidDataException("Invalid durability mode.");
        }

        var joined = arguments.ContainsKey("--workspace");
        var scratch = arguments.GetValueOrDefault("--scratch-root") ?? Path.GetTempPath();
        var workspaceProfile = arguments.GetValueOrDefault("--workspace-profile") ?? "load";
        var workspace = ProbeWorkspace.Open(scratch, runId, workspaceProfile, !joined);

        if (joined && workspace != arguments["--workspace"])
        {
            throw new InvalidDataException("Worker workspace identity differs.");
        }

        var actor = arguments.GetValueOrDefault("--actor") ?? "single";

        if (!QualificationConfiguration.SafeId(actor))
        {
            throw new InvalidDataException("Invalid actor identity.");
        }

        var prefix = actor + "-record-";

        try
        {
            var access = Enum.Parse<FileSystemStorageAccessKind>(arguments.GetValueOrDefault("--access") ?? "Local");
            var options = new FileSystemStorageOptions
            {
                RootPath = Path.Combine(workspace, "store"),
                Durability = mode,
                AccessKind = access
            };
            options.MaxQueryMetadataEntries = Number(arguments, "--max-query-entries", 100000, 1, 10_000_000);
            var estimatedFixtureBytes = checked((long)records * (2L * payloadBytes + documentBytes + 98304) + 536870912L);

            if (access == FileSystemStorageAccessKind.Local && new DriveInfo(Path.GetPathRoot(workspace)).AvailableFreeSpace < estimatedFixtureBytes)
            {
                throw new InvalidDataException("Insufficient free space for the declared fixture and metadata headroom.");
            }

            var blobs = new FileSystemBlobStoreProvider(options, new() { AllowFullScans = true });
            IBlobStoreClient blobClient = new BlobStoreClient("load", blobs, new() { AllowFullScans = true });
            var documents = new FileSystemDocumentStoreProvider(options);
            var typed = new DocumentStoreClient<LoadDocument>(documents, transforms: [new CompressionDocumentPayloadTransform()]);
            var documentType = DocumentTypeIdentity.For<LoadDocument>();
            var docBytes = new byte[documentBytes];
            docBytes.AsSpan().Fill(0x5a);
            var docHash = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(docBytes));
            using var hashSource = new PatternReadStream(payloadBytes);
            var expectedHash = await SHA256.HashDataAsync(hashSource);
            var raw = new FileSystemStore(options);

            for (var i = 0; i < records; i++)
            {
                using var seed = new PatternReadStream(payloadBytes);
                Require(await blobs.UploadAsync(new() { Key = BlobKey(i), Content = seed }));
                Require(await documents.UpsertAsync(documentType, new() { Key = Key(i), Content = docBytes, ContentHash = docHash, StoredContentHash = docHash }));
                await RawWriteAsync(i);
            }

            var model = new LoadDocument
            {
                Value = new string ('Z', documentBytes)
            };

            for (var worker = 0; worker < concurrency; worker++)
            {
                Require(await typed.UpsertAsync(new("typed", prefix + worker), model));
            }

            Require(await blobs.UploadAsync(new() { Key = new("hot", "shared"), Content = new MemoryStream([1]) }));
            var cases = new (string Name, Func<int, Task<long>> Run)[]
            {
                ("blob-read", async worker =>
                {
                    var download = Require(await blobs.DownloadAsync(BlobKey(worker)));
                    await using var stream = download.Content;
                    var hash = await SHA256.HashDataAsync(stream);

                    if (!hash.AsSpan().SequenceEqual(expectedHash))
                    {
                        throw new InvalidDataException("Blob bytes differ.");
                    }

                    return payloadBytes;
                }),
                ("blob-write", async worker =>
                {
                    using var source = new PatternReadStream(payloadBytes);
                    var info = Require(await blobs.UploadAsync(new() { Key = BlobKey(worker), Content = source }));

                    if (info.Length != payloadBytes)
                    {
                        throw new InvalidDataException("Write length differs.");
                    }

                    return payloadBytes;
                }),
                ("blob-metadata", async worker =>
                {
                    if (Require(await blobs.GetPropertiesAsync(BlobKey(worker))).Length != payloadBytes)
                    {
                        throw new InvalidDataException("Metadata differs.");
                    }

                    return 0;
                }),
                ("blob-update", async worker =>
                {
                    Require(await blobs.UpdatePropertiesAsync(new() { Key = BlobKey(worker), Properties = new() { ["worker"] = worker } }));

                    return 0;
                }),
                ("blob-keys", async worker =>
                {
                    var page = Require(await blobs.ListPageAsync(new() { Container = "load", Prefix = prefix, Take = 100 }));

                    if (page.Items.Count != Math.Min(records, 100) || (page.ContinuationToken is not null) != (records > 100))
                    {
                        throw new InvalidDataException("Key count or continuation differs.");
                    }

                    return 0;
                }),
                ("document-read", async worker =>
                {
                    var document = Require(await documents.GetAsync(documentType, Key(worker), DateTimeOffset.UtcNow));

                    if (!document.Content.AsSpan().SequenceEqual(docBytes))
                    {
                        throw new InvalidDataException("Document bytes differ.");
                    }

                    return docBytes.Length;
                }),
                ("document-write", async worker =>
                {
                    Require(await documents.UpsertAsync(documentType, new() { Key = Key(worker), Content = docBytes, ContentHash = docHash, StoredContentHash = docHash }));

                    return docBytes.Length;
                }),
                ("document-metadata", async worker =>
                {
                    Require(await documents.UpdatePropertiesAsync(documentType, new(Key(worker)) { Properties = new() { ["worker"] = worker } }, null, true));

                    return 0;
                }),
                ("document-keys", async worker =>
                {
                    var page = Require(await documents.ListPageAsync(documentType, new() { DocumentKey = new("load", prefix), Filter = DocumentKeyFilter.RowKeyPrefixMatch, Take = 100 }, DateTimeOffset.UtcNow));

                    if (page.Items.Count != Math.Min(records, 100) || (page.ContinuationToken is not null) != (records > 100))
                    {
                        throw new InvalidDataException("Document key count or continuation differs.");
                    }

                    return 0;
                }),
                ("document-count", async worker =>
                {
                    if (Require(await documents.CountAsync(documentType, new() { DocumentKey = new("load", prefix), Filter = DocumentKeyFilter.RowKeyPrefixMatch }, DateTimeOffset.UtcNow)) != records)
                    {
                        throw new InvalidDataException("Count differs.");
                    }

                    return 0;
                }),
                ("typed-compressed-roundtrip", async worker =>
                {
                    var key = new DocumentKey("typed", prefix + worker);
                    Require(await typed.UpsertAsync(key, model));

                    if (Require(await typed.GetAsync(key)).Value.Value != model.Value)
                    {
                        throw new InvalidDataException("Typed document differs.");
                    }

                    return model.Value.Length;
                }),
                ("blob-copy-move-helpers", async worker =>
                {
                    var copied = new BlobKey("helpers", prefix + worker + "-copy");
                    var moved = new BlobKey("helpers", prefix + worker + "-moved");
                    Require(await blobClient.CopyToAsync(BlobKey(worker), blobClient, copied));
                    Require(await blobClient.MoveToAsync(copied, blobClient, moved));

                    if (Require(await blobs.ExistsAsync(copied)))
                    {
                        throw new InvalidDataException("Blob helper retained source.");
                    }

                    return 2L * payloadBytes;
                }),
                ("typed-copy-move-many-helpers", async worker =>
                {
                    var original = new DocumentKey("typed", prefix + worker);
                    var copied = new DocumentKey("typed-helpers", prefix + worker + "-copy");
                    var moved = new DocumentKey("typed-helpers", prefix + worker + "-moved");
                    Require(await typed.CopyAsync(original, typed, copied));
                    Require(await typed.MoveAsync(copied, typed, moved));
                    var many = Require(await typed.GetManyAsync([original, moved, copied]));

                    if (!many[0].IsSuccess || !many[1].IsSuccess || !many[2].HasError<DocumentStoreNotFoundError>())
                    {
                        throw new InvalidDataException("Typed helper outcomes differ.");
                    }

                    return 2L * model.Value.Length;
                }),
                ("hot-key-etag", async worker =>
                {
                    var key = new BlobKey("hot", "shared");
                    var before = Require(await blobs.GetPropertiesAsync(key));
                    var update = await blobs.UpdatePropertiesAsync(new() { Key = key, IfMatchETag = before.ETag });

                    if (update.HasError<BlobStoreConflictError>())
                    {
                        throw new FileSystemExpectedConflictException();
                    }

                    Require(update);

                    return 0;
                }),
                ("retention-create-sweep", async worker =>
                {
                    var key = new BlobKey("expired", prefix + worker);
                    using var content = new PatternReadStream(4096);
                    Require(await blobs.UploadAsync(new() { Key = key, Content = content, ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) }));
                    var convergence = Stopwatch.StartNew();

                    while (convergence.Elapsed < TimeSpan.FromSeconds(retentionSeconds))
                    {
                        Require(await blobs.SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow, BatchSize = 128, MaxBatches = 1 }));

                        if (!Require(await blobs.ExistsAsync(key)))
                        {
                            return 4096;
                        }

                        await Task.Delay(10);
                    }

                    throw new InvalidDataException("Retention did not converge within the declared workload wait budget.");
                }),
                ("raw-equivalent-write", RawWriteAsync),
                ("raw-equivalent-read", async worker =>
                {
                    var path = raw.Resolve(RawIdentity(worker), false);
                    using var authority = await raw.AcquireAsync(path, false, default);
                    var record = raw.Read(path, RawIdentity(worker));
                    await using var stream = raw.OpenPayload(path, record);
                    authority.Dispose();
                    var hash = await SHA256.HashDataAsync(stream);

                    if (!hash.AsSpan().SequenceEqual(expectedHash))
                    {
                        throw new InvalidDataException("Control bytes differ.");
                    }

                    return payloadBytes;
                })
            };
            var outputs = new List<object>();
            var failed = false;

            for (var repetition = 0; repetition < repetitions; repetition++)
            {
                Console.WriteLine($"load actor={actor}; repetition {repetition + 1}/{repetitions}; warmup={warmup}s; measured={seconds}s; OS={(OperatingSystem.IsWindows() ? "Windows" : "Linux")}; {access}/{mode}; concurrency={concurrency}");
                var warm = await ExerciseAsync(TimeSpan.FromSeconds(warmup), repetition, false);

                if (warm.Any(s => !s.Errors.IsEmpty))
                {
                    failed = true;
                }

                var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                var filesystemBefore = FileSystemMetrics.Snapshot();
                var allocatedBefore = GC.GetTotalAllocatedBytes(true);
                var started = Stopwatch.StartNew();
                var samples = await ExerciseAsync(TimeSpan.FromSeconds(seconds), repetition, true);
                started.Stop();
                var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
                var filesystemDelta = FileSystemMetrics.Snapshot().ToDictionary(p => p.Key, p => p.Value - filesystemBefore[p.Key]);

                if (samples.Any(s => !s.Errors.IsEmpty || s.Latencies.IsEmpty))
                {
                    failed = true;
                }

                var successes = samples.Sum(s => (long)s.Latencies.Count);
                using var process = Process.GetCurrentProcess();
                var output = new
                {
                    repetition,
                    elapsedSeconds = started.Elapsed.TotalSeconds,
                    successfulOperations = successes,
                    successfulOperationsPerSecond = successes / started.Elapsed.TotalSeconds,
                    allocatedBytes = allocated,
                    allocatedBytesPerSuccessfulMixedOperation = successes == 0 ? (double? )null : (double)allocated / successes,
                    allocationScope = "Entire process, all async threads, including runner sampling, clients, transformations and fixture stream generation. Not per-case/provider-only allocation.",
                    gcCollections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray(),
                    workingSetBytes = process.WorkingSet64,
                    peakWorkingSetBytes = process.PeakWorkingSet64,
                    filesystemCounters = filesystemDelta,
                    stopwatchFrequency = Stopwatch.Frequency,
                    filesystemCounterScope = "Application instrumentation across providers/control and async workers in this process. Not OS syscall or network round-trip counts. Retained ownership is an event count; hold ticks end at local operation close.",
                    cases = samples.Select(s => s.Summarize()).ToArray(),
                    warmupErrors = warm.Select(s => new { s.Errors }).ToArray()
                };
                var artifact = $"load-repetition-{repetition + 1}.json";
                outputs.Add(new { repetition, artifact, elapsedSeconds = started.Elapsed.TotalSeconds, successfulOperations = successes,
                    successfulOperationsPerSecond = successes / started.Elapsed.TotalSeconds, allocatedBytes = allocated,
                    allocatedBytesPerSuccessfulMixedOperation = successes == 0 ? (double?)null : (double)allocated / successes });
                ProbeResult.Write(Path.Combine(evidence, artifact), output);
            }

            var verification = await VerifyCommittedRecordsAsync(raw);
            ProbeResult.Write(Path.Combine(evidence, "post-load-verification.json"), verification);
            ProbeResult.Write(Path.Combine(evidence, "load.json"), new { schemaVersion = 1, taskId = "TASK-022-local", status = failed ? "failed" : "passed", runId,
                profileId = (OperatingSystem.IsWindows() ? "Windows" : "Linux") + "-" + access + "-" + mode, host = Environment.MachineName, actor, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                caseCount = cases.Length * repetitions, concurrency, records, payloadBytes, documentBytes, retentionConvergenceSeconds = retentionSeconds, estimatedFixtureBytes, warmupSeconds = warmup, measuredSeconds = seconds, repetitions, cacheState = "uncontrolled",
                measurementPolicy = "Round-robin verified mix; reversed case order on alternating repetitions. Per-process concurrency. No remote qualification, budget pass or optimization claim.",
                failureDetails = failed ? new[] { "One or more workload cases failed or had no successful samples." } : Array.Empty<string>(),
                postLoadVerification = "post-load-verification.json",
                artifactPaths = Enumerable.Range(1, repetitions).Select(i => $"load-repetition-{i}.json").Append("post-load-verification.json").ToArray(), results = outputs });

            return failed ? 1 : 0;
            async Task<FileSystemLoadSamples[]> ExerciseAsync(TimeSpan duration, int repetition, bool measure)
            {
                var samples = cases.Select(c => new FileSystemLoadSamples(c.Name)).ToArray();
                var watch = Stopwatch.StartNew();
                await Task.WhenAll(Enumerable.Range(0, concurrency).Select(async worker =>
                {
                    long iteration = worker;

                    while (watch.Elapsed < duration)
                    {
                        var index = (int)(iteration++ % cases.Length);

                        if (repetition % 2 != 0)
                        {
                            index = cases.Length - 1 - index;
                        }

                        var sample = samples[index];
                        Interlocked.Increment(ref sample.Attempts);
                        var before = Stopwatch.GetTimestamp();

                        try
                        {
                            var bytes = await cases[index].Run(worker);

                            if (measure)
                            {
                                if (sample.Latencies.Count >= 1_000_000)
                                {
                                    throw new InvalidDataException("Latency sample capacity exceeded.");
                                }

                                sample.Latencies.Enqueue(Stopwatch.GetElapsedTime(before).TotalMicroseconds);
                                Interlocked.Add(ref sample.Bytes, bytes);
                            }
                        }
                        catch (FileSystemExpectedConflictException)
                        {
                            Interlocked.Increment(ref sample.Conflicts);
                        }
                        catch (Exception exception)
                        {
                            var diagnostic = exception.GetType().Name + (exception is InvalidDataException ? ": " + exception.Message : "");
                            sample.Errors.AddOrUpdate(diagnostic, 1, (_, count) => count + 1);
                        }
                    }
                }));

                return samples;
            }

            async Task<long> RawWriteAsync(int worker)
            {
                var identity = RawIdentity(worker);
                var path = raw.Resolve(identity);
                using var source = new PatternReadStream(payloadBytes);
                using var staged = await raw.StageAsync(path, source, null, default);
                using var authority = await raw.AcquireAsync(path, false, default);
                var previous = raw.Read(path, identity);
                var info = new BlobInfo
                {
                    Key = new("control", prefix + worker),
                    Length = staged.Length,
                    ContentHash = staged.Hash,
                    ETag = Guid.NewGuid().ToString("N"),
                    CreatedAt = previous?.Blob.CreatedAt ?? DateTimeOffset.UtcNow,
                    LastModifiedAt = DateTimeOffset.UtcNow
                };
                raw.Publish(path, new() { Identity = identity, Payload = staged.File, Length = staged.Length, Blob = info }, authority, default, staged);

                return payloadBytes;
            }

            BlobKey BlobKey(int worker)
            {
                return new("load", prefix + worker);
            }

            DocumentKey Key(int worker)
            {
                return new("load", prefix + worker);
            }

            string[] RawIdentity(int worker)
            {
                return["blobs", "control", prefix + worker];
            }
        }
        finally
        {
            if (!joined)
            {
                ProbeWorkspace.Open(scratch, runId, workspaceProfile, false);

                foreach (var file in Directory.EnumerateFileSystemEntries(workspace, "*", SearchOption.AllDirectories))
                {
                    ProbeWorkspace.RejectLinks(file);
                }

                Directory.Delete(workspace, true);
            }
        }
    }

    internal static T Require<T>(Result<T> result)
    {
        return result.IsSuccess ? result.Value : throw new InvalidDataException("Provider returned " + string.Join(",", result.Errors.Select(e => e is FileSystemStorageUnavailableError ? e.Message : e.GetType().Name))
        + (result.HasError<BlobStoreIndeterminateOperationError>() || result.HasError<DocumentStoreIndeterminateOperationError>() ? "; process last native publication code=" + Interlocked.Read(ref FileSystemMetrics.IndeterminateNativeCode) : ""));
    }

    private static async Task<object> VerifyCommittedRecordsAsync(FileSystemStore store)
    {
        long live = 0, absent = 0, bytes = 0, hashes = 0, physicalBytes = 0, generations = 0, staging = 0;

        foreach (var path in store.Paths("blobs").Concat(store.Paths("documents")))
        {
            using (var authority = await store.AcquireAsync(path, false, default))
            {
                var record = store.Read(path);

                if (record is null)
                {
                    absent++;
                }
                else
                {
                    await using var content = store.OpenPayload(path, record);
                    authority.Dispose();
                    var actual = "sha256:" + Convert.ToHexStringLower(await SHA256.HashDataAsync(content));
                    var expected = record.Blob?.ContentHash ?? record.Document?.StoredContentHash;

                    if (expected is not null && expected != actual)
                    {
                        throw new InvalidDataException("Post-load committed content hash differs.");
                    }

                    if (expected is not null)
                    {
                        hashes++;
                    }

                    bytes += content.Length;
                    live++;
                }
            }

            foreach (var file in Directory.EnumerateFiles(path))
            {
                FileSystemStore.RejectLinks(file);

                if (Path.GetFileName(file).StartsWith("payload-", StringComparison.Ordinal))
                {
                    try
                    {
                        physicalBytes += new FileInfo(file).Length;
                        generations++;
                    }
                    catch (FileNotFoundException)
                    {
                    } // A joined worker may reclaim an unreferenced physical name.
                }
                else if (file.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    staging++;
                }
            }
        }

        return new
        {
            status = "passed",
            liveRecords = live,
            absentOrTombstonedRecords = absent,
            verifiedLogicalBytes = bytes,
            verifiedHashes = hashes,
            observedPhysicalPayloadBytes = physicalBytes,
            observedPayloadGenerations = generations,
            observedStagingFiles = staging,
            scope = "Outside timed region. Per-record authority and immutable handles verify committed bytes. Physical counts are observations, not a cross-host snapshot; joined workers may remain active."
        };
    }

    private static int Number(IReadOnlyDictionary<string, string> args, string key, int fallback, int minimum, int maximum)
    {
        return !args.TryGetValue(key, out var text) ? fallback : int.TryParse(text, out var value) && value >= minimum && value <= maximum ? value : throw new InvalidDataException("Invalid load argument " + key);
    }
}

internal sealed class FileSystemExpectedConflictException : Exception;