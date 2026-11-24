// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Application.Storage;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

internal static class StorageDimensionsSnapshotProfile
{
    public static int Run(string[] args)
    {
        var budgeted = StorageDimensionsQualificationBudget.StartBudgetedChild(args);
        if (budgeted.HasValue) return budgeted.Value;
        var outputIndex = Array.IndexOf(args, "--output");
        if (outputIndex < 0 || outputIndex + 1 >= args.Length) throw new ArgumentException("Snapshot profiling requires a new --output file.");
        var output = Path.GetFullPath(args[outputIndex + 1]);
        if (File.Exists(output)) throw new ArgumentException("Snapshot profile output already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var blobs = Enumerable.Range(0, 50).Select(owner => StorageDimensionsQualification.Require(BlobDimensions.Create(DimensionQualificationData.Scalars(owner)))).ToArray();
        var documents = Enumerable.Range(0, 50).Select(owner => StorageDimensionsQualification.Require(DocumentDimensions.Create(DimensionQualificationData.Scalars(owner)))).ToArray();
        var blobStored = blobs.Select(BlobDimensionValues.Serialize).ToArray();
        var blobTyped = blobs.Select(value => JsonSerializer.Serialize(value)).ToArray();
        var documentStored = documents.Select(DocumentDimensionValues.Serialize).ToArray();
        var documentTyped = documents.Select(value => JsonSerializer.Serialize(value)).ToArray();
        var readers = new Dictionary<string, Func<int, int>>
        {
            ["blob/stored-codec"] = index => StorageDimensionsQualification.Require(BlobDimensionValues.Deserialize(blobStored[index])).Count,
            ["blob/typed-json"] = index => JsonSerializer.Deserialize<BlobDimensions>(blobTyped[index]).Count,
            ["document/stored-codec"] = index => StorageDimensionsQualification.Require(DocumentDimensionValues.Deserialize(documentStored[index])).Count,
            ["document/typed-json"] = index => JsonSerializer.Deserialize<DocumentDimensions>(documentTyped[index]).Count
        };
        var measurements = new List<object>();
        for (var run = 0; run < 5; run++)
        {
            foreach (var reader in run % 2 == 0 ? readers : readers.Reverse())
            {
                for (var warmup = 0; warmup < 1000; warmup++) reader.Value(warmup % 50);
                var allocation = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                var verifiedValues = 0;
                const int Operations = 10000;
                for (var iteration = 0; iteration < Operations; iteration++)
                {
                    var count = reader.Value(iteration % 50);
                    if (count != 8) throw new InvalidOperationException("Snapshot profile decoded the wrong value count.");
                    verifiedValues += count;
                }

                timer.Stop();
                measurements.Add(new
                {
                    Run = run + 1, Case = reader.Key, Operations, VerifiedValues = verifiedValues,
                    AllocatedBytesPerOperation = (GC.GetAllocatedBytesForCurrentThread() - allocation) / (double)Operations,
                    MicrosecondsPerOperation = timer.Elapsed.TotalMicroseconds / Operations
                });
            }
        }

        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Kind = "Isolated synchronous allocation profile, not volume qualification or an API latency comparison",
            WarmupPerCase = 1000, DatasetSnapshots = 50,
            StoredUtf8Bytes = blobStored.Sum(Encoding.UTF8.GetByteCount), TypedUtf8Bytes = blobTyped.Sum(Encoding.UTF8.GetByteCount),
            Measurements = measurements
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Snapshot allocation profile saved to {output}");
        return 0;
    }
}
