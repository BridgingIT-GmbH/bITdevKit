// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Storage;

using System.Text;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbDocumentAndCacheTests
{
    [Fact]
    public async Task Documents_PreserveExactKeys_DetachPayloads_AndFenceMutations()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Documents;
        var type = new DocumentTypeIdentity("ledger");
        var source = Write("A", "a", "one");
        var initial = await sut.UpsertAsync(type, source);
        initial.IsSuccess.ShouldBeTrue();
        await sut.UpsertAsync(type, Write("a", "A", "two"));
        var read = (await sut.GetAsync(type, source.Key, host.Clock.Now)).Value;
        Encoding.UTF8.GetString(read.Content).ShouldBe("one");
        (await sut.GetAsync(type, new("A", "A"), host.Clock.Now)).IsFailure.ShouldBeTrue();
        var changed = await sut.UpsertAsync(type, source with { Options = new() { IfMatchETag = initial.Value.ETag }, Content = Encoding.UTF8.GetBytes("next") });
        changed.IsSuccess.ShouldBeTrue();
        (await sut.UpsertAsync(type, source with { Options = new() { IfMatchETag = initial.Value.ETag } })).IsFailure.ShouldBeTrue();
        (await sut.DeleteAsync(type, source.Key, new() { IfMatchETag = initial.Value.ETag })).IsFailure.ShouldBeTrue();
        (await sut.UpsertAsync(type, source with { Options = new() { CreateOnly = true } })).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task NativePaging_CountsAndProjectsKeys_WithBoundContinuationsAndOrdinalOrder()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Documents;
        var type = new DocumentTypeIdentity("ledger");
        foreach (var row in new[] { "a1", "A2", "a2", "a3", "other" }) await sut.UpsertAsync(type, Write("P", row, row));
        var query = new DocumentQuery { DocumentKey = new("P", "a"), Filter = DocumentKeyFilter.RowKeyPrefixMatch, Take = 2 };
        var first = await sut.FindPageAsync(type, query, host.Clock.Now);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Items.Select(item => item.Key.RowKey).ShouldBe(["a1", "a2"]);
        var final = await sut.FindPageAsync(type, new DocumentQuery { DocumentKey = query.DocumentKey, Filter = query.Filter, Take = 2, ContinuationToken = first.Value.ContinuationToken }, host.Clock.Now);
        final.Value.Items.Single().Key.RowKey.ShouldBe("a3");
        (await sut.FindPageAsync(type, new DocumentQuery { DocumentKey = query.DocumentKey, Filter = query.Filter, Take = 1, ContinuationToken = first.Value.ContinuationToken }, host.Clock.Now)).IsFailure.ShouldBeTrue();
        var keys = await sut.ListPageAsync(type, query, host.Clock.Now);
        keys.IsSuccess.ShouldBeTrue();
        keys.Value.Items.Select(item => item.RowKey).ShouldBe(["a1", "a2"]);
        (await sut.CountAsync(type, new() { DocumentKey = new("P", "a"), Filter = DocumentKeyFilter.RowKeyPrefixMatch }, host.Clock.Now)).Value.ShouldBe(3);
        (await sut.ExistsAsync(type, new("P", "A2"), host.Clock.Now)).Value.ShouldBeTrue();
        (await sut.ExistsAsync(new("LEDGER"), new("P", "A2"), host.Clock.Now)).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExpirationAndMetadata_AreAtomic_AndRetentionIsBounded()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Documents;
        var type = new DocumentTypeIdentity("ledger");
        var write = Write("P", "one", "content") with { ExpiresAt = host.Clock.Now, PreserveExpiration = false };
        var stored = (await sut.UpsertAsync(type, write)).Value;
        (await sut.GetAsync(type, write.Key, host.Clock.Now)).IsFailure.ShouldBeTrue();
        (await sut.UpsertAsync(type, write with { Options = new() { CreateOnly = true } })).IsFailure.ShouldBeTrue();
        var update = await sut.UpdatePropertiesAsync(type, new(write.Key) { IfMatchETag = stored.ETag, Properties = PropertyBagForTest() }, host.Clock.Now.AddHours(1), false);
        update.IsSuccess.ShouldBeTrue();
        var visible = (await sut.GetAsync(type, write.Key, host.Clock.Now)).Value;
        Encoding.UTF8.GetString(visible.Content).ShouldBe("content");
        visible.Properties.Get<string>("label").ShouldBe("updated");
        await sut.UpsertAsync(type, Write("P", "old", "expired") with { ExpiresAt = host.Clock.Now, PreserveExpiration = false });
        var sweep = await sut.SweepExpiredAsync(new() { DocumentType = type, VisibilityCutoff = host.Clock.Now, BatchSize = 1, MaxBatches = 1 });
        sweep.Value.DeletedCount.ShouldBe(1);
        (await sut.GetAsync(type, write.Key, host.Clock.Now)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Cache_RenewsSlidingExpiration_WithoutExceedingAbsoluteCap()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Cache;
        await sut.SetAsync("key", [1, 2, 3], new() { SlidingExpiration = TimeSpan.FromMinutes(5), AbsoluteExpiration = host.Clock.Now.AddMinutes(8) });
        host.Clock.Now = host.Clock.Now.AddMinutes(4);
        (await sut.GetAsync("key")).ShouldBe([1, 2, 3]);
        host.Clock.Now = host.Clock.Now.AddMinutes(3);
        (await sut.GetAsync("key")).ShouldBe([1, 2, 3]);
        host.Clock.Now = host.Clock.Now.AddMinutes(1);
        (await sut.GetAsync("key")).ShouldBeNull();
    }

    [Fact]
    public async Task Cache_RelativeAndSlidingOnlyEntries_ExpireAtTheirNativeDeadlines()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.Cache;
        await sut.SetAsync("relative", [1], new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
        await sut.SetAsync("sliding", [2], new() { SlidingExpiration = TimeSpan.FromMinutes(3) });
        host.Clock.Now = host.Clock.Now.AddMinutes(2);
        await sut.RefreshAsync("sliding");
        host.Clock.Now = host.Clock.Now.AddMinutes(2);
        (await sut.GetAsync("relative")).ShouldBe([1]);
        (await sut.GetAsync("sliding")).ShouldBe([2]);
        host.Clock.Now = host.Clock.Now.AddMinutes(4);
        (await sut.GetAsync("relative")).ShouldBeNull();
        (await sut.GetAsync("sliding")).ShouldBeNull();
    }

    [Fact]
    public async Task CommonCache_EnumeratesAndRemovesPrefixes_ThroughExistingAdapter()
    {
        await using var host = new LiteDbStorageTestHost();
        var sut = host.CommonCache;
        await sut.SetAsync("ledger:one", "first");
        await sut.SetAsync("ledger:two", "second");
        await sut.SetAsync("other", "keep");
        (await sut.GetAsync<string>("ledger:one")).ShouldBe("first");
        (await sut.GetKeysAsync()).Order().ShouldBe(["ledger:one", "ledger:two", "other"]);
        await sut.RemoveStartsWithAsync("ledger:");
        (await sut.GetKeysAsync()).ShouldBe(["other"]);
        (await sut.GetAsync<string>("other")).ShouldBe("keep");
    }

    [Fact]
    public async Task Reopen_PreservesNativeDocumentsAndCacheValues()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-cache-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "cache.db");
            await using (var host = new LiteDbStorageTestHost(file))
            {
                await host.Cache.SetAsync("persistent", [4, 5], new());
                await host.Documents.UpsertAsync(new("ledger"), Write("P", "row", "saved"));
            }

            await using (var host = new LiteDbStorageTestHost(file))
            {
                (await host.Cache.GetAsync("persistent")).ShouldBe([4, 5]);
                (await host.Documents.GetAsync(new("ledger"), new("P", "row"), host.Clock.Now)).IsSuccess.ShouldBeTrue();
            }
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task TypedClientRegistration_PreservesNamedResolutionBatchOrderAndContinuationPaging()
    {
        await using var host = new LiteDbStorageTestHost(configure: services => services.AddDocumentStorage().WithLiteDbClient<LedgerDocument>());
        var sut = host.Scope.ServiceProvider.GetRequiredService<IDocumentStoreClient<LedgerDocument>>();
        var written = await sut.UpsertManyAsync([new(new("P", "one"), new LedgerDocument { Name = "first" }), new(new("P", "two"), new LedgerDocument { Name = "second" })]);
        written.IsSuccess.ShouldBeTrue();
        var many = await sut.GetManyAsync([new("P", "two"), new("P", "missing"), new("P", "one")]);
        many.IsSuccess.ShouldBeTrue();
        many.Value[0].Value.Value.Name.ShouldBe("second");
        many.Value[1].IsFailure.ShouldBeTrue();
        many.Value[2].Value.Value.Name.ShouldBe("first");
        var query = new DocumentQuery { DocumentKey = new("P", ""), Filter = DocumentKeyFilter.RowKeyPrefixMatch, Take = 1 };
        var first = await sut.FindPageAsync(query);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Items.Single().Value.Name.ShouldBe("first");
        var second = await sut.FindPageAsync(new() { DocumentKey = query.DocumentKey, Filter = query.Filter, Take = 1, ContinuationToken = first.Value.ContinuationToken });
        second.IsSuccess.ShouldBeTrue();
        second.Value.Items.Single().Value.Name.ShouldBe("second");
    }

    /// <summary>Supplies a fictional typed document for existing client integration checks.</summary>
    public sealed class LedgerDocument
    {
        /// <summary>Gets or sets the stored label.</summary>
        public string Name { get; set; }
    }

    private static PropertyBag PropertyBagForTest()
    {
        var properties = new PropertyBag();
        properties.Add("label", "updated");
        return properties;
    }

    private static StoredDocumentWrite Write(string partition, string row, string text) => new()
    {
        Key = new(partition, row), Content = Encoding.UTF8.GetBytes(text), Options = new(), Properties = new(), TransformMetadata = new()
    };
}
