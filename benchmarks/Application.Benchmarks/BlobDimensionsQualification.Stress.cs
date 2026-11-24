// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class BlobDimensionsQualification
{
    private long registrationSerial;
    private readonly long[] stressRevisions = new long[8];
    private static readonly string MaximumText = DimensionQualificationData.MaximumString;

    public async Task<IReadOnlyList<DimensionQualificationJob>> CreateStressJobsAsync()
    {
        for (var owner = 0; owner < 8; owner++)
        {
            await this.SeedStressOwnerAsync("stress-maximum", owner.ToString(), MaximumValues());
            await this.SeedStressOwnerAsync("stress-types", owner.ToString(), null);
            await this.SeedStressOwnerAsync("stress-registration", owner.ToString(), null);
        }

        // One provider exceeds both historical quota suggestions and the positive-cache capacity.
        for (var owner = 0; owner < 160; owner++)
            await this.SeedStressOwnerAsync("stress-distinct", owner.ToString(), StorageDimensionsQualification.Require(BlobDimensions.Create(
                Enumerable.Range(0, 64).Select(slot => KeyValuePair.Create<string, object>($"Runtime_{owner}_{slot}", slot)))));
        await using (var scope = this.scopes.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
            if (await context.Set<StorageBlobDimensionName>().LongCountAsync(row => row.Scope == "stress-distinct") != 10240)
                throw new InvalidOperationException("Distinct-name blob stress registration is incomplete.");
        }

        var filters = Enumerable.Range(0, 16).Select(slot => Leaf(MaximumName(slot), BlobDimensionOperator.Equal, MaximumText)).ToArray();
        BlobDimensionFilter Group(int depth) => BlobDimensionFilter.All(filters.Skip(depth * 4).Take(4)
            .Concat(depth == 3 ? [] : new[] { Group(depth + 1) }).ToArray());
        var filter = Group(0);
        return
        [
            new("blob/stress/maximum-collection-filter", false, 8, false, async worker =>
            {
                var page = StorageDimensionsQualification.Require(await this.providers[worker % 4].ListPageAsync(new()
                {
                    Container = "stress-maximum", DimensionFilter = filter, Take = 100
                }));
                if (page.Items.Count != 8 || page.Items.Any(item => item.Dimensions.Count != 64)) throw new InvalidOperationException("Maximum blob filter did not match.");
                return 8;
            }),
            new("blob/stress/distinct-name-point-read", false, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].GetPropertiesAsync(new("stress-distinct", (worker % 160).ToString())));
                if (info.Dimensions.Count != 64) throw new InvalidOperationException("Distinct-name blob snapshot did not match.");
                return 1;
            }),
            new("blob/stress/maximum-value-delta", true, 1, false, async worker =>
            {
                var revision = Interlocked.Increment(ref this.stressRevisions[worker % 8]);
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new()
                {
                    Key = new("stress-maximum", worker.ToString()), Dimensions = MaximumValues(revision % 2 != 0)
                }));
                if (info.Dimensions.Count != 64) throw new InvalidOperationException("Maximum blob delta lost values.");
                return 1;
            }),
            new("blob/stress/type-change-clear", true, 1, false, async worker =>
            {
                var phase = Interlocked.Increment(ref this.stressRevisions[worker % 8]) % 7;
                object value = phase switch { 0 => int.MaxValue, 1 => 999999999999.999999m, 2 => true, 3 => DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), 4 => MaximumText, _ => null };
                var dimensions = phase == 6 ? BlobDimensions.Empty : StorageDimensionsQualification.Require(BlobDimensions.Create(new Dictionary<string, object> { ["Kind"] = value }));
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new() { Key = new("stress-types", worker.ToString()), Dimensions = dimensions }));
                if (info.Dimensions.Count != dimensions.Count) throw new InvalidOperationException("Blob type/clear stress lost state.");
                return 1;
            }),
            new("blob/stress/identical-dimensions", true, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new()
                {
                    Key = new("stress-distinct", worker.ToString()),
                    Dimensions = StorageDimensionsQualification.Require(BlobDimensions.Create(Enumerable.Range(0, 64)
                        .Select(slot => KeyValuePair.Create<string, object>($"Runtime_{worker}_{slot}", slot))))
                }));
                if (info.Dimensions.Count != 64) throw new InvalidOperationException("Identical blob Dimensions changed.");
                return 1;
            }),
            new("blob/stress/insert-delete", true, 1, false, async worker =>
            {
                var key = new BlobKey("stress-churn", worker.ToString());
                using var content = new MemoryStream(DimensionQualificationData.Payload, writable: false);
                StorageDimensionsQualification.Require(await this.providers[worker % 4].UploadAsync(new()
                {
                    Key = key, Content = content,
                    Dimensions = StorageDimensionsQualification.Require(BlobDimensions.Create(Enumerable.Range(0, 8)
                        .Select(slot => KeyValuePair.Create<string, object>($"Churn_{slot}", slot))))
                }));
                var deleted = await this.providers[worker % 4].DeleteAsync(key);
                if (deleted.IsFailure) throw new InvalidOperationException("Inserted blob was not deleted: " + string.Join("; ", deleted.Errors.Select(error => error.Message)));
                return 1;
            }),
            new("blob/stress/registration-burst", true, 1, false, async worker =>
            {
                var serial = Interlocked.Increment(ref this.registrationSerial);
                var values = StorageDimensionsQualification.Require(BlobDimensions.Create(Enumerable.Range(0, 8)
                    .Select(slot => KeyValuePair.Create<string, object>($"Burst_{serial}_{slot}", slot))));
                var info = StorageDimensionsQualification.Require(await this.providers[0].UpdateDimensionsAsync(new() { Key = new("stress-registration", worker.ToString()), Dimensions = values }));
                if (info.Dimensions.Count != 8) throw new InvalidOperationException("Blob burst registration lost values.");
                return 1;
            })
        ];
    }

    public async Task<object> CleanupStressAsync()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        foreach (var container in new[] { "stress-maximum", "stress-types", "stress-registration", "stress-distinct" })
        {
            for (var owner = 0; owner < (container == "stress-distinct" ? 160 : 8); owner++)
            {
                var deleted = await this.providers[0].DeleteAsync(new(container, owner.ToString()));
                if (deleted.IsFailure) throw new InvalidOperationException("Blob stress cleanup failed.");
            }
        }

        using var content = new MemoryStream(DimensionQualificationData.Payload, writable: false);
        StorageDimensionsQualification.Require(await this.providers[0].UploadAsync(new()
        {
            Key = new("stress-retention", "expired"), Content = content, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Dimensions = StorageDimensionsQualification.Require(BlobDimensions.Create(new Dictionary<string, object> { ["Retained"] = true }))
        }));
        var swept = StorageDimensionsQualification.Require(await this.providers[0].SweepExpiredAsync(new() { ExpiresOnOrBefore = DateTimeOffset.UtcNow, BatchSize = 10, MaxBatches = 2 }));
        if (swept.DeletedCount != 1) throw new InvalidOperationException("Blob stress retention did not clean the owner.");
        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        var remainingValues = await context.Set<StorageBlobDimension>().LongCountAsync(row => row.Name.Scope.StartsWith("stress-"));
        var retainedNames = await context.Set<StorageBlobDimensionName>().LongCountAsync(row => row.Scope.StartsWith("stress-"));
        if (remainingValues != 0 || retainedNames < 10240) throw new InvalidOperationException($"Blob stress cleanup failed: {remainingValues} remaining values, {retainedNames} retained names (minimum 10240).");
        return new { RemainingValues = remainingValues, RetainedNames = retainedNames, RetentionDeletedOwners = swept.DeletedCount, CleanupMilliseconds = timer.Elapsed.TotalMilliseconds };
    }

    private async Task SeedStressOwnerAsync(string container, string name, BlobDimensions dimensions)
    {
        using var content = new MemoryStream(DimensionQualificationData.Payload, writable: false);
        StorageDimensionsQualification.Require(await this.providers[0].UploadAsync(new() { Key = new(container, name), Content = content, Dimensions = dimensions }));
    }

    private static string MaximumName(int slot) => $"Stress_{slot:D3}_" + new string('N', 117);

    private static BlobDimensions MaximumValues(bool alternate = false) => StorageDimensionsQualification.Require(BlobDimensions.Create(
        Enumerable.Range(0, 64).Select(slot => KeyValuePair.Create<string, object>(MaximumName(slot), slot == 63 && alternate ? (MaximumText[0] == 'A' ? "B" : "A") + MaximumText[1..] : MaximumText))));
}
