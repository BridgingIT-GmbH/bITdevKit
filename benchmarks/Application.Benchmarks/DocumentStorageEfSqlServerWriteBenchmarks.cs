// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>The kind of mutation a write benchmark measures.</summary>
public enum DocumentBenchmarkOperation
{
    /// <summary>New documents are inserted.</summary>
    Insert,

    /// <summary>Existing documents are replaced.</summary>
    Overwrite,

    /// <summary>Existing documents are deleted.</summary>
    Delete
}

/// <summary>
/// Measures writing a batch of documents (1 KB each) through the DevKit client. One call per document is the baseline,
/// the batch API is what the provider can commit in a single round trip.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project benchmarks/Application.Benchmarks/Application.Benchmarks.csproj -- --filter *DocumentStorageEfSqlServerWriteBenchmarks*
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 12)]
public class DocumentStorageEfSqlServerWriteBenchmarks
{
    private const int PayloadBytes = 1024;

    private DocumentBenchmarkDatabase database;
    private IDocumentStoreClient<BenchmarkWriteDocument> client;
    private DocumentKey[] keys;
    private DocumentWrite<BenchmarkWriteDocument>[] writes;
    private DocumentDelete[] deletes;
    private int iteration;

    [Params(10, 100, 1000)]
    public int Count { get; set; }

    [Params(DocumentBenchmarkOperation.Insert, DocumentBenchmarkOperation.Overwrite, DocumentBenchmarkOperation.Delete)]
    public DocumentBenchmarkOperation Operation { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var server = DocumentStorageEfSqlServerBenchmarkEnvironment.GetServerConnectionStringAsync().GetAwaiter().GetResult();
        this.database = DocumentBenchmarkDatabase.Create<BenchmarkDocumentDbContext>(server, DocumentStorageEfSqlServerBenchmarkEnvironment.DatabaseName("docbench_write"));
        this.database.PrepareAsync(0).GetAwaiter().GetResult();
        this.database.DeleteDocumentsOfTypeAsync<BenchmarkWriteDocument>().GetAwaiter().GetResult();
        this.client = this.database.CreateClient<BenchmarkWriteDocument>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.database.DeleteDocumentsOfTypeAsync<BenchmarkWriteDocument>().GetAwaiter().GetResult();
        this.database.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var run = Interlocked.Increment(ref this.iteration);
        this.keys = Enumerable.Range(0, this.Count)
            .Select(index => new DocumentKey($"w{index % 20:D2}", $"i{run}-{index}"))
            .ToArray();

        if (this.Operation != DocumentBenchmarkOperation.Insert)
        {
            var seeded = this.keys
                .Select((key, index) => new DocumentWrite<BenchmarkWriteDocument>(key, Create(index)))
                .ToArray();
            this.EnsureSucceeded(this.client.UpsertManyAsync(seeded).GetAwaiter().GetResult());
        }

        this.writes = this.keys
            .Select((key, index) => new DocumentWrite<BenchmarkWriteDocument>(key, Create(index + 1_000_000)))
            .ToArray();
        this.deletes = this.keys.Select(key => new DocumentDelete(key)).ToArray();
    }

    [Benchmark(Baseline = true)]
    public async Task Loop()
    {
        if (this.Operation == DocumentBenchmarkOperation.Delete)
        {
            foreach (var key in this.keys)
            {
                this.EnsureSucceeded(await this.client.DeleteAsync(key).ConfigureAwait(false));
            }

            return;
        }

        foreach (var write in this.writes)
        {
            this.EnsureSucceeded(await this.client.UpsertAsync(write.Key, write.Value).ConfigureAwait(false));
        }
    }

    [Benchmark]
    public async Task Many()
    {
        if (this.Operation == DocumentBenchmarkOperation.Delete)
        {
            this.EnsureSucceeded(await this.client.DeleteManyAsync(this.deletes).ConfigureAwait(false));

            return;
        }

        this.EnsureSucceeded(await this.client.UpsertManyAsync(this.writes).ConfigureAwait(false));
    }

    private static BenchmarkWriteDocument Create(int seed) =>
        new() { Value = DocumentBenchmarkDatabase.CreatePayload(PayloadBytes, seed) };

    private void EnsureSucceeded(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }
    }

    private void EnsureSucceeded<T>(Result<DocumentBatchResult<T>> result)
    {
        if (result.IsFailure || result.Value.FailedKeys.Count != 0)
        {
            throw new InvalidOperationException("The batch did not complete.");
        }
    }

    private void EnsureSucceeded<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }
    }
}
