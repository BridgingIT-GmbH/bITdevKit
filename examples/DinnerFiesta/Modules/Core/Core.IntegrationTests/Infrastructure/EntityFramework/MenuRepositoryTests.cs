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
public class MenuRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly CoreDbContext context;

    public MenuRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.context = this.fixture.CreateSqlServerDbContext();
    }

    [Fact]
    public async Task FindOneTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Menus.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldNotBeNull();
        result.Id.Value.ShouldBe(entity.Id.Value);
        result.Sections.ShouldNotBeNull();
        result.Sections.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FindAllWithSpecificationTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindAllAsync(
            new MenuForHostSpecification(entity.HostId));

        // Assert
        this.context.Menus.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldNotBeNull();
        result.Count().ShouldBeGreaterThan(0);
        result.All(e => e.HostId == entity.HostId).ShouldBeTrue();
        result.All(e => e.Sections is not null).ShouldBeTrue();
        result.All(e => e.Sections.Count > 0).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));

        // Act
        var result = await sut.ExistsAsync(entity.Id);

        // Assert
        this.context.Menus.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldBe(true);
    }

    [Fact]
    public async Task InsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Menus.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        result.Sections.ShouldNotBeNull();
        existingEntity.Sections.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        entity.ChangeName($"{entity.Name} Changed");
        var result = await sut.UpsertAsync(entity);

        // Assert
        this.context.Menus.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        result.action.ShouldBe(RepositoryActionResult.Updated);

        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        existingEntity.Name.ShouldBe(entity.Name);
        existingEntity.Sections.ShouldNotBeNull();
        existingEntity.Sections.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));

        // Act
        var result = await sut.DeleteAsync(entity.Id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    private async Task<Menu> InsertEntityAsync()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Menus(ticks).First();
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(this.context));

        return await sut.InsertAsync(entity);
    }

    private async Task<Menu> InsertEntityAsync(CoreDbContext context)
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Menus(ticks).First();
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Menu>(r => r.DbContext(context));

        return await sut.InsertAsync(entity);
    }
}