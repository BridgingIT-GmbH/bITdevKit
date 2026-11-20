// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Application.Storage;
using Common;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class BlobDimensionsQualification
{
    private readonly EntityFrameworkBlobStoreProvider<DimensionsQualificationContext>[] providers;
    private readonly IServiceScopeFactory scopes;

    public BlobDimensionsQualification(IServiceScopeFactory scopes)
    {
        this.scopes = scopes;
        this.providers = Enumerable.Range(0, 4).Select(_ => new EntityFrameworkBlobStoreProvider<DimensionsQualificationContext>(scopes)).ToArray();
    }

    public async Task SeedAsync(int owners, bool bulkFixture = false)
    {
        // Names and IDs are registered by ordinary writes. The larger immutable fixture uses those database IDs.
        for (var owner = 0; owner < Math.Min(DimensionQualificationData.RegistrationSeedCount, owners); owner++)
        {
            using var content = new MemoryStream(DimensionQualificationData.Payload, writable: false);
            StorageDimensionsQualification.Require(await this.providers[owner % 4].UploadAsync(new()
            {
                Key = Key(owner),
                Content = content,
                Dimensions = Values(owner)
            }));
        }

        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        var names = (await context.Set<StorageBlobDimensionName>().AsNoTracking().ToListAsync())
            .ToDictionary(name => (name.Scope, name.Name), name => name.Id);
        for (var start = DimensionQualificationData.RegistrationSeedCount; start < owners; start += 500)
        {
            for (var owner = start; owner < Math.Min(start + 500, owners); owner++)
            {
                var dimensions = Values(owner);
                var row = new StorageBlob
                {
                    Container = DimensionQualificationData.Scope(owner),
                    Name = DimensionQualificationData.Key(owner),
                    ContainerHash = HashHelper.ComputeSha256(DimensionQualificationData.Scope(owner)),
                    NameHash = HashHelper.ComputeSha256(DimensionQualificationData.Key(owner)),
                    Length = 1024,
                    ContentHash = DimensionQualificationData.ContentHash,
                    ETag = Guid.NewGuid().ToString("N"),
                    DimensionsJson = BlobDimensionValues.Serialize(dimensions)
                };
                row.Chunks.Add(new()
                {
                    Blob = row,
                    Index = 0,
                    Content = DimensionQualificationData.Payload,
                    Length = 1024
                });
                foreach (var item in dimensions)
                {
                    var value = new StorageBlobDimension { Owner = row, NameId = names[(row.Container, item.Key)] };
                    BlobDimensionValueEncoding.Encode(item.Value, value);
                    row.DimensionValues.Add(value);
                }

                context.Add(row);
            }

            if (bulkFixture)
            {
                await StorageDimensionsSqlFixture.WriteTrackedAsync(context);
            }
            else
            {
                await context.SaveChangesAsync();
            }

            context.ChangeTracker.Clear();
            if ((start + 500) % 100000 < 500 || start + 500 >= owners)
            {
                Console.WriteLine($"Blob fixture: {Math.Min(start + 500, owners):N0}/{owners:N0} owners seeded.");
            }
        }

        var actual = await context.Set<StorageBlobDimension>().LongCountAsync();
        if (actual != Enumerable.Range(0, owners).LongCount(DimensionQualificationData.HasDimensions) * 8L)
        {
            throw new InvalidOperationException($"Blob fixture contains {actual} values instead of {owners * 8L}.");
        }
    }

    public async Task<IReadOnlyList<DimensionQualificationJob>> CreateJobsAsync(int owners, string engine)
    {
        var jobs = new List<DimensionQualificationJob>();
        foreach (var scenario in new[] { "integer", "integer-range", "decimal", "decimal-range", "boolean", "string", "datetime-range", "and", "or", "anchored-negative", "anchored-not-equal", "null", "non-null", "exists" }.Where(scenario => engine != "sqlite" || scenario != "decimal-range"))
        {
            var filter = Filter(scenario);
            var expected = Enumerable.Range(0, owners).Where(owner => DimensionQualificationData.ScopeNumber(owner) == 0 && Matches(owner, scenario))
                .Select(DimensionQualificationData.Key).Order(StringComparer.Ordinal).ToArray();
            var verifyCompleteMembership = expected.Length <= 1000;
            var all = new List<string>();
            string continuation = null;
            string late = null;
            do
            {
                var page = StorageDimensionsQualification.Require(await this.providers[0].ListPageAsync(new()
                {
                    Container = "scope00",
                    DimensionFilter = filter,
                    Take = 100,
                    ContinuationToken = continuation
                }));
                if (continuation is not null && all.Count <= expected.Length - 100 && page.Items.Count == 100)
                {
                    late = continuation;
                }

                all.AddRange(page.Items.Select(item => item.Key.Name));
                continuation = verifyCompleteMembership ? page.ContinuationToken : null;
            } while (continuation is not null);

            if (!all.SequenceEqual(verifyCompleteMembership ? expected : expected.Take(100)))
            {
                throw new InvalidOperationException($"Blob {scenario} query returned the wrong fixture keys.");
            }

            foreach (var position in late is null ? new[] { ("first", (string)null) } : new[] { ("first", (string)null), ("late", late) })
            {
                var token = position.Item2;
                var expectedPage = token is null ? expected.Take(100).ToArray() : expected.Skip((expected.Length - 100) / 100 * 100).Take(100).ToArray();
                jobs.Add(new($"blob/{scenario}/{position.Item1}", false, expected.Length, expected.Length is >= 200 and <= 1000, async worker =>
                {
                    var page = StorageDimensionsQualification.Require(await this.providers[worker % 4].ListPageAsync(new()
                    {
                        Container = "scope00",
                        DimensionFilter = filter,
                        Take = 100,
                        ContinuationToken = token
                    }));
                    if (!page.Items.Select(item => item.Key.Name).SequenceEqual(expectedPage))
                    {
                        throw new InvalidOperationException("Blob page changed membership.");
                    }

                    return page.Items.Count;
                }));
            }
        }

        jobs.Add(new("blob/one-value-delta", true, 1, true, async worker =>
        {
            // Separate keys per writer, and a slot not referenced by the read cases.
            var owner = DimensionQualificationData.MutationOwner(worker);
            var revision = (int)(Interlocked.Increment(ref this.revisions[worker % 4]) % 2);
            var result = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new()
            {
                Key = Key(owner),
                Dimensions = Values(owner, revision)
            }));
            if (result.Dimensions.Count != 8)
            {
                throw new InvalidOperationException("Blob mutation lost dimensions.");
            }

            return 1;
        }));
        return jobs;
    }

    private readonly long[] revisions = new long[4];

    public void ClearCaches()
    {
        foreach (var provider in this.providers)
        {
            provider.ClearDimensionNameCache();
        }
    }

    public async Task<object> CaptureCountersAsync()
    {
        var snapshots = new List<BlobDimensionDiagnostics>();
        foreach (var provider in this.providers)
        {
            snapshots.Add(StorageDimensionsQualification.Require(await provider.GetDimensionDiagnosticsAsync()));
        }

        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        return new
        {
            Providers = snapshots,
            RetainedNames = await context.Set<StorageBlobDimensionName>().LongCountAsync()
        };
    }

    public async Task SeedBaselineAsync(int owners)
    {
        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>();
        for (var start = 0; start < owners; start += 500)
        {
            for (var owner = start; owner < Math.Min(start + 500, owners); owner++)
            {
                var row = new StorageBlob
                {
                    Container = DimensionQualificationData.Scope(owner),
                    Name = DimensionQualificationData.Key(owner),
                    ContainerHash = HashHelper.ComputeSha256(DimensionQualificationData.Scope(owner)),
                    NameHash = HashHelper.ComputeSha256(DimensionQualificationData.Key(owner)),
                    Length = 1024,
                    ContentHash = DimensionQualificationData.ContentHash,
                    ETag = Guid.NewGuid().ToString("N")
                };
                row.Chunks.Add(new()
                {
                    Blob = row,
                    Index = 0,
                    Content = DimensionQualificationData.Payload,
                    Length = 1024
                });
                context.Add(row);
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }

    public IReadOnlyList<DimensionQualificationJob> BaselineJobs(bool parentOnly)
    {
        IBlobStoreProvider provider = parentOnly ? new EntityFrameworkBlobStoreProvider<DimensionsParentOnlyContext>(this.scopes) : this.providers[0];
        return
        [
            new("blob/properties-read", false, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await provider.GetPropertiesAsync(Key(DimensionQualificationData.MetadataOwner(worker, false))));
                if (info.Length != 1024) { throw new InvalidOperationException("Baseline blob metadata did not match."); } return 1;
            }),
            new("blob/properties-write", true, 1, false, async worker =>
            {
                StorageDimensionsQualification.Require(await provider.UpdatePropertiesAsync(new() { Key = Key(DimensionQualificationData.MetadataOwner(worker, true)), Properties = new() { ["marker"] = 1 } }));
                return 1;
            }),
            new("blob/dimensionless-read", false, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await provider.GetPropertiesAsync(new("dimensionless", worker.ToString())));
                if (info.Length != 1024 || info.Dimensions.Count != 0) { throw new InvalidOperationException("Dimensionless blob metadata did not match."); } return 1;
            }),
            new("blob/dimensionless-write", true, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await provider.UpdatePropertiesAsync(new() { Key = new("dimensionless", (16 + worker).ToString()), Properties = new() { ["marker"] = 1 } }));
                if (info.Dimensions.Count != 0) { throw new InvalidOperationException("Dimensionless blob acquired dimensions."); } return 1;
            })
        ];
    }

    public async Task SeedDimensionlessAsync()
    {
        IBlobStoreProvider[] stores = [this.providers[0], new EntityFrameworkBlobStoreProvider<DimensionsParentOnlyContext>(this.scopes)];
        foreach (var store in stores)
        {
            for (var owner = 0; owner < 32; owner++)
            {
                using var content = new MemoryStream(DimensionQualificationData.Payload, writable: false);
                StorageDimensionsQualification.Require(await store.UploadAsync(new()
                {
                    Key = new("dimensionless", owner.ToString()),
                    Content = content
                }));
            }
        }
    }

    private static BlobKey Key(int owner) => new(DimensionQualificationData.Scope(owner), DimensionQualificationData.Key(owner));

    private static BlobDimensions Values(int owner, int revision = 0) => StorageDimensionsQualification.Require(BlobDimensions.Create(DimensionQualificationData.Scalars(owner, revision)));

    private static BlobDimensionFilter Leaf(string name, BlobDimensionOperator operation, object value = null) => BlobDimensionFilter.Where(StorageDimensionsQualification.Require(BlobDimensionPredicate.Create(name, operation, value)));

    private static BlobDimensionFilter Filter(string scenario)
    {
        BlobDimensionFilter ForGroup(int group)
        {
            var name = DimensionQualificationData.Name(group, 0);
            return scenario switch
            {
                "integer" => Leaf(name, BlobDimensionOperator.Equal, 0),
                "decimal" => Leaf(name, BlobDimensionOperator.Equal, 0.125m),
                "integer-range" => BlobDimensionFilter.All(Leaf(name, BlobDimensionOperator.GreaterThanOrEqual, 0), Leaf(name, BlobDimensionOperator.LessThan, 1)),
                "decimal-range" => BlobDimensionFilter.All(Leaf(name, BlobDimensionOperator.GreaterThanOrEqual, 0.125m), Leaf(name, BlobDimensionOperator.LessThan, 1m)),
                "boolean" => Leaf(name, BlobDimensionOperator.Equal, true),
                "string" => Leaf(name, BlobDimensionOperator.Equal, DimensionQualificationData.Label(0)),
                "datetime-range" => BlobDimensionFilter.All(Leaf(name, BlobDimensionOperator.GreaterThanOrEqual, DimensionQualificationData.Epoch), Leaf(name, BlobDimensionOperator.LessThan, DimensionQualificationData.Epoch.AddTicks(48000))),
                "and" => BlobDimensionFilter.All(Leaf(name, BlobDimensionOperator.Equal, 0), Leaf(DimensionQualificationData.Name(group, 1), BlobDimensionOperator.Equal, 0)),
                "or" => BlobDimensionFilter.Any(Leaf(name, BlobDimensionOperator.Equal, 0), Leaf(name, BlobDimensionOperator.Equal, 0.125m)),
                "null" => Leaf(DimensionQualificationData.Name(group, 6), BlobDimensionOperator.IsNull),
                "non-null" => Leaf(DimensionQualificationData.Name(group, 6), BlobDimensionOperator.IsNotNull),
                "exists" => Leaf(name, BlobDimensionOperator.Exists),
                "anchored-not-equal" => BlobDimensionFilter.All(Leaf(name, BlobDimensionOperator.Equal, 0), Leaf(DimensionQualificationData.Name(group, 1), BlobDimensionOperator.NotEqual, 1)),
                "anchored-negative" => BlobDimensionFilter.All(Leaf(name, BlobDimensionOperator.Equal, 0), Leaf("Absent", BlobDimensionOperator.Missing)),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };
        }

        return BlobDimensionFilter.Any(Enumerable.Range(0, scenario == "or" ? 2 : 4).Select(ForGroup).ToArray());
    }

    private static bool Matches(int owner, string scenario)
    {
        if (!DimensionQualificationData.HasDimensions(owner))
        {
            return false;
        }

        if (scenario == "or" && DimensionQualificationData.NameGroup(owner) >= 2)
        {
            return false;
        }

        var value = DimensionQualificationData.Scalars(owner)[0].Value;
        var integer = value is int number && number == 0;
        return scenario switch
        {
            "integer" or "integer-range" or "anchored-negative" => integer,
            "and" or "anchored-not-equal" => integer && DimensionQualificationData.Scalars(owner)[1].Value is int second && second == 0,
            "decimal" or "decimal-range" => value is decimal amount && amount == 0.125m,
            "boolean" => value is true,
            "string" => value is string text && text == DimensionQualificationData.Label(0),
            "datetime-range" => value is DateTime date && date < DimensionQualificationData.Epoch.AddTicks(48000),
            "null" => DimensionQualificationData.Scalars(owner)[6].Value is null,
            "non-null" => DimensionQualificationData.Scalars(owner)[6].Value is not null,
            "exists" => true,
            "or" => integer || value is decimal amount && amount == 0.125m,
            _ => false
        };
    }
}