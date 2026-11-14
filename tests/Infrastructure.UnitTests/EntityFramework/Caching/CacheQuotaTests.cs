// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

public class CacheQuotaTests
{
    [Theory]
    [InlineData(CacheEvictionMode.Evict, true)]
    [InlineData(CacheEvictionMode.Reject, false)]
    public async Task CollectionCountPressure_EvictsBoundedCandidatesOrRejectsAccordingToPolicy(CacheEvictionMode mode, bool admitted)
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: new() { Eviction = mode, Limits = new() { MaxEntries = 2 } });
        (await fixture.WriteAsync("first", [1])).IsSuccess.ShouldBeTrue();
        (await fixture.WriteAsync("second", [2])).IsSuccess.ShouldBeTrue();

        var result = await fixture.WriteAsync("third", [3]);

        result.IsSuccess.ShouldBe(admitted);
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(2);
        (await fixture.Provider.LookupAsync(fixture.Identity("third"), new())).Value.State.ShouldBe(admitted ? CacheReadValidationState.Valid : CacheReadValidationState.Missing);
        (await fixture.Provider.LookupAsync(fixture.Identity("first"), new())).Value.State.ShouldBe(admitted ? CacheReadValidationState.Missing : CacheReadValidationState.Valid);
    }

    [Fact]
    public async Task SharedCollectionQuota_IsPersistedAndCannotBeBypassedByAnotherNodesLargerConfiguration()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: new() { Limits = new() { MaxEntries = 2 } });
        await using var peer = new EntityFrameworkCacheStoreProvider("peer", new(), fixture.Context);

        var changed = await peer.InitializeAsync([fixture.Definition with { Policies = new() }]);

        changed.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
    }
}