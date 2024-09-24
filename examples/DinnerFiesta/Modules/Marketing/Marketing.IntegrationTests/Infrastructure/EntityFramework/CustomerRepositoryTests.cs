// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.IntegrationTests.Infrastructure;

using DevKit.Domain.Repositories;
using DevKit.Infrastructure.EntityFramework.Repositories;
using Domain;
using Marketing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using UnitTests;

[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("Infrastructure")]
public class CustomerRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly MarketingDbContext context;

    public CustomerRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.context = this.fixture.CreateSqlServerDbContext();
    }

    [Fact]
    public async Task FindOneTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Customers.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldNotBeNull();
        result.Id.Value.ShouldBe(entity.Id.Value);
        result.FirstName.ShouldBe(entity.FirstName);
        result.LastName.ShouldBe(entity.LastName);
    }

    [Fact]
    public async Task FindAll_ByCustomerForEmailSpecificationTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));
        var specification = new CustomerForEmailSpecification(entity.Email);

        // Act
        var results = await sut.FindAllAsync(specification);

        // Assert
        this.context.Customers.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
        results.First().ShouldNotBeNull();
        results.First().Id.Value.ShouldBe(entity.Id.Value);
        results.First().FirstName.ShouldBe(entity.FirstName);
        results.First().LastName.ShouldBe(entity.LastName);
        results.First().Email.ShouldBe(entity.Email);
    }

    [Fact]
    public async Task ExistsTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));

        // Act
        var result = await sut.ExistsAsync(entity.Id);

        // Assert
        this.context.Customers.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldBe(true);
    }

    [Fact]
    public async Task InsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Customers.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        existingEntity.FirstName.ShouldBe(entity.FirstName);
        existingEntity.LastName.ShouldBe(entity.LastName);
    }

    [Fact]
    public async Task UpsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        entity.ChangeName($"John {ticks}", $"Doe {ticks}");
        var result = await sut.UpsertAsync(entity);

        // Assert
        this.context.Customers.AsNoTracking().ToList().Count.ShouldBeGreaterThanOrEqualTo(1);
        result.action.ShouldBe(RepositoryActionResult.Updated);

        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        existingEntity.FirstName.ShouldBe(entity.FirstName);
        existingEntity.LastName.ShouldBe(entity.LastName);
    }

    [Fact]
    public async Task DeleteTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));

        // Act
        var result = await sut.DeleteAsync(entity.Id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    private async Task<Customer> InsertEntityAsync()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Customers(ticks).First();
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(this.context));

        return await sut.InsertAsync(entity);
    }

    private async Task<Customer> InsertEntityAsync(MarketingDbContext context)
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Customers(ticks).First();
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Customer>(r => r.DbContext(context));

        return await sut.InsertAsync(entity);
    }
}