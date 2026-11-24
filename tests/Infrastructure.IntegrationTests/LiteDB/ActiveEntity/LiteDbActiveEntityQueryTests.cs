// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityQueryTests
{
    [Fact]
    public async Task ReadOverloads_EquivalentSpecifications_ReturnSameSelection()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("A", 1), ("B", 2), ("C", 3));
        var sut = host.Provider;
        var specification = new Specification<LedgerEntry>(entry => entry.Amount >= 2);
        ISpecification<LedgerEntry>[] specifications = [specification, new Specification<LedgerEntry>(entry => entry.Amount < 4)];
        (await sut.FindAllAsync()).Value.Count().ShouldBe(3);
        (await sut.FindAllAsync(specification)).Value.Count().ShouldBe(2);
        (await sut.FindAllAsync(specifications)).Value.Count().ShouldBe(2);
        (await sut.FindOneAsync(specification)).IsSuccess.ShouldBeTrue();
        (await sut.FindOneAsync(specifications)).IsSuccess.ShouldBeTrue();
        (await sut.ExistsAsync()).Value.ShouldBeTrue();
        (await sut.ExistsAsync(specification)).Value.ShouldBeTrue();
        (await sut.ExistsAsync(specifications)).Value.ShouldBeTrue();
        (await sut.CountAsync(specification)).Value.ShouldBe(2);
        (await sut.CountAsync(specifications)).Value.ShouldBe(2);
        (await sut.FindAllIdsAsync(specification)).Value.Count().ShouldBe(2);
        (await sut.FindAllIdsAsync(specifications)).Value.Count().ShouldBe(2);
        (await sut.FindAllPagedAsync()).TotalCount.ShouldBe(3);
        (await sut.FindAllPagedAsync(specification)).TotalCount.ShouldBe(2);
        (await sut.FindAllPagedAsync(specifications)).TotalCount.ShouldBe(2);
        (await sut.FindAllIdsPagedAsync(specification)).TotalCount.ShouldBe(2);
        (await sut.FindAllIdsPagedAsync(specifications)).TotalCount.ShouldBe(2);
        (await sut.ProjectAllAsync(entry => entry.Name)).Value.Count().ShouldBe(3);
        (await sut.ProjectAllAsync(specification, entry => entry.Name)).Value.Count().ShouldBe(2);
        (await sut.ProjectAllAsync(specifications, entry => entry.Name)).Value.Count().ShouldBe(2);
        (await sut.ProjectAllPagedAsync(entry => entry.Name)).TotalCount.ShouldBe(3);
        (await sut.ProjectAllPagedAsync(specification, entry => entry.Name)).TotalCount.ShouldBe(2);
        (await sut.ProjectAllPagedAsync(specifications, entry => entry.Name)).TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task PagedQueries_EmptyLaterPage_KeepUnpagedTotal()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("A", 1), ("B", 2), ("C", 3));
        var sut = host.Provider;
        var options = new FindOptions<LedgerEntry> { Skip = 6, Take = 2 };
        var result = await sut.FindAllPagedAsync(options);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        result.TotalCount.ShouldBe(3);
        result.CurrentPage.ShouldBe(4);
        result.PageSize.ShouldBe(2);
        (await sut.FindAllIdsPagedAsync(options)).TotalCount.ShouldBe(3);
        (await sut.ProjectAllPagedAsync(entry => entry.Amount, options)).TotalCount.ShouldBe(3);
        (await sut.FindAllPagedAsync()).PageSize.ShouldBe(10);
    }

    [Fact]
    public async Task Selection_CompoundOrderingDistinctAndPaging_ApplyBeforeProjection()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await host.SeedAsync(("B", 1), ("A", 2), ("A", 3), ("C", 4));
        var options = new FindOptions<LedgerEntry>
        {
            Order = new OrderOption<LedgerEntry>(entry => entry.Name),
            Orders = [new OrderOption<LedgerEntry>(entry => entry.Amount, OrderDirection.Descending)],
            Skip = 1, Take = 2
        };
        var result = await host.Provider.ProjectAllPagedAsync(entry => entry.Amount, options);
        result.Value.ShouldBe([2, 1]);
        result.TotalCount.ShouldBe(4);
        options.Distinct = new DistinctOption<LedgerEntry>(entry => entry.Name);
        options.Skip = 0;
        (await host.Provider.FindAllPagedAsync(options)).TotalCount.ShouldBe(3);
        options.Order = new OrderOption<LedgerEntry>("Name asc, Amount desc");
        options.Orders = [];
        options.Distinct = null;
        (await host.Provider.ProjectAllAsync(entry => entry.Amount, options)).Value.ShouldBe([3, 2]);
    }

    [Fact]
    public async Task EmptyAndNullSpecifications_ReturnEmptyOrUnfilteredSuccess()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        (await sut.FindAllAsync((IEnumerable<ISpecification<LedgerEntry>>)null)).Value.ShouldBeEmpty();
        (await sut.ExistsAsync()).Value.ShouldBeFalse();
        (await sut.CountAsync()).Value.ShouldBe(0);
        (await sut.FindOneAsync(new Specification<LedgerEntry>(entry => entry.Amount > 0))).HasError<NotFoundError>().ShouldBeTrue();
        await host.SeedAsync(("Entry", 1));
        (await sut.FindAllAsync((ISpecification<LedgerEntry>)null)).Value.Count().ShouldBe(1);
    }

    [Fact]
    public async Task InvalidQueries_PagedAndScalarResults_PreserveFailure()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var invalid = new FindOptions<LedgerEntry> { Take = 0 };
        (await host.Provider.FindAllPagedAsync(invalid)).HasError<InvalidInputError>().ShouldBeTrue();
        (await host.Provider.FindAllIdsPagedAsync(invalid)).IsFailure.ShouldBeTrue();
        (await host.Provider.ProjectAllPagedAsync(entry => entry.Name, invalid)).IsFailure.ShouldBeTrue();
        (await host.Provider.CountAsync([], invalid)).IsFailure.ShouldBeTrue();
        (await host.Provider.ProjectAllAsync<string>(null)).IsFailure.ShouldBeTrue();
        (await host.Provider.FindAllAsync(new FindOptions<LedgerEntry> { Order = new OrderOption<LedgerEntry>("Name; Amount") })).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task MaterializationLimit_TooManyMatches_ReturnsFailureWithoutTruncation()
    {
        await using var host = new LiteDbActiveEntityTestHost(options => options.MaxMaterializedRows = 2);
        await host.SeedAsync(("A", 1), ("B", 2), ("C", 3));
        (await host.Provider.FindAllAsync()).HasError<InvalidOperationError>().ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(3);
        (await host.Provider.FindAllPagedAsync(new FindOptions<LedgerEntry> { Take = 1 })).TotalCount.ShouldBe(3);
    }
}
