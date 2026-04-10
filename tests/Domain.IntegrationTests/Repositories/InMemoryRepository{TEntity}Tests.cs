// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Bogus;
using Shouldly;
using System.Linq.Expressions;

[IntegrationTest("Domain")]
public class InMemoryRepositoryTests
{
    private readonly Faker faker;
    private readonly IEnumerable<StubEntityString> entities;
    private readonly IEnumerable<StubEntityGuid> entitiesGuid;

    public InMemoryRepositoryTests()
    {
        this.faker = new Faker();

        this.entities = Enumerable.Range(1, 20).Select(i => new StubEntityString
        {
            Id = $"Id{i}",
            FirstName = this.faker.Name.FirstName(),
            LastName = this.faker.Name.LastName(),
            Country = "USA",
            Age = this.faker.Random.Int(18, 80)
        }).ToList();

        this.entitiesGuid = Enumerable.Range(1, 20).Select(i => new StubEntityGuid
        {
            Id = Guid.NewGuid(),
            FirstName = this.faker.Name.FirstName(),
            LastName = this.faker.Name.LastName(),
            Country = "USA",
            Age = this.faker.Random.Int(18, 80)
        }).ToList();
    }

    [Fact]
    public async Task DeleteAsync_ValidEntity_EntityShouldBeRemoved()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));
        var entityToDelete = this.entities.First();

        // Act
        await sut.DeleteAsync(entityToDelete).AnyContext();
        var result = await sut.FindOneAsync(entityToDelete.Id).AnyContext();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByIdAsync_ValidId_EntityShouldBeRemoved()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));
        var idToDelete = this.entities.First().Id;

        // Act
        await sut.DeleteAsync(idToDelete).AnyContext();
        var result = await sut.FindOneAsync(idToDelete).AnyContext();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindAllAsync_WithNoParameters_ShouldReturnAllEntities()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));

        // Act
        var result = await sut.FindAllAsync().AnyContext();

        // Assert
        result.ShouldNotBeEmpty();
        result.Count().ShouldBe(this.entities.Count());
    }

    [Fact]
    public async Task CountAsync_WithValidSpecifications_ShouldReturnCorrectCount()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));
        var specifications = new List<ISpecification<StubEntityString>>
        {
            new StubHasNameSpecification(this.entities.First().FirstName),
            new StubHasMinimumAgeSpecification(0)
        };

        // Act
        var result = await sut.CountAsync(specifications).AnyContext();

        // Assert
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpsertAsync_NewEntityWithoutId_ShouldInsertWithGeneratedId()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));
        var newEntity = new StubEntityString
        {
            FirstName = this.faker.Name.FirstName(),
            LastName = this.faker.Name.LastName(),
            Country = "USA",
            Age = this.faker.Random.Int(18, 80)
        };

        // Act
        var result = await sut.UpsertAsync(newEntity).AnyContext();
        var findResult = await sut.FindOneAsync(result.entity.Id).AnyContext();

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Inserted);
        result.entity.Id.ShouldNotBeNullOrEmpty();
        findResult.ShouldNotBeNull();
        findResult.FirstName.ShouldBe(newEntity.FirstName);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEntity_ShouldUpdateEntity()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities)));
        var existingEntity = this.entities.First();
        var modifiedFirstName = this.faker.Name.FirstName();
        existingEntity.FirstName = modifiedFirstName;

        // Act
        var result = await sut.UpsertAsync(existingEntity).AnyContext();
        var findResult = await sut.FindOneAsync(existingEntity.Id).AnyContext();

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        findResult.ShouldNotBeNull();
        findResult.FirstName.ShouldBe(modifiedFirstName);
    }

    [Fact]
    public async Task UpdateSetAsync_WithSpecification_ShouldUpdateMatchingEntities()
    {
        // Arrange
        var entities = new[]
        {
            new StubEntityString { Id = "1", FirstName = "John", LastName = "Doe", Country = "USA", Age = 17 },
            new StubEntityString { Id = "2", FirstName = "Jane", LastName = "Doe", Country = "USA", Age = 18 },
            new StubEntityString { Id = "3", FirstName = "Max", LastName = "Doe", Country = "USA", Age = 19 }
        };
        var sut = new InMemoryRepository<StubEntityString>(o => o.Context(new InMemoryContext<StubEntityString>(entities)));

        // Act
        var result = await sut.UpdateSetAsync(
            new Specification<StubEntityString>(e => e.LastName == "Doe" && e.Age >= 18),
            set => set.Set(e => e.FirstName, "Adult")).AnyContext();
        var updated = (await sut.FindAllAsync(new Specification<StubEntityString>(e => e.LastName == "Doe")).AnyContext()).ToList();

        // Assert
        result.ShouldBe(2);
        updated.Single(e => e.Id == "1").FirstName.ShouldBe("John");
        updated.Single(e => e.Id == "2").FirstName.ShouldBe("Adult");
        updated.Single(e => e.Id == "3").FirstName.ShouldBe("Adult");
    }

    [Fact]
    public async Task UpdateSetAsync_WithComputedValue_ShouldUpdateMatchingEntities()
    {
        // Arrange
        var entities = new[]
        {
            new StubEntityString { Id = "1", FirstName = "John", LastName = "Doe", Country = "USA", Age = 17 },
            new StubEntityString { Id = "2", FirstName = "Jane", LastName = "Doe", Country = "USA", Age = 18 }
        };
        var sut = new InMemoryRepository<StubEntityString>(o => o.Context(new InMemoryContext<StubEntityString>(entities)));

        // Act
        var result = await sut.UpdateSetAsync(
            set => set.Set(e => e.Age, e => e.Age + 1)).AnyContext();
        var updated = (await sut.FindAllAsync().AnyContext()).OrderBy(e => e.Id).ToList();

        // Assert
        result.ShouldBe(2);
        updated.Select(e => e.Age).ShouldBe([18, 19]);
    }

    [Fact]
    public async Task DeleteSetAsync_WithSpecification_ShouldDeleteMatchingEntities()
    {
        // Arrange
        var entities = new[]
        {
            new StubEntityString { Id = "1", FirstName = "John", LastName = "Doe", Country = "USA", Age = 17 },
            new StubEntityString { Id = "2", FirstName = "Jane", LastName = "Doe", Country = "USA", Age = 18 },
            new StubEntityString { Id = "3", FirstName = "Max", LastName = "Moe", Country = "USA", Age = 19 }
        };
        var sut = new InMemoryRepository<StubEntityString>(o => o.Context(new InMemoryContext<StubEntityString>(entities)));

        // Act
        var result = await sut.DeleteSetAsync(new Specification<StubEntityString>(e => e.LastName == "Doe")).AnyContext();
        var remaining = (await sut.FindAllAsync().AnyContext()).ToList();

        // Assert
        result.ShouldBe(2);
        remaining.Count.ShouldBe(1);
        remaining.Single().Id.ShouldBe("3");
    }

    [Fact]
    public async Task DeleteSetAsync_WithOptions_ShouldDeleteShapedSet()
    {
        // Arrange
        var entities = new[]
        {
            new StubEntityString { Id = "1", FirstName = "John", LastName = "Doe", Country = "USA", Age = 17 },
            new StubEntityString { Id = "2", FirstName = "Jane", LastName = "Doe", Country = "USA", Age = 18 },
            new StubEntityString { Id = "3", FirstName = "Max", LastName = "Doe", Country = "USA", Age = 19 }
        };
        var sut = new InMemoryRepository<StubEntityString>(o => o.Context(new InMemoryContext<StubEntityString>(entities)));

        // Act
        var result = await sut.DeleteSetAsync(
            new Specification<StubEntityString>(e => e.LastName == "Doe"),
            new FindOptions<StubEntityString>
            {
                Order = new OrderOption<StubEntityString>(e => e.Age, OrderDirection.Descending),
                Take = 1
            }).AnyContext();
        var remaining = (await sut.FindAllAsync(new Specification<StubEntityString>(e => e.LastName == "Doe")).AnyContext())
            .OrderBy(e => e.Id)
            .ToList();

        // Assert
        result.ShouldBe(1);
        remaining.Select(e => e.Id).ShouldBe(["1", "2"]);
    }

    private class StubEntityString : AggregateRoot<string>
    {
        public string Country { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }

    private class StubEntityGuid : AggregateRoot<Guid>
    {
        public string Country { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }

    private class StubHasNameSpecification : Specification<StubEntityString>
    {
        private readonly string firstName;

        public StubHasNameSpecification(string firstName)
        {
            this.firstName = firstName;
        }

        public override Expression<Func<StubEntityString, bool>> ToExpression()
        {
            return p => p.FirstName == this.firstName;
        }
    }

    private class StubHasMinimumAgeSpecification : Specification<StubEntityString>
    {
        private readonly int age;

        public StubHasMinimumAgeSpecification(int age)
        {
            this.age = age;
        }

        public override Expression<Func<StubEntityString, bool>> ToExpression()
        {
            return p => p.Age >= this.age;
        }
    }
}
