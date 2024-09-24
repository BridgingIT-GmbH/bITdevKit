// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Infrastructure;

using Core.Infrastructure;
using DevKit.Infrastructure.EntityFramework.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class BillRepositoryTests
{
    private readonly TestEnvironmentFixture fixture;
    private readonly CoreDbContext context;

    public BillRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
    {
        this.fixture = fixture.WithOutput(output);
        this.context = this.fixture.CreateSqlServerDbContext();
    }

    [Fact]
    public async Task ContextRawTest()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Bill.Create(HostId.Create(), DinnerId.Create(), GuestId.Create(), Price.Create(23.99m, "EUR"));

        // Act
        this.context.Bills.Add(entity);
        await this.context.SaveChangesAsync();

        // Assert
        entity.Id.Value.ShouldNotBe(Guid.Empty);
        this.context.Bills.Count().ShouldBeGreaterThanOrEqualTo(1);
        var existingEntity = this.context.Bills.Find(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldNotBe(Guid.Empty);
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
        var existingEntity2 = this.context.Bills.FirstOrDefault(e => e.Id == entity.Id);
        existingEntity2.ShouldNotBeNull();
        existingEntity2.Id.Value.ShouldNotBe(Guid.Empty);
        existingEntity2.Id.Value.ShouldBe(entity.Id.Value);
    }

    [Fact]
    public async Task InsertTest()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        this.context.Bills.Count().ShouldBeGreaterThanOrEqualTo(1);
        //using var context = this.fixture.CreateSqlServerDbContext();
        var sut = new EntityFrameworkGenericRepository<Bill>(r => r.DbContext(this.context));

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        this.context.Bills.AsNoTracking().Count().ShouldBeGreaterThanOrEqualTo(1);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.Value.ShouldBe(entity.Id.Value);
    }

    private async Task<Bill> InsertEntityAsync()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Bill.Create(HostId.Create(), DinnerId.Create(), GuestId.Create(), Price.Create(23.99m, "EUR"));
        entity.Id = null;
        //using var context = this.fixture.CreateSqlServerDbContext();
        var sut = new EntityFrameworkGenericRepository<Bill>(r => r.DbContext(this.context));

        return await sut.InsertAsync(entity);
    }

    private async Task<Bill> InsertEntityAsync(CoreDbContext context)
    {
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Bill.Create(HostId.Create(), DinnerId.Create(), GuestId.Create(), Price.Create(23.99m, "EUR"));
        entity.Id = null;
        var sut = new EntityFrameworkGenericRepository<Bill>(r => r.DbContext(context));

        return await sut.InsertAsync(entity);
    }
}