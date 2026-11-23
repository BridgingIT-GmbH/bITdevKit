// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntitySetOperationTests
{
    [Fact]
    public async Task Sets_OrderedSelectionAndComputedAssignments_PreserveTargetsAndCounts()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("A", 1), ("B", 2), ("C", 3), ("D", 4));
        var options = new FindOptions<LedgerEntry> { Order = new OrderOption<LedgerEntry>(entry => entry.Amount), Skip = 1, Take = 2 };
        var result = await host.Provider.UpdateSetAsync(new Specification<LedgerEntry>(entry => entry.Amount > 0), set => set
            .Set(entry => entry.Amount, entry => entry.Amount + 10).Set(entry => entry.Name, "Updated"), options);
        result.Value.ShouldBe(2);
        var values = (await host.Provider.FindAllAsync()).Value.OrderBy(entry => entry.Amount).Select(entry => entry.Amount);
        values.ShouldBe([1, 4, 12, 13]);
        ISpecification<LedgerEntry>[] specs = [new Specification<LedgerEntry>(entry => entry.Name == "Updated"), new Specification<LedgerEntry>(entry => entry.Amount > 12)];
        (await host.Provider.DeleteSetAsync(specs)).Value.ShouldBe(1);
        (await host.Provider.DeleteSetAsync(new Specification<LedgerEntry>(entry => entry.Amount == 4))).Value.ShouldBe(1);
        (await host.Provider.CountAsync()).Value.ShouldBe(2);
    }

    [Fact]
    public async Task SetUpdate_ChangesIdentity_RollsBackEarlierRows()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("A", 1), ("B", 2));
        var result = await host.Provider.UpdateSetAsync([], set => set.Set(entry => entry.Id, Guid.NewGuid()));
        result.HasError<InvalidInputError>().ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(2);
        (await host.Provider.FindAllAsync()).Value.Select(entry => entry.Name).Order().ShouldBe(["A", "B"]);
    }

    [Fact]
    public async Task SetUpdate_LaterRowThrows_RollsBackWholeSet()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("A", 1), ("B", 0));
        var result = await host.Provider.UpdateSetAsync([], set => set.Set(entry => entry.Amount, entry => 10 / entry.Amount),
            new FindOptions<LedgerEntry> { Order = new OrderOption<LedgerEntry>(entry => entry.Name) });
        result.IsFailure.ShouldBeTrue();
        (await host.Provider.FindAllAsync()).Value.OrderBy(entry => entry.Name).Select(entry => entry.Amount).ShouldBe([1, 0]);
    }

    [Fact]
    public async Task SetUpdate_ConcurrencyAndNoMatches_KeepExpectedSemantics()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("A", 1));
        var stale = (await host.Provider.FindAllAsync()).Value.Single();
        (await host.Provider.UpdateSetAsync([], set => set.Set(entry => entry.Amount, 2))).Value.ShouldBe(1);
        (await host.Provider.UpdateAsync(stale)).HasError<ConcurrencyError>().ShouldBeTrue();
        (await host.Provider.UpdateSetAsync(new Specification<LedgerEntry>(entry => entry.Amount > 10), set => set.Set(entry => entry.Amount, 0))).Value.ShouldBe(0);
        (await host.Provider.UpdateSetAsync([], null)).HasError<InvalidInputError>().ShouldBeTrue();
        (await host.Provider.DeleteSetAsync(new Specification<LedgerEntry>(entry => entry.Amount > 10))).Value.ShouldBe(0);
    }

    [Fact]
    public async Task SetUpdate_CancellationInSecondBatch_RollsBackEarlierBatch()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        foreach (var amount in Enumerable.Range(1, 260))
        {
            (await host.Provider.InsertAsync(new LedgerEntry { Amount = amount })).IsSuccess.ShouldBeTrue();
        }

        using var cancellation = new CancellationTokenSource();
        await Should.ThrowAsync<OperationCanceledException>(() => host.Provider.UpdateSetAsync([], set => set
            .Set(entry => entry.Amount, entry => CancelAtSecondBatch(entry.Amount, cancellation)),
            new FindOptions<LedgerEntry> { Order = new OrderOption<LedgerEntry>(entry => entry.Amount) }, cancellation.Token));
        var result = await host.Provider.FindAllAsync();
        result.Value.Select(entry => entry.Amount).Order().ShouldBe(Enumerable.Range(1, 260));
    }

    private static int CancelAtSecondBatch(int value, CancellationTokenSource cancellation)
    {
        if (value == 129) { cancellation.Cancel(); }

        return value + 1000;
    }
}
