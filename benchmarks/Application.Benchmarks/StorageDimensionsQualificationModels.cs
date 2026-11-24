// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Application.Storage;
using Common;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Globalization;
using System.Text;

internal sealed class DimensionsQualificationContext(DbContextOptions<DimensionsQualificationContext> options) : DbContext(options), IBlobStoreContext, IDocumentStoreContext
{
    public DbSet<StorageBlob> StorageBlobs
    {
        get; set;
    }

    public DbSet<StorageBlobChunk> StorageBlobChunks
    {
        get; set;
    }

    public DbSet<StorageDocument> StorageDocuments
    {
        get; set;
    }
}

// Benchmark-only legacy schema. Production SQL Dimensions still require no model-builder configuration.
internal sealed class DimensionsParentOnlyContext(DbContextOptions<DimensionsParentOnlyContext> options) : DbContext(options), IBlobStoreContext, IDocumentStoreContext
{
    public DbSet<StorageBlob> StorageBlobs
    {
        get; set;
    }

    public DbSet<StorageBlobChunk> StorageBlobChunks
    {
        get; set;
    }

    public DbSet<StorageDocument> StorageDocuments
    {
        get; set;
    }

    internal static bool IncludeDimensionSnapshot
    {
        get; set;
    }

    internal static bool DiscardDimensionSnapshot
    {
        get; set;
    }

    internal static long MaterializedSnapshotCharacters;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        => builder.Conventions.Add(_ => new LegacyOwnerIndexes());

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<StorageBlob>().Ignore(owner => owner.DimensionValues);
        builder.Entity<StorageDocument>().Ignore(owner => owner.DimensionValues);
        if (!IncludeDimensionSnapshot)
        {
            builder.Entity<StorageBlob>().Ignore(owner => owner.DimensionsJson);
            builder.Entity<StorageDocument>().Ignore(owner => owner.DimensionsJson);
        }

        if (DiscardDimensionSnapshot)
        {
            builder.Entity<StorageBlob>().Property(owner => owner.DimensionsJson).HasConversion(value => value, value => DiscardSnapshot(value));
            builder.Entity<StorageDocument>().Property(owner => owner.DimensionsJson).HasConversion(value => value, value => DiscardSnapshot(value));
        }

        builder.Ignore<StorageBlobDimension>();
        builder.Ignore<StorageBlobDimensionName>();
        builder.Ignore<StorageDocumentDimension>();
        builder.Ignore<StorageDocumentDimensionName>();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static string DiscardSnapshot(string snapshot)
    {
        Interlocked.Add(ref MaterializedSnapshotCharacters, snapshot.Length);
        return null;
    }

    private sealed class LegacyOwnerIndexes : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder builder, IConventionContext<IConventionModelBuilder> context)
        {
            // Run after index attributes: the legacy baseline must not hide the added parent index's cost.
            var document = builder.Metadata.FindEntityType(typeof(StorageDocument));
            var visibility = document.GetIndexes().SingleOrDefault(index => index.Properties.Select(property => property.Name)
                .SequenceEqual(["TypeHash", "Type", "Id", "ExpiresAtUnixMilliseconds"]));
            if (visibility is not null)
            {
                document.RemoveIndex(visibility);
            }
        }
    }
}

internal static class DimensionQualificationData
{
    public static string Profile { get; set; } = "reference";

    public const int Seed = 17043;
    public const int ScopeCount = 10;
    public const int NamesPerScope = 32;
    public const int DimensionsPerOwner = 8;
    public static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly byte[] Payload = Encoding.UTF8.GetBytes("{\"data\":\"" + new string('a', 1013) + "\"}");
    public static readonly string MaximumString = CreateMaximumString();
    public static readonly string ContentHash = BlobContentHash.Prefix + HashHelper.ComputeSha256(Payload);

    private static string CreateMaximumString()
    {
        var bytes = new byte[192];
        new Random(Seed).NextBytes(bytes);
        return Convert.ToBase64String(bytes); // Exactly 256 ASCII/UTF-8 bytes with non-repeating seeded content.
    }

    public static int ScopeNumber(int owner) => Profile == "skew" ? owner % 10 == 0 ? owner / 10 % ScopeCount : 0 : owner % ScopeCount;

    public static bool HasDimensions(int owner) => Profile != "sparse" || GroupPosition(owner) % 10 == 0;

    public static int RegistrationSeedCount => Profile == "skew" ? 400 : 40;

    public static int MutationOwner(int worker) => Profile == "sparse" ? worker : 100 + worker * 200;

    public static int MetadataOwner(int worker, bool write) => Profile == "sparse" ? (write ? 16 : 0) + worker : (write ? 101 : 100) + worker * 40;

    public static int NameGroup(int owner) => owner / ScopeCount % 4;

    public static int GroupPosition(int owner) => owner / (ScopeCount * 4);

    public static int Bucket(int owner) => GroupPosition(owner) / 1200;

    public static string Scope(int owner) => $"scope{ScopeNumber(owner):D2}";

    public static DocumentTypeIdentity Type(int owner) => new($"QualificationScope{ScopeNumber(owner):D2}");

    public static string Key(int owner) => HashHelper.ComputeSha256($"{Seed}:{owner}")[..16] + "-" + owner.ToString("D10", CultureInfo.InvariantCulture);

    public static string Name(int group, int slot) => $"D{group * DimensionsPerOwner + slot:D2}";

    public static string Label(int bucket) => bucket.ToString("D6", CultureInfo.InvariantCulture) + "-X" + new string('é', 12);

    public static IReadOnlyDictionary<string, long> KindCounts(int owners)
    {
        var counts = new long[6];
        for (var owner = 0; owner < owners; owner++)
        {
            if (!HasDimensions(owner))
            {
                continue;
            }

            for (var slot = 0; slot < DimensionsPerOwner; slot++)
            {
                var schedule = (owner * DimensionsPerOwner + slot) % 50;
                counts[schedule < 45 ? schedule / 9 + 1 : 0]++;
            }
        }

        return new[] { "Null", "Integer", "Decimal", "Boolean", "String", "DateTime" }
            .Select((name, index) => (name, count: counts[index])).ToDictionary(item => item.name, item => item.count);
    }

    public static KeyValuePair<string, object>[] Scalars(int owner, int revision = 0)
    {
        if (!HasDimensions(owner))
        {
            return [];
        }

        var bucket = Bucket(owner);
        return Enumerable.Range(0, DimensionsPerOwner).Select(slot =>
        {
            // Fifty cells contain nine of each non-null kind and five nulls.
            var schedule = (owner * DimensionsPerOwner + slot) % 50;
            var change = slot == 7 ? revision : 0;
            object value = schedule switch
            {
                < 9 => bucket + change,
                < 18 => bucket + 0.125m + change,
                < 27 => (GroupPosition(owner) + change) % 2 == 0,
                < 36 => Label(bucket + change),
                < 45 => Epoch.AddTicks(owner + change),
                _ => null
            };
            return KeyValuePair.Create(Name(NameGroup(owner), slot), value);
        }).ToArray();
    }
}

internal sealed class DimensionQualificationCase(string name, int expectedMatches, bool selectiveTarget)
{
    private readonly long[] histogram = new long[200002];
    private long failures;
    private long rows;
    private long allocatedBytes;
    private readonly long[] queueHistogram = new long[200002];
    private long missedRequests;

    internal long IntervalAllocatedBytes
    {
        get; set;
    }

    public string Name { get; } = name;

    public int ExpectedMatches { get; } = expectedMatches;

    public bool SelectiveTarget { get; } = selectiveTarget;

    public long Operations => this.histogram.Sum();

    public long Failures => this.failures;

    public long MissedRequests => this.missedRequests;

    public long OfferedRequests => this.Operations + this.MissedRequests;

    public double QueueP95Milliseconds => this.Percentile(0.95, this.queueHistogram);

    public void RecordMissed() => Interlocked.Increment(ref this.missedRequests);

    public void RecordQueue(double milliseconds) => Interlocked.Increment(ref this.queueHistogram[Math.Clamp((int)Math.Ceiling(milliseconds * 100), 0, this.queueHistogram.Length - 1)]);

    public long VerifiedRows => this.rows;

    public long AllocatedBytes => this.allocatedBytes;

    public double AllocatedBytesPerOperation => this.Operations == 0 ? 0 : this.allocatedBytes / (double)this.Operations;

    public double P95Milliseconds => this.Percentile(0.95);

    public double P50Milliseconds => this.Percentile(0.50);

    public double P99Milliseconds => this.Percentile(0.99);

    public long OverTwoSeconds => this.histogram[^1];

    public IReadOnlyList<DimensionLatencyBucket> Distribution => this.histogram.Select((count, index) => (count, index))
        .Where(bucket => bucket.count != 0).Select(bucket => new DimensionLatencyBucket(bucket.index == this.histogram.Length - 1 ? null : bucket.index / 100d, bucket.count)).ToArray();

    public void Record(double milliseconds, int verifiedRows, bool failed)
    {
        Interlocked.Increment(ref this.histogram[Math.Clamp((int)Math.Ceiling(milliseconds * 100), 0, this.histogram.Length - 1)]);
        Interlocked.Add(ref this.rows, verifiedRows);
        if (failed)
        {
            Interlocked.Increment(ref this.failures);
        }
    }

    public void RecordAllocation(long bytes) => Interlocked.Add(ref this.allocatedBytes, bytes);

    private double Percentile(double fraction, long[] buckets = null)
    {
        buckets ??= this.histogram;
        var target = Math.Max(1, (long)Math.Ceiling(this.Operations * fraction));
        long accumulated = 0;
        for (var index = 0; index < buckets.Length; index++)
        {
            accumulated += buckets[index];
            if (accumulated >= target)
            {
                return index == buckets.Length - 1 ? double.PositiveInfinity : index / 100d;
            }
        }

        return 0;
    }
}

internal sealed record DimensionLatencyBucket(double? UpperBoundMilliseconds, long Count);