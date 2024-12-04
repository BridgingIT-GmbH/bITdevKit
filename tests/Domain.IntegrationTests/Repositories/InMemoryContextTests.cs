// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using BridgingIT.DevKit.Domain.Repositories;

public class InMemoryContextTests
{
    public class Constructor
    {
        [Fact]
        public void WhenDefaultConstructor_ThenCreatesEmptyContext()
        {
            // Arrange & Act
            var context = new InMemoryContext<StubEntity>();

            // Assert
            context.Entities.ShouldBeEmpty();
        }

        [Fact]
        public void WhenConstructedWithList_ThenPopulatesEntities()
        {
            // Arrange
            var entities = new List<StubEntity>
            {
                new() { Id = "id-1", Country = "Germany", FirstName = "John", LastName = "Doe", Age = 30 },
                new() { Id = "id-2", Country = "France", FirstName = "Jane", LastName = "Smith", Age = 25 }
            };

            // Act
            var context = new InMemoryContext<StubEntity>(entities);

            // Assert
            context.Entities.Count.ShouldBe(entities.Count);
        }

        [Fact]
        public void WhenConstructedWithNullList_ThenCreatesEmptyContext()
        {
            // Arrange & Act
            var context = new InMemoryContext<StubEntity>(entities: (List<StubEntity>)null);

            // Assert
            context.Entities.ShouldBeEmpty();
        }
    }

    public class TryAddMethod
    {
        [Fact]
        public void WhenAddingNewEntity_ThenSucceeds()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var entity = new StubEntity
            {
                Id = "new-id",
                Country = "Germany",
                FirstName = "Test",
                LastName = "User",
                Age = 25
            };

            // Act
            var result = context.TryAdd(entity);

            // Assert
            result.ShouldBeTrue();
            context.Entities.Count.ShouldBe(1);
            context.Entities.First().ShouldBe(entity);
        }

        [Fact]
        public void WhenAddingDuplicateId_ThenFails()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var id = "duplicate-id";
            var entity1 = new StubEntity
            {
                Id = id,
                Country = "Germany",
                FirstName = "Original",
                LastName = "User",
                Age = 25
            };
            var entity2 = new StubEntity
            {
                Id = id,
                Country = "France",
                FirstName = "Duplicate",
                LastName = "User",
                Age = 30
            };
            context.TryAdd(entity1);

            // Act
            var result = context.TryAdd(entity2);

            // Assert
            result.ShouldBeFalse();
            context.Entities.Count.ShouldBe(1);
            context.Entities.First().FirstName.ShouldBe("Original");
        }

        [Fact]
        public void WhenAddingNull_ThenReturnsFalse()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();

            // Act
            var result = context.TryAdd(null);

            // Assert
            result.ShouldBeFalse();
            context.Entities.ShouldBeEmpty();
        }
    }

    public class TryGetMethod
    {
        [Fact]
        public void WhenEntityExists_ThenReturnsTrue()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var entity = new StubEntity
            {
                Id = "get-id",
                Country = "Germany",
                FirstName = "Get",
                LastName = "Test",
                Age = 35
            };
            context.TryAdd(entity);

            // Act
            var result = context.TryGet(entity.Id, out var retrievedEntity);

            // Assert
            result.ShouldBeTrue();
            retrievedEntity.ShouldBe(entity);
        }

        [Fact]
        public void WhenEntityDoesNotExist_ThenReturnsFalse()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();

            // Act
            var result = context.TryGet("non-existent-id", out var retrievedEntity);

            // Assert
            result.ShouldBeFalse();
            retrievedEntity.ShouldBeNull();
        }
    }

    public class TryRemoveMethod
    {
        [Fact]
        public void WhenEntityExists_ThenRemovesAndReturnsTrue()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var entity = new StubEntity
            {
                Id = "remove-id",
                Country = "Germany",
                FirstName = "Remove",
                LastName = "Test",
                Age = 40
            };
            context.TryAdd(entity);

            // Act
            var result = context.TryRemove(entity.Id, out var removedEntity);

            // Assert
            result.ShouldBeTrue();
            removedEntity.ShouldBe(entity);
            context.Entities.ShouldBeEmpty();
        }

        [Fact]
        public void WhenEntityDoesNotExist_ThenReturnsFalse()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();

            // Act
            var result = context.TryRemove("non-existent-id", out var removedEntity);

            // Assert
            result.ShouldBeFalse();
            removedEntity.ShouldBeNull();
        }
    }

    public class TryUpdateMethod
    {
        [Fact]
        public void WhenEntityExists_ThenUpdatesAndReturnsTrue()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var id = "update-id";
            var entity = new StubEntity
            {
                Id = id,
                Country = "Germany",
                FirstName = "Original",
                LastName = "Test",
                Age = 25
            };
            context.TryAdd(entity);

            var updatedEntity = new StubEntity
            {
                Id = id,
                Country = "France",
                FirstName = "Updated",
                LastName = "User",
                Age = 30
            };

            // Act
            var result = context.TryUpdate(updatedEntity);

            // Assert
            result.ShouldBeTrue();
            context.TryGet(id, out var retrievedEntity);
            retrievedEntity.ShouldBe(updatedEntity);
        }

        [Fact]
        public void WhenEntityDoesNotExist_ThenReturnsFalse()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var entity = new StubEntity
            {
                Id = "non-existent-id",
                Country = "Germany",
                FirstName = "Test",
                LastName = "User",
                Age = 25
            };

            // Act
            var result = context.TryUpdate(entity);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public void WhenUpdatingWithNull_ThenReturnsFalse()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();

            // Act
            var result = context.TryUpdate(null);

            // Assert
            result.ShouldBeFalse();
        }
    }

    public class ConcurrencyTests
    {
        [Fact]
        public async Task WhenMultipleThreadsAccess_ThenMaintainsConsistency()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var tasks = new List<Task>();
            var iterations = 1000;
            var id = "concurrent-id";

            // Act
            for (var i = 0; i < iterations; i++)
            {
                var iteration = i;
                tasks.Add(Task.Run(() =>
                {
                    var entity = new StubEntity
                    {
                        Id = id,
                        Country = "Germany",
                        FirstName = $"Test {iteration}",
                        LastName = "User",
                        Age = 25 + iteration % 50
                    };
                    context.TryAdd(entity);
                    context.TryUpdate(entity);
                    context.TryGet(id, out _);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            context.Entities.Count.ShouldBe(1);
            context.TryGet(id, out var finalEntity);
            finalEntity.ShouldNotBeNull();
        }
    }

    public class ClearMethod
    {
        [Fact]
        public void WhenCalled_ThenRemovesAllEntities()
        {
            // Arrange
            var context = new InMemoryContext<StubEntity>();
            var entities = new[]
            {
                new StubEntity
                {
                    Id = "id-1",
                    Country = "Germany",
                    FirstName = "Test1",
                    LastName = "User",
                    Age = 25
                },
                new StubEntity
                {
                    Id = "id-2",
                    Country = "France",
                    FirstName = "Test2",
                    LastName = "User",
                    Age = 30
                }
            };

            foreach (var entity in entities)
            {
                context.TryAdd(entity);
            }

            // Act
            context.Clear();

            // Assert
            context.Entities.ShouldBeEmpty();
        }
    }
}