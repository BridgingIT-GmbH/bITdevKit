namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

[TypedEntityId<Guid>]
public class Blog : AggregateRoot<BlogId>
{
    private readonly List<Post> posts = [];

    //private Blog() { } // Private constructor required by EF Core

    public string Name { get; set; }

    public string Url { get; set; }

    public EmailAddressStub Email { get; set; }

    public IEnumerable<Post> Posts => this.posts.OrderBy(e => e.PublishedDate);

    public static Blog Create(string name, string url, EmailAddressStub email)
    {
        return new Blog()
        {
            Name = name,
            Url = url,
            Email = email,
        };
    }

    public Blog AddPost(Post post)
    {
        if (!this.posts.Contains(post))
        {
            this.posts.Add(post);
        }

        return this;
    }

    public Blog RemovePost(PostId postId)
    {
        var post = this.posts.Find(e => e.Id == postId) ?? throw new InvalidOperationException("Post not found");
        this.posts.Remove(post);

        return this;
    }

    public Blog RemovePost(Post post)
    {
        this.posts.Remove(post);

        return this;
    }

    public Blog PublishPost(PostId postId)
    {
        var post = this.posts.Find(e => e.Id == postId) ?? throw new InvalidOperationException("Post not found");
        post.Publish();

        return this;
    }
}

[TypedEntityId<Guid>]
public class Post : Entity<PostId>
{
    private Post() { } // Private constructor required by EF Core

    public BlogId BlogId { get; set; }

    public string Title { get; set; }

    public string Content { get; set; }

    public PostStatus Status { get; set; } = PostStatus.Draft;

    public DateOnly? PublishedDate { get; set; }

    public static Post Create(string title, string content) =>
        new Post
        {
            Title = title,
            Content = content,
        };

    public Post Publish(DateOnly? date = null)
    {
        this.Status = PostStatus.Published;
        this.PublishedDate = date ?? DateOnly.FromDateTime(DateTime.Now);

        return this;
    }
}

public class PostStatus(int id, string value, string code, string description) : Enumeration(id, value)
{
    public static PostStatus Draft = new(1, "Draft", "D", "Lorem Ipsum");
    public static PostStatus Review = new(2, "Review", "R", "Lorem Ipsum");
    public static PostStatus Published = new(2, "Published", "P", "Lorem Ipsum");

    public string Code { get; } = code;

    public string Description { get; } = description;

    public static IEnumerable<Status> GetAll() =>
        GetAll<Status>();

    public static Status GetByCode(string code) =>
        GetAll<Status>().FirstOrDefault(e => e.Code == code);
}

public class BlogEmailSpecification(string email) : Specification<Blog>
{
    private readonly string email = email;

    public override Expression<Func<Blog, bool>> ToExpression()
    {
        return t => t.Email.Value == this.email;
    }
}

public class BlogEntityTypeConfiguration : IEntityTypeConfiguration<Blog>
{
    public void Configure(EntityTypeBuilder<Blog> builder)
    {
        builder.ToTable("Blogs")
            .HasKey(d => d.Id)
            .IsClustered(false);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(
                id => id.Value,
                value => BlogId.Create(value));

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(e => e.Url)
            .IsRequired(false)
            .HasMaxLength(512);

        builder.OwnsOne(e => e.Email, b =>
        {
            b.Property(e => e.Value)
                .HasColumnName("Email")
                .IsRequired(false)
                .HasMaxLength(256);

            b.HasIndex(nameof(Blog.Email.Value))
                .IsUnique();
        });
        builder.Navigation(e => e.Email).IsRequired();

        builder.OwnsMany(e => e.Posts, b =>
        {
            b.ToTable("Posts");
            b.WithOwner().HasForeignKey("BlogId");
            b.HasKey("Id", "BlogId");

            b.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(
                    id => id.Value,
                    value => PostId.Create(value));

            b.Property("Title")
                .IsRequired().HasMaxLength(256);

            b.Property("Content")
                .IsRequired(false);

            b.Property(e => e.PublishedDate)
                .IsRequired(false);

            b.Property(e => e.Status)
                .HasConversion(
                    status => status.Id,
                    id => Enumeration.FromId<PostStatus>(id));
            //.HasConversion(
            //    new EnumerationConverter<int, string, PostStatus>());
        });
    }
}