// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories;

using System.Linq.Expressions;
using Domain.Model;
using Domain.Repositories;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

[UnitTest("Infrastructure")]
public class IncludeIfTests
{
    [Fact]
    public void IncludeIf_WithNullOptions_ReturnsOriginalQuery()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IncludeIfDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid()
                .ToString())
            .Options;
        using var context = new IncludeIfDbContext(options);
        var query = context.Blogs.AsQueryable();

        // Act
        var result = query.IncludeIf<BlogEntity>(null);

        // Assert
        result.ShouldBeSameAs(query);
    }

    [Fact]
    public void IncludeIf_WithThenIncludes_LoadsNestedGraph()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<IncludeIfDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid()
                .ToString())
            .Options;
        SeedData(dbOptions);

        using var plainContext = new IncludeIfDbContext(dbOptions);
        var plainBlog = plainContext.Blogs.First();
        plainBlog.Editor.ShouldBeNull();

        var includeOption = new SingleIncludeOption<BlogEntity, ReviewerEntity>(b => b.Editor);
        var editorProfileInclude = ((IIncludableOption<BlogEntity, ReviewerEntity>)includeOption)
            .ThenInclude(e => e.Profile);
        var options = new FindOptions<BlogEntity>().AddInclude(editorProfileInclude);

        using var context = new IncludeIfDbContext(dbOptions);

        // Act
        var blog = context.Blogs
            .IncludeIf(options)
            .ShouldHaveSingleItem();

        // Assert
        blog.Editor.ShouldNotBeNull();
        blog.Editor.DisplayName.ShouldBe("Blog Editor");
        blog.Editor.Profile.ShouldNotBeNull();
        blog.Editor.Profile.Bio.ShouldBe("Editor profile");
    }

    private static void SeedData(DbContextOptions<IncludeIfDbContext> options)
    {
        using var context = new IncludeIfDbContext(options);

        var commentAuthor = new ReviewerEntity { Id = Guid.NewGuid(), DisplayName = "Alice Reviewer" };
        var editor = new ReviewerEntity
        {
            Id = Guid.NewGuid(),
            DisplayName = "Blog Editor",
            Profile = new ReviewerProfileEntity
            {
                Id = Guid.NewGuid(),
                Bio = "Editor profile"
            }
        };
        var post = new PostEntity
        {
            Id = Guid.NewGuid(),
            Comments =
            [
                new CommentEntity
                {
                    Id = Guid.NewGuid(),
                    Author = commentAuthor,
                    Text = "First!"
                }
            ]
        };

        var blog = new BlogEntity
        {
            Id = Guid.NewGuid(),
            Title = "Test Blog",
            Posts = [post],
            Editor = editor
        };

        post.Blog = blog;
        post.Comments.ForEach(c => c.Post = post);

        context.Blogs.Add(blog);
        context.SaveChanges();
        context.ChangeTracker.Clear();
    }

    private class CollectionIncludeOption<TEntity, TElement> : IncludeOptionBase<TEntity>, IIncludableOption<TEntity, IEnumerable<TElement>>
        where TEntity : class, IEntity
    {
        public CollectionIncludeOption(Expression<Func<TEntity, IEnumerable<TElement>>> expression)
        {
            this.TypedExpression = expression;
            this.Expression = System.Linq.Expressions.Expression.Lambda<Func<TEntity, object>>(
                System.Linq.Expressions.Expression.Convert(expression.Body, typeof(object)),
                expression.Parameters);
        }

        public Expression<Func<TEntity, IEnumerable<TElement>>> TypedExpression { get; }
    }

    private class SingleIncludeOption<TEntity, TProperty> : IncludeOptionBase<TEntity>, IIncludableOption<TEntity, TProperty>
        where TEntity : class, IEntity
    {
        public SingleIncludeOption(Expression<Func<TEntity, TProperty>> expression)
        {
            this.TypedExpression = expression;
            this.Expression = System.Linq.Expressions.Expression.Lambda<Func<TEntity, object>>(
                System.Linq.Expressions.Expression.Convert(expression.Body, typeof(object)),
                expression.Parameters);
        }

        public Expression<Func<TEntity, TProperty>> TypedExpression { get; }
    }

    private class IncludeIfDbContext(DbContextOptions<IncludeIfDbContext> options) : DbContext(options)
    {
        public DbSet<BlogEntity> Blogs { get; set; }

        public DbSet<PostEntity> Posts { get; set; }

        public DbSet<CommentEntity> Comments { get; set; }

        public DbSet<ReviewerEntity> Reviewers { get; set; }
    }

    private class BlogEntity : AggregateRoot<Guid>
    {
        public string Title { get; set; }

        public List<PostEntity> Posts { get; set; } = [];

        public Guid? EditorId { get; set; }

        public ReviewerEntity Editor { get; set; }
    }

    private class PostEntity : Entity<Guid>
    {
        public Guid BlogEntityId { get; set; }

        public BlogEntity Blog { get; set; }

        public List<CommentEntity> Comments { get; set; } = [];
    }

    private class CommentEntity : Entity<Guid>
    {
        public Guid PostEntityId { get; set; }

        public PostEntity Post { get; set; }

        public ReviewerEntity Author { get; set; }

        public string Text { get; set; }
    }

    private class ReviewerEntity : Entity<Guid>
    {
        public string DisplayName { get; set; }

        public ReviewerProfileEntity Profile { get; set; }
    }

    private class ReviewerProfileEntity : Entity<Guid>
    {
        public string Bio { get; set; }
    }
}
