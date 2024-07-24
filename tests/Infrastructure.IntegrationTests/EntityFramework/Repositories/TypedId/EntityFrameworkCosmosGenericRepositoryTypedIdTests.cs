// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Domain.Repositories;
using DotNet.Testcontainers.Containers;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkCosmosGenericRepositoryTypedIdTests : EntityFrameworkGenericRepositoryTypedIdTestsBase
{
    private readonly TestEnvironmentFixture fixture;
    private readonly ITestOutputHelper output;

    public EntityFrameworkCosmosGenericRepositoryTypedIdTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
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

    //[SkippableFact]
    //public override async Task DeleteAsync_ByIdAsString_EntityDeleted()
    //{
    //    Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");
    //
    //    await base.DeleteAsync_ByIdAsString_EntityDeleted();
    //}

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

    //[SkippableFact]
    //public override async Task FindAllAsync_EntitySpecifications_EntitiesFound()
    //{
    //    Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");
    //
    //    await base.FindAllAsync_EntitySpecifications_EntitiesFound();
    //}

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

    //[SkippableFact]
    //public override async Task FindOneAsync_ExistingEntityIdAsString_EntityFound()
    //{
    //    Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");
    //
    //    await base.FindOneAsync_ExistingEntityIdAsString_EntityFound();
    //}

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

    [SkippableFact] // adjusted as the document is completely replaced and the posts count equals 3
    public override async Task UpsertAsync_ExistingEntityDisconnected_EntityUpdated()
    {
        Skip.IfNot(this.fixture.CosmosContainer.State == TestcontainersStates.Running, "container not running");

        // Arrange
        var faker = new Faker("en");
        var entity = await this.InsertEntityAsync();
        using var context = this.GetContext(null, true);
        var sut = this.CreateBlogRepository(context);
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        var disconnectedEntity = new Blog
        {
            Id = entity.Id, // has same id as entity > should update existing entity
            Name = $"{entity.Name} {ticks}",
            Email = EmailAddressStub.Create(faker.Person.Email)
        };
        disconnectedEntity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 1 - idx 3
        disconnectedEntity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 2 - idx 4
        disconnectedEntity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 3 - idx 5
        var result = await sut.UpsertAsync(disconnectedEntity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await sut.FindOneAsync(entity.Id, new FindOptions<Blog>() { NoTracking = true });
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(disconnectedEntity.Id);
        existingEntity.Name.ShouldBe(disconnectedEntity.Name);
        existingEntity.Email.ShouldBe(disconnectedEntity.Email);
        existingEntity.Posts.ShouldNotBeNull();
        existingEntity.Posts.ShouldNotBeEmpty();
        existingEntity.Posts.Count().ShouldBe(3); // 3
        existingEntity.Posts.ShouldContain(disconnectedEntity.Posts.ToArray()[0]);
        existingEntity.Posts.ShouldContain(disconnectedEntity.Posts.ToArray()[1]);
        existingEntity.Posts.ShouldContain(disconnectedEntity.Posts.ToArray()[2]);
        //existingEntity.Posts.ToArray()[3].Id.IsEmpty.ShouldBeFalse(); // added
        //existingEntity.Posts.ToArray()[4].Id.IsEmpty.ShouldBeFalse(); // added
        //existingEntity.Posts.ToArray()[5].Id.IsEmpty.ShouldBeFalse(); // added
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
        return this.fixture.EnsureCosmosDbContext(this.output);
    }
}