// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using Domain.Repositories;
using Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[UnitTest("Infrastructure")]
public class EntityFrameworkGenericRepositoryTests
{
    private readonly Faker<PersonStub> faker;
    private readonly DbContextOptions<TestPersonDbContext> dbContextOptions;
    private readonly ILoggerFactory loggerFactory;

    public EntityFrameworkGenericRepositoryTests()
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
    public async Task InsertAsync_WithNewEntity_InsertsAndReturnsEntity()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();

        // Act
        var result = await sut.InsertAsync(person);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(person.Id);
        context.Persons.ShouldContain(p => p.Id == person.Id);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingEntity_UpdatesAndReturnsEntity()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();
        await context.Persons.AddAsync(person);
        await context.SaveChangesAsync();

        person.FirstName = "UpdatedName";

        // Act
        var result = await sut.UpdateAsync(person);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(person.Id);
        result.FirstName.ShouldBe("UpdatedName");
        context.Persons.ShouldContain(p => p.Id == person.Id && p.FirstName == "UpdatedName");
    }

    [Fact]
    public async Task UpsertAsync_WithNewEntity_InsertsAndReturnsInsertedResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();

        // Act
        var (entity, action) = await sut.UpsertAsync(person);

        // Assert
        entity.ShouldNotBeNull();
        entity.Id.ShouldBe(person.Id);
        action.ShouldBe(RepositoryActionResult.Inserted);
        context.Persons.ShouldContain(p => p.Id == person.Id);
    }

    [Fact]
    public async Task UpsertAsync_WithExistingEntity_UpdatesAndReturnsUpdatedResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();
        await context.Persons.AddAsync(person);
        await context.SaveChangesAsync();

        person.FirstName = "UpdatedName";

        // Act
        var (entity, action) = await sut.UpsertAsync(person);

        // Assert
        entity.ShouldNotBeNull();
        entity.Id.ShouldBe(person.Id);
        entity.FirstName.ShouldBe("UpdatedName");
        action.ShouldBe(RepositoryActionResult.Updated);
        context.Persons.ShouldContain(p => p.Id == person.Id && p.FirstName == "UpdatedName");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingId_DeletesEntityAndReturnsDeletedResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();
        await context.Persons.AddAsync(person);
        await context.SaveChangesAsync();

        // Act
        var result = await sut.DeleteAsync(person.Id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        context.Persons.ShouldNotContain(p => p.Id == person.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingId_ReturnsNoneResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);

        // Act
        var result = await sut.DeleteAsync(Guid.NewGuid());

        // Assert
        result.ShouldBe(RepositoryActionResult.None);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingEntity_DeletesEntityAndReturnsDeletedResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();
        await context.Persons.AddAsync(person);
        await context.SaveChangesAsync();

        // Act
        var result = await sut.DeleteAsync(person);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        context.Persons.ShouldNotContain(p => p.Id == person.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingEntity_ReturnsNoneResult()
    {
        // Arrange
        using var context = new TestPersonDbContext(this.dbContextOptions);
        var sut = new EntityFrameworkGenericRepository<PersonStub>(this.loggerFactory, context);
        var person = this.faker.Generate();

        // Act
        var result = await sut.DeleteAsync(person);

        // Assert
        result.ShouldBe(RepositoryActionResult.None);
    }
}