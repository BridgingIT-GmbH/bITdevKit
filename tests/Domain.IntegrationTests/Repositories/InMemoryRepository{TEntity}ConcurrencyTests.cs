// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using FizzWare.NBuilder;

[IntegrationTest("Domain")]
public class InMemoryRepositoryConcurrencyTests
{
    private readonly List<StubEntityString> entities;

    public InMemoryRepositoryConcurrencyTests()
    {
        this.entities = Builder<StubEntityString>
            .CreateListOfSize(5)
            .All()
            .With(x => x.Country, "Germany")
            .With(x => x.ConcurrencyVersion, Guid.NewGuid())
            .Build()
            .ToList();
    }

    [Fact]
    public async Task WhenUpdatingWithSameVersion_ThenSucceeds()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities))
            .EnableOptimisticConcurrency(true));

        var entity = this.entities.First();
        var originalVersion = entity.ConcurrencyVersion;
        entity.FirstName = "UpdatedName";

        // Act
        var result = await sut.UpdateAsync(entity).AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.ConcurrencyVersion.ShouldNotBe(originalVersion);
        result.FirstName.ShouldBe("UpdatedName");
    }

    [Fact]
    public async Task WhenUpdatingWithDifferentVersion_ThenThrowsConcurrencyException()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities))
            .EnableOptimisticConcurrency(true));

        var entity = this.entities.First().Clone();
        var originalVersion = entity.ConcurrencyVersion;
        entity.ConcurrencyVersion = Guid.NewGuid(); // Different version
        entity.FirstName = "UpdatedName";

        // Act & Assert
        var exception = await Should.ThrowAsync<ConcurrencyException>(
            async () => await sut.UpdateAsync(entity).AnyContext()
        );

        exception.ShouldNotBeNull();
        exception.EntityId.ShouldBe(entity.Id);
        exception.ExpectedVersion.ShouldBe(entity.ConcurrencyVersion);
        exception.ActualVersion.ShouldBe(originalVersion);
    }

    [Fact]
    public async Task WhenInsertingNewEntity_ThenVersionIsAssigned()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>())
            .EnableOptimisticConcurrency(true));

        var newEntity = new StubEntityString
        {
            Id = "new-id",
            FirstName = "New",
            LastName = "Entity",
            Country = "Germany",
            Age = 25
        };

        // Act
        var result = await sut.InsertAsync(newEntity).AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.ConcurrencyVersion.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task WhenConcurrencyIsDisabled_ThenAllowsVersionMismatch()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities))
            .EnableOptimisticConcurrency(false));

        var entity = this.entities.First();
        var originalVersion = entity.ConcurrencyVersion;
        entity.ConcurrencyVersion = Guid.NewGuid(); // Different version
        entity.FirstName = "UpdatedName";

        // Act
        var result = await sut.UpdateAsync(entity).AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.ConcurrencyVersion.ShouldNotBe(originalVersion);
        result.ConcurrencyVersion.ShouldBe(entity.ConcurrencyVersion);
        result.FirstName.ShouldBe("UpdatedName");
    }

    [Fact]
    public async Task WhenParallelUpdates_ThenOnlyOneSucceeds()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities))
            .EnableOptimisticConcurrency(true));

        var entity = this.entities.First();
        var originalVersion = entity.ConcurrencyVersion;
        var updates = new List<Task>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            var updateEntity = new StubEntityString
            {
                Id = entity.Id,
                ConcurrencyVersion = originalVersion,
                FirstName = $"Update{i}",
                LastName = entity.LastName,
                Country = entity.Country,
                Age = entity.Age
            };

            updates.Add(Task.Run(async () =>
            {
                try
                {
                    await sut.UpdateAsync(updateEntity).AnyContext();
                }
                catch (ConcurrencyException)
                {
                    // Expected for parallel updates
                }
            }));
        }

        await Task.WhenAll(updates);

        // Assert
        var finalEntity = await sut.FindOneAsync(entity.Id).AnyContext();
        finalEntity.ShouldNotBeNull();
        finalEntity.ConcurrencyVersion.ShouldNotBe(originalVersion);
        finalEntity.FirstName.ShouldStartWith("Update"); // One of the updates succeeded
    }

    [Fact]
    public async Task WhenEmptyVersion_ThenAllowsUpdate()
    {
        // Arrange
        var sut = new InMemoryRepository<StubEntityString>(o => o
            .Context(new InMemoryContext<StubEntityString>(this.entities))
            .EnableOptimisticConcurrency(true));

        var entity = this.entities.First();
        entity.ConcurrencyVersion = Guid.Empty;
        entity.FirstName = "UpdatedName";

        // Act
        var result = await sut.UpdateAsync(entity).AnyContext();

        // Assert
        result.ShouldNotBeNull();
        result.ConcurrencyVersion.ShouldNotBe(Guid.Empty);
        result.FirstName.ShouldBe("UpdatedName");
    }

    private class StubEntityString : AggregateRoot<string>, IConcurrency
    {
        public string Country { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public Guid ConcurrencyVersion { get; set; }
    }
}