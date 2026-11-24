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

/// <summary>Verifies corrupt SQLite scalar fields cannot bypass native metadata memory reservations.</summary>
public sealed class CachePersistedScalarTests
{
    /// <summary>Rejects an oversized ownership node before materializing the persisted value.</summary>
    [Fact]
    public async Task Ownership_OversizedNodeId_IsBoundedOnServerAndRejectedAsCorruption()
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        var request = new CacheOwnershipRequest(fixture.Identity("owned"), "node", "interest", DateTimeOffset.UtcNow.AddSeconds(25), TimeSpan.FromSeconds(30));
        var owned = await fixture.Provider.AcquireOwnershipAsync(request);
        owned.IsSuccess.ShouldBeTrue();
        await using (var context = fixture.Context())
        {
            await context.CacheOwnerships.ExecuteUpdateAsync(set => set.SetProperty(row => row.NodeId, new string('x', 2 * 1024 * 1024)));
        }

        commands.Queries.Clear();
        var result = await fixture.Provider.ObserveOwnershipAsync(request, owned.Value.FlightId);

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        commands.Queries.Where(query => query.Contains("NodeId", StringComparison.Ordinal)).ShouldNotBeEmpty();
        commands.Queries.Where(query => query.Contains("NodeId", StringComparison.Ordinal))
            .ShouldAllBe(query => query.Contains("substr", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Rejects an oversized indexed prefix without treating its truncated value as valid entry metadata.</summary>
    [Fact]
    public async Task Lookup_OversizedKeyPrefix_IsBoundedOnServerAndRejectedAsCorruption()
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        (await fixture.WriteAsync("value", [1])).IsSuccess.ShouldBeTrue();
        await using (var context = fixture.Context())
        {
            await context.CacheEntries.ExecuteUpdateAsync(set => set.SetProperty(row => row.KeyPrefix, new string('a', 2 * 1024 * 1024)));
        }

        commands.Queries.Clear();
        var result = await fixture.Provider.LookupAsync(fixture.Identity("value"), new(ObserveAccess: false));

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        commands.Queries.Where(query => query.Contains("KeyPrefix", StringComparison.Ordinal))
            .ShouldAllBe(query => query.Contains("substr", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Bounds all scalar metadata reached while capturing a publication fence.</summary>
    /// <param name="field">The persisted root, collection or logical-key field corrupted outside normal provider writes.</param>
    [Theory]
    [InlineData("cursor")]
    [InlineData("collection")]
    [InlineData("key")]
    public async Task Fence_OversizedPersistedScalar_IsRejectedBeforeUse(string field)
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        (await fixture.WriteAsync("value", [1])).IsSuccess.ShouldBeTrue();
        await using (var context = fixture.Context())
        {
            if (field == "cursor")
            {
                await context.CacheStores.ExecuteUpdateAsync(set => set.SetProperty(row => row.CursorKey, new byte[2 * 1024 * 1024]));
            }
            else if (field == "collection")
            {
                await context.CacheCollections.ExecuteUpdateAsync(set => set.SetProperty(row => row.CanonicalIdentity, new string('x', 2 * 1024 * 1024)));
            }
            else
            {
                await context.CacheKeys.ExecuteUpdateAsync(set => set.SetProperty(row => row.CanonicalKey, new string('x', 2 * 1024 * 1024)));
            }
        }

        var result = await fixture.Provider.CaptureFenceAsync(fixture.Identity("value"));

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
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