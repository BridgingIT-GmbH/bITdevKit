// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

public abstract class EntityFrameworkDocumentStoreProviderTestsBase
{
    public virtual async Task CountResultAsync_ReturnsDocumentCount()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.CountResultAsync<PersonStub>(
            DocumentQueries.Count()
                .AllowFullScan()
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ShouldBe(5);
    }

    public virtual async Task DeleteResultAsync_DeletesEntity()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var entity = new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        };

        // Act
        var upsertResult = await sut.UpsertResultAsync(documentKey, entity);
        var deleteResult = await sut.DeleteResultAsync<PersonStub>(documentKey);

        // Assert
        var result = await sut.ExistsResultAsync<PersonStub>(documentKey);
        upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        deleteResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, deleteResult.Errors.Select(e => e.Message)));
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.ShouldBeFalse();
    }

    public virtual async Task FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithFullMatch()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    public virtual async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithRowKeyPrefix()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    public virtual async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithRowKeySuffix()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    public virtual async Task FindPageResultAsync_WithoutFilter_ReturnsEntities()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .AllowFullScan()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.Any(e => e.FirstName.Equals("Mary" + ticks))
            .ShouldBeTrue();
    }

    public virtual async Task ExistsResultAsync_WithExactKey_ReturnsExpectedValue()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var upsertResult = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Mary" + ticks));

        // Act
        var existing = await sut.ExistsResultAsync<PersonStub>(documentKey);
        var missing = await sut.ExistsResultAsync<PersonStub>(new DocumentKey("partition", "missing" + ticks));

        // Assert
        upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        existing.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, existing.Errors.Select(e => e.Message)));
        missing.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, missing.Errors.Select(e => e.Message)));
        existing.Value.ShouldBeTrue();
        missing.Value.ShouldBeFalse();
    }

    public virtual async Task ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey("partition", "row" + ticks)
                .WithFullMatch()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.All(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    public virtual async Task ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        await this.SeedPeopleAsync(sut, ticks);

        // Act
        var result = await sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .AllowFullScan()
                .Take(10)
                .Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.Any(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    public virtual async Task UpsertResultAsync_CreatesOrUpdatesSingleLogicalRow()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var sut = this.CreateProvider();

        // Act
        var firstUpsert = await sut.UpsertResultAsync(documentKey, this.CreatePerson("John"));
        var secondUpsert = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Jane"));

        // Assert
        var result = await sut.GetResultAsync<PersonStub>(documentKey);
        firstUpsert.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, firstUpsert.Errors.Select(e => e.Message)));
        secondUpsert.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, secondUpsert.Errors.Select(e => e.Message)));
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.FirstName.ShouldBe("Jane");
    }

    public virtual async Task UpsertResultAsync_PopulatesLookupHashesAndClearsLease()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var sut = this.CreateProvider();

        // Act
        var upsertResult = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Mary" + ticks));

        // Assert
        upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        var row = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDocuments.SingleAsync(e => e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey));

        row.TypeHash.ShouldNotBeNullOrWhiteSpace();
        row.PartitionKeyHash.ShouldBe(HashHelper.ComputeSha256(documentKey.PartitionKey));
        row.RowKeyHash.ShouldBe(HashHelper.ComputeSha256(documentKey.RowKey));
        row.ContentHash.ShouldNotBeNullOrWhiteSpace();
        row.LockedBy.ShouldBeNull();
        row.LockedUntil.ShouldBeNull();
    }

    public virtual async Task UpsertResultAsync_WithConcurrentWriters_PreservesSingleLogicalDocument()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);

        // Act
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            var sut = this.CreateProvider(this.CreateConcurrentWriterOptions(), forceNew: true);
            var upsertResult = await sut.UpsertResultAsync(documentKey, this.CreatePerson("Writer" + index));
            upsertResult.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, upsertResult.Errors.Select(e => e.Message)));
        }));

        // Assert
        var rows = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDocuments
                .Where(e => e.PartitionKey == documentKey.PartitionKey && e.RowKey == documentKey.RowKey)
                .ToListAsync());

        rows.Count.ShouldBe(1);
        rows[0].LockedBy.ShouldBeNull();
        rows[0].LockedUntil.ShouldBeNull();
        rows[0].ContentHash.ShouldNotBeNullOrWhiteSpace();
    }

    public virtual async Task UpsertResultAsync_WithPartitionKeyLongerThan256_ReturnsFailure()
    {
        // Arrange
        await this.ResetStoreAsync();
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey(new string('p', StorageDocument.MaximumKeyLength + 1), "row");

        // Act
        var result = await sut.UpsertResultAsync(documentKey, this.CreatePerson("TooLong"));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    public virtual async Task UpsertResultAsync_WithRowKeyLongerThan256_ReturnsFailure()
    {
        // Arrange
        await this.ResetStoreAsync();
        var sut = this.CreateProvider();
        var documentKey = new DocumentKey("partition", new string('r', StorageDocument.MaximumKeyLength + 1));

        // Act
        var result = await sut.UpsertResultAsync(documentKey, this.CreatePerson("TooLong"));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    public virtual async Task GetManyAsync_WithKeysAcrossPartitionsAndChunks_ReturnsResultsInInputOrder()
    {
        foreach (var parallelism in new[] { 1, 4 })
        {
            await this.AssertGetManyAcrossPartitionsAndChunksAsync(parallelism);
        }
    }

    private async Task AssertGetManyAcrossPartitionsAndChunksAsync(int parallelism)
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var provider = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { GetManyBatchSize = 2, GetManyMaxParallelism = parallelism });
        var client = new DocumentStoreClient<PersonStub>(provider);
        var keys = Enumerable.Range(0, 7).Select(i => new DocumentKey(i % 2 == 0 ? "partition-a" : "partition-b", "row" + ticks + "-" + i)).ToArray();
        foreach (var (key, index) in keys.Select((k, i) => (k, i)))
        {
            (await client.UpsertAsync(key, this.CreatePerson("Person" + index))).IsSuccess.ShouldBeTrue();
        }

        var missing = new DocumentKey("partition-a", "missing" + ticks);
        var request = new[] { keys[6], missing, keys[1], keys[0], keys[1], keys[4], keys[3], keys[2], keys[5] };

        // Act
        var result = await client.GetManyAsync(request);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Count.ShouldBe(request.Length);
        result.Value[1].Errors.ShouldContain(x => x is DocumentStoreNotFoundError);
        for (var index = 0; index < request.Length; index++)
        {
            if (index == 1)
            {
                continue;
            }

            result.Value[index].IsSuccess.ShouldBeTrue();
            result.Value[index].Value.Key.ShouldBe(request[index]);
            result.Value[index].Value.Value.FirstName.ShouldBe("Person" + Array.IndexOf(keys, request[index]));
        }
    }

    public virtual async Task GetManyAsync_WithExpiredDocument_ReturnsNotFound()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var provider = this.CreateProvider();
        var client = new DocumentStoreClient<PersonStub>(provider);
        var live = new DocumentKey("partition", "live" + ticks);
        var expired = new DocumentKey("partition", "expired" + ticks);
        (await client.UpsertAsync(live, this.CreatePerson("Live"))).IsSuccess.ShouldBeTrue();
        (await client.UpsertAsync(expired, this.CreatePerson("Expired"), new() { Expiration = ExpirationChange.After(TimeSpan.FromMilliseconds(50)) })).IsSuccess.ShouldBeTrue();
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        // Act
        var result = await client.GetManyAsync([live, expired]);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value[0].IsSuccess.ShouldBeTrue();
        result.Value[1].Errors.ShouldContain(x => x is DocumentStoreNotFoundError);
    }

    public virtual async Task ListPageResultAsync_WalkingFullTypeScan_ReturnsEveryKeyOnceInOrder()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { AllowFullScans = true });
        var expected = new List<DocumentKey>();
        foreach (var partition in new[] { "p1", "p2", "p3" })
        {
            for (var row = 0; row < 5; row++)
            {
                var key = new DocumentKey(partition, $"r{ticks}-{row:D2}");
                (await sut.UpsertResultAsync(key, this.CreatePerson("Person"))).IsSuccess.ShouldBeTrue();
                expected.Add(key);
            }
        }

        // Act
        var seen = new List<DocumentKey>();
        string token = null;
        do
        {
            var builder = DocumentQueries.Query().AllowFullScan().Take(4);
            var page = await sut.ListPageResultAsync<PersonStub>(token is null ? builder.Build() : builder.ContinueWith(token).Build());
            page.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, page.Errors.Select(e => e.Message)));
            seen.AddRange(page.Value.Items);
            token = page.Value.ContinuationToken;
        }
        while (token is not null);

        // Assert
        seen.ShouldBe(expected);
    }

    public virtual async Task SweepExpiredAsync_RemovesOnlyExpiredDocuments()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var provider = this.CreateProvider();
        var client = new DocumentStoreClient<PersonStub>(provider);
        var keep = new DocumentKey("partition", "keep" + ticks);
        var keepWithExpiry = new DocumentKey("partition", "future" + ticks);
        (await client.UpsertAsync(keep, this.CreatePerson("Keep"))).IsSuccess.ShouldBeTrue();
        (await client.UpsertAsync(keepWithExpiry, this.CreatePerson("Future"), new() { Expiration = ExpirationChange.After(TimeSpan.FromHours(1)) })).IsSuccess.ShouldBeTrue();
        for (var index = 0; index < 3; index++)
        {
            (await client.UpsertAsync(new("partition", $"expired{ticks}-{index}"), this.CreatePerson("Expired"), new() { Expiration = ExpirationChange.After(TimeSpan.FromMilliseconds(20)) })).IsSuccess.ShouldBeTrue();
        }

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        // Act
        var result = await provider.SweepExpiredAsync(new()
        {
            DocumentType = DocumentTypeIdentity.For<PersonStub>(),
            VisibilityCutoff = DateTimeOffset.UtcNow,
            BatchSize = 2,
            MaxBatches = 5
        });

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.DeletedCount.ShouldBe(3);
        (await client.GetAsync(keep)).IsSuccess.ShouldBeTrue();
        (await client.GetAsync(keepWithExpiry)).IsSuccess.ShouldBeTrue();
    }

    public virtual async Task UpsertResultAsync_GeneratesDistinctRowIdsInInsertOrder()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var sut = this.CreateProvider();
        var keys = Enumerable.Range(0, 50).Select(i => new DocumentKey("partition", $"row{ticks}-{i:D3}")).ToArray();

        // Act
        foreach (var key in keys)
        {
            (await sut.UpsertResultAsync(key, this.CreatePerson("Person"))).IsSuccess.ShouldBeTrue();
        }

        // Assert
        var rows = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDocuments.AsNoTracking().Where(e => e.PartitionKey == "partition").ToListAsync());
        var ids = keys.Select(k => rows.Single(r => r.RowKey == k.RowKey).Id).ToArray();
        ids.ShouldNotContain(Guid.Empty);
        ids.Distinct().Count().ShouldBe(keys.Length);
        this.AssertRowIdsInInsertOrder(ids);
    }

    public virtual async Task UpsertManyAsync_WithInsertsOverwritesAndDuplicates_AppliesInOrder()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var client = new DocumentStoreClient<PersonStub>(this.CreateProvider(documentStoreOptions: new DocumentStoreOptions { GetManyBatchSize = 2 }));
        var existing = new DocumentKey("partition-a", "existing" + ticks);
        var fresh = new DocumentKey("partition-b", "fresh" + ticks);
        (await client.UpsertAsync(existing, this.CreatePerson("Old"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await client.UpsertManyAsync([
            new(existing, this.CreatePerson("Overwritten")),
            new(fresh, this.CreatePerson("First")),
            new(fresh, this.CreatePerson("Second")),
            new(new("partition-c", "other" + ticks), this.CreatePerson("Other"))]);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.FailedKey.ShouldBeNull();
        result.Value.Items.Count.ShouldBe(4);
        result.Value.Items.Select(x => x.Key).ShouldBe([existing, fresh, fresh, new("partition-c", "other" + ticks)]);
        result.Value.Items[1].ETag.ShouldNotBe(result.Value.Items[2].ETag);
        (await client.GetAsync(existing)).Value.Value.FirstName.ShouldBe("Overwritten");
        (await client.GetAsync(fresh)).Value.Value.FirstName.ShouldBe("Second");
        (await client.GetAsync(fresh)).Value.ETag.ShouldBe(result.Value.Items[2].ETag);
        var rows = await this.ExecuteDbContextAsync(async dbContext =>
            await dbContext.StorageDocuments.AsNoTracking().Where(e => e.RowKey == fresh.RowKey).ToListAsync());
        rows.Count.ShouldBe(1);
    }

    public virtual async Task UpsertManyAsync_WithCreateOnlyConflict_CommitsPrefixAndReportsFailedKey()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var client = new DocumentStoreClient<PersonStub>(this.CreateProvider());
        var first = new DocumentKey("partition", "a" + ticks);
        var conflict = new DocumentKey("partition", "b" + ticks);
        var last = new DocumentKey("partition", "c" + ticks);
        (await client.UpsertAsync(conflict, this.CreatePerson("Existing"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await client.UpsertManyAsync([
            new(first, this.CreatePerson("First")),
            new(conflict, this.CreatePerson("Conflict"), new() { CreateOnly = true }),
            new(last, this.CreatePerson("Last"))]);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Select(x => x.Key).ShouldBe([first]);
        result.Value.FailedKey.ShouldBe(conflict);
        (await client.GetAsync(first)).IsSuccess.ShouldBeTrue();
        (await client.GetAsync(conflict)).Value.Value.FirstName.ShouldBe("Existing");
        (await client.GetAsync(last)).IsFailure.ShouldBeTrue();
    }

    public virtual async Task UpsertManyAsync_WithStaleETag_CommitsPrefixAndReportsFailedKey()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var client = new DocumentStoreClient<PersonStub>(this.CreateProvider());
        var first = new DocumentKey("partition", "a" + ticks);
        var stale = new DocumentKey("partition", "b" + ticks);
        var last = new DocumentKey("partition", "c" + ticks);
        var created = await client.UpsertAsync(stale, this.CreatePerson("V1"));
        (await client.UpsertAsync(stale, this.CreatePerson("V2"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await client.UpsertManyAsync([
            new(first, this.CreatePerson("First")),
            new(stale, this.CreatePerson("Stale"), new() { IfMatchETag = created.Value.ETag }),
            new(last, this.CreatePerson("Last"))]);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Select(x => x.Key).ShouldBe([first]);
        result.Value.FailedKey.ShouldBe(stale);
        (await client.GetAsync(stale)).Value.Value.FirstName.ShouldBe("V2");
        (await client.GetAsync(last)).IsFailure.ShouldBeTrue();
    }

    public virtual async Task UpsertManyAsync_WithRowKeyLongerThan256_CommitsPrefixAndReportsFailedKey()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var client = new DocumentStoreClient<PersonStub>(this.CreateProvider());
        var first = new DocumentKey("partition", "a" + ticks);
        var tooLong = new DocumentKey("partition", new string('x', 257));

        // Act
        var result = await client.UpsertManyAsync([
            new(first, this.CreatePerson("First")),
            new(tooLong, this.CreatePerson("TooLong"))]);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Select(x => x.Key).ShouldBe([first]);
        result.Value.FailedKey.ShouldBe(tooLong);
        (await client.GetAsync(first)).IsSuccess.ShouldBeTrue();
    }

    public virtual async Task DeleteManyAsync_WithMissingKeyAndETagMismatch_DeletesPrefixAndReportsFailedKey()
    {
        // Arrange
        await this.ResetStoreAsync();
        var ticks = DateTime.UtcNow.Ticks;
        var client = new DocumentStoreClient<PersonStub>(this.CreateProvider());
        var first = new DocumentKey("partition", "a" + ticks);
        var missing = new DocumentKey("partition", "missing" + ticks);
        var guarded = new DocumentKey("partition", "guarded" + ticks);
        var last = new DocumentKey("partition", "last" + ticks);
        (await client.UpsertAsync(first, this.CreatePerson("First"))).IsSuccess.ShouldBeTrue();
        (await client.UpsertAsync(guarded, this.CreatePerson("Guarded"))).IsSuccess.ShouldBeTrue();
        (await client.UpsertAsync(last, this.CreatePerson("Last"))).IsSuccess.ShouldBeTrue();

        // Act
        var result = await client.DeleteManyAsync([
            new(first),
            new(missing),
            new(guarded, new() { IfMatchETag = Guid.NewGuid().ToString("N") }),
            new(last)]);

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.ShouldBe([first, missing]);
        result.Value.FailedKey.ShouldBe(guarded);
        (await client.GetAsync(first)).IsFailure.ShouldBeTrue();
        (await client.GetAsync(guarded)).IsSuccess.ShouldBeTrue();
        (await client.GetAsync(last)).IsSuccess.ShouldBeTrue();
    }

    // Providers whose generator is sequential override this. The default accepts random ids (SQLite).
    protected virtual void AssertRowIdsInInsertOrder(IReadOnlyList<Guid> ids)
    {
    }

    protected virtual async Task ResetStoreAsync()
    {
        await this.ExecuteDbContextAsync(async dbContext =>
        {
            EnsureDocumentStoreTableCreated(dbContext);

            var rows = await dbContext.StorageDocuments.ToListAsync();
            if (rows.Count == 0)
            {
                return;
            }

            dbContext.StorageDocuments.RemoveRange(rows);
            await dbContext.SaveChangesAsync();
        });
    }

    protected abstract Task ExecuteDbContextAsync(Func<StubDbContext, Task> action);

    protected abstract Task<TResult> ExecuteDbContextAsync<TResult>(Func<StubDbContext, Task<TResult>> action);

    protected virtual EntityFrameworkDocumentStoreProviderOptions CreateConcurrentWriterOptions() => null;

    protected virtual EntityFrameworkDocumentStoreProvider<StubDbContext> CreateProvider(
        EntityFrameworkDocumentStoreProviderOptions options = null,
        bool forceNew = false,
        DocumentStoreOptions documentStoreOptions = null)
    {
        return null;
    }

    private static void EnsureDocumentStoreTableCreated(StubDbContext dbContext)
    {
        dbContext.Database.EnsureCreated();

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var tableName = dbContext.Model.FindEntityType(typeof(StorageDocument))?.GetTableName();
        if (TableExists(dbContext, tableName))
        {
            if (!HasColumn(dbContext, tableName, "ConcurrencyVersion") ||
                !HasColumn(dbContext, tableName, "TypeHash") ||
                !HasColumn(dbContext, tableName, "PartitionKeyHash") ||
                !HasColumn(dbContext, tableName, "RowKeyHash") ||
                !HasColumn(dbContext, tableName, "StoredContentHash") ||
                !HasColumn(dbContext, tableName, "ExpiresAtUnixMilliseconds") ||
                !HasColumn(dbContext, tableName, "TransformMetadata"))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
            }

            return;
        }

        dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    private static bool TableExists(DbContext dbContext, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return true;
        }

        var providerName = dbContext.Database.ProviderName;
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = providerName switch
            {
                { } name when name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sys.tables WHERE name = @name",
                { } name when name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM information_schema.tables WHERE table_name = @name",
                { } name when name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name",
                _ => null
            };

            if (command.CommandText is null)
            {
                return true;
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return command.ExecuteScalar() is not null;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static bool HasColumn(DbContext dbContext, string tableName, string columnName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName))
        {
            return false;
        }

        var providerName = dbContext.Database.ProviderName;
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = providerName switch
            {
                { } name when name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@table) AND name = @column",
                { } name when name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) =>
                    "SELECT 1 FROM information_schema.columns WHERE table_name = @table AND column_name = @column",
                { } name when name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) =>
                    $"PRAGMA table_info(\"{tableName}\")",
                _ => null
            };

            if (command.CommandText is null)
            {
                return true;
            }

            if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@table";
            tableParameter.Value = providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? $"dbo.{tableName}"
                : tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "@column";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            return command.ExecuteScalar() is not null;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private PersonStub CreatePerson(string firstName) => new()
    {
        Id = Guid.NewGuid(),
        Nationality = "USA",
        FirstName = firstName,
        LastName = "Doe",
        Age = 18
    };

    private async Task SeedPeopleAsync(EntityFrameworkDocumentStoreProvider<StubDbContext> sut, long ticks)
    {
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks), this.CreatePerson("Mary" + ticks))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
        (await sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"), this.CreatePerson("John"))).IsSuccess.ShouldBeTrue();
    }
}
