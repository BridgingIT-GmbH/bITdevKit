// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Infrastructure;

using System;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class DinnerRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly CoreDbContext context;

    public DinnerRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.context = this.fixture.CreateSqlServerDbContext();
    }

    [Fact]
    public async Task ContextRawTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Dinners(ticks).First();
        entity.Id = null;

        // Act
        this.context.Dinners.Add(entity);
        await this.context.SaveChangesAsync();

        // Assert
        entity.Id.Value.ShouldNotBe(Guid.Empty);
        this.context.Dinners.Count().ShouldBeGreaterThanOrEqualTo(1);
        var existingEntity = this.context.Dinners.Find(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldNotBe(Guid.Empty);
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        var existingEntity2 = this.context.Dinners.FirstOrDefault(e => e.Id == entity.Id);
        existingEntity2.ShouldNotBeNull();
        existingEntity2.Id.Value.ShouldNotBe(Guid.Empty);
        existingEntity2.Id.Value.ShouldBe(entity.Id.Value);
    }

    [Fact]
    public async Task FindOneTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Dinners.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldNotBeNull();
        result.Id.Value.ShouldBe(entity.Id.Value);
    }

    [Fact]
    public async Task FindAllWithSpecificationTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindAllAsync(
            new DinnerForHostSpecification(entity.HostId));

        // Assert
        this.context.Dinners.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
        result.All(e => e.HostId == entity.HostId).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));

        // Act
        var result = await sut.ExistsAsync(entity.Id);

        // Assert
        this.context.Dinners.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldBe(true);
    }

    [Fact]
    public async Task InsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Dinners.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
    }

    [Fact]
    public async Task UpsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        entity.ChangeName($"{entity.Name} Changed");
        var result = await sut.UpsertAsync(entity);

        // Assert
        this.context.Dinners.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.action.ShouldBe(RepositoryActionResult.Updated);

        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        existingEntity.Name.ShouldBe(entity.Name);
    }

    [Fact]
    public async Task DeleteTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));

        // Act
        var result = await sut.DeleteAsync(entity.Id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    private async Task<Dinner> InsertEntityAsync()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Dinners(ticks).First();
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(this.context));

        return await sut.InsertAsync(entity);
    }

    private async Task<Dinner> InsertEntityAsync(CoreDbContext context)
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Dinners(ticks).First();
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Dinner>(r => r.DbContext(context));

        return await sut.InsertAsync(entity);
    }
}