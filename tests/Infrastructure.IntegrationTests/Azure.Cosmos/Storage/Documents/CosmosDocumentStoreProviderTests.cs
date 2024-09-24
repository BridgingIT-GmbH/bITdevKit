// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using DotNet.Testcontainers.Containers;
using Infrastructure.Azure.Storage;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosDocumentStoreProviderTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly CosmosDocumentStoreProvider sut;

    public CosmosDocumentStoreProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        if (this.fixture.CosmosContainer.State == TestcontainersStates.Running)
        {
            this.sut = this.fixture.EnsureCosmosDocumentStoreProvider();
        }
        else
        {
            this.fixture.Output?.WriteLine("skipped test: container not running");
        }
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindAsync_WithoutFilter_ReturnsEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "Mary" + ticks,
            LastName = "Jane",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "Mary" + ticks,
            LastName = "Jane",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "Mary" + ticks,
            LastName = "Jane",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "Mary" + ticks,
            LastName = "Jane",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ListAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "Mary" + ticks,
            LastName = "Jane",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ListAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "Mary" + ticks,
            LastName = "Jane",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "a"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "b"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "c"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
            FirstName = "John",
            LastName = "Doe",
            Age = 18
        });
        await this.sut.UpsertAsync(new DocumentKey("partition", "row" + ticks + "d"),
        new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task UpsertAsync_CreatesOrUpdateEntity()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var entity = new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task DeleteAsync_DeletesEntity()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var documentKey = new DocumentKey("partition", "row" + ticks);
        var entity = new PersonStub
        {
            Id = Guid.NewGuid(),
            Nationality = "USA",
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