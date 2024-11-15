// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

public abstract class EntityFrameworkGenericRepositoryTypedIdTestsBase
{
    public virtual async Task DeleteAsync_ByEntity_EntityDeleted()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.DeleteAsync(entity);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    //public virtual async Task DeleteAsync_ByIdAsString_EntityDeleted()
    //{
    //    // Arrange
    //    var entity = await this.InsertEntityAsync();
    //    var sut = this.CreateRepository(this.GetContext());

    //    // Act
    //    var result = await sut.DeleteAsync(entity.Id.ToString());

    //    // Assert
    //    result.ShouldBe(RepositoryActionResult.Deleted);
    //    (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    //}

    public virtual async Task DeleteAsync_ById_EntityDeleted()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.DeleteAsync(entity.Id);

        // Assert
        result.ShouldBe(RepositoryActionResult.Deleted);
        (await sut.ExistsAsync(entity.Id)).ShouldBe(false);
    }

    public virtual async Task ExistsAsync_ExistingEntityId_EntityFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.ExistsAsync(entity.Id);

        // Assert
        this.GetContext()
            .Blogs.AsNoTracking()
            .ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(1);
        result.ShouldBeTrue();
    }

    public virtual async Task ExistsAsync_NotExistingEntityId_EntityNotFound()
    {
        // Arrange
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.ExistsAsync(BlogId.Create());

        // Assert
        result.ShouldBeFalse();
    }

    public virtual async Task FindAllAsync_AnyEntityPagedAndOrdered_EntitiesNotFound()
    {
        // Arrange
        var entity1 = await this.InsertEntityAsync("C");
        var entity2 = await this.InsertEntityAsync("D");
        var entity3 = await this.InsertEntityAsync("B");
        var entity4 = await this.InsertEntityAsync("A");
        var entity5 = await this.InsertEntityAsync("Z");

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllAsync(new Specification<Blog>(_ => false),
            new FindOptions<Blog>(10, 2, new OrderOption<Blog>(e => e.Name)));

        // Assert
        this.GetContext()
            .Blogs.AsNoTracking()
            .ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(5);
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
    }

    public virtual async Task FindAllAsync_AnyEntitySkipTakeAndOrdered_EntitiesFound()
    {
        // Arrange
        var entity1 = await this.InsertEntityAsync("A");
        var entity2 = await this.InsertEntityAsync("B");
        var entity3 = await this.InsertEntityAsync("D");
        var entity4 = await this.InsertEntityAsync("C");
        var entity5 = await this.InsertEntityAsync("E");

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllAsync(
            new Specification<Blog>(e => e.Name == entity1.Name || e.Name == entity2.Name || e.Name == entity3.Name || e.Name == entity4.Name || e.Name == entity5.Name),
            new FindOptions<Blog>(2, 2, new OrderOption<Blog>(e => e.Name)));

        // Assert
        this.GetContext()
            .Blogs.AsNoTracking()
            .ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(5);
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
            .ShouldBe(entity4); // C
        results.Last()
            .ShouldBe(entity3); // D
    }

    public virtual async Task FindAllAsync_AnyEntity_EntitiesFound()
    {
        // Arrange
        var entity1 = await this.InsertEntityAsync();
        var entity2 = await this.InsertEntityAsync();
        var entity3 = await this.InsertEntityAsync();
        var entity4 = await this.InsertEntityAsync();
        var entity5 = await this.InsertEntityAsync();

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllAsync();

        // Assert
        this.GetContext()
            .Blogs.AsNoTracking()
            .ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(5);
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.ShouldContain(entity1);
        results.ShouldContain(entity2);
        results.ShouldContain(entity3);
        results.ShouldContain(entity4);
        results.ShouldContain(entity5);
    }

    public virtual async Task FindAllAsync_EntityInvalidSpecification_EntitiesNotFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllAsync(new BlogEmailSpecification("UNKNOWN"));

        // Assert
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
        results.ShouldNotContain(entity);
    }

    public virtual async Task FindAllAsync_EntitySpecification_EntitiesFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllAsync(new BlogEmailSpecification(entity.Email));

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
    }

    public virtual async Task FindAllAsync_ChildEntitySpecification_EntitiesFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllAsync(new Specification<Blog>(e => e.Posts.Any(p => p.Status == PostStatus.Published)));

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.ShouldContain(entity);
    }

    public virtual async Task FindAllPagedAsync_AnyEntity_EntitiesFound()
    {
        // Arrange
        var entity1 = await this.InsertEntityAsync("C");
        var entity2 = await this.InsertEntityAsync("D");
        var entity3 = await this.InsertEntityAsync("B");
        var entity4 = await this.InsertEntityAsync("A");
        var entity5 = await this.InsertEntityAsync("Z");
        var entity6 = await this.InsertEntityAsync("E");

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllPagedResultAsync(new Specification<Blog>(e =>
                e.Name == entity1.Name || e.Name == entity2.Name || e.Name == entity3.Name || e.Name == entity4.Name || e.Name == entity5.Name || e.Name == entity6.Name),
            nameof(Blog.Name),
            1,
            2);

        // Assert
        this.GetContext()
            .Blogs.AsNoTracking()
            .ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(6);
        results.ShouldBeSuccess();
        results.Value.ShouldNotBeNull();
        results.Value.ShouldNotBeEmpty();
        results.TotalCount.ShouldBe(6);
        results.TotalPages.ShouldBe(3);
        results.CurrentPage.ShouldBe(1);
        results.HasNextPage.ShouldBeTrue();
        results.HasPreviousPage.ShouldBeFalse();
        results.Value.Count()
            .ShouldBe(2);
        results.Value.ShouldNotContain(entity1);
        results.Value.ShouldNotContain(entity2);
        results.Value.ShouldContain(entity3);
        results.Value.ShouldContain(entity4);
        results.Value.ShouldNotContain(entity5);
        results.Value.ShouldNotContain(entity6);
        results.Value.First()
            .ShouldBe(entity4); // A
        results.Value.Last()
            .ShouldBe(entity3); // B
    }

    public virtual async Task FindAllIdsAsync_AnyEntity_ManyFound()
    {
        // Arrange
        var prefix = DateTime.UtcNow.Ticks.ToString();
        var entity1 = await this.InsertEntityAsync(prefix);
        var entity2 = await this.InsertEntityAsync(prefix);
        var entity3 = await this.InsertEntityAsync(prefix);
        var entity4 = await this.InsertEntityAsync(prefix);
        var entity5 = await this.InsertEntityAsync(prefix);
        var entity6 = await this.InsertEntityAsync(prefix);

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var results = await sut.FindAllIdsResultAsync<Blog, BlogId>(new Specification<Blog>(e =>
            e.Name == entity1.Name || e.Name == entity2.Name || e.Name == entity3.Name || e.Name == entity4.Name || e.Name == entity5.Name || e.Name == entity6.Name));

        // Assert
        this.GetContext()
            .Blogs.AsNoTracking()
            .ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(6);
        results.ShouldBeSuccess();
        results.Value.ShouldNotBeNull();
        results.Value.ShouldNotBeEmpty();
        results.Value.Count()
            .ShouldBe(6);
    }

    public virtual async Task FindOneAsync_ExistingEntityByIdSpecification_EntityFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.FindOneAsync(new Specification<Blog>(e => e.Id == entity.Id));

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
    }

    public virtual async Task FindOneAsync_ExistingEntityBySpecification_EntityFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());
        var sut2 = this.CreatePostRepository(this.GetContext());

        // Act
        var result1 = await sut.FindOneAsync(new Specification<Blog>(e => e.Name == entity.Name));
        var result2 = await sut.FindOneAsync(new Specification<Blog>(e => e.Id ==
            entity.Posts.First()
                .BlogId));
        //var result3 = await sut2.FindAllAsync(
        //    new Specification<Post>(e => e.BlogId == entity.Id));

        // Assert
        result1.ShouldNotBeNull();
        result1.Id.ShouldBe(entity.Id);
        result2.ShouldNotBeNull();
        result2.Id.ShouldBe(entity.Id);
        //result3.ShouldNotBeNull();
        //result3.ShouldNotBeEmpty();
    }

    //public virtual async Task FindOneAsync_ExistingEntityIdAsString_EntityFound()
    //{
    //    // Arrange
    //    var entity = await this.InsertEntityAsync();
    //    var sut = this.CreateRepository(this.GetContext());

    //    // Act
    //    var result = await sut.FindOneAsync(entity.Id.ToString());

    //    // Assert
    //    result.ShouldNotBeNull();
    //    result.Id.ShouldBe(entity.Id);
    //}

    public virtual async Task FindOneAsync_ExistingEntityId_EntityFound()
    {
        // Arrange
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.FindOneAsync(entity.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entity.Id);
        result.Name.ShouldBe(entity.Name);
    }

    public virtual async Task FindOneAsync_NotExistingEntityId_EntityNotFound()
    {
        // Arrange
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.FindOneAsync(BlogId.Create());

        // Assert
        result.ShouldBeNull();
    }

    public virtual async Task InsertAsync_NewEntity_EntityInserted()
    {
        // Arrange
        var faker = new Faker();
        var entity = Blog.Create(faker.Company.CompanyName(), faker.Internet.Url(), EmailAddressStub.Create(faker.Person.Email))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())
                .Publish(faker.Date.PastDateOnly()))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())
                .Publish(faker.Date.PastDateOnly()))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text()));
        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        var result = await sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Inserted);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Id.IsEmpty.ShouldBeFalse();
        existingEntity.Posts.ShouldNotBeNull();
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[0]);
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[1]);
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[2]);
        existingEntity.Posts.ToArray()[0]
            .Id.IsEmpty.ShouldBeFalse();
        existingEntity.Posts.ToArray()[1]
            .Id.IsEmpty.ShouldBeFalse();
        existingEntity.Posts.ToArray()[2]
            .Id.IsEmpty.ShouldBeFalse();
    }

    public virtual async Task UpsertAsync_ExistingEntityChildRemoval_EntityUpdated()
    {
        // Arrange
        var faker = new Faker();
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Blog.Create(faker.Company.CompanyName(), faker.Internet.Url(), EmailAddressStub.Create(faker.Person.Email))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())
                .Publish(faker.Date.PastDateOnly()))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())
                .Publish(faker.Date.PastDateOnly()))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text()));
        var deletedPost1 = entity.Posts.First();
        var deletedPost2 = entity.Posts.Last();

        var sut = this.CreateBlogRepository(this.GetContext());

        // Act
        await sut.UpsertAsync(entity);
        entity.RemovePost(deletedPost1.Id); // remove 2
        entity.RemovePost(deletedPost2.Id);
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 1
        var addedPost = entity.Posts.Last();
        var result = await sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Id.IsEmpty.ShouldBeFalse();
        existingEntity.Name.ShouldBe(entity.Name);
        existingEntity.Posts.ShouldNotBeNull();
        existingEntity.Posts.ShouldNotBeEmpty();
        existingEntity.Posts.Count()
            .ShouldBe(2);
        existingEntity.Posts.ToArray()[0]
            .Id.IsEmpty.ShouldBeFalse();
        existingEntity.Posts.ToArray()[1]
            .Id.IsEmpty.ShouldBeFalse();

        existingEntity.Posts.ShouldNotContain(deletedPost1);
        existingEntity.Posts.ShouldNotContain(deletedPost2);
        existingEntity.Posts.ShouldContain(addedPost);
    }

    public virtual async Task UpsertAsync_ExistingEntityDisconnected_EntityUpdated()
    {
        // Arrange
        var faker = new Faker();
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
        var existingEntity = await sut.FindOneAsync(entity.Id, new FindOptions<Blog> { NoTracking = true });
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(disconnectedEntity.Id);
        existingEntity.Name.ShouldBe(disconnectedEntity.Name);
        existingEntity.Email.ShouldBe(disconnectedEntity.Email);
        existingEntity.Posts.ShouldNotBeNull();
        existingEntity.Posts.ShouldNotBeEmpty();
        existingEntity.Posts.Count()
            .ShouldBe(6); // 3 + 3
        existingEntity.Posts.ShouldContain(disconnectedEntity.Posts.ToArray()[0]);
        existingEntity.Posts.ShouldContain(disconnectedEntity.Posts.ToArray()[1]);
        existingEntity.Posts.ShouldContain(disconnectedEntity.Posts.ToArray()[2]);
        existingEntity.Posts.ToArray()[3]
            .Id.IsEmpty.ShouldBeFalse(); // added
        existingEntity.Posts.ToArray()[4]
            .Id.IsEmpty.ShouldBeFalse(); // added
        existingEntity.Posts.ToArray()[5]
            .Id.IsEmpty.ShouldBeFalse(); // added
    }

    public virtual async Task UpsertAsync_ExistingEntityNoTracking_EntityUpdated()
    {
        // Arrange
        var faker = new Faker();
        var entity = await this.InsertEntityAsync();
        using var context = this.GetContext(null, true);
        var sut = this.CreateBlogRepository(context);
        var ticks = DateTime.UtcNow.Ticks;
        entity = await sut.FindOneAsync(entity.Id, new FindOptions<Blog> { NoTracking = true });

        // Act
        entity.Name = $"{entity.Name} {ticks}";
        entity.Email = EmailAddressStub.Create(faker.Person.Email);
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 1 - idx 3
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 2 - idx 4
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 3 - idx 5
        var result = await sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await sut.FindOneAsync(entity.Id, new FindOptions<Blog> { NoTracking = true });
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Name.ShouldBe(entity.Name);
        existingEntity.Email.ShouldBe(entity.Email);
        existingEntity.Posts.ShouldNotBeNull();
        existingEntity.Posts.ShouldNotBeEmpty();
        existingEntity.Posts.Count()
            .ShouldBe(6); // 3 + 3
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[3]);
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[4]);
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[5]);
        existingEntity.Posts.ToArray()[3]
            .Id.IsEmpty.ShouldBeFalse(); // added
        existingEntity.Posts.ToArray()[4]
            .Id.IsEmpty.ShouldBeFalse(); // added
        existingEntity.Posts.ToArray()[5]
            .Id.IsEmpty.ShouldBeFalse(); // added
    }

    public virtual async Task UpsertAsync_ExistingEntity_EntityUpdated()
    {
        // Arrange
        var faker = new Faker();
        var entity = await this.InsertEntityAsync();
        var sut = this.CreateBlogRepository(this.GetContext());
        var ticks = DateTime.UtcNow.Ticks;

        // Act
        entity.Name = $"{entity.Name} {ticks}";
        entity.Email = EmailAddressStub.Create(faker.Person.Email);
        var location1 = LocationStub.Create(faker.Company.CompanyName(),
            faker.Address.StreetAddress(),
            faker.Address.BuildingNumber(),
            faker.Address.ZipCode(),
            faker.Address.City(),
            faker.Address.Country());
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 1 - idx 3
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 2 - idx 4
        entity.AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())); // add 3 - idx 5
        var result = await sut.UpsertAsync(entity);

        // Assert
        result.action.ShouldBe(RepositoryActionResult.Updated);
        var existingEntity = await sut.FindOneAsync(entity.Id);
        existingEntity.ShouldNotBeNull();
        existingEntity.Id.ShouldBe(entity.Id);
        existingEntity.Id.IsEmpty.ShouldBeFalse();
        existingEntity.Name.ShouldBe(entity.Name);
        existingEntity.Email.ShouldBe(entity.Email);
        existingEntity.Posts.ShouldNotBeNull();
        existingEntity.Posts.ShouldNotBeEmpty();
        existingEntity.Posts.Count()
            .ShouldBe(6); // 3 + 3
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[3]);
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[4]);
        existingEntity.Posts.ShouldContain(entity.Posts.ToArray()[5]);
        existingEntity.Posts.ToArray()[3]
            .Id.IsEmpty.ShouldBeFalse(); // added
        existingEntity.Posts.ToArray()[4]
            .Id.IsEmpty.ShouldBeFalse(); // added
        existingEntity.Posts.ToArray()[5]
            .Id.IsEmpty.ShouldBeFalse(); // added
    }

    protected virtual StubDbContext GetContext(string connectionString = null, bool forceNew = false)
    {
        return null;
    }

    protected IGenericRepository<Blog> CreateBlogRepository(StubDbContext context)
    {
        //return new GenericRepositoryLoggingBehavior<Blog>(
        //    XunitLoggerFactory.Create(this.output),
        //    //new GenericRepositoryIncludeDecorator<Blog>(e => e.Locations, // not needed for OwnedEntities
        //    new EntityFrameworkGenericRepository<Blog>(r => r.DbContext(context)));

        return new EntityFrameworkGenericRepository<Blog>(r => r.DbContext(context));
    }

    protected IGenericRepository<Post> CreatePostRepository(StubDbContext context)
    {
        //return new GenericRepositoryLoggingBehavior<Blog>(
        //    XunitLoggerFactory.Create(this.output),
        //    //new GenericRepositoryIncludeDecorator<Blog>(e => e.Locations, // not needed for OwnedEntities
        //    new EntityFrameworkGenericRepository<Blog>(r => r.DbContext(context)));

        return new EntityFrameworkGenericRepository<Post>(r => r.DbContext(context));
    }

    protected async Task<Blog> InsertEntityAsync(string namePrefix = null)
    {
        var faker = new Faker();
        var entity = Blog.Create(namePrefix + faker.Company.CompanyName(), faker.Internet.Url(), EmailAddressStub.Create(faker.Person.Email))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())
                .Publish(faker.Date.PastDateOnly()))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text())
                .Publish(faker.Date.PastDateOnly()))
            .AddPost(Post.Create(faker.Hacker.Phrase(), faker.Lorem.Text()));
        var sut = this.CreateBlogRepository(this.GetContext());

        return await sut.InsertAsync(entity);
    }
}