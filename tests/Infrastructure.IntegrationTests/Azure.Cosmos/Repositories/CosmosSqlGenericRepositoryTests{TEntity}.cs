// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Domain.Repositories;
using Domain.Specifications;
using DotNet.Testcontainers.Containers;
using Infrastructure.Azure;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class CosmosSqlGenericRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly IGenericRepository<PersonStub> sut;

    public CosmosSqlGenericRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        if (this.fixture.CosmosContainer.State == TestcontainersStates.Running)
        {
            var provider = this.fixture.EnsureCosmosSqlProviderPersonStub();
            this.sut = new CosmosSqlGenericRepository<PersonStub>(o => o.Provider(provider));
        }
        else
        {
            this.fixture.Output?.WriteLine("skipped test: container not running");
        }
    }

    [SkippableFact]
    public async Task DeleteAsync_ByEntity_EntityDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.DeleteAsync(entity);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await this.sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    [SkippableFact]
    public async Task DeleteAsync_ByIdAsString_EntityDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.DeleteAsync(entity.Id.ToString());

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await this.sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    [SkippableFact]
    public async Task DeleteAsync_ById_EntityDeleted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.DeleteAsync(entity.Id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await this.sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    [SkippableFact]
    public async Task ExistsAsync_ExistingEntityId_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.ExistsAsync(entity.Id);

        // Assert
        result.ShouldBeTrue();
    }

    [SkippableFact]
    public async Task ExistsAsync_NotExistingEntityId_EntityNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange

        // Act
        var result = await this.sut.ExistsAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeFalse();
    }

    [SkippableFact]
    public async Task FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity1 = await this.InsertEntityAsync(17);
        var entity2 = await this.InsertEntityAsync(18);
        var entity3 = await this.InsertEntityAsync(20);
        var entity4 = await this.InsertEntityAsync(18);
        var entity5 = await this.InsertEntityAsync();
        // Act
        var results = await this.sut.FindAllAsync(new Specification<PersonStub>(_ => false),
            new FindOptions<PersonStub>(10, 2, new OrderOption<PersonStub>(e => e.Age)));

        // Assert
        //this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(5);
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
    }

    [SkippableFact]
    public async Task FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity1 = await this.InsertEntityAsync(17);
        var entity2 = await this.InsertEntityAsync(16);
        var entity3 = await this.InsertEntityAsync(20);
        var entity4 = await this.InsertEntityAsync(18);
        var entity5 = await this.InsertEntityAsync();

        // Act
        var results = await this.sut.FindAllAsync(new Specification<PersonStub>(e =>
                e.FirstName == entity1.FirstName ||
                e.FirstName == entity2.FirstName ||
                e.FirstName == entity3.FirstName ||
                e.FirstName == entity4.FirstName ||
                e.FirstName == entity5.FirstName),
            new FindOptions<PersonStub>(2, 2, new OrderOption<PersonStub>(e => e.Age)));

        // Assert
        //this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(5);
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.Count()
            .ShouldBe(2);
        results.ShouldNotContain(entity1);
        results.ShouldNotContain(entity2);
        results.ShouldContain(entity3);
        results.ShouldContain(entity4);
        results.ShouldNotContain(entity5);
        results.First()
            .ShouldBe(entity4); // age 18
        results.Last()
            .ShouldBe(entity3); // age 20
    }

    [SkippableFact]
    public async Task FindAllAsync_AnyEntity_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity1 = await this.InsertEntityAsync();
        var entity2 = await this.InsertEntityAsync();
        var entity3 = await this.InsertEntityAsync();
        var entity4 = await this.InsertEntityAsync();
        var entity5 = await this.InsertEntityAsync();

        // Act
        var results = await this.sut.FindAllAsync();

        // Assert
        //this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(5);
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.ShouldContain(entity1);
        results.ShouldContain(entity2);
        results.ShouldContain(entity3);
        results.ShouldContain(entity4);
        results.ShouldContain(entity5);
    }

    [SkippableFact]
    public async Task FindAllAsync_EntityInvalidSpecification_EntitiesNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var results = await this.sut.FindAllAsync(new PersonByEmailSpecification("UNKNOWN"));

        // Assert
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
        results.ShouldNotContain(entity);
    }

    [SkippableFact]
    public async Task FindAllAsync_EntitySpecifications_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var results = await this.sut.FindAllAsync(new List<ISpecification<PersonStub>> { new PersonByEmailSpecification(entity.Email.Value), new PersonIsAdultSpecification() });

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.ShouldContain(entity);
        results.Count()
            .ShouldBe(1);
        results.First()
            .ShouldNotBeNull();
        results.First()
            .Id.ShouldBe(entity.Id);
        results.First()
            .Locations.ShouldNotBeNull();
        results.First()
            .Locations.ShouldNotBeEmpty();
    }

    [SkippableFact]
    public async Task FindAllAsync_EntitySpecification_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var results = await this.sut.FindAllAsync(new PersonByEmailSpecification(entity.Email));

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.ShouldContain(entity);
        results.Count()
            .ShouldBe(1);
        results.First()
            .ShouldNotBeNull();
        results.First()
            .Id.ShouldBe(entity.Id);
        results.First()
            .Locations.ShouldNotBeNull();
        results.First()
            .Locations.ShouldNotBeEmpty();
    }

    [SkippableFact]
    public async Task FindAllPagedAsync_AnyEntity_EntitiesFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity1 = await this.InsertEntityAsync(17);
        var entity2 = await this.InsertEntityAsync(18);
        var entity3 = await this.InsertEntityAsync(21);
        var entity4 = await this.InsertEntityAsync(20); // second page
        var entity5 = await this.InsertEntityAsync();
        var entity6 = await this.InsertEntityAsync(19); // second page

        // Act
        var results = await this.sut.FindAllPagedResultAsync(new Specification<PersonStub>(e =>
                e.FirstName == entity1.FirstName ||
                e.FirstName == entity2.FirstName ||
                e.FirstName == entity3.FirstName ||
                e.FirstName == entity4.FirstName ||
                e.FirstName == entity5.FirstName ||
                e.FirstName == entity6.FirstName),
            eh => eh.Age,
            //ordering: nameof(PersonStub.Age) // this is flawed
            2,
            2);

        // Assert
        //this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(6);
        results.ShouldNotBeNull();
        results.Value.ShouldNotBeNull();
        results.Value.ShouldNotBeEmpty();
        results.TotalCount.ShouldBe(6);
        results.TotalPages.ShouldBe(3);
        results.CurrentPage.ShouldBe(2);
        results.HasNextPage.ShouldBeTrue();
        results.HasPreviousPage.ShouldBeTrue();
        results.Value.Count()
            .ShouldBe(2);
        results.Value.ShouldNotContain(entity1);
        results.Value.ShouldNotContain(entity2);
        results.Value.ShouldNotContain(entity3);
        results.Value.ShouldNotContain(entity5);
        results.Value.ShouldContain(entity4);
        results.Value.ShouldContain(entity6);
        results.Value.First()
            .ShouldBe(entity6); // age 19
        results.Value.Last()
            .ShouldBe(entity4); // age 20
    }

    [SkippableFact]
    public async Task FindOneAsync_ExistingEntityByIdSpecification_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.FindOneAsync(new Specification<PersonStub>(e => e.Id == entity.Id));

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.FirstName.ShouldBe(entity.FirstName);
        result.LastName.ShouldBe(entity.LastName);
        result.Locations.ShouldNotBeNull();
        result.Locations.ShouldNotBeEmpty();
    }

    [SkippableFact]
    public async Task FindOneAsync_ExistingEntityBySpecification_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.FindOneAsync(new Specification<PersonStub>(e => e.FirstName == entity.FirstName));

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.FirstName.ShouldBe(entity.FirstName);
        result.LastName.ShouldBe(entity.LastName);
        result.Locations.ShouldNotBeNull();
        result.Locations.ShouldNotBeEmpty();
    }

    [SkippableFact]
    public async Task FindOneAsync_ExistingEntityIdAsString_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.FindOneAsync(entity.Id.ToString());

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.FirstName.ShouldBe(entity.FirstName);
        result.LastName.ShouldBe(entity.LastName);
        result.Locations.ShouldNotBeNull();
        result.Locations.ShouldNotBeEmpty();
    }

    [SkippableFact]
    public async Task FindOneAsync_ExistingEntityId_EntityFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var entity = await this.InsertEntityAsync();

        // Act
        var result = await this.sut.FindOneAsync(entity.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.FirstName.ShouldBe(entity.FirstName);
        result.LastName.ShouldBe(entity.LastName);
        result.Locations.ShouldNotBeNull();
        result.Locations.ShouldNotBeEmpty();
    }

    [SkippableFact]
    public async Task FindOneAsync_NotExistingEntityId_EntityNotFound()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        // Act
        var result = await this.sut.FindOneAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [SkippableFact]
    public async Task InsertAsync_NewEntity_EntityInserted()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24);
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));

        // Act
        var result = await this.sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Inserted);
        var existingEntity = await this.sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Id.ShouldNotBe(Guid.Empty);
        existingEntity.FirstName.ShouldBe(entity.FirstName);
        existingEntity.LastName.ShouldBe(entity.LastName);
        existingEntity.Locations.ShouldNotBeNull();
        existingEntity.Locations.ShouldNotBeEmpty();
        existingEntity.Locations.Count.ShouldBe(4);
        existingEntity.Locations.Any(l => l.Name ==
                entity.Locations.First()
                    .Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name ==
                entity.Locations.Last()
                    .Name)
            .ShouldBeTrue();
    }

    [SkippableFact]
    public async Task UpsertAsync_ExistingEntityChildRemoval_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24);
        var location1 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location1);
        var location2 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location2);
        var location3 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location3);
        var location4 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location4);

        // Act
        await this.sut.UpsertAsync(entity);
        entity.RemoveLocation(location1);
        entity.RemoveLocation(location2);
        var location5 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location5);
        var result = await this.sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await this.sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Id.ShouldNotBe(Guid.Empty);
        existingEntity.FirstName.ShouldBe(entity.FirstName);
        existingEntity.LastName.ShouldBe(entity.LastName);
        existingEntity.Locations.ShouldNotBeNull();
        existingEntity.Locations.ShouldNotBeEmpty();
        existingEntity.Locations.Count.ShouldBe(3);
        existingEntity.Locations.Any(l => l.Name == location1.Name)
            .ShouldBeFalse(); // removed
        existingEntity.Locations.Any(l => l.Name == location2.Name)
            .ShouldBeFalse(); // removed
        existingEntity.Locations.Any(l => l.Name == location3.Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name == location4.Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name == location5.Name)
            .ShouldBeTrue();
    }

    [SkippableFact]
    public async Task UpsertAsync_ExistingEntityDisconnected_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var entity = await this.InsertEntityAsync();
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
        var result = await this.sut.UpsertAsync(disconnectedEntity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await this.sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(disconnectedEntity.Id);
        existingEntity.FirstName.ShouldBe(disconnectedEntity.FirstName);
        existingEntity.LastName.ShouldBe(disconnectedEntity.LastName);
        existingEntity.Age.ShouldBe(disconnectedEntity.Age);
        existingEntity.Locations.ShouldNotBeNull();
        existingEntity.Locations.ShouldNotBeEmpty();
        existingEntity.Locations.Count.ShouldBe(3); // locations get overwritten with a new collection
        existingEntity.Locations.Any(l => l.Name ==
                disconnectedEntity.Locations.First()
                    .Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name ==
                disconnectedEntity.Locations.Last()
                    .Name)
            .ShouldBeTrue();
    }

    [SkippableFact]
    public async Task UpsertAsync_ExistingEntityNoTracking_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var entity = await this.InsertEntityAsync();
        var ticks = DateTime.UtcNow.Ticks;
        entity = await this.sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });

        // Act
        entity.FirstName = $"John {ticks}";
        entity.LastName = $"Doe {ticks}";
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));
        var result = await this.sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await this.sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.FirstName.ShouldBe(entity.FirstName);
        existingEntity.LastName.ShouldBe(entity.LastName);
        existingEntity.Locations.ShouldNotBeNull();
        existingEntity.Locations.ShouldNotBeEmpty();
        existingEntity.Locations.Count.ShouldBe(4);
        existingEntity.Locations.Any(l => l.Name ==
                entity.Locations.First()
                    .Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name ==
                entity.Locations.Last()
                    .Name)
            .ShouldBeTrue();
    }

    [SkippableFact]
    public async Task UpsertAsync_ExistingEntity_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker();
        var entity = await this.InsertEntityAsync();
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        entity.FirstName = $"John {ticks}";
        entity.LastName = $"Doe {ticks}";
        var location1 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location1);
        var location2 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location2);
        var location3 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddLocation(location3);
        var result = await this.sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await this.sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Id.ShouldNotBe(Guid.Empty);
        existingEntity.FirstName.ShouldBe(entity.FirstName);
        existingEntity.LastName.ShouldBe(entity.LastName);
        existingEntity.Locations.ShouldNotBeNull();
        existingEntity.Locations.ShouldNotBeEmpty();
        existingEntity.Locations.Count.ShouldBe(4);
        existingEntity.Locations.Any(l => l.Name == location1.Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name == location2.Name)
            .ShouldBeTrue();
        existingEntity.Locations.Any(l => l.Name == location3.Name)
            .ShouldBeTrue();
    }

    private async Task<PersonStub> InsertEntityAsync(int age = 24)
    {
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", age);
        entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country()));

        return await this.sut.InsertAsync(entity);
    }
}