// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Data.Common;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class CacheOwnershipJsonTests
{
    [Fact]
    public async Task ChildInterests_RetainIndividualQuotaUntilReleaseAndRejectExcessGroups()
    {
        var policies = new CacheCollectionOptions { Limits = new() { MaxFollowersPerKey = 1 } };
        await using var fixture = await CacheRelationalTestStore.CreateAsync(collection: policies);
        var identity = fixture.Identity("key");
        var owner = (await fixture.Provider.AcquireOwnershipAsync(Request(identity, "owner"))).Value;
        var before = (await fixture.Provider.GetQuotaAsync()).Value.MetadataBytes;
        (await fixture.Provider.AcquireOwnershipAsync(Request(identity, "naïve"))).Value.State.ShouldBe(CacheOwnershipState.Pending);
        (await fixture.Provider.GetQuotaAsync()).Value.MetadataBytes.ShouldBe(before + CacheOwnershipInterestState.InterestMetadataBytes);
        var rejected = await fixture.Provider.AcquireOwnershipAsync(Request(identity, "third"));
        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);
        (await fixture.Provider.GetQuotaAsync()).Value.MetadataBytes.ShouldBe(before + CacheOwnershipInterestState.InterestMetadataBytes);
        await using (var verify = fixture.Context())
        {
            var flight = await verify.CacheOwnerships.SingleAsync();
            flight.InterestsJson.ShouldAllBe(character => character <= 127);
            CacheOwnershipInterestState.Read(flight.InterestsJson).Select(interest => interest.InterestId).ShouldContain("naïve");
        }

        (await fixture.Provider.ReleaseInterestAsync(identity, owner.FlightId, "naïve")).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.ReleaseInterestAsync(identity, owner.FlightId, "naïve")).IsSuccess.ShouldBeTrue();
        (await fixture.Provider.GetQuotaAsync()).Value.MetadataBytes.ShouldBe(before);
        (await fixture.Provider.AcquireOwnershipAsync(Request(identity, "third"))).Value.State.ShouldBe(CacheOwnershipState.Pending);
    }

    [Fact]
    public async Task ExpiredParent_DeletesJsonChildrenInFiniteUnitsAndReleasesEveryMetadataCharge()
    {
        var clock = new Clock();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(timeProvider: clock);
        var baseline = (await fixture.Provider.GetQuotaAsync()).Value.MetadataBytes;
        var identity = fixture.Identity("key");
        foreach (var group in new[] { "owner", "one", "two" })
        {
            (await fixture.Provider.AcquireOwnershipAsync(new(identity, group, group, clock.GetUtcNow().AddSeconds(20), TimeSpan.FromSeconds(30)))).IsSuccess.ShouldBeTrue();
        }

        clock.Advance(TimeSpan.FromMinutes(12));
        for (var index = 0; index < 10; index++)
        {
            var result = await fixture.Provider.MaintainAsync(new(1, clock.GetUtcNow(), clock.GetUtcNow().AddSeconds(5)));
            result.IsSuccess.ShouldBeTrue(string.Join(", ", result.Errors.Select(error => error.Message)));
        }

        (await fixture.Provider.GetQuotaAsync()).Value.MetadataBytes.ShouldBe(baseline);
        await using var verify = fixture.Context();
        (await verify.CacheOwnerships.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task FlightLookup_ProjectsHeadersBeforeBoundedJsonAndRejectsOversizedPersistedState()
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        var identity = fixture.Identity("key");
        var owner = (await fixture.Provider.AcquireOwnershipAsync(Request(identity, "owner"))).Value;
        commands.Queries.Clear();

        (await fixture.Provider.AcquireOwnershipAsync(Request(identity, "second"))).IsSuccess.ShouldBeTrue();

        var headers = commands.Queries.First(sql => sql.Contains("__Caching_Ownerships", StringComparison.Ordinal));
        headers.ShouldNotContain("InterestsJson");
        commands.Queries.Any(sql => sql.Contains("substr", StringComparison.OrdinalIgnoreCase) && sql.Contains("InterestsJson", StringComparison.Ordinal)).ShouldBeTrue();
        await using (var corrupt = fixture.Context())
        {
            var oversized = new string(' ', CacheOwnershipInterestState.MaximumJsonBytes + 1);
            await corrupt.CacheOwnerships.ExecuteUpdateAsync(set => set.SetProperty(flight => flight.InterestsJson, oversized));
        }

        var observed = await fixture.Provider.ObserveOwnershipAsync(Request(identity, "second"), owner.FlightId);
        observed.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
    }

    [Fact]
    public void JsonFormat_RejectsDuplicateChildrenAndFiniteCapacityOverflow()
    {
        var duplicate = "[{\"i\":\"group\",\"d\":1},{\"i\":\"group\",\"d\":2}]";
        Should.Throw<CacheRelationalException>(() => CacheOwnershipInterestState.Read(duplicate)).Code.ShouldBe(CacheErrorCode.Corruption);
        var children = Enumerable.Range(0, CacheOwnershipInterestState.MaximumInterests + 1).Select(index => new CacheOwnershipInterest("group-" + index, 1)).ToArray();
        Should.Throw<CacheRelationalException>(() => CacheOwnershipInterestState.Write(children)).Code.ShouldBe(CacheErrorCode.Capacity);
    }

    private static CacheOwnershipRequest Request(CacheEntryIdentity identity, string group) => new(identity, group, group, DateTimeOffset.UtcNow.AddSeconds(25), TimeSpan.FromSeconds(30));

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan value) => this.now += value;
    }

    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Queries.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}