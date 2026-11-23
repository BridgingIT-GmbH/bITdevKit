// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Storage;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbPermalinkTests
{
    [Fact]
    public async Task Identity_UniquenessMoveAndDelete_FollowStorageEventOrder()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Permalinks;
        var source = StorageResourceLocation.ForFile("files", "source");
        var target = StorageResourceLocation.ForFile("files", "target");
        var first = (await sut.GetOrCreateAsync(source, occurredAt: host.Clock.Now)).Value;
        (await sut.GetOrCreateAsync(source)).Value.Id.ShouldBe(first.Id);
        var moved = await sut.MoveAsync(source, target, host.Clock.Now.AddSeconds(1));
        moved.IsSuccess.ShouldBeTrue();
        moved.Value.Id.ShouldBe(first.Id);
        (await sut.GetByLocationAsync(source)).IsFailure.ShouldBeTrue();
        (await sut.GetByIdAsync(first.Id)).Value.Location.ShouldBe(target);
        await sut.DeleteByLocationAsync(target, host.Clock.Now.AddSeconds(2));
        (await sut.GetOrCreateAsync(target, occurredAt: host.Clock.Now)).IsFailure.ShouldBeTrue();
        (await sut.GetByIdAsync(first.Id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }

    [Fact]
    public async Task PrefixMoves_PreserveDescendantIdentities_AndFenceDelayedEvents()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Permalinks;
        var root = StorageResourceLocation.ForFile("files", "tree");
        var child = StorageResourceLocation.ForFile("files", "tree/child/file");
        var sibling = StorageResourceLocation.ForFile("files", "tree-other/file");
        var first = (await sut.GetOrCreateAsync(child)).Value;
        await sut.GetOrCreateAsync(sibling);
        (await sut.MovePrefixAsync(root, StorageResourceLocation.ForFile("files", "moved"), host.Clock.Now.AddSeconds(1))).Value.ShouldBe(1);
        (await sut.GetByIdAsync(first.Id)).Value.Location.Path.ShouldBe("moved/child/file");
        (await sut.GetByLocationAsync(sibling)).IsSuccess.ShouldBeTrue();
        (await sut.GetOrCreateAsync(child, occurredAt: host.Clock.Now)).IsFailure.ShouldBeTrue();
        (await sut.DeletePrefixAsync(StorageResourceLocation.ForFile("files", "moved"), host.Clock.Now.AddSeconds(2))).Value.ShouldBe(1);
    }

    [Fact]
    public async Task ExpirationAndConditionalMutation_UsePersistedVersions()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Permalinks;
        var first = (await sut.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "file"))).Value;
        var updated = (await sut.UpdateExpirationAsync(first.Id, new() { ExpiresAt = host.Clock.Now, IfMatchETag = first.ETag })).Value;
        updated.Status.ShouldBe(StoragePermalinkStatus.Expired);
        (await sut.DeleteAsync(first.Id, new() { IfMatchETag = first.ETag })).IsFailure.ShouldBeTrue();
        (await sut.ListPageAsync(new() { Status = StoragePermalinkStatus.Expired })).Value.Items.Single().Id.ShouldBe(first.Id);
        (await sut.ListPageAsync(new() { ContinuationToken = "invalid" })).IsFailure.ShouldBeTrue();
        (await sut.DeleteAsync(first.Id, new() { IfMatchETag = updated.ETag })).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Reopen_PreservesIdentitiesAndPrefixDeletionFences()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-permalink-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "links.db");
            StoragePermalinkId id;
            var location = StorageResourceLocation.ForFile("files", "tree/file");
            await using (var host = new LiteDbStorageTestHost(file))
            {
                id = (await host.Permalinks.GetOrCreateAsync(location)).Value.Id;
                await host.Permalinks.DeletePrefixAsync(StorageResourceLocation.ForFile("files", "tree"), host.Clock.Now);
            }

            await using (var host = new LiteDbStorageTestHost(file))
            {
                (await host.Permalinks.GetByIdAsync(id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
                (await host.Permalinks.GetOrCreateAsync(StorageResourceLocation.ForFile("files", "tree/new"), occurredAt: host.Clock.Now.AddSeconds(-1))).IsFailure.ShouldBeTrue();
            }
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task ExistingFileBehavior_PreservesLinkIdentityThroughRenameAndDelete()
    {
        await using var host = new LiteDbStorageTestHost();
        var queue = host.Services.GetRequiredService<StoragePermalinkChangeQueue>();
        queue.Complete(); // Exercise the existing immediate fallback dispatcher in this unstarted test host.
        var files = host.Files("linked");
        (await files.WriteFileAsync("source", new MemoryStream([1]))).IsSuccess.ShouldBeTrue();
        var link = (await host.Permalinks.GetByLocationAsync(StorageResourceLocation.ForFile("linked", "source"))).Value;
        (await files.MoveFileAsync("source", "target")).IsSuccess.ShouldBeTrue();
        (await host.Permalinks.GetByIdAsync(link.Id)).Value.Location.Path.ShouldBe("target");
        (await files.DeleteFileAsync("target")).IsSuccess.ShouldBeTrue();
        (await host.Permalinks.GetByIdAsync(link.Id)).Value.Status.ShouldBe(StoragePermalinkStatus.Deleted);
    }
}
