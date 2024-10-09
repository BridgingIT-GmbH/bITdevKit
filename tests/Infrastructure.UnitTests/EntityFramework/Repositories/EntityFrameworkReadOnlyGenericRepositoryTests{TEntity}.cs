// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using System.Linq.Expressions;
using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[UnitTest("Infrastructure")]
public class EntityFrameworkReadOnlyGenericRepositoryTests
{
    private readonly Faker<PersonStub> faker;
    private readonly DbContextOptions<TestPersonDbContext> dbContextOptions;
    private readonly ILoggerFactory loggerFactory;

    public EntityFrameworkReadOnlyGenericRepositoryTests()
    {
        this.faker = new Faker<PersonStub>()
            .RuleFor(p => p.Id, f => Guid.NewGuid())
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Age, f => f.Random.Int(18, 80));

        this.dbContextOptions = new DbContextOptionsBuilder<TestPersonDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid()
                .ToString())
            .Options;
        this.loggerFactory = Substitute.For<ILoggerFactory>();
    }

    [Fact]
    public async Task FindAllAsync_WithoutSpecifications_ReturnsAllEntities()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(5);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindAllAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(5);
    }

    [Fact]
    public async Task FindAllAsync_WithSpecification_ReturnsFilteredEntities()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(10);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);
        var specification = new PersonByFirstNameSpecification(entities[0].FirstName);

        // Act
        var result = await sut.FindAllAsync(specification);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(1);
        result.First()
            .FirstName.ShouldBe(entities[0].FirstName);
    }

    [Fact]
    public async Task FindOneAsync_WithExistingId_ReturnsEntity()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entity = this.faker.Generate();
        await context.Persons.AddAsync(entity);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task FindOneAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindOneAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingId_ReturnsTrue()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entity = this.faker.Generate();
        await context.Persons.AddAsync(entity);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.ExistsAsync(entity.Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingId_ReturnsFalse()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.ExistsAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CountAsync_WithoutSpecifications_ReturnsCorrectCount()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(7);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.CountAsync();

        // Assert
        result.ShouldBe(7);
    }

    [Fact]
    public async Task FindOneResultAsync_WithExistingId_ReturnsSuccessResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entity = this.faker.Generate();
        await context.Persons.AddAsync(entity);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindOneResultAsync(entity.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public async Task FindOneResultAsync_WithNonExistingId_ReturnsFailureResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindOneResultAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors[0]
            .ShouldBeOfType<NotFoundResultError>();
    }

    [Fact]
    public async Task FindAllResultAsync_ReturnsSuccessResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(5);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindAllResultAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count()
            .ShouldBe(5);
    }

    [Fact]
    public async Task FindAllResultAsync_WithSpecification_ReturnsFilteredSuccessResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(10);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);
        var specification = new PersonByFirstNameSpecification(entities[0].FirstName);

        // Act
        var result = await sut.FindAllResultAsync(specification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count()
            .ShouldBe(1);
        result.Value.First()
            .FirstName.ShouldBe(entities[0].FirstName);
    }

    [Fact]
    public async Task FindAllPagedResultAsync_ReturnsPagedResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(20);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindAllPagedResultAsync(e => e.LastName, 2, 5);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count()
            .ShouldBe(5);
        result.TotalCount.ShouldBe(20);
        result.CurrentPage.ShouldBe(2);
        result.PageSize.ShouldBe(5);
        result.TotalPages.ShouldBe(4);
    }

    [Fact]
    public async Task FindAllPagedResultAsync_WithSpecification_ReturnsFilteredPagedResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(20);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);
        var specification = new PersonByFirstNameSpecification(entities[0].FirstName);

        // Act
        var result = await sut.FindAllPagedResultAsync(specification, e => e.LastName);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count()
            .ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        result.CurrentPage.ShouldBe(1);
        result.PageSize.ShouldBe(10);
        result.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task FindAllIdsAsync_ReturnsAllIds()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(5);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.FindAllIdsAsync<PersonStub, Guid>();

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(5);
        result.ShouldBe(entities.Select(e => e.Id), true);
    }

    [Fact]
    public async Task FindAllIdsAsync_WithSpecification_ReturnsFilteredIds()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(10);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);
        var specification = new PersonByFirstNameSpecification(entities[0].FirstName);

        // Act
        var result = await sut.FindAllIdsAsync<PersonStub, Guid>(specification);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(1);
        result.First()
            .ShouldBe(entities[0].Id);
    }

    [Fact]
    public async Task FindAllIdsAsync_WithMultipleSpecifications_ReturnsFilteredIds()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(20);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);
        var specifications = new List<ISpecification<PersonStub>>
        {
            new PersonByFirstNameSpecification(entities[0].FirstName)
                .Or(new PersonByFirstNameSpecification(entities[1].FirstName)),
            //AND
            new Specification<PersonStub>(e => e.Age > 0)
        };

        // Act
        var result = await sut.FindAllIdsAsync<PersonStub, Guid>(specifications);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(2);
        result.ShouldContain(entities[0].Id);
        result.ShouldContain(entities[1].Id);
    }

    [Fact]
    public async Task FindAllIdsAsync_WithOptions_ReturnsLimitedIds()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var entities = this.faker.Generate(10);
        await context.Persons.AddRangeAsync(entities);
        await context.SaveChangesAsync();

        var sut = new EntityFrameworkReadOnlyGenericRepository<PersonStub>(this.loggerFactory, context);
        var options = new FindOptions<PersonStub> { Take = 5, Order = new OrderOption<PersonStub>(e => e.LastName) };

        // Act
        var result = await sut.FindAllIdsAsync<PersonStub, Guid>(options);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(5);
        result.ShouldBe(entities.OrderBy(e => e.LastName)
            .Take(5)
            .Select(e => e.Id));
    }
}

public class TestPersonDbContext(DbContextOptions<TestPersonDbContext> options) : DbContext(options)
{
    public DbSet<PersonStub> Persons { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PersonStub>()
            .HasKey(p => p.Id);
        modelBuilder.Entity<PersonStub>()
            .Property(p => p.FirstName)
            .IsRequired();
        modelBuilder.Entity<PersonStub>()
            .Property(p => p.LastName)
            .IsRequired();
        modelBuilder.Entity<PersonStub>()
            .Property(p => p.Age)
            .IsRequired();
    }
}

public class PersonByFirstNameSpecification(string firstName) : Specification<PersonStub>
{
    public override Expression<Func<PersonStub, bool>> ToExpression()
    {
        return person => person.FirstName == firstName;
    }
}