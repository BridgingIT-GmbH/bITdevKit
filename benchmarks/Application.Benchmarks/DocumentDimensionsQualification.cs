// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Application.Storage;
using Common;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class DocumentDimensionsQualification
{
    private readonly EntityFrameworkDocumentStoreProvider<DimensionsQualificationContext>[] providers;
    private readonly IServiceScopeFactory scopes;

    public DocumentDimensionsQualification(IServiceScopeFactory scopes)
    {
        this.scopes = scopes;
        this.providers = Enumerable.Range(0, 4).Select(_ => new EntityFrameworkDocumentStoreProvider<DimensionsQualificationContext>(scopes)).ToArray();
    }

    public async Task SeedAsync(int owners, bool bulkFixture = false)
    {
        // Names and IDs are registered by ordinary writes. The larger immutable fixture uses those database IDs.
        for (var owner = 0; owner < Math.Min(DimensionQualificationData.RegistrationSeedCount, owners); owner++)
        {
            StorageDimensionsQualification.Require(await this.providers[owner % 4].UpsertAsync(DimensionQualificationData.Type(owner), new()
            {
                Key = Key(owner),
                Content = DimensionQualificationData.Payload,
                ContentHash = DimensionQualificationData.ContentHash,
                StoredContentHash = DimensionQualificationData.ContentHash,
                Dimensions = Values(owner)
            }));
        }

        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        var names = (await context.Set<StorageDocumentDimensionName>().AsNoTracking().ToListAsync())
            .ToDictionary(name => (name.Scope, name.Name), name => name.Id);
        for (var start = DimensionQualificationData.RegistrationSeedCount; start < owners; start += 500)
        {
            for (var owner = start; owner < Math.Min(start + 500, owners); owner++)
            {
                var dimensions = Values(owner);
                var row = new StorageDocument
                {
                    Type = DimensionQualificationData.Type(owner).Value,
                    TypeHash = HashHelper.ComputeSha256(DimensionQualificationData.Type(owner).Value),
                    PartitionKey = "records",
                    PartitionKeyHash = HashHelper.ComputeSha256("records"),
                    RowKey = DimensionQualificationData.Key(owner),
                    RowKeyHash = HashHelper.ComputeSha256(DimensionQualificationData.Key(owner)),
                    Content = DimensionQualificationData.Payload,
                    ContentHash = DimensionQualificationData.ContentHash,
                    StoredContentHash = DimensionQualificationData.ContentHash,
                    DimensionsJson = DocumentDimensionValues.Serialize(dimensions)
                };
                foreach (var item in dimensions)
                {
                    var value = new StorageDocumentDimension { Owner = row, NameId = names[(row.Type, item.Key)] };
                    DocumentDimensionValueEncoding.Encode(item.Value, value);
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
                Console.WriteLine($"Document fixture: {Math.Min(start + 500, owners):N0}/{owners:N0} owners seeded.");
            }
        }

        var actual = await context.Set<StorageDocumentDimension>().LongCountAsync();
        if (actual != Enumerable.Range(0, owners).LongCount(DimensionQualificationData.HasDimensions) * 8L)
        {
            throw new InvalidOperationException($"Document fixture contains {actual} values instead of {owners * 8L}.");
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
                var page = StorageDimensionsQualification.Require(await this.providers[0].ListPageAsync(DimensionQualificationData.Type(0), new()
                {
                    DimensionFilter = filter,
                    Take = 100,
                    ContinuationToken = continuation
                }, DateTimeOffset.UtcNow));
                if (continuation is not null && all.Count <= expected.Length - 100 && page.Items.Count == 100)
                {
                    late = continuation;
                }

                all.AddRange(page.Items.Select(item => item.RowKey));
                continuation = verifyCompleteMembership ? page.ContinuationToken : null;
            } while (continuation is not null);

            if (!all.SequenceEqual(verifyCompleteMembership ? expected : expected.Take(100)))
            {
                throw new InvalidOperationException($"Document {scenario} query returned the wrong fixture keys.");
            }

            jobs.Add(new($"document/{scenario}/count", false, expected.Length, false, async worker =>
            {
                var count = StorageDimensionsQualification.Require(await this.providers[worker % 4].CountAsync(DimensionQualificationData.Type(0), new()
                {
                    DimensionFilter = filter
                }, DateTimeOffset.UtcNow));
                if (count != expected.Length)
                {
                    throw new InvalidOperationException("Document count changed membership.");
                }

                return checked((int)count);
            }));
            foreach (var position in late is null ? new[] { ("first", (string)null) } : new[] { ("first", (string)null), ("late", late) })
            {
                var token = position.Item2;
                var expectedPage = token is null ? expected.Take(100).ToArray() : expected.Skip((expected.Length - 100) / 100 * 100).Take(100).ToArray();
                jobs.Add(new($"document/{scenario}/{position.Item1}", false, expected.Length, expected.Length is >= 200 and <= 1000, async worker =>
                {
                    var page = StorageDimensionsQualification.Require(await this.providers[worker % 4].ListPageAsync(DimensionQualificationData.Type(0), new()
                    {
                        DimensionFilter = filter,
                        Take = 100,
                        ContinuationToken = token
                    }, DateTimeOffset.UtcNow));
                    if (!page.Items.Select(item => item.RowKey).SequenceEqual(expectedPage))
                    {
                        throw new InvalidOperationException("Document page changed membership.");
                    }

                    return page.Items.Count;
                }));
                jobs.Add(new($"document/payload/{scenario}/{position.Item1}", false, expected.Length, expected.Length is >= 200 and <= 1000, async worker =>
                {
                    var page = StorageDimensionsQualification.Require(await this.providers[worker % 4].FindPageAsync(DimensionQualificationData.Type(0), new()
                    {
                        DimensionFilter = filter,
                        Take = 100,
                        ContinuationToken = token
                    }, DateTimeOffset.UtcNow));
                    if (!page.Items.Select(item => item.Key.RowKey).SequenceEqual(expectedPage) || page.Items.Any(item => item.Content.Length != 1024))
                    {
                        throw new InvalidOperationException("Document payload page failed verification.");
                    }

                    return page.Items.Count;
                }));
            }
        }

        jobs.Add(new("document/one-value-delta", true, 1, true, async worker =>
        {
            // Separate keys per writer, and a slot not referenced by the read cases.
            var owner = DimensionQualificationData.MutationOwner(worker);
            var revision = (int)(Interlocked.Increment(ref this.revisions[worker % 4]) % 2);
            var result = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(DimensionQualificationData.Type(owner), new()
            {
                Key = Key(owner),
                Dimensions = Values(owner, revision)
            }));
            if (result.Dimensions.Count != 8)
            {
                throw new InvalidOperationException("Document mutation lost dimensions.");
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
        var snapshots = new List<DocumentDimensionDiagnostics>();
        foreach (var provider in this.providers)
        {
            snapshots.Add(StorageDimensionsQualification.Require(await provider.GetDimensionDiagnosticsAsync(DimensionQualificationData.Type(0))));
        }

        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        return new
        {
            Providers = snapshots,
            RetainedNames = await context.Set<StorageDocumentDimensionName>().LongCountAsync()
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
                context.Add(new StorageDocument
                {
                    Type = DimensionQualificationData.Type(owner).Value,
                    TypeHash = HashHelper.ComputeSha256(DimensionQualificationData.Type(owner).Value),
                    PartitionKey = "records",
                    PartitionKeyHash = HashHelper.ComputeSha256("records"),
                    RowKey = DimensionQualificationData.Key(owner),
                    RowKeyHash = HashHelper.ComputeSha256(DimensionQualificationData.Key(owner)),
                    Content = DimensionQualificationData.Payload,
                    ContentHash = DimensionQualificationData.ContentHash,
                    StoredContentHash = DimensionQualificationData.ContentHash
                });
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }

    public IReadOnlyList<DimensionQualificationJob> BaselineJobs(bool parentOnly)
    {
        IDocumentStoreProvider provider = parentOnly ? new EntityFrameworkDocumentStoreProvider<DimensionsParentOnlyContext>(this.scopes) : this.providers[0];
        return
        [
            new("document/properties-read", false, 1, false, async worker =>
            {
                var owner = DimensionQualificationData.MetadataOwner(worker, false);
                var stored = StorageDimensionsQualification.Require(await provider.GetAsync(DimensionQualificationData.Type(owner), Key(owner), DateTimeOffset.UtcNow));
                if (stored.Content.Length != 1024) { throw new InvalidOperationException("Baseline document payload did not match."); } return 1;
            }),
            new("document/properties-write", true, 1, false, async worker =>
            {
                var owner = DimensionQualificationData.MetadataOwner(worker, true);
                StorageDimensionsQualification.Require(await provider.UpdatePropertiesAsync(DimensionQualificationData.Type(owner),
                    new(Key(owner)) { Properties = new() { ["marker"] = 1 } }, null, true));
                return 1;
            }),
            new("document/dimensionless-read", false, 1, false, async worker =>
            {
                var stored = StorageDimensionsQualification.Require(await provider.GetAsync(new("QualificationDimensionless"), new("records", worker.ToString()), DateTimeOffset.UtcNow));
                if (stored.Content.Length != 1024 || stored.Dimensions.Count != 0) { throw new InvalidOperationException("Dimensionless document did not match."); } return 1;
            }),
            new("document/dimensionless-write", true, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await provider.UpdatePropertiesAsync(new("QualificationDimensionless"),
                    new(new("records", (16 + worker).ToString())) { Properties = new() { ["marker"] = 1 } }, null, true));
                if (info.Dimensions.Count != 0) { throw new InvalidOperationException("Dimensionless document acquired dimensions."); } return 1;
            })
        ];
    }

    public async Task SeedDimensionlessAsync()
    {
        IDocumentStoreProvider[] stores = [this.providers[0], new EntityFrameworkDocumentStoreProvider<DimensionsParentOnlyContext>(this.scopes)];
        foreach (var store in stores)
        {
            for (var owner = 0; owner < 32; owner++)
            {
                StorageDimensionsQualification.Require(await store.UpsertAsync(new("QualificationDimensionless"), new()
                {
                    Key = new("records", owner.ToString()),
                    Content = DimensionQualificationData.Payload,
                    ContentHash = DimensionQualificationData.ContentHash,
                    StoredContentHash = DimensionQualificationData.ContentHash
                }));
            }
        }
    }

    private static DocumentKey Key(int owner) => new("records", DimensionQualificationData.Key(owner));

    private static DocumentDimensions Values(int owner, int revision = 0) => StorageDimensionsQualification.Require(DocumentDimensions.Create(DimensionQualificationData.Scalars(owner, revision)));

    private static DocumentDimensionFilter Leaf(string name, DocumentDimensionOperator operation, object value = null) => DocumentDimensionFilter.Where(StorageDimensionsQualification.Require(DocumentDimensionPredicate.Create(name, operation, value)));

    private static DocumentDimensionFilter Filter(string scenario)
    {
        DocumentDimensionFilter ForGroup(int group)
        {
            var name = DimensionQualificationData.Name(group, 0);
            return scenario switch
            {
                "integer" => Leaf(name, DocumentDimensionOperator.Equal, 0),
                "decimal" => Leaf(name, DocumentDimensionOperator.Equal, 0.125m),
                "integer-range" => DocumentDimensionFilter.All(Leaf(name, DocumentDimensionOperator.GreaterThanOrEqual, 0), Leaf(name, DocumentDimensionOperator.LessThan, 1)),
                "decimal-range" => DocumentDimensionFilter.All(Leaf(name, DocumentDimensionOperator.GreaterThanOrEqual, 0.125m), Leaf(name, DocumentDimensionOperator.LessThan, 1m)),
                "boolean" => Leaf(name, DocumentDimensionOperator.Equal, true),
                "string" => Leaf(name, DocumentDimensionOperator.Equal, DimensionQualificationData.Label(0)),
                "datetime-range" => DocumentDimensionFilter.All(Leaf(name, DocumentDimensionOperator.GreaterThanOrEqual, DimensionQualificationData.Epoch), Leaf(name, DocumentDimensionOperator.LessThan, DimensionQualificationData.Epoch.AddTicks(48000))),
                "and" => DocumentDimensionFilter.All(Leaf(name, DocumentDimensionOperator.Equal, 0), Leaf(DimensionQualificationData.Name(group, 1), DocumentDimensionOperator.Equal, 0)),
                "or" => DocumentDimensionFilter.Any(Leaf(name, DocumentDimensionOperator.Equal, 0), Leaf(name, DocumentDimensionOperator.Equal, 0.125m)),
                "null" => Leaf(DimensionQualificationData.Name(group, 6), DocumentDimensionOperator.IsNull),
                "non-null" => Leaf(DimensionQualificationData.Name(group, 6), DocumentDimensionOperator.IsNotNull),
                "exists" => Leaf(name, DocumentDimensionOperator.Exists),
                "anchored-not-equal" => DocumentDimensionFilter.All(Leaf(name, DocumentDimensionOperator.Equal, 0), Leaf(DimensionQualificationData.Name(group, 1), DocumentDimensionOperator.NotEqual, 1)),
                "anchored-negative" => DocumentDimensionFilter.All(Leaf(name, DocumentDimensionOperator.Equal, 0), Leaf("Absent", DocumentDimensionOperator.Missing)),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };
        }

        return DocumentDimensionFilter.Any(Enumerable.Range(0, scenario == "or" ? 2 : 4).Select(ForGroup).ToArray());
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