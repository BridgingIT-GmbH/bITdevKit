// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.FileSystem;
using BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

public class CacheManifestTests
{
    [Fact]
    public async Task AbandonedMetadataTemporary_BlocksANewAttemptWithoutAdoptingOrDeletingItsBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "cache-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var transient = new ResourceBudget("transient", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("resident", 16 * CacheDefaults.MiB);
        try
        {
            using var authority = await FileSystemAuthority.AcquireAsync(new() { RootPath = root }, root, "authority", false, default);
            await CacheCheckedMetadata.WriteAsync(root, "manifest.json", new { Value = "current" }, authority, true, transient, resident, default);
            var abandoned = Path.Combine(root, "manifest.json.write.tmp");
            await File.WriteAllTextAsync(abandoned, "unresolved previous candidate");

            await Should.ThrowAsync<IOException>(async () => await CacheCheckedMetadata.WriteAsync(root, "manifest.json", new { Value = "new" },
                authority, true, transient, resident, default));

            (await File.ReadAllTextAsync(abandoned)).ShouldBe("unresolved previous candidate");
            Directory.EnumerateFiles(root, "*.tmp").Count().ShouldBe(1);
            using var current = await CacheCheckedMetadata.ReadAsync<Dictionary<string, string>>(Path.Combine(root, "manifest.json"), transient, resident, default);
            current.Value["Value"].ShouldBe("current");
        }
        finally { Directory.Delete(root, recursive: true); }

        transient.Used.ShouldBe(0);
    }

    [Fact]
    public async Task AtomicManifest_AcquiresOnlyItsCompleteChecksummedSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "cache-manifest-" + Guid.NewGuid().ToString("N"));
        FileSystemDurability.EnsureDirectory(root);
        var transient = new ResourceBudget("transient", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("resident", 16 * CacheDefaults.MiB);
        try
        {
            using var authority = await FileSystemAuthority.AcquireAsync(new() { RootPath = root }, root, "authority", false, default);
            var first = new CacheManifest { StoreId = Guid.NewGuid().ToString("N"), CollectionIdentity = "scope", Shard = 1,
                Generation = Guid.NewGuid().ToString("N"), Journal = Guid.NewGuid().ToString("N") };
            var bytes = await CacheCheckedMetadata.WriteAsync(root, "manifest.json", first, authority, true, transient, resident, default);
            using var acquired = await CacheCheckedMetadata.ReadAsync<CacheManifest>(Path.Combine(root, "manifest.json"), transient, resident, default);

            acquired.Value.Validate(first.StoreId, "scope", 1);
            acquired.Bytes.ShouldBe(bytes);
            acquired.Value.Generation.ShouldBe(first.Generation);
            await CacheCheckedMetadata.WriteAsync(root, "manifest.json", first with { Generation = Guid.NewGuid().ToString("N"), Sequence = 1 }, authority,
                true, transient, resident, default);
            acquired.Value.Generation.ShouldBe(first.Generation);
            transient.Used.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        transient.Used.ShouldBe(0);
        resident.Used.ShouldBe(0);
    }

    [Fact]
    public async Task CorruptedOrOversizedManifest_DoesNotReturnAnOlderReadableGeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), "cache-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "manifest.json");
        var transient = new ResourceBudget("transient", 16 * CacheDefaults.MiB);
        var resident = new ResourceBudget("resident", 16 * CacheDefaults.MiB);
        try
        {
            using var authority = await FileSystemAuthority.AcquireAsync(new() { RootPath = root }, root, "authority", false, default);
            await CacheCheckedMetadata.WriteAsync(root, "manifest.json", new { Value = "current" }, authority, true, transient, resident, default);
            await using (var damaged = new FileStream(path, FileMode.Open, FileAccess.Write))
            {
                damaged.Position = 49;
                damaged.WriteByte(0x7f);
            }

            (await Should.ThrowAsync<CacheTransferException>(async () =>
                await CacheCheckedMetadata.ReadAsync<CacheManifest>(path, transient, resident, default))).Code.ShouldBe(CacheErrorCode.Corruption);
            await using (var oversized = new FileStream(path, FileMode.Create, FileAccess.Write)) { oversized.SetLength(FileSystemCacheOptions.MaximumMetadataBytes + 49L); }

            (await Should.ThrowAsync<CacheTransferException>(async () =>
                await CacheCheckedMetadata.ReadAsync<CacheManifest>(path, transient, resident, default))).Code.ShouldBe(CacheErrorCode.Corruption);
        }
        finally { Directory.Delete(root, recursive: true); }

        transient.Used.ShouldBe(0);
    }
}