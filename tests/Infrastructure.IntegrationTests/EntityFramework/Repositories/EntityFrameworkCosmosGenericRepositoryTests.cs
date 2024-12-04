// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Domain.Repositories;
using DotNet.Testcontainers.Containers;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection4))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkCosmosGenericRepositoryTests : EntityFrameworkGenericRepositoryTestsBase
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;

    public EntityFrameworkCosmosGenericRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.output = output;

        if (this.fixture.CosmosContainer.State != TestcontainersStates.Running)
        {
            this.fixture.Output?.WriteLine("skipped test: container not running");
        }
    }

    [SkippableFact]
    public override async Task DeleteAsync_ByEntity_EntityDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.DeleteAsync_ByEntity_EntityDeleted();
    }

    [SkippableFact]
    public override async Task DeleteAsync_ByIdAsString_EntityDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.DeleteAsync_ByIdAsString_EntityDeleted();
    }

    [SkippableFact]
    public override async Task DeleteAsync_ById_EntityDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.DeleteAsync_ById_EntityDeleted();
    }

    [SkippableFact]
    public override async Task ExistsAsync_ExistingEntityId_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ExistsAsync_ExistingEntityId_EntityFound();
    }

    [SkippableFact]
    public override async Task ExistsAsync_NotExistingEntityId_EntityNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.ExistsAsync_NotExistingEntityId_EntityNotFound();
    }

    [SkippableFact]
    public override async Task FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound();
    }

    [SkippableFact]
    public override async Task FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound();
    }

    [SkippableFact]
    public override async Task FindAllAsync_AnyEntity_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllAsync_AnyEntity_EntitiesFound();
    }

    [SkippableFact]
    public override async Task FindAllAsync_EntityInvalidSpecification_EntitiesNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllAsync_EntityInvalidSpecification_EntitiesNotFound();
    }

    [SkippableFact]
    public override async Task FindAllAsync_EntitySpecifications_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllAsync_EntitySpecifications_EntitiesFound();
    }

    [SkippableFact]
    public override async Task FindAllAsync_EntitySpecification_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllAsync_EntitySpecification_EntitiesFound();
    }

    [SkippableFact]
    public override async Task FindAllPagedAsync_AnyEntity_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllPagedAsync_AnyEntity_EntitiesFound();
    }

    [SkippableFact]
    public override async Task FindAllIdsAsync_AnyEntity_ManyFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindAllIdsAsync_AnyEntity_ManyFound();
    }

    [SkippableFact]
    public override async Task FindOneAsync_ExistingEntityByIdSpecification_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindOneAsync_ExistingEntityByIdSpecification_EntityFound();
    }

    [SkippableFact]
    public override async Task FindOneAsync_ExistingEntityBySpecification_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindOneAsync_ExistingEntityBySpecification_EntityFound();
    }

    [SkippableFact]
    public override async Task FindOneAsync_ExistingEntityIdAsString_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindOneAsync_ExistingEntityIdAsString_EntityFound();
    }

    [SkippableFact]
    public override async Task FindOneAsync_ExistingEntityId_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindOneAsync_ExistingEntityId_EntityFound();
    }

    [SkippableFact]
    public override async Task FindOneAsync_NotExistingEntityId_EntityNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.FindOneAsync_NotExistingEntityId_EntityNotFound();
    }

    [SkippableFact]
    public override async Task InsertAsync_NewEntity_EntityInserted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.InsertAsync_NewEntity_EntityInserted();
    }

    [SkippableFact]
    public override async Task UpsertAsync_ExistingEntityChildRemoval_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertAsync_ExistingEntityChildRemoval_EntityUpdated();
    }

    [SkippableFact] // adjusted as the document is completely replaced and the locations count equals 3
    public override async Task UpsertAsync_ExistingEntityDisconnected_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var entity = await this.InsertEntityAsync();
        using var context = this.GetContext(null, true);
        var sut = this.CreateRepository(context);
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        var disconnectedEntity = new PersonStub
        {
            Id = entity.Id, // has same id as entity > should update existing entity
            FirstName = $"Mary {ticks}",
            LastName = $"Jane {ticks}",
            Age = entity.Age
        };
        disconnectedEntity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        disconnectedEntity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        disconnectedEntity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        var result = await sut.UpsertAsync(disconnectedEntity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(disconnectedEntity.Id);
        existingEntity.FirstName.ShouldBe(disconnectedEntity.FirstName);
        existingEntity.LastName.ShouldBe(disconnectedEntity.LastName);
        existingEntity.Age.ShouldBe(disconnectedEntity.Age);
        existingEntity.Locations.ShouldNotBeNull();
        existingEntity.Locations.ShouldNotBeEmpty();
        existingEntity.Locations.Count.ShouldBe(3);
    }

    [SkippableFact]
    public override async Task UpsertAsync_ExistingEntityNoTracking_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertAsync_ExistingEntityNoTracking_EntityUpdated();
    }

    [SkippableFact]
    public override async Task UpsertAsync_ExistingEntity_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        await base.UpsertAsync_ExistingEntity_EntityUpdated();
    }

    protected override StubDbContext GetContext(string connectionString = null, bool forceNew = false)
    {
        return this.fixture.EnsureCosmosDbContext(this.output, null, true);
    }
}