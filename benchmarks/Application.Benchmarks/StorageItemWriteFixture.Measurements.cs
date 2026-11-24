// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using Application.Storage;
using Common;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class StorageItemWriteFixture
{
    private readonly Dictionary<int, int> blobRevisions = [];
    private readonly Dictionary<int, int> documentRevisions = [];
    private readonly Dictionary<int, bool> blobActive = [];
    private readonly Dictionary<int, bool> documentActive = [];
    private readonly Dictionary<string, string> blobEtags = [];
    private readonly Dictionary<string, string> documentEtags = [];
    private long nextAddition;
    private BlobStoreClient bulkClient;

    /// <summary>Measures acknowledged add/content-update calls and verifies persistent state outside timing.</summary>
    /// <param name="engine">The recorded backend name.</param>
    /// <param name="feature">The blob or document feature.</param>
    /// <param name="owners">Existing collection size.</param>
    /// <param name="dimensions">Zero or three Dimensions per item.</param>
    /// <param name="smoke">Whether to run the short correctness workload.</param>
    /// <param name="output">The evidence directory.</param>
    /// <param name="bulkOnly">Whether to run only public UploadManyAsync blob groups.</param>
    /// <returns>The operation summaries and per-repetition samples.</returns>
    /// <example><code>var results = await fixture.MeasureAsync("sqlserver", "blob", 10000, 3, false, output);</code></example>
    public async Task<object[]> MeasureAsync(string engine, string feature, int owners, int dimensions, bool smoke, string output, bool bulkOnly = false)
    {
        var random = new Random(17043 + owners + dimensions + (feature == "blob" ? 1 : 2));
        var groups = new List<(string Operation, int Count, int Concurrency)>();
        if (!bulkOnly)
        {
            groups.AddRange([("add", 1, 1), ("update", 1, 1), ("add", 100, 1), ("update", 100, 1)]);
        }

        if (feature == "blob")
        {
            groups.AddRange([("add", 100, 4), ("update", 100, 4)]);
        }

        var samples = groups.ToDictionary(group => group, _ => new List<double>());
        var runs = groups.ToDictionary(group => group, _ => new List<object>());
        foreach (var group in groups)
        {
            for (var warmup = 0; warmup < (smoke ? 1 : 3); warmup++)
            {
                await ExecuteSampleAsync(group.Operation, group.Count, group.Concurrency);
            }
        }

        for (var run = 0; run < (smoke ? 1 : 5); run++)
        {
            for (var offset = 0; offset < groups.Count; offset++)
            {
                var group = groups[(run + offset) % groups.Count];
                var times = new List<double>();
                var count = group.Count == 1 ? (smoke ? 3 : 100) : (smoke ? 2 : 10);
                for (var sample = 0; sample < count; sample++)
                {
                    times.Add(await ExecuteSampleAsync(group.Operation, group.Count, group.Concurrency));
                }

                samples[group].AddRange(times);
                var record = new
                {
                    Run = run + 1,
                    Operation = group.Operation,
                    Items = group.Count,
                    Mode = Mode(group),
                    group.Concurrency,
                    Milliseconds = times,
                    P50 = Percentile(times, 0.5),
                    P95 = Percentile(times, 0.95),
                    ItemsPerSecond = count * group.Count * 1000d / times.Sum()
                };
                runs[group].Add(record);
                await SaveAsync(output, $"{feature}-{owners}-{dimensions}-{group.Operation}-{group.Count}-{Mode(group)}-run-{run + 1}.json", record);
            }

            Console.WriteLine($"{engine}/{feature}/{owners:N0}/{dimensions} Dimensions: write round {run + 1} verified.");
        }

        var results = groups.Select(group => (object)new
        {
            Engine = engine,
            Feature = feature,
            Owners = owners,
            Dimensions = dimensions,
            PayloadBytes = 1024,
            group.Operation,
            Items = group.Count,
            Mode = Mode(group),
            group.Concurrency,
            Samples = samples[group].Count,
            P50Milliseconds = Percentile(samples[group], 0.5),
            P95Milliseconds = Percentile(samples[group], 0.95),
            MaximumMilliseconds = samples[group].Max(),
            ItemsPerSecond = samples[group].Count * group.Count * 1000d / samples[group].Sum(),
            OperationChecksPassed = true,
            Runs = runs[group]
        }).ToArray();
        await SaveAsync(output, $"{feature}-{owners}-{dimensions}-results.json", results);
        return results;

        string Mode((string Operation, int Count, int Concurrency) group) => group.Count == 1 ? "single"
            : feature == "document" ? "native-bulk" : group.Concurrency == 4 ? "upload-many" : "sequential";

        async Task<double> ExecuteSampleAsync(string operation, int count, int concurrency)
        {
            var prepared = new List<PreparedItem>();
            var indices = new HashSet<int>();
            for (var item = 0; item < count; item++)
            {
                var owner = -1;
                string key;
                var revisions = feature == "blob" ? this.blobRevisions : this.documentRevisions;
                var active = feature == "blob" ? this.blobActive : this.documentActive;
                if (operation == "add")
                {
                    key = $"added-{++this.nextAddition:D12}";
                }
                else
                {
                    do
                    {
                        owner = random.Next(owners);
                    } while (!indices.Add(owner));
                    key = Key(owner);
                }

                var revision = owner < 0 ? 1 : revisions.GetValueOrDefault(owner) + 1;
                var enabled = owner < 0 || !active.GetValueOrDefault(owner);
                if (owner >= 0)
                {
                    revisions[owner] = revision;
                    active[owner] = enabled;
                }

                var body = Payload(revision);
                prepared.Add(new(key, body, Hash(body), dimensions == 0 ? null : Require(BlobDimensions.Create(Values(revision, enabled))),
                    dimensions == 0 ? null : Require(DocumentDimensions.Create(Values(revision, enabled)))));
            }

            var writes = prepared.Select(item => new StoredDocumentWrite
            {
                Key = new("records", item.Key),
                Content = item.Content,
                ContentHash = item.Hash,
                StoredContentHash = item.Hash,
                Dimensions = item.DocumentDimensions,
                Options = new() { CreateOnly = operation == "add" }
            }).ToArray();
            var uploads = prepared.Select(item => new StoredBlobUpload
            {
                Key = new(ScopeName, item.Key),
                Content = new MemoryStream(item.Content, writable: false),
                ExpectedContentHash = item.Hash,
                Dimensions = item.BlobDimensions,
                OverwriteMode = operation == "add" ? BlobOverwriteMode.FailIfExists : BlobOverwriteMode.Overwrite
            }).ToArray();
            var publicUploads = uploads.Select(upload => new BlobUpload
            {
                Key = upload.Key,
                Content = upload.Content,
                ExpectedContentHash = upload.ExpectedContentHash,
                Dimensions = upload.Dimensions,
                OverwriteMode = upload.OverwriteMode
            }).ToArray();
            this.bulkClient ??= new BlobStoreClient("ef", this.blobs);
            var returnedEtags = new List<string>();
            var timer = Stopwatch.StartNew();
            try
            {
                if (feature == "blob" && concurrency > 1)
                {
                    var result = Require(await this.bulkClient.UploadManyAsync(publicUploads, new() { MaxConcurrency = concurrency }));
                    if (!result.AllSucceeded || result.Items.Count != count || !result.Items.Select(item => item.Key.Name).SequenceEqual(prepared.Select(item => item.Key)))
                    {
                        throw new InvalidOperationException("Public blob upload batch did not acknowledge every item in input order.");
                    }

                    returnedEtags.AddRange(result.Items.Select(item => item.Result.Value.ETag));
                }
                else if (feature == "blob")
                {
                    foreach (var upload in uploads)
                    {
                        returnedEtags.Add(Require(await this.blobs.UploadAsync(upload)).ETag);
                    }
                }
                else if (count == 1)
                {
                    returnedEtags.Add(Require(await this.documents.UpsertAsync(DocumentType, writes[0])).ETag);
                }
                else
                {
                    var result = Require(await this.documents.UpsertManyAsync(DocumentType, writes));
                    if (result.Items.Count != count || result.FailedKey is not null || result.FailedKeys.Count != 0)
                    {
                        throw new InvalidOperationException("Document write group did not commit every item.");
                    }

                    returnedEtags.AddRange(result.Items.Select(item => item.ETag));
                }
            }
            finally
            {
                timer.Stop();
                foreach (var upload in uploads)
                {
                    upload.Content.Dispose();
                }
            }

            var etags = feature == "blob" ? this.blobEtags : this.documentEtags;
            for (var index = 0; index < prepared.Count; index++)
            {
                var key = prepared[index].Key;
                var etag = returnedEtags[index];
                if (string.IsNullOrWhiteSpace(etag) || (etags.TryGetValue(key, out var previous) && previous == etag))
                {
                    throw new InvalidOperationException("Write failed to invalidate the ETag.");
                }

                etags[key] = etag;
            }

            await this.VerifyPersistedAsync(feature, prepared, operation == "add");
            if (operation == "add")
            {
                foreach (var item in prepared)
                {
                    etags.Remove(item.Key);
                }
            }

            return timer.Elapsed.TotalMilliseconds;
        }
    }

    private async Task VerifyPersistedAsync(string feature, IReadOnlyList<PreparedItem> items, bool cleanup)
    {
        var expected = items.ToDictionary(item => item.Key);
        var keys = expected.Keys.ToArray();
        var hashes = keys.Select(HashHelper.ComputeSha256).ToArray();
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        if (feature == "blob")
        {
            var scopeHash = HashHelper.ComputeSha256(ScopeName);
            var query = context.StorageBlobs.Where(row => row.ContainerHash == scopeHash && hashes.Contains(row.NameHash) && row.Container == ScopeName && keys.Contains(row.Name));
            var rows = await query.AsNoTracking().Include(row => row.Chunks).Include(row => row.DimensionValues).ThenInclude(row => row.Name).AsSplitQuery().ToArrayAsync();
            if (rows.Length != items.Count)
            {
                throw new InvalidOperationException("Persisted blob membership differs.");
            }

            foreach (var row in rows)
            {
                var item = expected[row.Name];
                var dimensions = Require(BlobDimensionValues.Deserialize(row.DimensionsJson));
                if (!row.Chunks.OrderBy(chunk => chunk.Index).SelectMany(chunk => chunk.Content).SequenceEqual(item.Content) || row.ContentHash != item.Hash
                    || dimensions.Count != (item.BlobDimensions?.Count ?? 0) || row.DimensionValues.Count != dimensions.Count
                    || (item.BlobDimensions is not null && item.BlobDimensions.Any(pair => !dimensions.TryGetValue(pair.Key, out var value) || value != pair.Value)))
                {
                    throw new InvalidOperationException("Persisted blob content/hash/snapshot differs.");
                }

                foreach (var value in row.DimensionValues)
                {
                    var encoded = new StorageBlobDimension();
                    BlobDimensionValueEncoding.Encode(item.BlobDimensions[value.Name.Name], encoded);
                    if (value.ValueType != encoded.ValueType || value.IntegralValue != encoded.IntegralValue || value.DecimalValue != encoded.DecimalValue || value.StringValue != encoded.StringValue)
                    {
                        throw new InvalidOperationException("Persisted blob search value differs.");
                    }
                }
            }

            if (cleanup && await query.ExecuteDeleteAsync() != items.Count)
            {
                throw new InvalidOperationException("Owned blob cleanup did not remove exactly the added rows.");
            }
        }
        else
        {
            var typeHash = HashHelper.ComputeSha256(DocumentType.Value);
            var partitionHash = HashHelper.ComputeSha256("records");
            var query = context.StorageDocuments.Where(row => row.TypeHash == typeHash && row.PartitionKeyHash == partitionHash && hashes.Contains(row.RowKeyHash)
                && row.Type == DocumentType.Value && row.PartitionKey == "records" && keys.Contains(row.RowKey));
            var rows = await query.AsNoTracking().Include(row => row.DimensionValues).ThenInclude(row => row.Name).AsSplitQuery().ToArrayAsync();
            if (rows.Length != items.Count)
            {
                throw new InvalidOperationException("Persisted document membership differs.");
            }

            foreach (var row in rows)
            {
                var item = expected[row.RowKey];
                var dimensions = Require(DocumentDimensionValues.Deserialize(row.DimensionsJson));
                if (!row.Content.SequenceEqual(item.Content) || row.ContentHash != item.Hash || row.StoredContentHash != item.Hash
                    || dimensions.Count != (item.DocumentDimensions?.Count ?? 0) || row.DimensionValues.Count != dimensions.Count
                    || (item.DocumentDimensions is not null && item.DocumentDimensions.Any(pair => !dimensions.TryGetValue(pair.Key, out var value) || value != pair.Value)))
                {
                    throw new InvalidOperationException("Persisted document content/hash/snapshot differs.");
                }

                foreach (var value in row.DimensionValues)
                {
                    var encoded = new StorageDocumentDimension();
                    DocumentDimensionValueEncoding.Encode(item.DocumentDimensions[value.Name.Name], encoded);
                    if (value.ValueType != encoded.ValueType || value.IntegralValue != encoded.IntegralValue || value.DecimalValue != encoded.DecimalValue || value.StringValue != encoded.StringValue)
                    {
                        throw new InvalidOperationException("Persisted document search value differs.");
                    }
                }
            }

            if (cleanup && await query.ExecuteDeleteAsync() != items.Count)
            {
                throw new InvalidOperationException("Owned document cleanup did not remove exactly the added rows.");
            }
        }
    }

    private static double Percentile(IEnumerable<double> samples, double percentile)
    {
        var sorted = samples.Order().ToArray();
        return sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * percentile) - 1)];
    }

    private sealed record PreparedItem(string Key, byte[] Content, string Hash, BlobDimensions BlobDimensions, DocumentDimensions DocumentDimensions);
}
