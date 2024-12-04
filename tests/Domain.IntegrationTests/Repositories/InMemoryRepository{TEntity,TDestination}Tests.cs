// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.Mapping;
using Bogus;
using Shouldly;

[IntegrationTest("Domain")]
public class InMemoryRepositoryDbTests
{
    private readonly Faker faker;
    private readonly IEnumerable<StubEntity> entities;
    private readonly AutoMapperEntityMapper mapper;

    public InMemoryRepositoryDbTests()
    {
        this.faker = new Faker();

        var basicEntities = Enumerable.Range(1, 20).Select(i => new StubEntity
        {
            Id = $"Id{i}",
            FirstName = "John",
            LastName = this.faker.Name.LastName(),
            Country = "USA",
            Age = this.faker.Random.Int(18, 80)
        }).ToList();

        var specialEntity = new StubEntity
        {
            Id = "Id99",
            FirstName = "John",
            LastName = "Doe",
            Age = 38,
            Country = "USA"
        };

        this.entities = basicEntities.Concat([specialEntity]);
        this.mapper = new AutoMapperEntityMapper(StubEntityMapperConfiguration.Create());
    }

    [Fact]
    public async Task DeleteAsync_ValidEntity_ShouldRemoveEntity()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);
        var entityToDelete = (await sut.FindAllAsync().AnyContext()).First();

        // Act
        await sut.DeleteAsync(entityToDelete).AnyContext();
        var result = await sut.FindOneAsync(entityToDelete.Id).AnyContext();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByIdAsync_ValidId_ShouldRemoveEntity()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);
        var idToDelete = this.entities.First().Id;

        // Act
        await sut.DeleteAsync(idToDelete).AnyContext();
        var result = await sut.FindOneAsync(idToDelete).AnyContext();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAllAsync_NoSpecification_ShouldReturnAllValidEntities()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);

        // Act
        var result = await sut.FindAllAsync().AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.All(e => !e.Id.IsNullOrEmpty() &&
                       !e.FirstName.IsNullOrEmpty() &&
                       !e.LastName.IsNullOrEmpty())
              .ShouldBeTrue();
        result.ShouldContain(e => e.FirstName == "John" && e.LastName == "Doe");
    }

    [Fact]
    public async Task FindAllAsync_WithSpecificationBehavior_ShouldApplyDefaultSpecification()
    {
        // Arrange
        var repository = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);
        var sut = new RepositorySpecificationBehavior<StubEntity>(
            new Specification<StubEntity>(t => t.Country == "USA"),
            repository);

        // Act
        var result = await sut.FindAllAsync().AnyContext();

        // Assert
        result.ShouldNotBeEmpty();
        result.Count().ShouldBe(21);
        result.ShouldContain(e => e.Id == "Id99");
    }

    [Fact]
    public async Task FindAllAsync_WithNameSpecification_ShouldReturnMatchingEntities()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);

        // Act
        var result = await sut.FindAllAsync(
            new StubHasNameSpecification("John", "Doe"),
            new FindOptions<StubEntity>(orderExpression: e => e.Country)
        ).AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.Count().ShouldBe(1);
        var firstEntity = result.FirstOrDefault();
        firstEntity.ShouldNotBeNull();
        firstEntity.Id.ShouldNotBeNullOrEmpty();
        firstEntity.FirstName.ShouldBe("John");
        firstEntity.LastName.ShouldBe("Doe");
    }

    [Fact]
    public async Task FindOneAsync_ExistingId_ShouldReturnCorrectEntity()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);

        // Act
        var result = await sut.FindOneAsync("Id99").AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("Id99");
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
    }

    [Fact]
    public async Task UpsertAsync_ExistingEntity_ShouldUpdateEntity()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntity, StubDbEntity>(
            o => o.Context(new InMemoryContext<StubEntity>(this.entities))
                 .Mapper(this.mapper),
            e => e.Identifier);
        var updatedEntity = new StubEntity
        {
            Id = "Id1",
            FirstName = this.faker.Name.FirstName(),
            LastName = this.faker.Name.LastName()
        };

        // Act
        var result = await sut.UpsertAsync(updatedEntity).AnyContext();
        var findResult = await sut.FindOneAsync("Id1").AnyContext();

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        result.entity.ShouldNotBeNull();
        result.entity.Id.ShouldNotBeNullOrEmpty();
        result.entity.Id.ShouldBe("Id1");
        findResult.ShouldNotBeNull();
        findResult.Id.ShouldBe(result.entity.Id);
        findResult.FirstName.ShouldBe(updatedEntity.FirstName);
    }

    private class StubHasNameSpecification : Specification<StubEntity>
    {
        private readonly string firstName;
        private readonly string lastName;

        public StubHasNameSpecification(string firstName, string lastName)
        {
            this.firstName = firstName;
            this.lastName = lastName;
        }

        public override Expression<Func<StubEntity, bool>> ToExpression()
        {
            return e => e.FirstName == this.firstName && e.LastName == this.lastName;
        }
    }
}