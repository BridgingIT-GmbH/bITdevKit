// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure;

using System.Linq.Expressions;
using DotNet.Testcontainers.Containers;
using Infrastructure.Azure;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosSqlProviderTests : IDisposable
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;
    private readonly ICosmosSqlProvider<PersonStub> sut;

    public CosmosSqlProviderTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.output = output;

        if (this.fixture.CosmosContainer.State == TestcontainersStates.Running)
        {
            this.sut = this.fixture.EnsureCosmosSqlProviderPersonStub();
        }
        else
        {
            this.fixture.Output?.WriteLine("skipped test: container not running");
        }
    }

    [SkippableFact]
    public void Constructror_TypeWithoutIdProperty_ThrowsArgumentException()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        // Assert
        Should.Throw<ArgumentException>(() => new CosmosSqlProvider<EmailAddressStub>(o => o
            .Client(this.fixture.EnsureCosmosClient())
            .PartitionKey(e => e.Value)));
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemAsync_ExistingItemId_ItemFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();

        // Act
        var result = await this.sut.ReadItemAsync(item.Id.ToString());

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(item.Id);
        result.FirstName.ShouldBe(item.FirstName);
        result.LastName.ShouldBe(item.LastName);
        result.Email.ShouldBe(item.Email);
        result.Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemAsync_ExistingItemIdWithExplicitPartitionKey_ItemFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();

        // Act
        var result = await this.sut.ReadItemAsync(item.Id.ToString(), item.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(item.Id);
        result.FirstName.ShouldBe(item.FirstName);
        result.LastName.ShouldBe(item.LastName);
        result.Email.ShouldBe(item.Email);
        result.Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemAsync_ExistingItemIdWithWrongExplicitPartitionKey_ItemNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();

        // Act
        var result = await this.sut.ReadItemAsync(item.Id.ToString(), "UNKNOWN");

        // Assert
        result.ShouldBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemAsync_NotExistingItemId_ItemNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange & Act
        var result = await this.sut.ReadItemAsync(Guid.NewGuid()
            .ToString());

        // Assert
        result.ShouldBeNull();
    }

    // TODO: Add tests for FindAllAsync (+specifications)

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task CreateItemAsync_NewItem_ItemInserted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var item = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24) { Id = Guid.NewGuid() };
        item.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));

        // Act
        await this.sut.CreateItemAsync(item);

        // Assert
        var result = await this.sut.ReadItemAsync(item.Id.ToString());
        result.ShouldNotBeNull();
        result.Id.ShouldBe(item.Id);
        result.FirstName.ShouldBe(item.FirstName);
        result.LastName.ShouldBe(item.LastName);
        result.Email.ShouldBe(item.Email);
        result.Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task CreateItemAsync_NewItemWithNoId_ThrowsArgumentException()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var item = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24);

        // Act
        // Assert
        await Should.ThrowAsync<ArgumentException>(async () => await this.sut.CreateItemAsync(item));
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task UpsertItemAsync_NewItem_ItemInserted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var item = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24) { Id = Guid.NewGuid() };

        // Act
        await this.sut.UpsertItemAsync(item);

        // Assert
        var result = await this.sut.ReadItemAsync(item.Id.ToString());
        result.ShouldNotBeNull();
        result.Id.ShouldBe(item.Id);
        result.FirstName.ShouldBe(item.FirstName);
        result.LastName.ShouldBe(item.LastName);
        result.Email.ShouldBe(item.Email);
        result.Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task UpsertItemAsync_NewItemWithNoId_ThrowsArgumentException()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var item = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24);

        // Act
        // Assert
        await Should.ThrowAsync<ArgumentException>(async () => await this.sut.UpsertItemAsync(item));
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task UpsertItemAsync_ExistingItem_ItemUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        item.FirstName = $"John {ticks}";
        item.LastName = $"Doe {ticks}";
        await this.sut.UpsertItemAsync(item);

        // Assert
        var result = await this.sut.ReadItemAsync(item.Id.ToString());
        result.ShouldNotBeNull();
        result.Id.ShouldBe(item.Id);
        result.FirstName.ShouldBe(item.FirstName);
        result.LastName.ShouldBe(item.LastName);
        result.Email.ShouldBe(item.Email);
        result.Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemsAsync_ExistingItemExpression_ItemFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        var results = await this.sut.ReadItemsAsync(e => e.FirstName == item.FirstName);

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.First()
            .ShouldNotBeNull();
        results.First()
            .Id.ShouldBe(item.Id);
        results.First()
            .Email.ShouldBe(item.Email);
        results.First()
            .Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemsAsync_ExistingItemExpressions_ItemFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        var results = await this.sut.ReadItemsAsync(
            new List<Expression<Func<PersonStub, bool>>> { e => e.FirstName == item.FirstName, e => e.LastName == item.LastName, e => e.Age > 10 });

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.First()
            .ShouldNotBeNull();
        results.First()
            .Id.ShouldBe(item.Id);
        results.First()
            .Email.ShouldBe(item.Email);
        results.First()
            .Locations.ShouldNotBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task ReadItemsAsync_ExistingItemExpressionPagedAndOrdered_ItemFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item1 = await this.CreateItemAsync(17);
        var item2 = await this.CreateItemAsync(18);
        var item3 = await this.CreateItemAsync(21);
        var item4 = await this.CreateItemAsync(20); // second page
        var item5 = await this.CreateItemAsync();
        var item6 = await this.CreateItemAsync(19); // second page

        // Act
        var results = await this.sut.ReadItemsAsync(e =>
                e.FirstName == item1.FirstName ||
                e.FirstName == item2.FirstName ||
                e.FirstName == item3.FirstName ||
                e.FirstName == item4.FirstName ||
                e.FirstName == item5.FirstName ||
                e.FirstName == item6.FirstName,
            2,
            2,
            e => e.Age);

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.Count()
            .ShouldBe(2);
        results.ShouldNotContain(item1);
        results.ShouldNotContain(item2);
        results.ShouldNotContain(item3);
        results.ShouldNotContain(item5);
        results.ShouldContain(item4);
        results.ShouldContain(item6);
        results.First()
            .ShouldBe(item6); // age 18
        results.Last()
            .ShouldBe(item4); // age 20
    }

    [SkippableFact]
    public async Task ReadItemsAsync_ExistingItemInvalidExpression_ItemNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        var results = await this.sut.ReadItemsAsync(e => e.FirstName == "UNKNOWN");

        // Assert
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task DeleteItemAsync_ById_ItemDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();

        // Act
        var result = await this.sut.DeleteItemAsync(item.Id.ToString());

        // Assert
        result.ShouldBeTrue();
        (await this.sut.ReadItemAsync(item.Id.ToString())).ShouldBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task DeleteItemAsync_ByIdWithExplicitPartitionKey_ItemDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();

        // Act
        var result = await this.sut.DeleteItemAsync(item.Id.ToString(), item.Id);

        // Assert
        result.ShouldBeTrue();
        (await this.sut.ReadItemAsync(item.Id.ToString())).ShouldBeNull();
    }

    //[Fact(Skip = "The Cosmos DB Linux Emulator Docker image does not run on Microsoft's CI environment (GitHub, Azure DevOps).")] // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/45.
    [SkippableFact]
    public async Task DeleteItemAsync_ByIdWithWrongExplicitPartitionKey_ItemNotDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var item = await this.CreateItemAsync();

        // Act
        var result = await this.sut.DeleteItemAsync(item.Id.ToString(), "UNKNOWN");

        // Assert
        result.ShouldBeFalse();
    }

    public void Dispose() { }

    private async Task<PersonStub> CreateItemAsync(int age = 24)
    {
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var item = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", age) { Id = Guid.NewGuid() };
        item.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));

        return await this.sut.CreateItemAsync(item);
    }
}