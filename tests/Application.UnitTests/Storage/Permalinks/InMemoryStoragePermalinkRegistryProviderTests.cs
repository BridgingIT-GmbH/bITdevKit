// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.UnitTests.Storage.Permalinks;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Time.Testing;

[UnitTest("Application")]
public sealed class InMemoryStoragePermalinkRegistryProviderTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task GetOrCreateAsync_WithSameLocation_ReturnsStableIdentifier()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var location = StorageResourceLocation.ForBlob("reports", new("public", "report.pdf"));

        var first = await sut.GetOrCreateAsync(location);
        var second = await sut.GetOrCreateAsync(location);

        first.IsSuccess.ShouldBeTrue();
        second.Value.Id.ShouldBe(first.Value.Id);
        first.Value.Id.Value.Length.ShouldBe(StoragePermalinkId.Length);
    }

    [Fact]
    public async Task MoveAsync_WithExistingSource_PreservesIdentifierAndExpiration()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var source = StorageResourceLocation.ForFile("files", "incoming/report.pdf");
        var target = StorageResourceLocation.ForFile("files", "archive/report.pdf");
        var created = await sut.GetOrCreateAsync(source, new() { ExpiresAt = this.time.GetUtcNow().AddDays(1) });

        var moved = await sut.MoveAsync(source, target, this.time.GetUtcNow().AddMinutes(1));

        moved.IsSuccess.ShouldBeTrue();
        moved.Value.Id.ShouldBe(created.Value.Id);
        moved.Value.ExpiresAt.ShouldBe(created.Value.ExpiresAt);
        (await sut.GetByLocationAsync(source)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOrCreateAsync_WithDelayedUpsertAfterDelete_ReturnsConflict()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var location = StorageResourceLocation.ForFile("files", "report.pdf");
        var mutation = this.time.GetUtcNow();
        await sut.GetOrCreateAsync(location, occurredAt: mutation);
        await sut.DeleteByLocationAsync(location, mutation.AddMinutes(1));

        var result = await sut.GetOrCreateAsync(location, occurredAt: mutation);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(x => x is StoragePermalinkConflictError);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithNewUpsertAfterDelete_CreatesNewIdentifier()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var location = StorageResourceLocation.ForFile("files", "report.pdf");
        var first = await sut.GetOrCreateAsync(location, occurredAt: this.time.GetUtcNow());
        await sut.DeleteByLocationAsync(location, this.time.GetUtcNow().AddMinutes(1));

        var second = await sut.GetOrCreateAsync(location, occurredAt: this.time.GetUtcNow().AddMinutes(2));

        second.IsSuccess.ShouldBeTrue();
        second.Value.Id.ShouldNotBe(first.Value.Id);
    }

    [Fact]
    public async Task UpdateExpirationAsync_WithPastExpiration_ReportsExpired()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var created = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "report.pdf"));

        var result = await sut.UpdateExpirationAsync(created.Value.Id, new() { ExpiresAt = this.time.GetUtcNow().AddSeconds(-1), IfMatchETag = created.Value.ETag });

        result.Value.Status.ShouldBe(StoragePermalinkStatus.Expired);
    }

    [Fact]
    public async Task MoveAsync_AfterExpirationMaintenance_UsesStorageChangeOrdering()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var source = StorageResourceLocation.ForFile("files", "incoming/report.pdf");
        var target = StorageResourceLocation.ForFile("files", "archive/report.pdf");
        var createdAt = this.time.GetUtcNow();
        var created = await sut.GetOrCreateAsync(source, occurredAt: createdAt);
        this.time.Advance(TimeSpan.FromMinutes(10));
        await sut.UpdateExpirationAsync(created.Value.Id, new() { ExpiresAt = this.time.GetUtcNow().AddDays(1) });

        var moved = await sut.MoveAsync(source, target, createdAt.AddMinutes(5));

        moved.IsSuccess.ShouldBeTrue(string.Join(" | ", moved.Errors.Select(x => x.Message)));
        moved.Value.Id.ShouldBe(created.Value.Id);
        (await sut.GetByLocationAsync(target)).Value.Id.ShouldBe(created.Value.Id);
    }

    [Fact]
    public async Task ListPageAsync_WithBoundedPage_ReturnsContinuation()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "a.txt"));
        await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "b.txt"));

        var first = await sut.ListPageAsync(new() { Take = 1 });
        var second = await sut.ListPageAsync(new() { Take = 1, ContinuationToken = first.Value.ContinuationToken });

        first.Value.Items.Count.ShouldBe(1);
        first.Value.ContinuationToken.ShouldNotBeNull();
        second.Value.Items.Single().Id.ShouldNotBe(first.Value.Items.Single().Id);
    }

    [Fact]
    public async Task DeleteBeforeDelayedUpsert_LeavesOrderingTombstone()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var location = StorageResourceLocation.ForBlob("reports", new("public", "report.pdf"));
        var deletedAt = this.time.GetUtcNow().AddMinutes(2);

        await sut.DeleteByLocationAsync(location, deletedAt);
        var result = await sut.GetOrCreateAsync(location, occurredAt: deletedAt.AddMinutes(-1));

        result.Errors.ShouldContain(x => x is StoragePermalinkConflictError);
    }

    [Fact]
    public async Task PrefixDeleteBeforeDelayedChildUpsert_LeavesOrderingTombstone()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var prefix = StorageResourceLocation.ForFile("files", "archive");
        var deletedAt = this.time.GetUtcNow().AddMinutes(2);

        await sut.DeletePrefixAsync(prefix, deletedAt);
        var result = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"), occurredAt: deletedAt.AddMinutes(-1));

        result.Errors.ShouldContain(x => x is StoragePermalinkConflictError);
    }

    [Fact]
    public async Task DelayedDelete_DoesNotRemoveNewerMapping()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var location = StorageResourceLocation.ForFile("files", "report.pdf");
        var createdAt = this.time.GetUtcNow().AddMinutes(2);
        var created = await sut.GetOrCreateAsync(location, occurredAt: createdAt);

        await sut.DeleteByLocationAsync(location, createdAt.AddMinutes(-1));

        (await sut.GetByLocationAsync(location)).Value.Id.ShouldBe(created.Value.Id);
    }

    [Fact]
    public async Task MovePrefix_WithExistingDestination_PreservesSourceIdentifierAndTombstonesDestination()
    {
        var sut = new InMemoryStoragePermalinkRegistryProvider(this.time);
        var source = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "incoming/report.pdf"), occurredAt: this.time.GetUtcNow());
        var destination = await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"), occurredAt: this.time.GetUtcNow());

        var result = await sut.MovePrefixAsync(StorageResourceLocation.ForFile("files", "incoming"), StorageResourceLocation.ForFile("files", "archive"), this.time.GetUtcNow().AddMinutes(1));

        result.Value.ShouldBe(1);
        (await sut.GetByLocationAsync(StorageResourceLocation.ForFile("files", "archive/report.pdf"))).Value.Id.ShouldBe(source.Value.Id);
        (await sut.GetByIdAsync(destination.Value.Id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }
}
