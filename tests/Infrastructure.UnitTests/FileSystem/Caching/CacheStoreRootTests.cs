// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheStoreRootTests
{
    [Fact]
    public async Task Root_PersistsIdentityAndProfileAndCreatesNoUnusedShards()
    {
        var path = Path.Combine(Path.GetTempPath(), "cache-root-" + Guid.NewGuid().ToString("N"));
        var transient = new ResourceBudget("transient", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("resident", 16 * CacheDefaults.MiB);
        var profile = new FileSystemCacheOptions(new() { Kind = "FileSystem", RootPath = path });
        var sut = new CacheStoreRoot(profile, transient, resident);
        Directory.Exists(path).ShouldBeFalse();
        try
        {
            using var first = await sut.OpenAsync(default);
            using var second = await new CacheStoreRoot(profile, transient, resident).OpenAsync(default);
            var a = new CacheEntryIdentity("site", "files", "1", "../not-a-path");
            var b = new CacheEntryIdentity("SITE", "FILES", "2", "../NOT-A-PATH");

            first.Value.StoreId.ShouldBe(second.Value.StoreId);
            first.Value.Shards.ShouldBe(256);
            CacheStoreRoot.Shard(a).ShouldBe(CacheStoreRoot.Shard(b));
            sut.ShardDirectory(a).ShouldBe(sut.ShardDirectory(b));
            sut.ShardDirectory(a).ShouldNotContain("not-a-path", Case.Insensitive);
            Directory.Exists(Path.Combine(path, "collections")).ShouldBeFalse();
            var mismatch = new CacheStoreRoot(new(new() { RootPath = path, Durability = "Buffered" }), transient, resident);
            (await Should.ThrowAsync<CacheTransferException>(async () => await mismatch.OpenAsync(default))).Code.ShouldBe(CacheErrorCode.Compatibility);
            Should.Throw<CacheTransferException>(() => sut.Resolve("..", "outside"));
        }
        finally { Directory.Delete(path, recursive: true); }

        transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task ExistingUnrelatedRoot_IsNotAdoptedAsCacheFormat()
    {
        var path = Path.Combine(Path.GetTempPath(), "cache-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "existing.txt"), "preserve");
        var sut = new CacheStoreRoot(new(new() { RootPath = path }), new("transient", 16 * CacheDefaults.MiB), new("resident", 16 * CacheDefaults.MiB));
        try
        {
            (await Should.ThrowAsync<CacheTransferException>(async () => await sut.OpenAsync(default))).Code.ShouldBe(CacheErrorCode.Compatibility);
            (await File.ReadAllTextAsync(Path.Combine(path, "existing.txt"))).ShouldBe("preserve");
            File.Exists(Path.Combine(path, "cache-root.json")).ShouldBeFalse();
        }
        finally { Directory.Delete(path, recursive: true); }
    }
}