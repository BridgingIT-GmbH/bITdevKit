// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;

// model inspiration: https://guides.rubyonrails.org/active_record_querying.html
// - Customer *-- Order: One-to-many (Customer has many Orders).
// - Order *-- Book: Many-to-many (via inferred BookOrders table).
// - Customer *-- Review: One-to-many (Customer writes many Reviews).
// - Book *-- Review: One-to-many (Book receives many Reviews, Has single author).
// - Supplier *-- Book: One-to-many (Supplier supplies many Books).
// - Author *-- Book: One-to-many (Author writes many Books).
//
//      classDiagram
//          direction TB
//          class Customer
//      {
//              +FirstName: string
//              +LastName: string
//              +Title: string
//              +Email: string
//              +Visits: int
//              +OrdersCount: int
//              +LockVersion: int
//              +Orders: ICollection ~Order~
//              +Reviews: ICollection ~Review~
//          }
//
//      class Order
//      {
//              +DateSubmitted: DateTimeOffset
//              +Status: int
//              +Subtotal: decimal
//              +Shipping: decimal
//              +Tax: decimal
//              +Total: decimal
//              +Books: ICollection ~Book~
//          }
//
//      class Review
//      {
//              +Title: string
//              +Body: string
//              +Rating: int
//              +State: int
//      }
//
//      class Book
//      {
//              +Title: string
//              +YearPublished: int
//              +Isbn: string
//              +Price: decimal
//              +OutOfPrint: bool
//              +Views: int
//              +Orders: ICollection ~Order~
//              +Reviews: ICollection ~Review~
//          }
//
//      class Supplier
//      {
//              +Name: string
//              +Books: ICollection ~Book~
//          }
//
//      class Author
//      {
//              +FirstName: string
//              +LastName: string
//              +Title: string
//              +Books: ICollection ~Book~
//          }
//
//      Customer "1" *-- "0..*" Order : has
//      Order "1" *-- "0..*" Book : has
//      Customer "1" *-- "0..*" Review : writes
//      Book "1" *-- "0..*" Review : receives
//      Supplier "1" *-- "0..*" Book : supplies
//      Author "1" *-- "0..*" Book : writes

[DebuggerDisplay("Id={Id}, FirstName={FirstName}, LastName={LastName}")]
[ActiveEntityFeatures(ActiveEntityFeatures.Forwarders | ActiveEntityFeatures.Specifications | ActiveEntityFeatures.QueryDsl | ActiveEntityFeatures.ConventionFinders)] // triggers the source generator to enable extra features
public partial class Customer2 : ActiveEntity<Customer2, Guid> // non TypedId
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Title { get; set; }
}

 [DebuggerDisplay("Id={Id}, FirstName={FirstName}, LastName={LastName}")]
[TypedEntityId<Guid>] // code generates a typedId called CustomerId
[ActiveEntityFeatures(ActiveEntityFeatures.Forwarders | ActiveEntityFeatures.Specifications | ActiveEntityFeatures.QueryDsl | ActiveEntityFeatures.ConventionFinders)] // triggers the source generator to enable extra features
public partial class Customer : ActiveEntity<Customer, CustomerId>, IAuditable, IConcurrency
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Title { get; set; }
    public bool IsActive { get; set; } = true;
    public EmailAddressStub Email { get; set; }
    public int Visits { get; set; } = 0;
    public DateTime? LastVisited { get; set; }
    public int OrdersCount { get; set; } = 0;
    public int LockVersion { get; set; } = 0;
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public AuditState AuditState { get; set; } = new AuditState();
    public Guid ConcurrencyVersion { get; set; }

    // Inline static query extension -> implemented directly in the entity using WithProviderAsync.
    /// <summary>
    /// Finds the customers by last name.
    /// </summary>
    /// <param name="name">the name</param>
    public static Task<Result<IEnumerable<Customer>>> FindAllByLastNameCustomAsync(string name, CancellationToken cancellationToken = default) =>
        WithContextAsync(async (context) =>
            await context.Provider.FindAllAsync(new Specification<Customer>(c => c.LastName == name), null, cancellationToken));

    //// Static query forwarder (manual) -> delegates to the extension method
    //public static Task<Result<IEnumerable<Customer>>> FindAllByFirstNameAsync(string name) =>
    //    (null as ActiveEntity<Customer, CustomerId>).FindAllByFirstNameAsync(name);
    //    // Note: extension method is defined on ActiveEntity<Customer, CustomerId> so we can call it with a dummy/null instance
}

public class BasicCustomerValidator : AbstractValidator<Customer>
{
    public BasicCustomerValidator()
    {
        this.RuleFor(c => c.FirstName).NotEmpty().MaximumLength(50);
        this.RuleFor(c => c.LastName).NotEmpty().MaximumLength(50);
        this.RuleFor(c => c.Email.Value).EmailAddress();
    }
}

public class BusinessCustomerValidator : AbstractValidator<Customer>
{
    public BusinessCustomerValidator()
    {
        this.RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
    }
}

public class DeleteCustomerValidator : AbstractValidator<Customer>
{
    public DeleteCustomerValidator()
    {
        this.RuleFor(c => c.Id).MustAsync(async (id, ct) =>
            !(await Order.ExistsAsync(o => o.CustomerId == id && o.Status == OrderStatus.Pending, null, ct)).Value)
            .WithMessage("Cannot delete customer with pending orders.");
    }
}

//public partial class Customer // this is generated by the source generator as a forwarder to the extension method
//{
//    public static Task<Result<IEnumerable<Customer>>> FindAllByFirstNameAsync(string name) =>
//        (null as ActiveEntity<Customer, CustomerId>).FindAllByFirstNameAsync(name);
//}

public static class CustomerQueryExtensions
{
    /// <summary>
    /// Finds the customers by first name.
    /// </summary>
    /// <param name="name">the name</param>
    public static Task<Result<IEnumerable<Customer>>> FindAllByFirstNameCustomAsync( // triggers the soure generator and adds a static query forwarder (auto) in Customer
        this ActiveEntity<Customer, CustomerId> _, string name, CancellationToken cancellationToken = default)
    {
        return Customer.WithContextAsync(context =>
            context.Provider.FindAllAsync(new Specification<Customer>(c => c.FirstName == name), null, cancellationToken));
    }

    /// <summary>
    /// Finds the customers by title
    /// </summary>
    /// <param name="title">the title</param>
    public static Task<Result<IEnumerable<Customer>>> FindAllByTitleCustomAsync( // triggers the soure generator and adds a static query forwarder (auto) in Customer
        this ActiveEntity<Customer, CustomerId> _, string title)
    {
        return Customer.WithContextAsync(context =>
            context.Provider.FindAllAsync(new Specification<Customer>(c => c.Title == title)));
    }
}

public class CustomerDto
{
    public string Name { get; set; }
    public string Title { get; set; }
}

public class CustomerTotals
{
    public int VisitCount { get; set; }

    public int OrderCount { get; set; }
}

[DebuggerDisplay("Id={Id}, CustomerId={CustomerId}, Status={Status}")]
[TypedEntityId<Guid>]
[ActiveEntityFeatures(ActiveEntityFeatures.Forwarders | ActiveEntityFeatures.Specifications | ActiveEntityFeatures.QueryDsl | ActiveEntityFeatures.ConventionFinders)] // methods in OrderQueryExtensions
public partial class Order : ActiveEntity<Order, OrderId>, IAuditable, IConcurrency
{
    public DateTimeOffset DateSubmitted { get; set; }
    public OrderStatus Status { get; set; }// = smart enumeration
    public Currency Currency { get; set; } = Currency.UsDollar; // value object
    public decimal Subtotal { get; set; } = 0.00m;
    public decimal Shipping { get; set; } = 0.00m;
    public decimal Tax { get; set; } = 0.00m;
    public decimal Total => this.Subtotal + this.Shipping + this.Tax;

    public CustomerId CustomerId { get; set; }
    public Customer Customer { get; set; }

    public ICollection<OrderBook> OrderBooks { get; set; } = [];

    public AuditState AuditState { get; set; } = new AuditState();
    public Guid ConcurrencyVersion { get; set; }

    protected override async Task<Result> OnBeforeInsertAsync(IActiveEntityEntityProvider<Order, OrderId> provider, CancellationToken ct)
    {
        // only active users can place orders (this is one way to enforce this. Idealy this should be done in the logic/app/domain layer)
        var customerId = this.Customer?.Id ?? this.CustomerId;
        var customerResult = await Customer.FindOneAsync(e => e.Id == customerId && e.IsActive, null, ct);
        if (customerResult.IsFailure)
        {
            return customerResult; // not found or not active
        }

        return Result.Success();
    }

    protected override async Task<Result> OnAfterInsertAsync(IActiveEntityEntityProvider<Order, OrderId> provider, CancellationToken ct)
    {
        await Task.Delay(0, ct);
        //var prov = GetProvider();
        //await this.DeleteAsync(prov, ct); // synchronous wait is ok here because we are already async

        return Result.Success();
    }
}

public static class OrderQueryExtensions
{
    /// <summary>
    /// Finds the orders for the customer.
    /// </summary>
    /// <param name="customer">the customer</param>
    public static Task<Result<IEnumerable<Order>>> FindAllForAsync( // triggers the soure generator and adds a static query forwarder (auto) in Order
        this ActiveEntity<Order, OrderId> _, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return Order.WithContextAsync(context =>
            context.Provider.FindAllAsync(new Specification<Order>(c => c.CustomerId == customer.Id)));
    }

    /// <summary>
    /// Finds the orders for the customer.
    /// </summary>
    /// <param name="status">the status</param>
    public static Task<Result<IEnumerable<Order>>> FindAllForAsync( // triggers the soure generator and adds a static query forwarder (auto) in Order
        this ActiveEntity<Order, OrderId> _, OrderStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return Order.WithContextAsync(context =>
            context.Provider.FindAllAsync(new Specification<Order>(c => c.Status == status)));
    }
}

public class Review : ActiveEntity<Review, Guid>, IAuditable, IConcurrency
{
    public string Title { get; set; }
    public string Body { get; set; }
    public int Rating { get; set; }
    public int State { get; set; }

    // Foreign keys
    public CustomerId CustomerId { get; set; }
    public Guid BookId { get; set; }

    // Navigation properties (one-way to avoid cycles)
    public Customer Customer { get; set; }
    public Book Book { get; set; }

    public AuditState AuditState { get; set; } = new AuditState();
    public Guid ConcurrencyVersion { get; set; }
}

public class Book : ActiveEntity<Book, Guid>, IAuditable, IConcurrency
{
    public string Title { get; set; }
    public int YearPublished { get; set; }
    public string Isbn { get; set; }
    public decimal Price { get; set; } = 0.00m;
    public bool OutOfPrint { get; set; } = false;
    public int Views { get; set; } = 0;

    public Guid? SupplierId { get; set; }
    public Supplier Supplier { get; set; }
    public Guid? AuthorId { get; set; }
    public Author Author { get; set; }

    public ICollection<OrderBook> OrderBooks { get; set; } = [];

    public AuditState AuditState { get; set; } = new AuditState();
    public Guid ConcurrencyVersion { get; set; }
}

public class OrderBook
{
    public OrderId OrderId { get; set; }
    public Order Order { get; set; }

    public Guid BookId { get; set; }
    public Book Book { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class Supplier : ActiveEntity<Supplier, Guid>, IAuditable, IConcurrency
{
    [Required]
    [MinLength(3)]
    [MaxLength(100)]
    public string Name { get; set; }

    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    [Required]
    public string Email { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 1;
    public ICollection<Book> Books { get; set; } = [];
    public AuditState AuditState { get; set; } = new AuditState();
    public Guid ConcurrencyVersion { get; set; }
}

public class Author : ActiveEntity<Author, Guid>, IAuditable, IConcurrency
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Title { get; set; }
    public ICollection<Book> Books { get; set; } = [];
    public AuditState AuditState { get; set; } = new AuditState();
    public Guid ConcurrencyVersion { get; set; }
}

[DebuggerDisplay("Id={Id}, Value={Value}")]
public class OrderStatus(int id, string value, string code, string description) : Enumeration(id, value)
{
    public static readonly OrderStatus New = new(1, "New", "NEW", "Lorem Ipsum");
    public static readonly OrderStatus Pending = new(2, "Pending", "PND", "Lorem Ipsum");
    public static readonly OrderStatus Submitted = new(3, "Submitted", "SUB", "Lorem Ipsum");
    public static readonly OrderStatus Processing = new(4, "Processing", "PRC", "Lorem Ipsum");
    public static readonly OrderStatus Shipped = new(5, "Shipped", "SHP", "Lorem Ipsum");

    public string Code { get; } = code;

    public string Description { get; } = description;

    public static OrderStatus FromId(int id) => FromId<OrderStatus>(id);

    public static IEnumerable<OrderStatus> GetAll() => GetAll<OrderStatus>();

    public static OrderStatus GetByCode(string code) => GetAll<OrderStatus>().FirstOrDefault(e => e.Code == code);
}

public class ActiveEntityDbContext : DbContext, IOutboxDomainEventContext
{
    public ActiveEntityDbContext() { }

    public ActiveEntityDbContext(DbContextOptions options)
        : base(options) { }

    //public DbSet<Customer> Customers { get; set; } // dbset needed?

    //public DbSet<Order> Orders { get; set; } // dbset needed?

    //public DbSet<Review> Reviews { get; set; }

    //public DbSet<Book> Books { get; set; }

    //public DbSet<Supplier> Suppliers { get; set; }

    //public DbSet<Author> Authors { get; set; }

    public DbSet<OutboxDomainEvent> OutboxDomainEvents { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("-"); // needed to create the migrations
            //optionsBuilder.UseSqlite("-"); // needed to create the migrations
            optionsBuilder.ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
    }
}

public class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers")
            .HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => CustomerId.Create(value));

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // Tell EF Core to use the application-provided value

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Title)
            .HasMaxLength(50);

        builder.OwnsOne(b => b.Email,
            pb =>
            {
                pb.Property(e => e.Value)
                    .IsRequired()
                    .HasMaxLength(256);
            });

        builder.Property(x => x.Visits)
            .HasDefaultValue(0);

        builder.Property(x => x.LastVisited)
            .IsRequired(false);

        builder.Property(x => x.OrdersCount)
            .HasDefaultValue(0);

        builder.Property(x => x.LockVersion)
            .HasDefaultValue(0);

        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Reviews)
            .WithOne(r => r.Customer)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOneAuditState();
    }
}

public class OrderEntityTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders")
            .HasKey(x => x.Id).IsClustered(false);

        builder.Navigation(o => o.Customer).AutoInclude();
        builder.Navigation(o => o.OrderBooks).AutoInclude();

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => OrderId.Create(value));

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.Property(x => x.DateSubmitted)
            .IsRequired();

        builder.Property(x => x.Subtotal)
            .HasDefaultValue(0.00m)
            .HasPrecision(18, 2);

        builder.Property(x => x.Shipping)
            .HasDefaultValue(0.00m)
            .HasPrecision(18, 2);

        builder.Property(x => x.Tax)
            .HasDefaultValue(0.00m)
            .HasPrecision(18, 2);

        builder.OwnsOne(b => b.Currency,
            pb =>
            {
                pb.Property(e => e.Code)
                    .IsRequired()
                    .HasMaxLength(8);
            });

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion(new EnumerationConverter<int, string, OrderStatus>());

        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.OrderBooks)
           .WithOne(ob => ob.Order)
           .HasForeignKey(ob => ob.OrderId);

        builder.OwnsOneAuditState();
    }
}

public class OrderBookEntityTypeConfiguration : IEntityTypeConfiguration<OrderBook>
{
    public void Configure(EntityTypeBuilder<OrderBook> builder)
    {
        builder.ToTable("OrderBooks")
            .HasKey(ob => new { ob.OrderId, ob.BookId });
        builder.Navigation(ob => ob.Book).AutoInclude();

        builder.HasOne(ob => ob.Order)
            .WithMany(o => o.OrderBooks)
            .HasForeignKey(ob => ob.OrderId);

        builder.HasOne(ob => ob.Book)
            .WithMany(b => b.OrderBooks)
            .HasForeignKey(ob => ob.BookId);

        builder.Property(ob => ob.Quantity)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(ob => ob.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();
    }
}

public class ReviewEntityTypeConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // Tell EF Core to use the application-provided value

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Body)
            .HasMaxLength(1000);

        builder.Property(x => x.Rating)
            .IsRequired();

        builder.Property(x => x.State)
            .IsRequired();

        builder.HasOne(r => r.Customer)
           .WithMany(c => c.Reviews)
           .HasForeignKey(r => r.CustomerId)
           .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Book)
            .WithMany() // No back-reference to avoid recursion
            .HasForeignKey(r => r.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.State);

        builder.OwnsOneAuditState();
    }
}

public class BookEntityTypeConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books")
            .HasKey(x => x.Id).IsClustered(false);
        builder.Navigation(o => o.Author).AutoInclude();
        builder.Navigation(o => o.Supplier).AutoInclude();

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // Tell EF Core to use the application-provided value

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.YearPublished)
            .IsRequired();

        builder.Property(x => x.Isbn)
            .IsRequired()
            .HasMaxLength(13);

        builder.Property(x => x.Price)
            .HasDefaultValue(0.00m)
            .HasPrecision(18, 2);

        builder.Property(x => x.OutOfPrint)
            .HasDefaultValue(false);

        builder.Property(x => x.Views)
            .HasDefaultValue(0);

        builder.HasMany(b => b.OrderBooks)
            .WithOne(ob => ob.Book)
            .HasForeignKey(ob => ob.BookId);

        builder.HasOne(b => b.Supplier)
            .WithMany(s => s.Books)
            .HasForeignKey(b => b.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Isbn);
        //.IsUnique();

        builder.OwnsOneAuditState();
    }
}

public class SupplierEntityTypeConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers")
            .HasKey(x => x.Id).IsClustered(false);
        //builder.Navigation(o => o.Books).AutoInclude(); // causes recursion issues (stack overflow)

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // Tell EF Core to use the application-provided value

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Rating)
            .IsRequired();

        builder.HasIndex(x => x.Name);
        //.IsUnique();

        builder.OwnsOneAuditState();
    }
}

public class AuthorEntityTypeConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("Authors")
            .HasKey(x => x.Id).IsClustered(false);
        builder.Navigation(o => o.Books).AutoInclude();

        builder.Property(e => e.ConcurrencyVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever(); // Tell EF Core to use the application-provided value

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Title)
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.FirstName, x.LastName });
        //.IsUnique();

        builder.OwnsOneAuditState();
    }
}

//public partial class Customer
//{
//    /// <summary>
//    /// Starts a fluent query for Customer.
//    /// </summary>
//    public static CustomerQuery Query() => new CustomerQuery();

//    /// <summary>
//    /// Provides a fluent DSL for querying Customer.
//    /// </summary>
//    public class CustomerQuery : ActiveEntityQuery<Customer, CustomerId> // see that the TypedId is used here, not the underlying Guid
//    {
//        protected override Task<Result<IEnumerable<Customer>>> FindAllInternal(
//            IEnumerable<ISpecification<Customer>> specs,
//            FindOptions<Customer> options,
//            CancellationToken ct) =>
//            Customer.WithProviderAsync(provider => provider.FindAllAsync(specs, options, ct));

//        protected override Task<ResultPaged<Customer>> FindAllPagedInternal(
//            IEnumerable<ISpecification<Customer>> specs,
//            FindOptions<Customer> options,
//            CancellationToken ct) =>
//            Customer.WithProviderAsync(provider => provider.FindAllPagedAsync(specs, options, ct));

//        protected override Task<Result<Customer>> FindOneInternal(
//            IEnumerable<ISpecification<Customer>> specs,
//            FindOptions<Customer> options,
//            CancellationToken ct) =>
//            Customer.WithProviderAsync(provider => provider.FindOneAsync(specs, options, ct));

//        protected override Task<Result<bool>> ExistsInternal(
//            IEnumerable<ISpecification<Customer>> specs,
//            FindOptions<Customer> options, // adjusted
//            CancellationToken ct) =>
//            Customer.WithProviderAsync(provider => provider.ExistsAsync(specs, options, ct));

//        protected override Task<Result<long>> CountInternal(
//            IEnumerable<ISpecification<Customer>> specs,
//            FindOptions<Customer> options, // adjusted
//            CancellationToken ct) =>
//            Customer.WithProviderAsync(provider => provider.CountAsync(specs, options, ct));

//        protected override Task<Result<IEnumerable<TProjection>>> ProjectAllInternal<TProjection>(
//            IEnumerable<ISpecification<Customer>> specs,
//            Expression<Func<Customer, TProjection>> projection,
//            FindOptions<Customer> options,
//            CancellationToken ct) =>
//            Customer.WithProviderAsync(provider => provider.ProjectAllAsync(specs, projection, this.options, ct));

//        protected override Task<ResultPaged<TProjection>> ProjectAllPagedInternal<TProjection>(
//                IEnumerable<ISpecification<Customer>> specs,
//                Expression<Func<Customer, TProjection>> projection,
//                FindOptions<Customer> options,
//                CancellationToken ct) =>
//                Customer.WithProviderAsync(provider => provider.ProjectAllPagedAsync(specs, projection, options, ct));
//    }
//}
