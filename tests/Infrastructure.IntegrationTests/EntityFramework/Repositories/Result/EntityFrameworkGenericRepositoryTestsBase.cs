// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Domain;
using Domain.Repositories;
using Infrastructure.EntityFramework.Repositories;

public abstract class EntityFrameworkGenericRepositoryResultTestsBase
{
    private readonly Faker faker = new();

    // public virtual async Task DeleteAsync_ByEntity_EntityDeleted()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.DeleteAsync(entity);
    //
    //     // Assert
    //     result.ShouldBe(RepositoryActionResult.Deleted);
    //     (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    // }
    //
    // public virtual async Task DeleteAsync_ByIdAsString_EntityDeleted()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.DeleteAsync(entity.Id.ToString());
    //
    //     // Assert
    //     result.ShouldBe(RepositoryActionResult.Deleted);
    //     (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    // }
    //
    // public virtual async Task DeleteAsync_ById_EntityDeleted()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.DeleteAsync(entity.Id);
    //
    //     // Assert
    //     result.ShouldBe(RepositoryActionResult.Deleted);
    //     (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    // }
    //
    // public virtual async Task ExistsAsync_ExistingEntityId_EntityFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.ExistsAsync(entity.Id);
    //
    //     // Assert
    //     this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
    //     result.ShouldBeTrue();
    // }
    //
    // public virtual async Task ExistsAsync_NotExistingEntityId_EntityNotFound()
    // {
    //     // Arrange
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.ExistsAsync(Guid.NewGuid());
    //
    //     // Assert
    //     result.ShouldBeFalse();
    // }
    //
    // public virtual async Task FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound()
    // {
    //     // Arrange
    //     var entity1 = await this.InsertEntityAsync(17);
    //     var entity2 = await this.InsertEntityAsync(18);
    //     var entity3 = await this.InsertEntityAsync(20);
    //     var entity4 = await this.InsertEntityAsync(18);
    //     var entity5 = await this.InsertEntityAsync();
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllAsync(new Specification<PersonStub>(_ => false),
    //         new FindOptions<PersonStub>(10, 2, new OrderOption<PersonStub>(e => e.Age)));
    //
    //     // Assert
    //     this.GetContext()
    //         .Persons.AsNoTracking()
    //         .ToList()
    //         .Count.ShouldBeGreaterThanOrEqualTo(5);
    //     results.ShouldNotBeNull();
    //     results.ShouldBeEmpty();
    // }
    //
    // public virtual async Task FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound()
    // {
    //     // Arrange
    //     var entity1 = await this.InsertEntityAsync(17);
    //     var entity2 = await this.InsertEntityAsync(16);
    //     var entity3 = await this.InsertEntityAsync(20);
    //     var entity4 = await this.InsertEntityAsync(18);
    //     var entity5 = await this.InsertEntityAsync();
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllAsync(new Specification<PersonStub>(e =>
    //             e.FirstName == entity1.FirstName ||
    //             e.FirstName == entity2.FirstName ||
    //             e.FirstName == entity3.FirstName ||
    //             e.FirstName == entity4.FirstName ||
    //             e.FirstName == entity5.FirstName),
    //         new FindOptions<PersonStub>(2, 2, new OrderOption<PersonStub>(e => e.Age)));
    //
    //     // Assert
    //     this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(5);
    //     results.ShouldNotBeNull();
    //     results.ShouldNotBeEmpty();
    //     results.Count().ShouldBe(2);
    //     results.ShouldNotContain(entity1);
    //     results.ShouldNotContain(entity2);
    //     results.ShouldContain(entity3);
    //     results.ShouldContain(entity4);
    //     results.ShouldNotContain(entity5);
    //     results.First().ShouldBe(entity4); // age 18
    //     results.Last().ShouldBe(entity3); // age 20
    // }

    public virtual async Task FindAllResultAsync_AnyEntity_EntitiesFound()
    {
        // Arrange
        var entity1 = await this.InsertEntityAsync();
        var entity2 = await this.InsertEntityAsync();
        var entity3 = await this.InsertEntityAsync();
        var entity4 = await this.InsertEntityAsync();
        var entity5 = await this.InsertEntityAsync();

        var sut = this.CreateRepository(this.GetContext());

        // Act
        var result = await sut.FindAllResultAsync();

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeEmpty();
        result.Value.Select(e => e.Id).ShouldContain(entity1.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity2.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity3.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity4.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity5.Id);
    }

    // public virtual async Task FindAllAsync_EntityInvalidSpecification_EntitiesNotFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllAsync(new PersonByEmailSpecification("UNKNOWN"));
    //
    //     // Assert
    //     results.ShouldNotBeNull();
    //     results.ShouldBeEmpty();
    //     results.ShouldNotContain(entity);
    // }
    //
    // public virtual async Task FindAllAsync_EntitySpecifications_EntitiesFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllAsync(new List<ISpecification<PersonStub>> { new PersonByEmailSpecification(entity.Email.Value), new PersonIsAdultSpecification() });
    //
    //     // Assert
    //     results.ShouldNotBeNull();
    //     results.ShouldNotBeEmpty();
    //     results.ShouldContain(entity);
    //     results.Count().ShouldBe(1);
    //     results.First().ShouldNotBeNull();
    //     results.First().Id.ShouldBe(entity.Id);
    //     results.First().Locations.ShouldNotBeNull();
    //     results.First().Locations.ShouldNotBeEmpty();
    // }
    //
    // public virtual async Task FindAllAsync_EntitySpecification_EntitiesFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllAsync(new PersonByEmailSpecification(entity.Email));
    //
    //     // Assert
    //     results.ShouldNotBeNull();
    //     results.ShouldNotBeEmpty();
    //     results.ShouldContain(entity);
    //     results.Count().ShouldBe(1);
    //     results.First().ShouldNotBeNull();
    //     results.First().Id.ShouldBe(entity.Id);
    //     results.First().Locations.ShouldNotBeNull();
    //     results.First().Locations.ShouldNotBeEmpty();
    // }
    //
    // public virtual async Task FindAllPagedAsync_AnyEntity_EntitiesFound()
    // {
    //     // Arrange
    //     var entity1 = await this.InsertEntityAsync(22);
    //     var entity2 = await this.InsertEntityAsync(21);
    //     var entity3 = await this.InsertEntityAsync(20);
    //     var entity4 = await this.InsertEntityAsync(18);
    //     var entity5 = await this.InsertEntityAsync();
    //     var entity6 = await this.InsertEntityAsync(25);
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllResultPagedAsync(new Specification<PersonStub>(e =>
    //             e.FirstName == entity1.FirstName ||
    //             e.FirstName == entity2.FirstName ||
    //             e.FirstName == entity3.FirstName ||
    //             e.FirstName == entity4.FirstName ||
    //             e.FirstName == entity5.FirstName ||
    //             e.FirstName == entity6.FirstName),
    //         nameof(PersonStub.Age),
    //         1,
    //         2);
    //
    //     // Assert
    //     this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(6);
    //     results.ShouldNotBeNull();
    //     results.ShouldBeSuccess();
    //     results.Value.ShouldNotBeNull();
    //     results.Value.ShouldNotBeEmpty();
    //     results.TotalCount.ShouldBe(6);
    //     results.TotalPages.ShouldBe(3);
    //     results.CurrentPage.ShouldBe(1);
    //     results.HasNextPage.ShouldBeTrue();
    //     results.HasPreviousPage.ShouldBeFalse();
    //     results.Value.Count().ShouldBe(2);
    //     results.Value.ShouldNotContain(entity1);
    //     results.Value.ShouldNotContain(entity2);
    //     results.Value.ShouldContain(entity3);
    //     results.Value.ShouldContain(entity4);
    //     results.Value.ShouldNotContain(entity5);
    //     results.Value.ShouldNotContain(entity6);
    //     results.Value.First().ShouldBe(entity4); // age 18
    //     results.Value.Last().ShouldBe(entity3); // age 20
    // }
    //
    // public virtual async Task FindAllIdsAsync_AnyEntity_ManyFound()
    // {
    //     // Arrange
    //     var entity1 = await this.InsertEntityAsync(22);
    //     var entity2 = await this.InsertEntityAsync(21);
    //     var entity3 = await this.InsertEntityAsync(20);
    //     var entity4 = await this.InsertEntityAsync(18);
    //     var entity5 = await this.InsertEntityAsync();
    //     var entity6 = await this.InsertEntityAsync(25);
    //
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var results = await sut.FindAllIdsResultAsync<PersonStub, Guid>(new Specification<PersonStub>(e =>
    //         e.FirstName == entity1.FirstName ||
    //         e.FirstName == entity2.FirstName ||
    //         e.FirstName == entity3.FirstName ||
    //         e.FirstName == entity4.FirstName ||
    //         e.FirstName == entity5.FirstName ||
    //         e.FirstName == entity6.FirstName));
    //
    //     // Assert
    //     this.GetContext().Persons.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(6);
    //     results.ShouldNotBeNull();
    //     results.ShouldBeSuccess();
    //     results.Value.ShouldNotBeNull();
    //     results.Value.ShouldNotBeEmpty();
    //     results.Value.Count().ShouldBe(6);
    // }
    //
    // public virtual async Task FindOneAsync_ExistingEntityByIdSpecification_EntityFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.FindOneAsync(new Specification<PersonStub>(e => e.Id == entity.Id));
    //
    //     // Assert
    //     result.ShouldNotBeNull();
    //     result.Id.ShouldBe(entity.Id);
    //     result.FirstName.ShouldBe(entity.FirstName);
    //     result.LastName.ShouldBe(entity.LastName);
    //     result.Locations.ShouldNotBeNull();
    //     result.Locations.ShouldNotBeEmpty();
    //     result.Status.Equals(Status.Active);
    // }
    //
    // public virtual async Task FindOneAsync_ExistingEntityBySpecification_EntityFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.FindOneAsync(new Specification<PersonStub>(e => e.FirstName == entity.FirstName));
    //
    //     // Assert
    //     result.ShouldNotBeNull();
    //     result.Id.ShouldBe(entity.Id);
    //     result.FirstName.ShouldBe(entity.FirstName);
    //     result.LastName.ShouldBe(entity.LastName);
    //     result.Locations.ShouldNotBeNull();
    //     result.Locations.ShouldNotBeEmpty();
    //     result.Status.Equals(Status.Active);
    // }
    //
    // public virtual async Task FindOneAsync_ExistingEntityIdAsString_EntityFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.FindOneAsync(entity.Id.ToString());
    //
    //     // Assert
    //     result.ShouldNotBeNull();
    //     result.Id.ShouldBe(entity.Id);
    //     result.FirstName.ShouldBe(entity.FirstName);
    //     result.LastName.ShouldBe(entity.LastName);
    //     result.Locations.ShouldNotBeNull();
    //     result.Locations.ShouldNotBeEmpty();
    //     result.Status.Equals(Status.Active);
    // }
    //
    // public virtual async Task FindOneAsync_ExistingEntityId_EntityFound()
    // {
    //     // Arrange
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.FindOneAsync(entity.Id);
    //
    //     // Assert
    //     result.ShouldNotBeNull();
    //     result.Id.ShouldBe(entity.Id);
    //     result.FirstName.ShouldBe(entity.FirstName);
    //     result.LastName.ShouldBe(entity.LastName);
    //     result.Locations.ShouldNotBeNull();
    //     result.Locations.ShouldNotBeEmpty();
    //     result.Status.Equals(Status.Active);
    // }
    //
    // public virtual async Task FindOneAsync_NotExistingEntityId_EntityNotFound()
    // {
    //     // Arrange
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.FindOneAsync(Guid.NewGuid());
    //
    //     // Assert
    //     result.ShouldBeNull();
    // }
    //
    // public virtual async Task InsertAsync_NewEntity_EntityInserted()
    // {
    //     // Arrange
    //     var faker = new Faker();
    //     var ticks = DateTime.UtcNow.Ticks;
    //     var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24, Status.Active);
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     var result = await sut.UpsertAsync(entity);
    //
    //     // Assert
    //     result.action.ShouldBe(RepositoryActionResult.Inserted);
    //     var existingEntity = await sut.FindOneAsync(entity.Id);
    //     existingEntity.ShouldNotBeNull();
    //     existingEntity.Id.ShouldBe(entity.Id);
    //     existingEntity.Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.FirstName.ShouldBe(entity.FirstName);
    //     existingEntity.Status.Equals(Status.Active);
    //     existingEntity.LastName.ShouldBe(entity.LastName);
    //     existingEntity.Locations.ShouldNotBeNull();
    //     existingEntity.Locations.ShouldNotBeEmpty();
    //     existingEntity.Locations.Count.ShouldBe(4);
    //     existingEntity.Locations[0].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[1].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[2].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[3].Id.ShouldNotBe(Guid.Empty);
    // }
    //
    // public virtual async Task UpsertAsync_ExistingEntityChildRemoval_EntityUpdated()
    // {
    //     // Arrange
    //     var faker = new Faker();
    //     var ticks = DateTime.UtcNow.Ticks;
    //     var entity = new PersonStub($"John {ticks}", $"Doe {ticks}", $"John.Doe{ticks}@gmail.com", 24);
    //     var location1 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location1);
    //     var location2 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location2);
    //     var location3 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location3);
    //     var location4 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location4);
    //     var sut = this.CreateRepository(this.GetContext());
    //
    //     // Act
    //     await sut.UpsertAsync(entity);
    //     entity.RemoveLocation(location1);
    //     entity.RemoveLocation(location2);
    //     var location5 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location5);
    //     var result = await sut.UpsertAsync(entity);
    //
    //     // Assert
    //     result.action.ShouldBe(RepositoryActionResult.Updated);
    //     var existingEntity = await sut.FindOneAsync(entity.Id);
    //     existingEntity.ShouldNotBeNull();
    //     existingEntity.Id.ShouldBe(entity.Id);
    //     existingEntity.Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.FirstName.ShouldBe(entity.FirstName);
    //     existingEntity.LastName.ShouldBe(entity.LastName);
    //     existingEntity.Locations.ShouldNotBeNull();
    //     existingEntity.Locations.ShouldNotBeEmpty();
    //     existingEntity.Locations.Count.ShouldBe(3);
    //     existingEntity.Locations[0].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[1].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[2].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations.ShouldNotContain(location1);
    //     existingEntity.Locations.ShouldNotContain(location2);
    //     existingEntity.Locations.ShouldContain(location3);
    //     existingEntity.Locations.ShouldContain(location4);
    //     existingEntity.Locations.ShouldContain(location5);
    // }
    //
    // public virtual async Task UpsertAsync_ExistingEntityDisconnected_EntityUpdated()
    // {
    //     // Arrange
    //     var faker = new Faker();
    //     var entity = await this.InsertEntityAsync();
    //     using var context = this.GetContext(null, true);
    //     var sut = this.CreateRepository(context);
    //     var ticks = DateTime.UtcNow.Ticks;
    //
    //     // Act
    //     var disconnectedEntity = new PersonStub
    //     {
    //         Id = entity.Id, // has same id as entity > should update existing entity
    //         FirstName = $"Mary {ticks}",
    //         LastName = $"Jane {ticks}",
    //         Age = entity.Age
    //     };
    //     disconnectedEntity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     disconnectedEntity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     disconnectedEntity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     var result = await sut.UpsertAsync(disconnectedEntity);
    //
    //     // Assert
    //     result.action.ShouldBe(RepositoryActionResult.Updated);
    //     var existingEntity = await sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });
    //     existingEntity.ShouldNotBeNull();
    //     existingEntity.Id.ShouldBe(disconnectedEntity.Id);
    //     existingEntity.FirstName.ShouldBe(disconnectedEntity.FirstName);
    //     existingEntity.LastName.ShouldBe(disconnectedEntity.LastName);
    //     existingEntity.Age.ShouldBe(disconnectedEntity.Age);
    //     existingEntity.Locations.ShouldNotBeNull();
    //     existingEntity.Locations.ShouldNotBeEmpty();
    //     existingEntity.Locations.Count.ShouldBe(4);
    // }
    //
    // public virtual async Task UpsertAsync_ExistingEntityNoTracking_EntityUpdated()
    // {
    //     // Arrange
    //     var faker = new Faker();
    //     var entity = await this.InsertEntityAsync();
    //     using var context = this.GetContext(null, true);
    //     var sut = this.CreateRepository(context);
    //     var ticks = DateTime.UtcNow.Ticks;
    //     entity = await sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });
    //
    //     // Act
    //     entity.FirstName = $"John {ticks}";
    //     entity.LastName = $"Doe {ticks}";
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     entity.AddLocation(LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country()));
    //     var result = await sut.UpsertAsync(entity);
    //
    //     // Assert
    //     result.action.ShouldBe(RepositoryActionResult.Updated);
    //     var existingEntity = await sut.FindOneAsync(entity.Id, new FindOptions<PersonStub> { NoTracking = true });
    //     existingEntity.ShouldNotBeNull();
    //     existingEntity.Id.ShouldBe(entity.Id);
    //     existingEntity.FirstName.ShouldBe(entity.FirstName);
    //     existingEntity.LastName.ShouldBe(entity.LastName);
    //     existingEntity.Locations.ShouldNotBeNull();
    //     existingEntity.Locations.ShouldNotBeEmpty();
    //     existingEntity.Locations.Count.ShouldBe(4);
    // }
    //
    // public virtual async Task UpsertAsync_ExistingEntity_EntityUpdated()
    // {
    //     // Arrange
    //     var faker = new Faker();
    //     var entity = await this.InsertEntityAsync();
    //     var sut = this.CreateRepository(this.GetContext());
    //     var ticks = DateTime.UtcNow.Ticks;
    //
    //     // Act
    //     entity.FirstName = $"John {ticks}";
    //     entity.LastName = $"Doe {ticks}";
    //     var location1 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location1);
    //     var location2 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location2);
    //     var location3 = LocationStub.Create(faker.Company.CompanyName(),
    //         faker.Address.StreetAddress(),
    //         faker.Address.BuildingNumber(),
    //         faker.Address.ZipCode(),
    //         faker.Address.City(),
    //         faker.Address.Country());
    //     entity.AddLocation(location3);
    //     var result = await sut.UpsertAsync(entity);
    //
    //     // Assert
    //     result.action.ShouldBe(RepositoryActionResult.Updated);
    //     var existingEntity = await sut.FindOneAsync(entity.Id);
    //     existingEntity.ShouldNotBeNull();
    //     existingEntity.Id.ShouldBe(entity.Id);
    //     existingEntity.Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.FirstName.ShouldBe(entity.FirstName);
    //     existingEntity.LastName.ShouldBe(entity.LastName);
    //     existingEntity.Locations.ShouldNotBeNull();
    //     existingEntity.Locations.ShouldNotBeEmpty();
    //     existingEntity.Locations.Count.ShouldBe(4);
    //     existingEntity.Locations[0].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[1].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[2].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations[3].Id.ShouldNotBe(Guid.Empty);
    //     existingEntity.Locations.ShouldContain(entity.Locations[0]);
    //     existingEntity.Locations.ShouldContain(location1);
    //     existingEntity.Locations.ShouldContain(location2);
    //     existingEntity.Locations.ShouldContain(location3);
    // }

    public virtual async Task FindAllResultPagedAsync_AnyEntityWithFilter_EntitiesFound()
    {
        SpecificationResolver.Clear();
        SpecificationResolver.Register<PersonStub, PersonByEmailSpecification>("PersonHasEmail");

        // Arrange
        var entity1 = await this.InsertEntityAsync();
        var entity2 = await this.InsertEntityAsync();
        var entity3 = await this.InsertEntityAsync();
        var entity4 = await this.InsertEntityAsync();
        var entity5 = await this.InsertEntityAsync();

        var filter = new FilterModel
        {
            Page = 1,
            PageSize = 999999,
            Filters =
            [
                new FilterCriteria // standard filter: equals
                    { Field = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 18, Logic = FilterLogicOperator.Or },
                new FilterCriteria // standard filter: ends with
                    { Field = "FirstName", Operator = FilterOperator.EndsWith, Value = entity1.FirstName[2..], Logic = FilterLogicOperator.Or },
                new FilterCriteria // standard filter: contains
                    { Field = "FirstName", Operator = FilterOperator.Contains, Value = entity1.FirstName[..3], Logic = FilterLogicOperator.Or },
                new FilterCriteria // custom filter: named specification
                {
                    CustomType = FilterCustomType.NamedSpecification,
                    SpecificationName = "PersonHasEmail",
                    SpecificationArguments = [entity1.Email.Value],
                    Logic = FilterLogicOperator.Or
                },
                new FilterCriteria // custom filter: full text search
                {
                    CustomType = FilterCustomType.FullTextSearch,
                    CustomParameters = new Dictionary<string, object>
                    {
                        ["searchTerm"] = entity1.FirstName,
                        ["fields"] = new[] { "FirstName", "LastName" }
                    }
                }
            ],
            Orderings = [new FilterOrderCriteria { Field = "LastName", Direction = OrderDirection.Ascending }]
        };

        var sut = this.CreateRepository(this.GetContext());

        // Act
        var result = await sut.FindAllResultPagedAsync(filter);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeEmpty();
        result.Value.Select(e => e.Id).ShouldContain(entity1.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity2.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity3.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity4.Id);
        result.Value.Select(e => e.Id).ShouldContain(entity5.Id);
    }

    protected virtual StubDbContext GetContext(string connectionString = null, bool forceNew = false)
    {
        return null;
    }

    protected IGenericRepository<PersonStub> CreateRepository(StubDbContext context)
    {
        //return new GenericRepositoryLoggingBehavior<PersonStub>(
        //    XunitLoggerFactory.Create(this.output),
        //    //new GenericRepositoryIncludeDecorator<PersonStub>(e => e.Locations, // not needed for OwnedEntities
        //    new EntityFrameworkGenericRepository<PersonStub>(r => r.DbContext(context)));

        return new EntityFrameworkGenericRepository<PersonStub>(r => r.DbContext(context));
    }

    protected async Task<PersonStub> InsertEntityAsync(int age = 24)
    {
        var entity = new PersonStub(this.faker.Person.FirstName, this.faker.Person.LastName, this.faker.Person.Email, age, Status.Active);
        entity.AddLocation(LocationStub.Create(this.faker.Company.CompanyName(),
            this.faker.Address.StreetAddress(),
            this.faker.Address.BuildingNumber(),
            this.faker.Address.ZipCode(),
            this.faker.Address.City(),
            this.faker.Address.Country()));
        var sut = this.CreateRepository(this.GetContext());

        return await sut.InsertAsync(entity);
    }
}