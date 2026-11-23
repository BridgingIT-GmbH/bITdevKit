// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.ActiveEntity;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

[UnitTest("Infrastructure")]
public class ActiveEntityProviderPagingTests
{
    [Theory]
    [InlineData(1, false, true, new[] { 6, 3 })]
    [InlineData(2, true, true, new[] { 2, 5 })]
    [InlineData(3, true, false, new[] { 1 })]
    [InlineData(4, true, false, new int[0])]
    public async Task PagedQueries_WithRelationalFilteringAndTiedOrdering_ReturnTheSamePage(
        int page, bool hasPreviousPage, bool hasNextPage, int[] expectedIds)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new PagingDbContext(new DbContextOptionsBuilder<PagingDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();
        context.Entities.AddRange(
            new PagingEntity { Id = 1, Rank = 2, Included = true },
            new PagingEntity { Id = 2, Rank = 1, Included = true },
            new PagingEntity { Id = 3, Rank = 1, Included = true },
            new PagingEntity { Id = 4, Rank = 1 },
            new PagingEntity { Id = 5, Rank = 2, Included = true },
            new PagingEntity { Id = 6, Rank = 1, Included = true },
            new PagingEntity { Id = 7, Rank = 2 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var provider = new EntityFrameworkActiveEntityProvider<PagingEntity, int, PagingDbContext>(context);
        var sut = new ActiveEntityContext<PagingEntity, int>(provider, []);
        var filter = FilterModelBuilder.For<PagingEntity>()
            .AddFilter(entity => entity.Included, FilterOperator.Equal, true)
            .AddOrdering(entity => entity.Rank, OrderDirection.Ascending)
            .AddOrdering(entity => entity.Id, OrderDirection.Descending)
            .SetPaging(page, 2)
            .Build();

        var entities = await PagingEntity.FindAllPagedAsync(sut, filter);
        var ids = await PagingEntity.FindAllIdsPagedAsync(sut, filter);
        var projections = await PagingEntity.ProjectAllPagedAsync(sut, filter, entity => entity.Id * 10);

        AssertPage(entities);
        AssertPage(ids);
        AssertPage(projections);
        entities.Value.Select(entity => entity.Id).ShouldBe(expectedIds);
        ids.Value.ShouldBe(expectedIds);
        projections.Value.ShouldBe(expectedIds.Select(id => id * 10));

        void AssertPage<T>(ResultPaged<T> result)
        {
            result.IsSuccess.ShouldBeTrue(result.ToString());
            result.CurrentPage.ShouldBe(page);
            result.PageSize.ShouldBe(2);
            result.TotalCount.ShouldBe(5);
            result.TotalPages.ShouldBe(3);
            result.HasPreviousPage.ShouldBe(hasPreviousPage);
            result.HasNextPage.ShouldBe(hasNextPage);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinctQueries_WithSqlite_ReturnOneMatchingRowPerGroup(bool relationalNulls)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new PagingDbContext(new DbContextOptionsBuilder<PagingDbContext>()
            .UseSqlite(connection, options => options.UseRelationalNulls(relationalNulls))
            .Options);
        await context.Database.EnsureCreatedAsync();
        context.Entities.AddRange(
            new PagingEntity { Id = 1 },
            new PagingEntity { Id = 2, Included = true },
            new PagingEntity { Id = 3, Included = true },
            new PagingEntity { Id = 4, Rank = 10, Included = true },
            new PagingEntity { Id = 5, Rank = 10, Included = true },
            new PagingEntity { Id = 6, Rank = 20 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var sut = new EntityFrameworkActiveEntityProvider<PagingEntity, int, PagingDbContext>(context);
        var specification = new Specification<PagingEntity>(entity => entity.Included);
        var options = new FindOptions<PagingEntity>
        {
            Distinct = new DistinctOption<PagingEntity>(entity => entity.Rank),
            Order = new OrderOption<PagingEntity>(entity => entity.Id, OrderDirection.Descending),
            NoTracking = true
        };

        var unpaged = await sut.ProjectAllAsync(specification, entity => entity.Id, options);
        options.Skip = 1;
        options.Take = 1;
        var entities = await sut.FindAllPagedAsync(specification, options);
        var ids = await sut.FindAllIdsPagedAsync(specification, options);
        var projections = await sut.ProjectAllPagedAsync(specification, entity => entity.Id * 10, options);

        unpaged.IsSuccess.ShouldBeTrue(unpaged.ToString());
        unpaged.Value.ShouldBe([4, 2]);
        AssertPage(entities);
        AssertPage(ids);
        AssertPage(projections);
        entities.Value.Select(entity => entity.Id).ShouldBe([2]);
        ids.Value.ShouldBe([2]);
        projections.Value.ShouldBe([20]);
        context.ChangeTracker.Entries<PagingEntity>().ShouldBeEmpty();

        static void AssertPage<T>(ResultPaged<T> result)
        {
            result.IsSuccess.ShouldBeTrue(result.ToString());
            result.TotalCount.ShouldBe(2);
            result.CurrentPage.ShouldBe(2);
            result.PageSize.ShouldBe(1);
            result.HasPreviousPage.ShouldBeTrue();
            result.HasNextPage.ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PagedQueries_WithFilterAndOrdering_UseTheUnpagedTotal(bool useEntityFramework)
    {
        await using var context = CreateDbContext();
        var sut = await CreateProviderAsync(context, useEntityFramework, 8);
        var specification = new Specification<PagingEntity>(entity => entity.Id >= 2 && entity.Id <= 6);
        var options = new FindOptions<PagingEntity>
        {
            Skip = 2,
            Take = 2,
            Order = new OrderOption<PagingEntity>(entity => entity.Id, OrderDirection.Descending)
        };

        var entities = await sut.FindAllPagedAsync(specification, options);
        var ids = await sut.FindAllIdsPagedAsync([specification], options);
        var projections = await sut.ProjectAllPagedAsync([specification], entity => entity.Id, options);

        AssertSecondPage(entities);
        AssertSecondPage(ids);
        AssertSecondPage(projections);
        entities.Value.Select(entity => entity.Id).ShouldBe([4, 3]);
        ids.Value.ShouldBe([4, 3]);
        projections.Value.ShouldBe([4, 3]);
        options.Skip.ShouldBe(2);
        options.Take.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FindAllPagedAsync_WithoutExplicitLimit_UsesDefaultPageSize(bool useEntityFramework)
    {
        await using var context = CreateDbContext();
        var sut = await CreateProviderAsync(context, useEntityFramework, 12);

        var result = await sut.FindAllPagedAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(10);
        result.TotalCount.ShouldBe(12);
        result.PageSize.ShouldBe(10);
        result.HasNextPage.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProjectAllPagedAsync_WhenProjectionThrows_PreservesFailureAndKnownMetadata(bool useEntityFramework)
    {
        await using var context = CreateDbContext();
        var sut = await CreateProviderAsync(context, useEntityFramework, 5);
        var options = new FindOptions<PagingEntity> { Skip = 2, Take = 2 };

        var result = await sut.ProjectAllPagedAsync(entity => ProjectWithFailure(entity.Id), options);

        result.IsFailure.ShouldBeTrue();
        result.GetError<ExceptionError>().ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
        result.TotalCount.ShouldBe(5);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task FindAllPagedAsync_WithDisposedEfContext_ReturnsFailedPage()
    {
        var dbContext = CreateDbContext();
        var provider = new EntityFrameworkActiveEntityProvider<PagingEntity, int, PagingDbContext>(dbContext);
        var sut = new ActiveEntityContext<PagingEntity, int>(provider, []);
        var options = new FindOptions<PagingEntity> { Skip = 4, Take = 2 };
        await dbContext.DisposeAsync();

        var result = await PagingEntity.FindAllPagedAsync(sut, entity => entity.Id > 0, options);

        result.IsFailure.ShouldBeTrue();
        result.GetError<ExceptionError>().Exception.ShouldBeOfType<ObjectDisposedException>();
        result.Value.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.CurrentPage.ShouldBe(3);
        result.PageSize.ShouldBe(2);
    }

    private static PagingDbContext CreateDbContext() => new(new DbContextOptionsBuilder<PagingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task<IActiveEntityEntityProvider<PagingEntity, int>> CreateProviderAsync(
        PagingDbContext context, bool useEntityFramework, int count)
    {
        var entities = Enumerable.Range(1, count).Select(id => new PagingEntity { Id = id }).ToArray();
        if (useEntityFramework)
        {
            context.AddRange(entities);
            await context.SaveChangesAsync();
            return new EntityFrameworkActiveEntityProvider<PagingEntity, int, PagingDbContext>(context);
        }

        return new ActiveEntityInMemoryProvider<PagingEntity, int>(
            NullLoggerFactory.Instance,
            new ActiveEntityInMemoryProviderOptions<PagingEntity>
            {
                Context = new InMemoryContext<PagingEntity>(entities)
            });
    }

    private static void AssertSecondPage<T>(ResultPaged<T> result)
    {
        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(5);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(2);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    private static int ProjectWithFailure(int id) => throw new InvalidOperationException($"Could not project entity {id}");

    public sealed class PagingEntity : ActiveEntity<PagingEntity, int>
    {
        public int? Rank { get; set; }

        public bool Included { get; set; }
    }

    private sealed class PagingDbContext(DbContextOptions<PagingDbContext> options) : DbContext(options)
    {
        public DbSet<PagingEntity> Entities => this.Set<PagingEntity>();
    }
}
