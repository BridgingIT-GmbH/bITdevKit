// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using Application.Storage;

[IntegrationTest("Application")]
//[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class InMemoryDocumentStoreProviderTests(ITestOutputHelper output) : TestsBase(output)
{
    private readonly InMemoryDocumentStoreProvider sut = new(XunitLoggerFactory.Create(output));

    [Fact]
    public async Task FindAsync_WithoutFilter_ReturnsEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindAsync<PersonStub>();

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBeGreaterThanOrEqualTo(5); // due to other tests
        result.Any(e => e.FirstName.Equals("Mary" + ticks))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task FindAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindAsync<PersonStub>(new DocumentKey("partition", "row" + ticks), DocumentKeyFilter.FullMatch);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(1);
        result.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    [Fact]
    public async Task FindAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindAsync<PersonStub>(new DocumentKey("partition", "row" + ticks), DocumentKeyFilter.RowKeyPrefixMatch);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(5);
        result.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    [Fact]
    public async Task FindAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindAsync<PersonStub>(new DocumentKey("partition", "row" + ticks), DocumentKeyFilter.RowKeySuffixMatch);

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(1);
        result.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    [Fact]
    public async Task ListAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.ListAsync<PersonStub>();

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBeGreaterThanOrEqualTo(5); // due to other tests
        result.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Any(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Country = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.ListAsync<PersonStub>(new DocumentKey("partition", "row" + ticks));

        // Assert
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(1);
        result.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.All(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task UpsertAsync_CreatesOrUpdateEntity()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var entity = new PersonStub
        {
            Id = Guid.NewGuid(),
            Country = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        };

        // Act
        await this.sut.UpsertAsync(documentKey, entity);

        // Assert
        var result = await this.sut.FindAsync<PersonStub>(documentKey);
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(1);
        result.First()
            .ShouldBe(entity);
    }

    [Fact]
    public async Task DeleteAsync_DeletesEntity()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var entity = new PersonStub
        {
            Id = Guid.NewGuid(),
            Country = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        };

        // Act
        await this.sut.UpsertAsync(documentKey, entity);
        await this.sut.DeleteAsync<PersonStub>(documentKey);

        // Assert
        var result = await this.sut.FindAsync<PersonStub>(documentKey);
        result.ShouldNotBeNull();
        result.Count()
            .ShouldBe(0);
    }
}