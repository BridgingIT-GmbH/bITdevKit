// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class DocumentDimensionsQualification
{
    private long registrationSerial;
    private readonly long[] stressRevisions = new long[8];
    private static readonly string MaximumText = DimensionQualificationData.MaximumString;

    public async Task<IReadOnlyList<DimensionQualificationJob>> CreateStressJobsAsync()
    {
        for (var owner = 0; owner < 8; owner++)
        {
            await this.SeedStressOwnerAsync("QualificationMaximum", owner.ToString(), MaximumValues());
            await this.SeedStressOwnerAsync("QualificationTypes", owner.ToString(), null);
            await this.SeedStressOwnerAsync("QualificationRegistration", owner.ToString(), null);
        }

        // One provider exceeds both historical quota suggestions and the positive-cache capacity.
        for (var owner = 0; owner < 160; owner++)
            await this.SeedStressOwnerAsync("QualificationDistinct", owner.ToString(), StorageDimensionsQualification.Require(DocumentDimensions.Create(
                Enumerable.Range(0, 64).Select(slot => KeyValuePair.Create<string, object>($"Runtime_{owner}_{slot}", slot)))));
        await using (var scope = this.scopes.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
            if (await context.Set<StorageDocumentDimensionName>().LongCountAsync(row => row.Scope == "QualificationDistinct") != 10240)
                throw new InvalidOperationException("Distinct-name document stress registration is incomplete.");
        }

        var filters = Enumerable.Range(0, 16).Select(slot => Leaf(MaximumName(slot), DocumentDimensionOperator.Equal, MaximumText)).ToArray();
        DocumentDimensionFilter Group(int depth) => DocumentDimensionFilter.All(filters.Skip(depth * 4).Take(4)
            .Concat(depth == 3 ? [] : new[] { Group(depth + 1) }).ToArray());
        var filter = Group(0);
        return
        [
            new("document/stress/maximum-collection-filter", false, 8, false, async worker =>
            {
                var page = StorageDimensionsQualification.Require(await this.providers[worker % 4].FindPageAsync(new("QualificationMaximum"), new()
                {
                    DimensionFilter = filter, Take = 100
                }, DateTimeOffset.UtcNow));
                if (page.Items.Count != 8 || page.Items.Any(item => item.Dimensions.Count != 64)) throw new InvalidOperationException("Maximum document filter did not match.");
                return 8;
            }),
            new("document/stress/distinct-name-point-read", false, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].GetAsync(new("QualificationDistinct"), new("records", (worker % 160).ToString()), DateTimeOffset.UtcNow));
                if (info.Dimensions.Count != 64) throw new InvalidOperationException("Distinct-name document snapshot did not match.");
                return 1;
            }),
            new("document/stress/maximum-value-delta", true, 1, false, async worker =>
            {
                var revision = Interlocked.Increment(ref this.stressRevisions[worker % 8]);
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new("QualificationMaximum"), new()
                {
                    Key = new("records", worker.ToString()), Dimensions = MaximumValues(revision % 2 != 0)
                }));
                if (info.Dimensions.Count != 64) throw new InvalidOperationException("Maximum document delta lost values.");
                return 1;
            }),
            new("document/stress/type-change-clear", true, 1, false, async worker =>
            {
                var phase = Interlocked.Increment(ref this.stressRevisions[worker % 8]) % 7;
                object value = phase switch { 0 => int.MaxValue, 1 => 999999999999.999999m, 2 => true, 3 => DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), 4 => MaximumText, _ => null };
                var dimensions = phase == 6 ? DocumentDimensions.Empty : StorageDimensionsQualification.Require(DocumentDimensions.Create(new Dictionary<string, object> { ["Kind"] = value }));
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new("QualificationTypes"), new() { Key = new("records", worker.ToString()), Dimensions = dimensions }));
                if (info.Dimensions.Count != dimensions.Count) throw new InvalidOperationException("Document type/clear stress lost state.");
                return 1;
            }),
            new("document/stress/identical-dimensions", true, 1, false, async worker =>
            {
                var info = StorageDimensionsQualification.Require(await this.providers[worker % 4].UpdateDimensionsAsync(new("QualificationDistinct"), new()
                {
                    Key = new("records", worker.ToString()),
                    Dimensions = StorageDimensionsQualification.Require(DocumentDimensions.Create(Enumerable.Range(0, 64)
                        .Select(slot => KeyValuePair.Create<string, object>($"Runtime_{worker}_{slot}", slot))))
                }));
                if (info.Dimensions.Count != 64) throw new InvalidOperationException("Identical document Dimensions changed.");
                return 1;
            }),
            new("document/stress/insert-delete", true, 1, false, async worker =>
            {
                var type = new DocumentTypeIdentity("QualificationChurn");
                var key = new DocumentKey("records", worker.ToString());
                StorageDimensionsQualification.Require(await this.providers[worker % 4].UpsertAsync(type, new()
                {
                    Key = key, Content = DimensionQualificationData.Payload,
                    ContentHash = DimensionQualificationData.ContentHash, StoredContentHash = DimensionQualificationData.ContentHash,
                    Dimensions = StorageDimensionsQualification.Require(DocumentDimensions.Create(Enumerable.Range(0, 8)
                        .Select(slot => KeyValuePair.Create<string, object>($"Churn_{slot}", slot))))
                }));
                var deleted = await this.providers[worker % 4].DeleteAsync(type, key);
                if (deleted.IsFailure) throw new InvalidOperationException("Inserted document was not deleted.");
                return 1;
            }),
            new("document/stress/registration-burst", true, 1, false, async worker =>
            {
                var serial = Interlocked.Increment(ref this.registrationSerial);
                var values = StorageDimensionsQualification.Require(DocumentDimensions.Create(Enumerable.Range(0, 8)
                    .Select(slot => KeyValuePair.Create<string, object>($"Burst_{serial}_{slot}", slot))));
                var info = StorageDimensionsQualification.Require(await this.providers[0].UpdateDimensionsAsync(new("QualificationRegistration"), new() { Key = new("records", worker.ToString()), Dimensions = values }));
                if (info.Dimensions.Count != 8) throw new InvalidOperationException("Document burst registration lost values.");
                return 1;
            })
        ];
    }

    public async Task<object> CleanupStressAsync()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        foreach (var container in new[] { "QualificationMaximum", "QualificationTypes", "QualificationRegistration", "QualificationDistinct" })
        {
            var deletes = Enumerable.Range(0, container == "QualificationDistinct" ? 160 : 8)
                .Select(owner => new DocumentDelete(new("records", owner.ToString()))).ToArray();
            var deleted = StorageDimensionsQualification.Require(await this.providers[0].DeleteManyAsync(new(container), deletes));
            if (deleted.Items.Count != deletes.Length) throw new InvalidOperationException("Document stress bulk cleanup failed.");
        }

        StorageDimensionsQualification.Require(await this.providers[0].UpsertAsync(new("QualificationRetention"), new()
        {
            Key = new("records", "expired"), Content = DimensionQualificationData.Payload, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            ContentHash = DimensionQualificationData.ContentHash, StoredContentHash = DimensionQualificationData.ContentHash,
            Dimensions = StorageDimensionsQualification.Require(DocumentDimensions.Create(new Dictionary<string, object> { ["Retained"] = true }))
        }));
        var swept = StorageDimensionsQualification.Require(await this.providers[0].SweepExpiredAsync(new() { DocumentType = new("QualificationRetention"), VisibilityCutoff = DateTimeOffset.UtcNow, BatchSize = 10, MaxBatches = 2 }));
        if (swept.DeletedCount != 1) throw new InvalidOperationException("Document stress retention did not clean the owner.");
        await using var scope = this.scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        var remainingValues = await context.Set<StorageDocumentDimension>().LongCountAsync(row => row.Name.Scope == "QualificationMaximum" || row.Name.Scope == "QualificationTypes" || row.Name.Scope == "QualificationRegistration" || row.Name.Scope == "QualificationDistinct" || row.Name.Scope == "QualificationRetention" || row.Name.Scope == "QualificationChurn");
        var retainedNames = await context.Set<StorageDocumentDimensionName>().LongCountAsync(row => row.Scope == "QualificationMaximum" || row.Scope == "QualificationTypes" || row.Scope == "QualificationRegistration" || row.Scope == "QualificationDistinct" || row.Scope == "QualificationRetention" || row.Scope == "QualificationChurn");
        if (remainingValues != 0 || retainedNames < 10240) throw new InvalidOperationException("Document stress cleanup lost dictionary invariants.");
        return new { RemainingValues = remainingValues, RetainedNames = retainedNames, RetentionDeletedOwners = swept.DeletedCount, CleanupMilliseconds = timer.Elapsed.TotalMilliseconds };
    }

    private async Task SeedStressOwnerAsync(string container, string name, DocumentDimensions dimensions)
    {
        StorageDimensionsQualification.Require(await this.providers[0].UpsertAsync(new(container), new()
        {
            Key = new("records", name), Content = DimensionQualificationData.Payload, Dimensions = dimensions,
            ContentHash = DimensionQualificationData.ContentHash, StoredContentHash = DimensionQualificationData.ContentHash
        }));
    }

    private static string MaximumName(int slot) => $"Stress_{slot:D3}_" + new string('N', 117);

    private static DocumentDimensions MaximumValues(bool alternate = false) => StorageDimensionsQualification.Require(DocumentDimensions.Create(
        Enumerable.Range(0, 64).Select(slot => KeyValuePair.Create<string, object>(MaximumName(slot), slot == 63 && alternate ? (MaximumText[0] == 'A' ? "B" : "A") + MaximumText[1..] : MaximumText))));
}
