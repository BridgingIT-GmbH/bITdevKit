// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using Application.Storage;

[IntegrationTest("Application")]
public class InMemoryDocumentStoreProviderTests(ITestOutputHelper output) : TestsBase(output)
{
    private readonly IDocumentStoreClient<PersonStub> sut = new DocumentStoreClient<PersonStub>(new InMemoryDocumentStoreProvider(XunitLoggerFactory.Create(output)), options: new() { AllowFullScans = true });

    [Fact]
    public async Task FindPageAsync_WithContinuation_ReturnsMetadataEntriesInOrder()
    {
        var partition = "people-" + Guid.NewGuid().ToString("N");
        foreach (var row in new[] { "001", "002", "003" }) await this.sut.UpsertAsync(new(partition, row), Person(row));
        var first = await this.sut.FindPageAsync(DocumentQueries.Query().ForKey(partition, "00").WithRowKeyPrefix().Take(2).Build());
        var second = await this.sut.FindPageAsync(DocumentQueries.Query().ForKey(partition, "00").WithRowKeyPrefix().Take(2).ContinueWith(first.Value.ContinuationToken).Build());

        first.IsSuccess.ShouldBeTrue();
        first.Value.Items.Select(x => x.Value.FirstName).ShouldBe(["First001", "First002"]);
        first.Value.Items.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.ETag) && x.ContentHash.StartsWith("sha256:"));
        second.Value.Items.Select(x => x.Value.FirstName).ShouldBe(["First003"]);
    }

    [Fact]
    public async Task ConditionalWriteAndDelete_WithChangedEtag_ReturnConflict()
    {
        var key = new DocumentKey("people", Guid.NewGuid().ToString("N"));
        var created = await this.sut.UpsertAsync(key, Person("one"));
        await this.sut.UpsertAsync(key, Person("two"), new() { IfMatchETag = created.Value.ETag });

        var staleWrite = await this.sut.UpsertAsync(key, Person("three"), new() { IfMatchETag = created.Value.ETag });
        var staleDelete = await this.sut.DeleteAsync(key, new() { IfMatchETag = created.Value.ETag });

        staleWrite.Errors.ShouldContain(x => x is DocumentStoreConflictError);
        staleDelete.Errors.ShouldContain(x => x is DocumentStoreConflictError);
    }

    [Fact]
    public async Task Expiration_IsLogicallyInvisible_AndCanBeRevived()
    {
        var key = new DocumentKey("people", Guid.NewGuid().ToString("N"));
        var created = await this.sut.UpsertAsync(key, Person("expired"), new() { Expiration = ExpirationChange.At(DateTimeOffset.UtcNow.AddMilliseconds(10)) });
        await Task.Delay(30);

        (await this.sut.ExistsAsync(key)).Value.ShouldBeFalse();
        (await this.sut.GetAsync(key)).Errors.ShouldContain(x => x is DocumentStoreNotFoundError);

        var revived = await this.sut.UpdatePropertiesAsync(new(key) { IfMatchETag = created.Value.ETag, Expiration = ExpirationChange.Clear });
        revived.IsSuccess.ShouldBeTrue();
        (await this.sut.ExistsAsync(key)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task FindPageAsync_ContinuationUsesFirstPageVisibilityCutoff()
    {
        var now = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new AdjustableTimeProvider(now);
        var client = new DocumentStoreClient<PersonStub>(
            new InMemoryDocumentStoreProvider(),
            options: new() { AllowFullScans = true },
            timeProvider: timeProvider);
        var partition = "snapshot-" + Guid.NewGuid().ToString("N");
        foreach (var row in new[] { "001", "002", "003" })
        {
            await client.UpsertAsync(new(partition, row), Person(row), new()
            {
                Expiration = ExpirationChange.At(now.AddMinutes(1))
            });
        }

        var query = DocumentQueries.Query().ForKey(partition, "00").WithRowKeyPrefix().Take(2).Build();
        var first = await client.FindPageAsync(query);
        timeProvider.Advance(TimeSpan.FromMinutes(2));
        var second = await client.FindPageAsync(DocumentQueries.Query()
            .ForKey(partition, "00").WithRowKeyPrefix().Take(2)
            .ContinueWith(first.Value.ContinuationToken).Build());

        second.Value.Items.Select(x => x.Key.RowKey).ShouldBe(["003"]);
        (await client.FindPageAsync(query)).Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListPageAsync_ContinuationCannotBeUsedForFind()
    {
        var partition = "tokens-" + Guid.NewGuid().ToString("N");
        foreach (var row in new[] { "001", "002" }) await this.sut.UpsertAsync(new(partition, row), Person(row));
        var first = await this.sut.ListPageAsync(DocumentQueries.Query().ForKey(partition, "00").WithRowKeyPrefix().Take(1).Build());

        var result = await this.sut.FindPageAsync(DocumentQueries.Query().ForKey(partition, "00").WithRowKeyPrefix().Take(1).ContinueWith(first.Value.ContinuationToken).Build());

        result.Errors.ShouldContain(x => x is DocumentStoreContinuationTokenQueryMismatchError);
    }

    [Fact]
    public async Task SweepExpiredAsync_RemovesPhysicalRowsInBoundedBatches()
    {
        var provider = new InMemoryDocumentStoreProvider();
        var client = new DocumentStoreClient<PersonStub>(provider, options: new() { AllowFullScans = true });
        var cutoff = DateTimeOffset.UtcNow;
        foreach (var row in new[] { "001", "002", "003" })
        {
            await client.UpsertAsync(new("retention", row), Person(row), new() { Expiration = ExpirationChange.At(cutoff.AddMinutes(-1)) });
        }

        var result = await provider.SweepExpiredAsync(new()
        {
            DocumentType = DocumentTypeIdentity.For<PersonStub>(),
            VisibilityCutoff = cutoff,
            BatchSize = 2,
            MaxBatches = 1
        });

        result.Value.DeletedCount.ShouldBe(2);
        result.Value.BatchCount.ShouldBe(1);
        result.Value.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task CopyAsync_AcrossClients_PreservesPropertiesAndExpiration()
    {
        var source = new DocumentStoreClient<PersonStub>(new InMemoryDocumentStoreProvider());
        var target = new DocumentStoreClient<PersonStub>(new InMemoryDocumentStoreProvider());
        var sourceKey = new DocumentKey("source", Guid.NewGuid().ToString("N"));
        var targetKey = new DocumentKey("target", Guid.NewGuid().ToString("N"));
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var properties = new PropertyBag();
        properties.Set("region", "eu");
        await source.UpsertAsync(sourceKey, Person("copy"), new()
        {
            Expiration = ExpirationChange.At(expiresAt),
            Properties = properties
        });

        var result = await source.CopyAsync(sourceKey, target, targetKey);
        var copied = await target.GetAsync(targetKey);

        result.IsSuccess.ShouldBeTrue();
        copied.Value.Value.FirstName.ShouldBe("Firstcopy");
        copied.Value.Properties.Get<string>("region").ShouldBe("eu");
        copied.Value.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public async Task MoveAsync_ToSameClientAndKey_IsExistenceCheckedNoOp()
    {
        var key = new DocumentKey("people", Guid.NewGuid().ToString("N"));
        await this.sut.UpsertAsync(key, Person("same"));

        var result = await this.sut.MoveAsync(key, this.sut, key);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SourceDeleted.ShouldBeFalse();
        (await this.sut.ExistsAsync(key)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnumerateKeysAsync_StopsAtMandatoryMaximum()
    {
        var partition = "bounded-" + Guid.NewGuid().ToString("N");
        foreach (var row in new[] { "001", "002", "003" })
        {
            await this.sut.UpsertAsync(new(partition, row), Person(row));
        }

        var keys = new List<DocumentKey>();
        await foreach (var key in this.sut.EnumerateKeysAsync(
            DocumentQueries.Query().ForKey(partition, "00").WithRowKeyPrefix().Take(1).Build(),
            new() { MaxItems = 2 }))
        {
            keys.Add(key);
        }

        keys.Select(key => key.RowKey).ShouldBe(["001", "002"]);
    }

    [Fact]
    public async Task DeleteByQueryAsync_WhenDocumentChangesBeforeDelete_ReportsConflictAndRetainsDocument()
    {
        var key = new DocumentKey("maintenance", Guid.NewGuid().ToString("N"));
        await this.sut.UpsertAsync(key, Person("before"));
        var racing = new RacingDeleteClient(this.sut, key);

        var result = await racing.DeleteByQueryAsync(
            DocumentQueries.Query().ForKey(key).WithFullMatch().Take(1).Build(),
            new() { MaxItems = 1 });
        var current = await this.sut.GetAsync(key);

        result.Value.FailedKeys.ShouldBe([key]);
        current.IsSuccess.ShouldBeTrue();
        current.Value.Value.FirstName.ShouldBe("Firstchanged");
    }

    private static PersonStub Person(string value) => new() { Id = Guid.NewGuid(), Country = "USA", FirstName = "First" + value, LastName = "Last" + value, Age = 20 };

    private sealed class AdjustableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset value = value;

        public override DateTimeOffset GetUtcNow() => this.value;

        public void Advance(TimeSpan duration) => this.value = this.value.Add(duration);
    }

    private sealed class RacingDeleteClient(IDocumentStoreClient<PersonStub> inner, DocumentKey raceKey)
        : DocumentStoreClientBehaviorBase<PersonStub>(inner)
    {
        public override async Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
        {
            if (key == raceKey)
            {
                await this.Inner.UpsertAsync(key, Person("changed"), cancellationToken: cancellationToken);
            }
            return await base.DeleteAsync(key, options, cancellationToken);
        }
    }
}
