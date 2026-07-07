// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using DotNet.Testcontainers.Containers;
using Infrastructure.Azure;

[IntegrationTest("Infrastructure")]
[Collection(nameof(CosmosDocumentStoreTestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosDocumentStoreProviderTests
{
    private readonly CosmosDocumentStoreTestEnvironmentFixture fixture;
    private readonly CosmosDocumentStoreProvider sut;

    public CosmosDocumentStoreProviderTests(ITestOutputHelper output, CosmosDocumentStoreTestEnvironmentFixture fixture)
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
    public async Task FindPageResultAsync_WithoutFilter_ReturnsEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().AllowFullScan().Take(100).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBeGreaterThanOrEqualTo(5); // due to other tests
        result.Value.Items.Any(e => e.FirstName.Equals("Mary" + ticks))
            .ShouldBeTrue();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindPageResultAsync_WithDocumentKeyAndFilterFullMatch_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithFullMatch().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeyPrefix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithRowKeyPrefix().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(5);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task FindPageResultAsync_WithDocumentKeyAndFilterRowKeySuffix_ReturnsFilteredEntities()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithRowKeySuffix().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .FirstName.ShouldBe("Mary" + ticks);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ListPageResultAsync_WithoutFilter_ReturnsDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.ListPageResultAsync<PersonStub>(DocumentQueries.Query().AllowFullScan().Take(100).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBeGreaterThanOrEqualTo(5); // due to other tests
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.Any(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ListPageResultAsync_WithDocumentKeyAndFilter_ReturnsFilteredDocumentKeys()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Mary" + ticks,
                LastName = "Jane",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "a"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "b"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "c"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });
        await this.sut.UpsertResultAsync(new DocumentKey("partition", "row" + ticks + "d"),
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "John",
                LastName = "Doe",
                Age = 18
            });

        // Act
        var result = await this.sut.ListPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey("partition", "row" + ticks).WithFullMatch().Take(10).Build());

        // Assert
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.All(d => d.PartitionKey.Equals("partition"))
            .ShouldBeTrue();
        result.Value.Items.All(d => d.RowKey.StartsWith("row" + ticks))
            .ShouldBeTrue();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task UpsertResultAsync_CreatesOrUpdateEntity()
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
        await this.sut.UpsertResultAsync(documentKey, entity);

        // Assert
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey(documentKey.PartitionKey, documentKey.RowKey).WithFullMatch().Take(10).Build());
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(1);
        result.Value.Items.First()
            .ShouldBe(entity);
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task DeleteResultAsync_DeletesEntity()
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
        await this.sut.UpsertResultAsync(documentKey, entity);
        await this.sut.DeleteResultAsync<PersonStub>(documentKey);

        // Assert
        var result = await this.sut.FindPageResultAsync<PersonStub>(DocumentQueries.Query().ForKey(documentKey.PartitionKey, documentKey.RowKey).WithFullMatch().Take(10).Build());
        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        result.Value.Items.Count
            .ShouldBe(0);
    }

    [SkippableFact]
    public async Task ResultApi_WithRealCosmosProvider_PagesListsCountsAndChecksExistence()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ticks = DateTime.UtcNow.Ticks;
        var partitionKey = "result-partition-" + ticks;
        var firstKey = new DocumentKey(partitionKey, "row-1");
        var secondKey = new DocumentKey(partitionKey, "row-2");
        await this.sut.UpsertResultAsync(
            firstKey,
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Cosmos",
                LastName = "One",
                Age = 31
            },
            timeout.Token);
        await this.sut.UpsertResultAsync(
            secondKey,
            new PersonStub
            {
                Id = Guid.NewGuid(),
                Nationality = "USA",
                FirstName = "Cosmos",
                LastName = "Two",
                Age = 32
            },
            timeout.Token);

        var firstPage = await this.sut.FindPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(1)
                .Build(),
            timeout.Token);
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.Items.Count.ShouldBe(1);
        var keyPage = await this.sut.ListPageResultAsync<PersonStub>(
            DocumentQueries.Query()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Take(10)
                .Build(),
            timeout.Token);
        var count = await this.sut.CountResultAsync<PersonStub>(
            DocumentQueries.Count()
                .ForKey(partitionKey, "row-")
                .WithRowKeyPrefix()
                .Build(),
            timeout.Token);
        var exists = await this.sut.ExistsResultAsync<PersonStub>(firstKey, timeout.Token);
        var loaded = await this.sut.GetResultAsync<PersonStub>(firstKey, timeout.Token);

        keyPage.IsSuccess.ShouldBeTrue();
        keyPage.Value.Items.OrderBy(e => e.RowKey).ShouldBe([firstKey, secondKey]);
        count.IsSuccess.ShouldBeTrue();
        count.Value.ShouldBe(2);
        exists.IsSuccess.ShouldBeTrue();
        exists.Value.ShouldBeTrue();
        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.FirstName.ShouldBe("Cosmos");
    }
}
