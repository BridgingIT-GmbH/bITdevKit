//// MIT-License
//// Copyright BridgingIT GmbH - All Rights Reserved
//// Use of this source code is governed by an MIT-style license that can be
//// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

//using System;
//using System.Linq;
//using System.Threading.Tasks;
//using BridgingIT.DevKit.Infrastructure.EntityFramework;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Shouldly;
//using Xunit;

//[IntegrationTest("Infrastructure")]
//[Collection(nameof(TestEnvironmentCollection6))] // https://xunit.net/docs/shared-context#collection-fixture
//public class GenericEntityMergeStrategyTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : TestsBase(output, services =>
//{
//    services.AddLogging();
//})
//{
//    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
//    private readonly ITestOutputHelper output = output;

//    [Fact]
//    public async Task MergeAsync_UpdatesScalarProperties()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        context.Database.EnsureCreated();

//        var existing = new Order { Subtotal = 10m, Status = OrderStatus.New, Customer = new Customer { FirstName = "John", LastName = "Doe", Email = EmailAddressStub.Create("johndoe@example.com") } };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        context.Orders.Find(existing.Id).ShouldNotBeNull();
//        (await context.Orders.FirstOrDefaultAsync(e => e.Id.Equals(existing.Id))).ShouldNotBeNull();
//        (await context.Orders.FirstOrDefaultAsync(e => e.Id == existing.Id)).ShouldNotBeNull();
//        context.ChangeTracker.Clear(); // Clear tracker to simulate detached state

//        var incoming = new Order { Id = existing.Id, Subtotal = 20m, Status = OrderStatus.Pending };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.Subtotal.ShouldBe(20m);
//        merged.Status.ShouldBe(OrderStatus.Pending);
//        context.Entry(merged).State.ShouldBe(EntityState.Modified);
//    }

//    [Fact]
//    public async Task MergeAsync_UpdatesReferenceNavigation()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        context.Database.EnsureCreated();

//        var existing = new Order { Customer = new Customer { FirstName = "John", LastName = "Doe", Email = EmailAddressStub.Create("johndoe@example.com") } };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        context.Orders.Find(existing.Id).ShouldNotBeNull();
//        (await context.Orders.FirstOrDefaultAsync(e => e.Id.Equals(existing.Id))).ShouldNotBeNull();
//        (await context.Orders.FirstOrDefaultAsync(e => e.Id == existing.Id)).ShouldNotBeNull();
//        context.ChangeTracker.Clear(); // Clear tracker to simulate detached state

//        var incoming = new Order { Id = existing.Id, CustomerId = existing.CustomerId, Customer = new Customer { Id = existing.Customer.Id, FirstName = "Johnny", LastName = "Doe", Email = EmailAddressStub.Create("johndoe@example.com") } };

//        // Act
//        //context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        context.ChangeTracker.Clear(); // Clear tracker to simulate detached state
//        //var a = context.Orders.Find(existing.Id);
//        var b = (await context.Orders.Include(e => e.Customer).FirstOrDefaultAsync(e => e.Id == existing.Id));
//        merged.Customer.ShouldNotBeNull();
//        merged.Customer.FirstName.ShouldBe("Johnny");
//        context.Entry(merged.Customer).State.ShouldBe(EntityState.Modified);
//    }

//    [Fact]
//    public async Task MergeAsync_ReplacesReferenceNavigation()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var customer1 = new Customer { Id = Guid.NewGuid(), FirstName = "John" };
//        var customer2 = new Customer { Id = Guid.NewGuid(), FirstName = "Jane" };
//        await context.Customers.AddRangeAsync(customer1, customer2);
//        await context.SaveChangesAsync();

//        var existing = new Order { Id = Guid.NewGuid(), CustomerId = customer1.Id, Customer = customer1 };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order { Id = existing.Id, CustomerId = customer2.Id, Customer = customer2 };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.CustomerId.ShouldBe(customer2.Id);
//        merged.Customer.FirstName.ShouldBe("Jane");
//        context.Entry(merged).Property(nameof(Order.CustomerId)).CurrentValue.ShouldBe(customer2.Id);
//        context.Entry(merged.Customer).State.ShouldBe(EntityState.Unchanged); // since it's attached as existing
//    }

//    [Fact]
//    public async Task MergeAsync_AddsToCollection()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var book = new Book { Id = Guid.NewGuid(), Title = "New Book" };
//        await context.Books.AddAsync(book);
//        await context.SaveChangesAsync();

//        var existing = new Order { Id = Guid.NewGuid(), OrderBooks = [] };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order
//        {
//            Id = existing.Id,
//            OrderBooks = [new OrderBook { OrderId = existing.Id, BookId = book.Id, Quantity = 1, UnitPrice = 10m }]
//        };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.OrderBooks.Count.ShouldBe(1);
//        merged.OrderBooks.First().BookId.ShouldBe(book.Id);
//        context.Entry(merged.OrderBooks.First()).State.ShouldBe(EntityState.Added);
//    }

//    [Fact]
//    public async Task MergeAsync_RemovesFromCollection()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var book = new Book { Id = Guid.NewGuid(), Title = "To Remove" };
//        await context.Books.AddAsync(book);
//        await context.SaveChangesAsync();

//        var existing = new Order
//        {
//            Id = Guid.NewGuid(),
//            OrderBooks = [new OrderBook { OrderId = Guid.NewGuid(), BookId = book.Id, Quantity = 1, UnitPrice = 10m }]
//        };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order { Id = existing.Id, OrderBooks = [] };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.OrderBooks.Count.ShouldBe(0);
//        context.ChangeTracker.Entries<OrderBook>().FirstOrDefault()?.State.ShouldBe(EntityState.Deleted);
//    }

//    [Fact]
//    public async Task MergeAsync_UpdatesInCollection()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var book = new Book { Id = Guid.NewGuid(), Title = "To Update" };
//        await context.Books.AddAsync(book);
//        await context.SaveChangesAsync();

//        var existing = new Order
//        {
//            Id = Guid.NewGuid(),
//            OrderBooks = [new OrderBook { OrderId = Guid.NewGuid(), BookId = book.Id, Quantity = 1, UnitPrice = 10m }]
//        };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order
//        {
//            Id = existing.Id,
//            OrderBooks = [new OrderBook { OrderId = existing.Id, BookId = book.Id, Quantity = 2, UnitPrice = 15m }]
//        };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.OrderBooks.Count.ShouldBe(1);
//        merged.OrderBooks.First().Quantity.ShouldBe(2);
//        merged.OrderBooks.First().UnitPrice.ShouldBe(15m);
//        context.Entry(merged.OrderBooks.First()).State.ShouldBe(EntityState.Modified);
//    }

//    [Fact]
//    public async Task MergeAsync_HandlesOwnedType()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var existing = new Order
//        {
//            Id = Guid.NewGuid(),
//        };
//        existing.AuditState.SetCreated("User1");
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order
//        {
//            Id = existing.Id,
//        };
//        incoming.AuditState.SetCreated("User2");

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.AuditState.CreatedBy.ShouldBe("User2");
//        //merged.AuditState.CreatedDate.ShouldBeGreaterThan(existing.AuditState.CreatedDate);
//        context.Entry(merged).State.ShouldBe(EntityState.Modified);
//        // Owned types don't have separate entries, but scalars are updated
//    }

//    [Fact]
//    public async Task MergeAsync_DetectsCycles()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var customer = new Customer { Id = Guid.NewGuid() };
//        var order = new Order { Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer };
//        customer.Orders = [order];
//        await context.Orders.AddAsync(order);
//        await context.SaveChangesAsync();

//        var incomingCustomer = new Customer { Id = customer.Id };
//        var incoming = new Order { Id = order.Id, Customer = incomingCustomer };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming);

//        // Assert
//        merged.ShouldNotBeNull(); // no stack overflow
//        merged.Customer.ShouldNotBeNull();
//    }

//    [Fact]
//    public async Task MergeAsync_RespectsMaxDepth()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var existing = new Order { Id = Guid.NewGuid() };
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order { Id = existing.Id };

//        var options = new GenericEntityMergeStrategy.Options { MaxDepth = 1 };

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming, options);

//        // Assert
//        merged.ShouldNotBeNull();
//        // No deeper recursion beyond depth 1
//    }

//    [Fact]
//    public async Task MergeAsync_IgnoresSpecifiedNavigations()
//    {
//        // Arrange
//        var context = new ActiveEntityDbContext(new DbContextOptionsBuilder<ActiveEntityDbContext>().UseSqlServer(this.fixture.SqlConnectionString).Options);
//        var existing = new Order { Id = Guid.NewGuid() };
//        existing.AuditState.SetCreated("Old");
//        await context.Orders.AddAsync(existing);
//        await context.SaveChangesAsync();

//        var incoming = new Order { Id = existing.Id };
//        incoming.AuditState.SetCreated("New");

//        var options = new GenericEntityMergeStrategy.Options();
//        options.IgnoredNavigations.Add(nameof(Order.AuditState));

//        // Act
//        var merged = await GenericEntityMergeStrategy.MergeAsync(context, incoming, options);

//        // Assert
//        merged.AuditState.CreatedBy.ShouldBe("Old"); // ignored, not updated
//    }
//}