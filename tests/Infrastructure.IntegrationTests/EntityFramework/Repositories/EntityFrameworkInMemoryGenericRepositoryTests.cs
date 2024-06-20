// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkInMemoryGenericRepositoryTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : EntityFrameworkGenericRepositoryTestsBase
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [Fact]
    public override async Task DeleteAsync_ByEntity_EntityDeleted()
    {
        await base.DeleteAsync_ByEntity_EntityDeleted();
    }

    [Fact]
    public override async Task DeleteAsync_ByIdAsString_EntityDeleted()
    {
        await base.DeleteAsync_ByIdAsString_EntityDeleted();
    }

    [Fact]
    public override async Task DeleteAsync_ById_EntityDeleted()
    {
        await base.DeleteAsync_ById_EntityDeleted();
    }

    [Fact]
    public override async Task ExistsAsync_ExistingEntityId_EntityFound()
    {
        await base.ExistsAsync_ExistingEntityId_EntityFound();
    }

    [Fact]
    public override async Task ExistsAsync_NotExistingEntityId_EntityNotFound()
    {
        await base.ExistsAsync_NotExistingEntityId_EntityNotFound();
    }

    [Fact]
    public override async Task FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound()
    {
        await base.FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound();
    }

    [Fact]
    public override async Task FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound()
    {
        await base.FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound();
    }

    [Fact]
    public override async Task FindAllAsync_AnyEntity_EntitiesFound()
    {
        await base.FindAllAsync_AnyEntity_EntitiesFound();
    }

    [Fact]
    public override async Task FindAllAsync_EntityInvalidSpecification_EntitiesNotFound()
    {
        await base.FindAllAsync_EntityInvalidSpecification_EntitiesNotFound();
    }

    [Fact]
    public override async Task FindAllAsync_EntitySpecifications_EntitiesFound()
    {
        await base.FindAllAsync_EntitySpecifications_EntitiesFound();
    }

    [Fact]
    public override async Task FindAllAsync_EntitySpecification_EntitiesFound()
    {
        await base.FindAllAsync_EntitySpecification_EntitiesFound();
    }

    [Fact]
    public override async Task FindAllPagedAsync_AnyEntity_EntitiesFound()
    {
        await base.FindAllPagedAsync_AnyEntity_EntitiesFound();
    }

    [Fact]
    public override async Task FindAllIdsAsync_AnyEntity_ManyFound()
    {
        await base.FindAllIdsAsync_AnyEntity_ManyFound();
    }

    [Fact]
    public override async Task FindOneAsync_ExistingEntityByIdSpecification_EntityFound()
    {
        await base.FindOneAsync_ExistingEntityByIdSpecification_EntityFound();
    }

    [Fact]
    public override async Task FindOneAsync_ExistingEntityBySpecification_EntityFound()
    {
        await base.FindOneAsync_ExistingEntityBySpecification_EntityFound();
    }

    [Fact]
    public override async Task FindOneAsync_ExistingEntityIdAsString_EntityFound()
    {
        await base.FindOneAsync_ExistingEntityIdAsString_EntityFound();
    }

    [Fact]
    public override async Task FindOneAsync_ExistingEntityId_EntityFound()
    {
        await base.FindOneAsync_ExistingEntityId_EntityFound();
    }

    [Fact]
    public override async Task FindOneAsync_NotExistingEntityId_EntityNotFound()
    {
        await base.FindOneAsync_NotExistingEntityId_EntityNotFound();
    }

    [Fact]
    public override async Task InsertAsync_NewEntity_EntityInserted()
    {
        await base.InsertAsync_NewEntity_EntityInserted();
    }

    [Fact]
    public override async Task UpsertAsync_ExistingEntityChildRemoval_EntityUpdated()
    {
        await base.UpsertAsync_ExistingEntityChildRemoval_EntityUpdated();
    }

    [Fact]
    public override async Task UpsertAsync_ExistingEntityDisconnected_EntityUpdated()
    {
        await base.UpsertAsync_ExistingEntityDisconnected_EntityUpdated();
    }

    [Fact]
    public override async Task UpsertAsync_ExistingEntityNoTracking_EntityUpdated()
    {
        await base.UpsertAsync_ExistingEntityNoTracking_EntityUpdated();
    }

    [Fact]
    public override async Task UpsertAsync_ExistingEntity_EntityUpdated()
    {
        await base.UpsertAsync_ExistingEntity_EntityUpdated();
    }

    protected override StubDbContext GetContext(string connectionString = null, bool forceNew = false)
    {
        return this.fixture.EnsureInMemoryDbContext(this.output, forceNew);
    }
}