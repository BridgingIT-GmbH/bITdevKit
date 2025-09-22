// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection5))] // https://xunit.net/docs/shared-context#collection-fixture
public class EntityFrameworkActiveEntityTests(ITestOutputHelper output, TestEnvironmentFixture fixture) : TestsBase(output, services =>
{
    fixture.EnsureSqlServerDbContext();
    services.AddLogging();
    services.AddSingleton(provider => XunitLogger.Create(output));
    services.AddSingleton(provider => XunitLoggerFactory.Create(output));
    services.AddSqlServerDbContext<ActiveEntityDbContext>(fixture.SqlConnectionString);
    services.AddNotifier()
        .AddHandlers();

    services.AddActiveEntity(cfg =>
    {
        cfg.For<Customer, CustomerId>()
            .UseEntityFrameworkProvider(b => b.Context<ActiveEntityDbContext>())
            .AddLoggingBehavior()
            .AddValidatorBehavior<Customer, CustomerId, BasicCustomerValidator>(o => o.ApplyOnInsert())
            .AddValidatorBehavior<Customer, CustomerId, BusinessCustomerValidator>(o => o.ApplyOnUpdate())
            .AddValidatorBehavior<Customer, CustomerId, DeleteCustomerValidator>(o => o.ApplyOnDelete())
            .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false })
            //.AddDomainEventOutboxPublishingBehavior<Customer, CustomerId, ActiveEntityDbContext>() // not ideal as all generics have to be passed again
            //.AddDomainEventOutboxPublishingBehavior(o => o.UseContext<ActiveEntityDbContext>())
            .AddAuditStateBehavior(o => o.EnableSoftDelete(false));

        cfg.For<Order, OrderId>()
            .UseEntityFrameworkProvider(b => b
                //.Context<ActiveEntityDbContext>() // optional when DbContext specified with Options<TContext>()
                .Options<ActiveEntityDbContext>(o => o
                    .GenericMergeStrategy(
                        new GenericEntityMergeStrategy.Options
                        {
                            MaxDepth = 10,
                            Logger = XunitLogger.Create(output)
                        })))
            .AddLoggingBehavior()
            .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });
        //.AddDomainEventOutboxPublishingBehavior<Order, OrderId, ActiveEntityDbContext>()
        //.AddDomainEventOutboxPublishingBehavior(o => o.UseContext<ActiveEntityDbContext>())

        cfg.For<Book, Guid>()
            .UseEntityFrameworkProvider(b => b.Context<ActiveEntityDbContext>())
            .AddLoggingBehavior()
            .AddAuditStateBehavior(o => o.EnableSoftDelete(false));

        cfg.For<Author, Guid>()
            .UseEntityFrameworkProvider(b => b.Context<ActiveEntityDbContext>())
            .AddLoggingBehavior()
            .AddAuditStateBehavior(o => o.EnableSoftDelete(false));

        cfg.For<Supplier, Guid>()
            .UseEntityFrameworkProvider(b => b.Context<ActiveEntityDbContext>())
            .AddLoggingBehavior()
            .AddAnnotationsValidator()
            .AddAuditStateBehavior(o => o.EnableSoftDelete());

        cfg.For<Review, Guid>()
            .UseEntityFrameworkProvider(b => b.Context<ActiveEntityDbContext>())
            .AddLoggingBehavior()
            .AddAuditStateBehavior(o => o.EnableSoftDelete());
    });

    // Test in-memory provider
    //services.AddActiveEntity(cfg =>
    //{
    //    cfg.For<Customer, CustomerId>()
    //        .AddLoggingBehavior()
    //        .UseInMemoryProvider();
    //});

    // Register the global service provider for ActiveEntity configurator
    ActiveEntityConfigurator.SetGlobalServiceProvider(services.BuildServiceProvider()); // app.UseActiveEntity(app.Services);  // Sets global SP
})
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    [Fact]
    public async Task InsertCustomer_Test()
    {
        // Arrange
        var sut = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };

        // Act
        var @event = new CustomerCreatedDomainEvent(sut);
        sut.RegisterDomainEvent(@event);
        var insertResult = await sut.InsertAsync(); // should publish the domain event after insert

        // Assert
        insertResult.ShouldBeSuccess();
        insertResult.Value.ShouldNotBeNull();
        insertResult.Value.Id.ShouldBe(sut.Id);
        sut.HasDomainEvents().ShouldBeFalse(); // events cleared as published (behavior)
        var findResult = await Customer.FindOneAsync(sut.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
        findResult.Value.Id.ShouldBe(sut.Id);
    }

    [Fact]
    public async Task InsertInvalidCustomer_Test()
    {
        // Arrange
        var sut = new Customer
        {
            // no FirstName, LastName which are required (db contraint)
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };

        // Act
        var @event = new CustomerCreatedDomainEvent(sut);
        sut.RegisterDomainEvent(@event);
        var insertResult = await sut.InsertAsync(); // should not publish (behavior) the domain event as insert fails

        // Assert
        insertResult.ShouldBeFailure();
        insertResult.HasError().ShouldBeTrue();
        sut.HasDomainEvents().ShouldBeTrue(); // events still registered as not published (behavior)
    }

    [Fact]
    public async Task InsertInvalidCustomer_WithValidationBehavior_Test()
    {
        // Arrange
        var sut = new Customer
        {
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        }; // Missing FirstName/LastName

        // Act
        var insertResult = await sut.InsertAsync();

        // Assert
        insertResult.ShouldBeFailure();
        insertResult.HasError<FluentValidationError>().ShouldBeTrue();
        var validationError = insertResult.Errors.OfType<FluentValidationError>().First();
        validationError.Errors.ShouldContain(e => e.PropertyName == "FirstName");
        validationError.Errors.ShouldContain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public async Task DeleteCustomer_WithValidationFailure_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };
        var customerInsertResult = await customer.InsertAsync();
        customerInsertResult.ShouldBeSuccess();
        customer = customerInsertResult.Value;
        var order = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, Subtotal = 10m };
        await order.InsertAsync();

        // Act
        var deleteResult = await customer.DeleteAsync();

        // Assert
        deleteResult.ShouldBeFailure();
        deleteResult.HasError<FluentValidationError>().ShouldBeTrue();
        var validationError = deleteResult.Errors.OfType<FluentValidationError>().First();
        validationError.Errors.ShouldContain(e => e.ErrorMessage.Contains("Cannot delete customer with pending orders"));
    }

    [Fact]
    public async Task UpdateInvalidCustomer_WithValidationBehavior_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await customer.InsertAsync();
        insertResult.ShouldBeSuccess();
        customer = insertResult.Value;
        customer.LastName = string.Empty; // Invalid update

        // Act
        var updateResult = await customer.UpdateAsync();

        // Assert
        updateResult.ShouldBeFailure();
        updateResult.HasError<FluentValidationError>().ShouldBeTrue();
        var validationError = updateResult.Errors.OfType<FluentValidationError>().First();
        validationError.Errors.ShouldContain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public async Task InsertMultipleCustomers_Test()
    {
        // Arrange
        var customers = new[]
        {
            new Customer
            {
                FirstName = "Bulk1",
                LastName = "Doe",
                Email = EmailAddressStub.Create("bulk1.doe@example.com"),
                Title = "Mr."
            },
            new Customer
            {
                FirstName = "Bulk2",
                LastName = "Doe",
                Email = EmailAddressStub.Create("bulk2.doe@example.com"),
                Title = "Ms."
            },
            new Customer
            {
                FirstName = "Bulk3",
                LastName = "Doe",
                Email = EmailAddressStub.Create("bulk3.doe@example.com"),
                Title = "Dr."
            }
        };
        foreach (var customer in customers)
        {
            customer.RegisterDomainEvent(new CustomerCreatedDomainEvent(customer));
        }

        // Act
        var results = await Customer.InsertAsync(customers);

        // Assert
        results.ShouldAllBe(r => r.IsSuccess);
        results.Count().ShouldBe(3);
        foreach (var result in results)
        {
            result.Value.ShouldNotBeNull();
            result.Value.Id.ShouldNotBeNull();
            result.Value.HasDomainEvents().ShouldBeFalse(); // Events cleared after publishing
            var findResult = await Customer.FindOneAsync(result.Value.Id);
            findResult.ShouldBeSuccess();
            findResult.Value.ShouldNotBeNull();
            findResult.Value.FirstName.ShouldBe(result.Value.FirstName);
        }
    }

    [Fact]
    public async Task InsertCustomerWithTransaction_Test()
    {
        // Arrange
        var sut = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };

        // Act
        var transactionResult = await Customer.WithTransactionAsync(async context =>
        {
            var @event = new CustomerCreatedDomainEvent(sut);
            sut.RegisterDomainEvent(@event);
            return await sut.InsertAsync(context); // should publish (behavior) the domain event after insert
        });

        // Assert
        transactionResult.ShouldBeSuccess();
        var findResult = await Customer.FindOneAsync(sut.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
        findResult.Value.Id.ShouldBe(sut.Id);
    }

    [Fact]
    public async Task InsertCustomerWithInvalidTransaction_Test()
    {
        // Arrange
        var customer1 = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };
        var customer2 = new Customer
        {
            Email = EmailAddressStub.Create("mary.jane@example.com"),
            Title = "Ms."
        };

        // Act
        var transactionResult = await Customer.WithTransactionAsync(async context =>
        {
            var event1 = new CustomerCreatedDomainEvent(customer1);
            var event2 = new CustomerCreatedDomainEvent(customer2);
            customer1.RegisterDomainEvent(event1);
            customer2.RegisterDomainEvent(event2);
            await customer1.InsertAsync(context);
            await customer2.InsertAsync(context); // invalid, will cause rollback of transaction
            return default;
        });

        // Assert
        transactionResult.ShouldBeFailure();
        var findResult1 = await Customer.FindOneAsync(customer1.Id);
        findResult1.ShouldBeFailure();
        var findResult2 = await Customer.FindOneAsync(customer2.Id);
        findResult2.ShouldBeFailure();
    }

    [Fact]
    public async Task InsertCustomerWithValidTransaction_Test()
    {
        // Arrange
        var customer1 = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };
        var customer2 = new Customer
        {
            FirstName = "Mary",
            LastName = "Jane",
            Email = EmailAddressStub.Create("mary.jane@example.com"),
            Title = "Ms."
        };

        // Act
        var transactionResult = await Customer.WithTransactionAsync(async context =>
        {
            var event1 = new CustomerCreatedDomainEvent(customer1);
            var event2 = new CustomerCreatedDomainEvent(customer2);
            customer1.RegisterDomainEvent(event1);
            customer2.RegisterDomainEvent(event2);
            await customer1.InsertAsync(context);
            await customer2.InsertAsync(context);

            return Result<IEnumerable<Customer>>.Success([customer1, customer2]);
        });

        // Assert
        transactionResult.ShouldBeSuccess();
        var findResult1 = await Customer.FindOneAsync(customer1.Id);
        findResult1.ShouldBeSuccess();
        var findResult2 = await Customer.FindOneAsync(customer2.Id);
        findResult2.ShouldBeSuccess();
    }

    [Fact]
    public async Task InsertCustomerWithFailedTransaction_Test()
    {
        // Arrange
        var sut = new Customer
        {
            // no FirstName, LastName which are required (db contraint)
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };

        // Act
        var transactionResult = await Customer.WithTransactionAsync(async context =>
        {
            var @event = new CustomerCreatedDomainEvent(sut);
            sut.RegisterDomainEvent(@event);
            return await sut.InsertAsync(context); // should not publish (behavior) the domain event as insert fails
        });

        // Assert
        transactionResult.ShouldBeFailure();
        transactionResult.HasError().ShouldBeTrue();
        sut.HasDomainEvents().ShouldBeTrue(); // events still registered as not published (behavior)
        var findResult = await Customer.FindOneAsync(sut.Id);
        findResult.ShouldBeFailure();
    }

    [Fact]
    public async Task InsertCustomersWithFailedTransaction_Test()
    {
        // Arrange: Prepare multiple customers for batch insert
        var customers = new[]
        {
            new Customer
            {
                FirstName = "Batch1",
                LastName = "Doe",
                Email = EmailAddressStub.Create("batch1.doe@example.com"),
                Title = "Mr."
            },
            new Customer
            {
                FirstName = "Batch2",
                LastName = "Doe",
                Email = EmailAddressStub.Create("batch2.doe@example.com"),
                Title = "Ms."
            },
            new Customer
            {
                // Invalid customer (missing FirstName)
                LastName = "Doe",
                Email = EmailAddressStub.Create("batch3.doe@example.com"),
                Title = "Dr."
            }
        };

        // Act: Attempt batch insert within a transaction (should fail due to invalid customer)
        var transactionResult = await Customer.WithTransactionAsync(async context =>
        {
            var results = await Customer.InsertAsync(context, customers);
            return results.All(r => r.IsSuccess) ? Result.Success() : Result.Failure("Batch insert failed.");
        });

        // Assert: Verify transaction rolled back
        transactionResult.ShouldBeFailure();
        //transactionResult.HasError("Batch insert failed.").ShouldBeTrue();
        foreach (var customer in customers)
        {
            var findResult = await Customer.FindOneAsync(customer.Id);
            findResult.ShouldBeFailure(); // Nothing persisted due to rollback
        }
    }

    [Fact]
    public async Task UpdateCustomer_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = EmailAddressStub.Create("jane.doe@example.com"),
            Title = "Ms."
        };
        var insertResult = await customer.InsertAsync();
        insertResult.ShouldBeSuccess();
        customer = insertResult.Value;
        customer.FirstName = "Janet";
        var @event = new CustomerChangedDomainEvent(customer);
        customer.RegisterDomainEvent(@event);

        // Act
        var updateResult = await customer.UpdateAsync();

        // Assert
        updateResult.ShouldBeSuccess();
        var findResult = await Customer.FindOneAsync(customer.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.FirstName.ShouldBe("Janet");
    }

    [Fact]
    public async Task UpdateCustomerSet_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = EmailAddressStub.Create("jane.doe@example.com"),
            Title = "Ms.",
            Visits = 10,
        };
        var insertResult = await customer.InsertAsync();
        insertResult.ShouldBeSuccess();
        customer = insertResult.Value;

        // Act
        var updateResult = await customer.UpdateAsync(u => u // updates only these set values to this entity in the provider (db)
                .Set(c => c.FirstName, "Janet")
                .Set(c => c.Visits, c => c.Visits + 1)
                .Set(c => c.IsActive, false));

        // Assert
        updateResult.ShouldBeSuccess();
        updateResult.Value.FirstName.ShouldBe("Janet");
        updateResult.Value.Visits.ShouldBe(11);
        updateResult.Value.IsActive.ShouldBeFalse();

        var findResult = await Customer.FindOneAsync(customer.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.FirstName.ShouldBe("Janet");
        findResult.Value.Visits.ShouldBe(11);
        findResult.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateCustomer_ConcurrencyFailure_Test()
    {
        // Arrange: Insert a customer into the DB
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = EmailAddressStub.Create("jane.doe@example.com"),
            Title = "Ms."
        };
        var insertResult = await customer.InsertAsync();
        insertResult.ShouldBeSuccess();
        customer = insertResult.Value;
        var originalConcurrencyVersion = customer.ConcurrencyVersion; // Capture initial concurrency version

        // Simulate concurrent update to cause a concurrency conflict
        var concurrentCustomer = await Customer.FindOneAsync(customer.Id);
        concurrentCustomer.ShouldBeSuccess();
        concurrentCustomer.Value.FirstName = "ConcurrentJane";
        var concurrentUpdateResult = await concurrentCustomer.Value.UpdateAsync(); // generates new concurrency version
        concurrentUpdateResult.ShouldBeSuccess();

        // Prepare original customer for update with outdated concurrency version
        customer.FirstName = "Janet";
        customer.ConcurrencyVersion = originalConcurrencyVersion; // set outdated version to trigger failure
        var @event = new CustomerChangedDomainEvent(customer);
        customer.RegisterDomainEvent(@event);

        // Act: Attempt to update the customer (should fail due to concurrency)
        var updateResult = await customer.UpdateAsync();

        // Assert: Verify update failure and DB correctness
        updateResult.ShouldBeFailure();
        updateResult.HasError<ConcurrencyError>().ShouldBeTrue(); // Expecting ConcurrencyError as per design
        customer.HasDomainEvents().ShouldBeTrue(); // Events not cleared due to failure

        // Re-fetch from DB to confirm original state (from concurrent update) persists
        var findResult = await Customer.FindOneAsync(customer.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
        findResult.Value.FirstName.ShouldBe("ConcurrentJane"); // Reflects the concurrent update
        findResult.Value.LastName.ShouldBe("Doe"); // Unchanged from original
        findResult.Value.Email.Value.ShouldBe("jane.doe@example.com"); // Unchanged from original
        findResult.Value.Title.ShouldBe("Ms."); // Unchanged from original
    }

    [Fact]
    public async Task UpdateMultipleCustomers_Test()
    {
        // Arrange
        var customers = new[]
        {
            new Customer
            {
                FirstName = "Update1",
                LastName = "Doe",
                Email = EmailAddressStub.Create("update1.doe@example.com"),
                Title = "Mr."
            },
            new Customer
            {
                FirstName = "Update2",
                LastName = "Doe",
                Email = EmailAddressStub.Create("update2.doe@example.com"),
                Title = "Ms."
            }
        };
        var insertResults = await Customer.InsertAsync(customers);
        insertResults.ShouldAllBe(r => r.IsSuccess);
        var insertedCustomers = insertResults.Select(r => r.Value).ToList();
        foreach (var customer in insertedCustomers)
        {
            customer.FirstName += " Updated";
            customer.RegisterDomainEvent(new CustomerChangedDomainEvent(customer));
        }

        // Act
        var results = await Customer.UpdateAsync(insertedCustomers);

        // Assert
        results.ShouldAllBe(r => r.IsSuccess);
        results.Count().ShouldBe(2);
        foreach (var result in results)
        {
            result.Value.ShouldNotBeNull();
            result.Value.FirstName.ShouldContain("Updated");
            result.Value.HasDomainEvents().ShouldBeFalse(); // Events cleared after publishing
            var findResult = await Customer.FindOneAsync(result.Value.Id);
            findResult.ShouldBeSuccess();
            findResult.Value.FirstName.ShouldContain("Updated");
        }
    }

    [Fact]
    public async Task UpdateDisconnectedCustomer_Test()
    {
        // Arrange: Insert a customer into the DB
        var originalCustomer = new Customer
        {
            FirstName = "Original",
            LastName = "Doe",
            Email = EmailAddressStub.Create("original.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await originalCustomer.InsertAsync();
        insertResult.ShouldBeSuccess();
        var insertedId = insertResult.Value.Id;

        // Simulate disconnected entity (e.g., from remote source): Create a new instance with same ID but updated properties
        var disconnectedCustomer = new Customer
        {
            Id = insertedId, // Same ID to simulate existing entity
            FirstName = "Updated",
            LastName = "Doe Updated",
            Email = EmailAddressStub.Create("updated.doe@example.com"),
            Title = "Dr.",
            ConcurrencyVersion = originalCustomer.ConcurrencyVersion // for concurrency check pass
        };

        // Act: Update the disconnected entity
        var updateResult = await disconnectedCustomer.UpdateAsync();

        // Assert: Verify update success and DB correctness
        updateResult.ShouldBeSuccess();
        updateResult.Value.ShouldNotBeNull();
        updateResult.Value.Id.ShouldBe(insertedId);
        updateResult.Value.FirstName.ShouldBe("Updated");
        updateResult.Value.LastName.ShouldBe("Doe Updated");
        updateResult.Value.Email.Value.ShouldBe("updated.doe@example.com");
        updateResult.Value.Title.ShouldBe("Dr.");

        // Re-fetch from DB to confirm persistence
        var findResult = await Customer.FindOneAsync(insertedId);
        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
        findResult.Value.FirstName.ShouldBe("Updated");
        findResult.Value.LastName.ShouldBe("Doe Updated");
        findResult.Value.Email.Value.ShouldBe("updated.doe@example.com");
        findResult.Value.Title.ShouldBe("Dr.");
    }

    [Fact]
    public async Task UpdateAndDeleteCustomerSet_Test()
    {
        // Arrange: insert some customers
        var insertResults = await Customer.InsertAsync(
        [
            new() { FirstName = "Bulk1", LastName = "Dow", Email = EmailAddressStub.Create("bulk1@example.com"), Title = "Mr." },
            new() { FirstName = "Bulk2", LastName = "Dow", Email = EmailAddressStub.Create("bulk2@example.com"), Title = "Ms." },
            new() { FirstName = "Bulk3", LastName = "Dow", Email = EmailAddressStub.Create("bulk3@example.com"), Title = "Dr." }
        ]);
        insertResults.ShouldAllBe(r => r.IsSuccess);

        // Act 1: bulk update all customers with LastName == "Dow"
        var updateResult = await Customer.UpdateSetAsync(
            c => c.LastName == "Dow",
            u => u
                .Set(c => c.Title, "Updated")
                .Set(c => c.IsActive, false));

        // Assert 1: all customers updated
        updateResult.ShouldBeSuccess();
        updateResult.Value.ShouldBeGreaterThanOrEqualTo(3);

        var updatedCustomers = await Customer.FindAllAsync(c => c.LastName == "Dow");
        updatedCustomers.ShouldBeSuccess();
        updatedCustomers.Value.ShouldAllBe(c => c.Title == "Updated" && !c.IsActive);

        // Act 2: bulk delete all customers with LastName == "Dow"
        var deleteResult = await Customer.DeleteSetAsync(c => c.LastName == "Dow");

        // Assert 2: all customers deleted
        deleteResult.ShouldBeSuccess();
        deleteResult.Value.ShouldBeGreaterThanOrEqualTo(3);

        var remainingCustomers = await Customer.FindAllAsync(c => c.LastName == "Dow");
        remainingCustomers.ShouldBeSuccess();
        remainingCustomers.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertCustomer_Test()
    {
        // Arrange
        // Test 1: Insert a new customer
        var newCustomer = new Customer
        {
            FirstName = "UpsertNew",
            LastName = "Doe",
            Email = EmailAddressStub.Create("upsertnew.doe@example.com"),
            Title = "Mr."
        };
        newCustomer.RegisterDomainEvent(new CustomerCreatedDomainEvent(newCustomer));

        // Act 1: Upsert new customer
        var insertResult = await newCustomer.UpsertAsync();

        // Assert 1: Verify insert
        insertResult.ShouldBeSuccess();
        insertResult.Value.entity.ShouldNotBeNull();
        insertResult.Value.action.ShouldBe(RepositoryActionResult.Inserted);
        insertResult.Value.entity.FirstName.ShouldBe("UpsertNew");
        insertResult.Value.entity.HasDomainEvents().ShouldBeFalse(); // Events cleared after publishing
        var findResult = await Customer.FindOneAsync(insertResult.Value.entity.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.FirstName.ShouldBe("UpsertNew");

        // Test 2: Update the existing customer
        var existingCustomer = insertResult.Value.entity;
        existingCustomer.FirstName = "UpsertUpdated";
        existingCustomer.RegisterDomainEvent(new CustomerChangedDomainEvent(existingCustomer));

        // Act 2: Upsert existing customer
        var updateResult = await existingCustomer.UpsertAsync();

        // Assert 2: Verify update
        updateResult.ShouldBeSuccess();
        updateResult.Value.entity.ShouldNotBeNull();
        updateResult.Value.action.ShouldBe(RepositoryActionResult.Updated);
        updateResult.Value.entity.FirstName.ShouldBe("UpsertUpdated");
        updateResult.Value.entity.HasDomainEvents().ShouldBeFalse(); // Events cleared after publishing
        findResult = await Customer.FindOneAsync(updateResult.Value.entity.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.FirstName.ShouldBe("UpsertUpdated");
    }

    [Fact]
    public async Task UpsertDisconnectedCustomerAsUpsert_Test()
    {
        // Arrange: Insert a customer into the DB
        var originalCustomer = new Customer
        {
            FirstName = "OriginalUpsert",
            LastName = "Doe",
            Email = EmailAddressStub.Create("originalupsert.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await originalCustomer.InsertAsync();
        insertResult.ShouldBeSuccess();
        var insertedId = insertResult.Value.Id;

        // Simulate disconnected entity for update
        var disconnectedCustomer = new Customer
        {
            Id = insertedId,
            FirstName = "UpdatedUpsert",
            LastName = "Doe Updated",
            Email = EmailAddressStub.Create("updatedupsert.doe@example.com"),
            Title = "Dr.",
            ConcurrencyVersion = originalCustomer.ConcurrencyVersion // for concurrency check pass
        };

        // Act: Upsert the disconnected entity (should update)
        var upsertResult = await disconnectedCustomer.UpsertAsync();

        // Assert: Verify update success and DB correctness
        upsertResult.ShouldBeSuccess();
        upsertResult.Value.entity.ShouldNotBeNull();
        upsertResult.Value.action.ShouldBe(RepositoryActionResult.Updated);
        upsertResult.Value.entity.FirstName.ShouldBe("UpdatedUpsert");

        // Re-fetch from DB to confirm persistence
        var findResult = await Customer.FindOneAsync(insertedId);
        findResult.ShouldBeSuccess();
        findResult.Value.FirstName.ShouldBe("UpdatedUpsert");
        findResult.Value.LastName.ShouldBe("Doe Updated");
        findResult.Value.Email.Value.ShouldBe("updatedupsert.doe@example.com");
        findResult.Value.Title.ShouldBe("Dr.");
    }

    [Fact]
    public async Task UpsertDisconnectedCustomer_ConcurrencyFailure_Test()
    {
        // Arrange: Insert a customer into the DB
        var originalCustomer = new Customer
        {
            FirstName = "OriginalUpsert",
            LastName = "Doe",
            Email = EmailAddressStub.Create("originalupsert.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await originalCustomer.InsertAsync();
        insertResult.ShouldBeSuccess();
        var insertedId = insertResult.Value.Id;
        var originalConcurrencyVersion = insertResult.Value.ConcurrencyVersion; // Capture initial concurrency version

        // Simulate another update to the customer in the DB to cause a concurrency conflict
        var concurrentCustomer = await Customer.FindOneAsync(insertedId);
        concurrentCustomer.ShouldBeSuccess();
        concurrentCustomer.Value.FirstName = "ConcurrentUpdate";
        var concurrentUpdateResult = await concurrentCustomer.Value.UpdateAsync(); // generates new concurrency version
        concurrentUpdateResult.ShouldBeSuccess();

        // Simulate disconnected entity for update with outdated concurrency version
        var disconnectedCustomer = new Customer
        {
            Id = insertedId,
            FirstName = "UpdatedUpsert",
            LastName = "Doe Updated",
            Email = EmailAddressStub.Create("updatedupsert.doe@example.com"),
            Title = "Dr.",
            ConcurrencyVersion = originalConcurrencyVersion // outdated version to trigger concurrency failure
        };
        disconnectedCustomer.RegisterDomainEvent(new CustomerChangedDomainEvent(disconnectedCustomer));

        // Act: Attempt to upsert the disconnected entity (should fail due to concurrency)
        var upsertResult = await disconnectedCustomer.UpsertAsync();

        // Assert: Verify upsert failure due to concurrency
        upsertResult.ShouldBeFailure();
        upsertResult.HasError<ConcurrencyError>().ShouldBeTrue(); // Expecting ConcurrencyError as per design
        upsertResult.Value.action.ShouldBe(RepositoryActionResult.None); // No action performed
        disconnectedCustomer.HasDomainEvents().ShouldBeTrue(); // Events not cleared due to failure

        // Re-fetch from DB to confirm original state (from concurrent update) persists
        var findResult = await Customer.FindOneAsync(insertedId);
        findResult.ShouldBeSuccess();
        findResult.Value.FirstName.ShouldBe("ConcurrentUpdate"); // Reflects the concurrent update
        findResult.Value.LastName.ShouldBe("Doe"); // Unchanged from original
        findResult.Value.Email.Value.ShouldBe("originalupsert.doe@example.com"); // Unchanged from original
        findResult.Value.Title.ShouldBe("Mr."); // Unchanged from original
    }

    [Fact]
    public async Task UpsertMultipleCustomers_Test()
    {
        // Arrange
        var existingCustomer = new Customer
        {
            FirstName = "Existing",
            LastName = "Doe",
            Email = EmailAddressStub.Create("existing.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await existingCustomer.InsertAsync();
        insertResult.ShouldBeSuccess();
        existingCustomer = insertResult.Value;
        var customers = new[]
        {
            new Customer // New customer
            {
                FirstName = "New",
                LastName = "Doe",
                Email = EmailAddressStub.Create("new.doe@example.com"),
                Title = "Ms."
            },
            new Customer // Existing customer
            {
                Id = existingCustomer.Id,
                FirstName = "Existing Updated",
                LastName = "Doe",
                Email = EmailAddressStub.Create("existing.doe@example.com"),
                Title = "Dr.",
                ConcurrencyVersion = existingCustomer.ConcurrencyVersion // for concurrency check pass
            }
        };
        foreach (var customer in customers)
        {
            customer.RegisterDomainEvent(new CustomerChangedDomainEvent(customer));
        }

        // Act
        var results = await Customer.UpsertAsync(customers);

        // Assert
        results.Count().ShouldBe(2);
        results.ShouldAllBe(r => r.IsSuccess);
        var insertResult2 = results.First();
        var updateResult = results.Last();
        insertResult2.Value.action.ShouldBe(RepositoryActionResult.Inserted);
        insertResult2.Value.entity.FirstName.ShouldBe("New");
        updateResult.Value.action.ShouldBe(RepositoryActionResult.Updated);
        updateResult.Value.entity.FirstName.ShouldBe("Existing Updated");
        foreach (var result in results)
        {
            result.Value.entity.HasDomainEvents().ShouldBeFalse(); // Events cleared after publishing
            var findResult = await Customer.FindOneAsync(result.Value.entity.Id);
            findResult.ShouldBeSuccess();
            findResult.Value.FirstName.ShouldBe(result.Value.entity.FirstName);
        }
    }

    [Fact]
    public async Task FindAllCustomersWithFilterAndSpecifications_Test()
    {
        // Arrange: Seed customers
        await this.SeedDataAsync();
        var filter = FilterModelBuilder.For<Customer>()
            .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
            .SetPaging(1, 2).Build();
        var additionalSpec = new Specification<Customer>(c => c.Title == "Mr.");

        // Act: Query with filter and additional specification
        var result = await Customer.FindAllAsync(filter, [additionalSpec]);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldAllBe(c => c.LastName == "Doe" && c.Title == "Mr.");
        result.Value.Count().ShouldBeLessThanOrEqualTo(2); // Respect paging
        result.Value.Count().ShouldBeGreaterThanOrEqualTo(1); // At least one matching customer from SeedDataAsync
    }

    [Fact]
    public async Task ProjectCustomersToDto_Test()
    {
        // Arrange: Seed customers
        await this.SeedDataAsync();
        var options = new FindOptions<Customer> { Take = 2 };

        // Act: Project customers to anonymous type
        var projectResult = await Customer.ProjectAllAsync(
            e => new CustomerDto { Name = $"{e.FirstName} {e.LastName}", Title = e.Title }, options);

        // Assert
        projectResult.ShouldBeSuccess();
        projectResult.Value.Count().ShouldBeLessThanOrEqualTo(2); // Respect take limit
        projectResult.Value.ShouldAllBe(dto => dto.Name != null);

        // Test paged projection
        var pagedProjectResult = await Customer.ProjectAllPagedAsync(
            e => new CustomerDto { Name = $"{e.FirstName} {e.LastName}" }, options);
        pagedProjectResult.ShouldBeSuccess();
        pagedProjectResult.Value.Count().ShouldBeLessThanOrEqualTo(2);
        pagedProjectResult.Value.Count().ShouldBeGreaterThanOrEqualTo(2); // From SeedDataAsync
    }

    [Fact]
    public async Task ProjectCustomersWithFilterModelAndSorting_Test()
    {
        // Arrange: Seed data
        await this.SeedDataAsync();
        var filter = FilterModelBuilder.For<Customer>()
            .AddFilter(c => c.LastName, FilterOperator.Equal, "Doe")
            .AddCustomFilter(FilterCustomType.TextIn)
                .AddParameter("field", "Title")
                .AddParameter("values", "Mr.;Ms.").Done()
            .AddOrdering(c => c.FirstName, OrderDirection.Ascending)
            .SetPaging(1, 2).Build();
        var projection = (Expression<Func<Customer, CustomerDto>>)(c => new CustomerDto
        {
            Name = $"{c.FirstName} {c.LastName}",
            Title = c.Title
        });

        // Act: Project customers with FilterModel to CustomerDto
        var pagedProjectResult = await Customer.ProjectAllPagedAsync(filter, projection);

        // Assert
        pagedProjectResult.ShouldBeSuccess();
        pagedProjectResult.Value.Count().ShouldBeLessThanOrEqualTo(2); // Respect paging
        pagedProjectResult.Value.ShouldAllBe(dto =>
            dto.Name.EndsWith("Doe") &&
            (dto.Title == "Mr." || dto.Title == "Ms."));
        //pagedProjectResult.Value.ShouldBeOrderedBy(dto => dto.Name.Split(' ')[0]); // Sort by FirstName
        pagedProjectResult.Value.ShouldAllBe(dto =>
            !string.IsNullOrEmpty(dto.Name) && !string.IsNullOrEmpty(dto.Title));
    }

    [Fact]
    public async Task ExistsAndCountCustomers_Test()
    {
        // Arrange: Seed customers
        await this.SeedDataAsync();
        var customer = new Customer
        {
            FirstName = "ExistsTest",
            LastName = "Doe",
            Email = EmailAddressStub.Create("existstest.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await customer.InsertAsync();
        insertResult.ShouldBeSuccess();
        var customerId = insertResult.Value.Id;

        // Act & Assert: Test ExistsAsync
        var existsByIdResult = await Customer.ExistsAsync(customerId);
        existsByIdResult.ShouldBeSuccess();
        existsByIdResult.Value.ShouldBeTrue();

        var existsByExpressionResult = await Customer.ExistsAsync(c => c.LastName == "Doe");
        existsByExpressionResult.ShouldBeSuccess();
        existsByExpressionResult.Value.ShouldBeTrue();

        var existsBySpecResult = await Customer.ExistsAsync(
            new Specification<Customer>(c => c.FirstName == "ExistsTest"));
        existsBySpecResult.ShouldBeSuccess();
        existsBySpecResult.Value.ShouldBeTrue();

        var existsByFilterResult = await Customer.ExistsAsync(FilterModelBuilder.For<Customer>()
            .AddFilter(c => c.Email.Value, FilterOperator.Equal, "existstest.doe@example.com").Build());
        existsByFilterResult.ShouldBeSuccess();
        existsByFilterResult.Value.ShouldBeTrue();

        // Act & Assert: Test CountAsync
        var countByExpressionResult = await Customer.CountAsync(c => c.LastName == "Doe");
        countByExpressionResult.ShouldBeSuccess();
        countByExpressionResult.Value.ShouldBeGreaterThanOrEqualTo(2); // From SeedDataAsync + inserted customer

        var countBySpecResult = await Customer.CountAsync(new Specification<Customer>(c => c.Title == "Mr."));
        countBySpecResult.ShouldBeSuccess();
        countBySpecResult.Value.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task UpdateBookAndAuthor_Test()
    {
        var book = new Book
        {
            Title = "1984",
            Isbn = "9780451524935",
            Price = 12.99m,
            YearPublished = 1949,
            Author = new Author
            {
                FirstName = "George",
                LastName = "Orwell",
                Title = "Mr."
            },
            Supplier = new Supplier
            {
                Name = "Penguin Random House",
                Email = "ph@example.com",
                Rating = 3
            },
            Views = 10
        };

        var bookInsertResult = await book.InsertAsync();
        bookInsertResult.ShouldBeSuccess();
        book = bookInsertResult.Value;

        // Update book and author
        book.Title = "Nineteen Eighty-Four";
        book.Views = 20;
        book.Author.FirstName = "George R.";
        book.Author.LastName = "Orwell";
        var updateBookResult = await book.UpdateAsync(); // also updates the referenced author and supplier
        updateBookResult.ShouldBeSuccess();

        var findResult = await Book.FindOneAsync(book.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
        findResult.Value.Id.ShouldBe(book.Id);
        findResult.Value.Title.ShouldBe("Nineteen Eighty-Four");
        findResult.Value.Views.ShouldBe(20);
        findResult.Value.Supplier.ShouldNotBeNull();
        findResult.Value.Author.ShouldNotBeNull();
        findResult.Value.Author.FirstName.ShouldBe("George R.");
    }

    [Fact]
    public async Task DeleteCustomer_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "DeleteMe",
            LastName = "Doe",
            Email = EmailAddressStub.Create("deleteme.doe@example.com"),
            Title = "Mr."
        };
        var insertResult = await customer.InsertAsync();
        insertResult.ShouldBeSuccess();
        customer = insertResult.Value;

        // Act
        var deleteResult = await customer.DeleteAsync();

        // Assert
        deleteResult.ShouldBeSuccess();
        var findResult = await Customer.FindOneAsync(customer.Id);
        findResult.ShouldBeFailure();
        findResult.HasError<NotFoundError>().ShouldBeTrue();
    }

    [Fact]
    public async Task SoftDeleteSupplier_AuditStateBehavior_Test()
    {
        // Arrange: Insert a supplier with audit state behavior (soft delete enabled)
        var supplier = new Supplier
        {
            Name = "SoftDelete Supplier",
            Email = "test@example.com"
        };
        var insertResult = await supplier.InsertAsync();
        insertResult.ShouldBeSuccess();
        supplier = insertResult.Value;

        // Act: Delete the supplier (should soft delete due to AuditStateBehavior)
        var deleteResult = await supplier.DeleteAsync();

        // Assert: Verify soft delete and audit state
        deleteResult.ShouldBeSuccess();
        var findResult = await Supplier.FindOneAsync(supplier.Id);
        findResult.ShouldBeFailure(); // Should not find soft-deleted supplier by default
    }

    [Fact]
    public async Task FindAllCustomers_Test()
    {
        // Arrange
        await this.SeedDataAsync();

        // Act
        var findAllResult = await Customer.FindAllAsync();

        // Assert
        findAllResult.ShouldBeSuccess();
        findAllResult.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FindAllCustomers_WithSpecification_Test()
    {
        // Arrange
        await this.SeedDataAsync();
        var spec = new Specification<Customer>(c => c.LastName == "Doe");

        // Act
        var findAllResult = await Customer.FindAllAsync(spec);

        // Assert
        findAllResult.ShouldBeSuccess();
        findAllResult.Value.ShouldAllBe(c => c.LastName == "Doe");
        findAllResult.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FindAllCustomers_WithGeneratedSpecification_Test()
    {
        // Arrange
        await this.SeedDataAsync();

        // Act
        var findAllResult = await Customer.FindAllAsync(
            Customer.Specifications.LastNameEquals("Doe")); // specification is generated by source generator

        // test some generated specicifications (compile-time check)
        Order.Specifications.StatusEquals(OrderStatus.Pending);
        Customer.Specifications.FirstNameEquals("John");
        Customer.Specifications.FirstNameNotEquals("John");
        Customer.Specifications.LastNameNotEquals("Doe");
        Customer.Specifications.LastVisitedEquals(DateTime.UtcNow);
        Customer.Specifications.LastVisitedNotEquals(DateTime.UtcNow);
        Customer.Specifications.LastVisitedGreaterThan(DateTime.UtcNow);
        Customer.Specifications.LastVisitedGreaterThanOrEquals(DateTime.UtcNow);
        Customer.Specifications.LastVisitedLessThan(DateTime.UtcNow);
        Customer.Specifications.LastVisitedLessThanOrEquals(DateTime.UtcNow);
        Customer.Specifications.VisitsEquals(11);
        Customer.Specifications.VisitsNotEquals(11);
        Customer.Specifications.VisitsGreaterThan(10);
        Customer.Specifications.VisitsGreaterThanOrEquals(10);
        Customer.Specifications.VisitsLessThan(10);
        Customer.Specifications.VisitsLessThanOrEquals(10);
        Customer.Specifications.IsActiveEquals(true);
        Customer.Specifications.IsActiveNotEquals(true);
        Customer.Specifications.EmailEquals(EmailAddressStub.Create("johndoe@example.com"));
        Customer.Specifications.EmailNotEquals(EmailAddressStub.Create("johndoe@example.com"));
        Customer.Specifications.ConcurrencyVersionEquals(Guid.NewGuid());
        Customer.Specifications.ConcurrencyVersionNotEquals(Guid.NewGuid());
        Customer.Specifications.TitleEquals("Mr.");
        Customer.Specifications.TitleNotEquals("Mr.");
        var s1 = Customer.Specifications.AuditStateIsNotNull;
        var s2 = Customer.Specifications.AuditStateIsNull;

        // Assert
        findAllResult.ShouldBeSuccess();
        findAllResult.Value.ShouldAllBe(c => c.LastName == "Doe");
        findAllResult.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FindAllCustomer_CustomQuery_Test()
    {
        // Arrange: seed baseline data (John Doe, Jane Doe, Alice Smith, etc.)
        await this.SeedDataAsync();
        var ct = CancellationToken.None; // For testing optional CancellationToken

        // Act: Use the static finder (FindAllByLastNameAsync)
        var does = await Customer.FindAllByLastNameCustomAsync("Doe", ct);   // inline static
        var johns = await Customer.FindAllByFirstNameCustomAsync("John", ct); // static forwarder, source generated

        // Assert 1: LastName finder
        does.ShouldBeSuccess();
        does.Value.ShouldNotBeEmpty();
        does.Value.ShouldAllBe(c => c.LastName == "Doe");

        // Assert 2: FirstName finder
        johns.ShouldBeSuccess();
        johns.Value.ShouldNotBeEmpty();
        johns.Value.ShouldAllBe(c => c.FirstName == "John");
    }

    [Fact]
    public async Task FindAllCustomer_ConventionFinder_Test()
    {
        // Arrange: seed baseline data (John Doe, Jane Doe, Alice Smith, etc.)
        await this.SeedDataAsync();
        var ct = CancellationToken.None; // For testing optional CancellationToken

        // Act: Use the static finder (FindAllByLastNameAsync)
        var does = await Customer.FindAllByLastNameAsync("Doe", null, ct); // convention finder, generated by ConventionFinderFeature
        var johns = await Customer.FindAllByFirstNameAsync("John", null, ct); // convention finder, generated by ConventionFinderFeature
        var orders1 = await Order.FindAllByStatusAsync(OrderStatus.Shipped);
        var orders2 = await Order.FindAllByCurrencyAsync(Currency.UsDollar);
        var doesOrders = await Order.FindAllByCustomerIdAsync(does.Value.FirstOrDefault().Id);

        // Assert 1: LastName finder
        does.ShouldBeSuccess();
        does.Value.ShouldNotBeEmpty();
        does.Value.ShouldAllBe(c => c.LastName == "Doe");

        // Assert 2: FirstName finder
        johns.ShouldBeSuccess();
        johns.Value.ShouldNotBeEmpty();
        johns.Value.ShouldAllBe(c => c.FirstName == "John");
    }

    [Fact]
    public async Task FindAllCustomers_QueryFeature_Test()
    {
        // Arrange
        await this.SeedDataAsync();
        var ct = CancellationToken.None; // For testing optional CancellationToken

        // Act 1: Basic Where with expression + ToListAsync
        var findAllResult1 = await Customer.Query()
            .Where(c => c.LastName == "Doe")
            .ToListAsync(ct);

        // Act 2: Where with specification + ToListAsync
        var findAllResult2 = await Customer.Query()
            .Where(Customer.Specifications.LastNameEquals("Doe")).ToListAsync(ct);

        // Act 3: Chaining And/Or with specifications
        var chainedResult = await Customer.Query()
            .Where(Customer.Specifications.LastNameEquals("Doe"))
            .And(Customer.Specifications.IsActiveEquals(true))
            .Or(Customer.Specifications.LastNameEquals("Smith")).ToListAsync(ct);

        // Act 4: Where with multiple specifications (params array)
        var multiSpecResult = await Customer.Query()
            .Where(
                Customer.Specifications.LastNameEquals("Doe"),
                Customer.Specifications.IsActiveEquals(true)).ToListAsync(ct);

        // Act 5: FirstOrDefaultAsync (should find one)
        var firstOrDefaultResult = await Customer.Query()
            .Where(c => c.LastName == "Doe")
            .FirstOrDefaultAsync(ct);

        // Act 6: FirstAsync (should find one)
        var firstResult = await Customer.Query()
            .Where(c => c.LastName == "Doe").FirstAsync(ct);

        // Act 7: FirstAsync with no match (should fail with NotFoundError)
        var firstNoMatchResult = await Customer.Query()
            .Where(c => c.LastName == "NonExistent")
            .FirstAsync(ct);

        // Act 8: AnyAsync (should be true)
        var anyResult = await Customer.Query()
            .Where(c => c.LastName == "Doe").AnyAsync(ct);

        // Act 9: CountAsync
        var countResult = await Customer.Query()
            .Where(c => c.LastName == "Doe").CountAsync(ct);

        // Act 10: ProjectAllAsync (simple projection to DTO)
        var projectResult = await Customer.Query()
            .Where(c => c.LastName == "Doe")
            .ProjectAllAsync(c => new CustomerDto { Name = $"{c.FirstName} {c.LastName}" }, ct);

        var projectResult2 = await Customer.Query()
            .Include(c => c.Orders) // include related entities if needed
            .ProjectAllAsync(c => new CustomerTotals { OrderCount = c.Orders.Sum(p => 1) }, ct);

        // Act 11: ProjectAllPagedAsync (paged projection)
        var projectPagedResult = await Customer.Query()
            .Where(c => c.LastName == "Doe")
            .Skip(1)
            .Take(1).ProjectAllPagedAsync(c => new CustomerDto { Name = $"{c.FirstName} {c.LastName}" }, ct);

        // Act 12: Include (eager loading) + ToListAsync, verify Orders are loaded
        var includeResult = await Customer.Query()
            .Where(c => c.LastName == "Doe" && c.Orders.Count > 0)
            .Include(c => c.Orders).ToListAsync(ct);

        // Act 13: OrderBy / OrderByDescending + ToListAsync
        var orderedResult = await Customer.Query()
            .Where(c => c.LastName == "Doe")
            .OrderBy(c => c.FirstName)
            .OrderByDescending(c => c.Visits) // multiple orders
            .ToListAsync(ct);

        // Act 14: Skip / Take + ToPagedListAsync
        var pagedResult = await Customer.Query()
            .Where(c => c.LastName == "Doe" || c.LastName == "Smith")
            .Skip(1)
            .Take(2).ToPagedListAsync(ct);

        // Assert 1: Basic Where + ToListAsync
        findAllResult1.ShouldBeSuccess();
        findAllResult1.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
        findAllResult1.Value.ShouldAllBe(c => c.LastName == "Doe");

        // Assert 2: Where with specification + ToListAsync
        findAllResult2.ShouldBeSuccess();
        findAllResult2.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
        findAllResult2.Value.ShouldAllBe(c => c.LastName == "Doe");

        // Assert 3: Chaining And/Or
        chainedResult.ShouldBeSuccess();
        chainedResult.Value.ShouldAllBe(c => c.LastName == "Doe" || c.LastName == "Smith");

        // Assert 4: Where with multiple specifications
        multiSpecResult.ShouldBeSuccess();
        multiSpecResult.Value.ShouldAllBe(c => c.LastName == "Doe" && c.IsActive);

        // Assert 5: FirstOrDefaultAsync
        firstOrDefaultResult.ShouldBeSuccess();
        firstOrDefaultResult.Value.ShouldNotBeNull();
        firstOrDefaultResult.Value.LastName.ShouldBe("Doe");

        // Assert 6: FirstAsync
        firstResult.ShouldBeSuccess();
        firstResult.Value.ShouldNotBeNull();
        firstResult.Value.LastName.ShouldBe("Doe");

        // Assert 7: FirstAsync with no match
        firstNoMatchResult.ShouldBeFailure();
        firstNoMatchResult.HasError<NotFoundError>().ShouldBeTrue();

        // Assert 8: AnyAsync
        anyResult.ShouldBeSuccess();
        anyResult.Value.ShouldBeTrue();

        // Assert 9: CountAsync
        countResult.ShouldBeSuccess();
        countResult.Value.ShouldBeGreaterThanOrEqualTo(2);

        // Assert 10: ProjectAllAsync
        projectResult.ShouldBeSuccess();
        projectResult.Value.ShouldNotBeEmpty();
        projectResult.Value.ShouldAllBe(dto => dto.Name.EndsWith("Doe"));

        //projectResult2.ShouldBeSuccess();
        //projectResult2.Value.ShouldNotBeEmpty();
        //projectResult2.Value.ShouldAllBe(dto => dto.Name.EndsWith("Doe"));

        // Assert 11: ProjectAllPagedAsync
        projectPagedResult.ShouldBeSuccess();
        projectPagedResult.Value.Count().ShouldBe(1);
        projectPagedResult.Value.ShouldAllBe(dto => dto.Name.EndsWith("Doe"));

        // Assert 12: Include + ToListAsync (verify loading)
        includeResult.ShouldBeSuccess();
        includeResult.Value.ShouldNotBeEmpty();
        includeResult.Value.ShouldAllBe(c => c.Orders.SafeAny());

        // Assert 13: OrderBy / OrderByDescending
        orderedResult.ShouldBeSuccess();
        //orderedResult.Value.ShouldBeInOrder(SortDirection.Ascending, c => c.FirstName); // Verify ascending FirstName

        // Assert 14: Skip / Take + ToPagedListAsync
        pagedResult.ShouldBeSuccess();
        pagedResult.Value.Count().ShouldBe(2);
    }

    [Fact]
    public async Task InsertOrder_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = EmailAddressStub.Create("john.doe@example.com"),
            Title = "Mr."
        };
        var customerInsertResult = await customer.InsertAsync();
        customerInsertResult.ShouldBeSuccess();
        customer = customerInsertResult.Value;

        var book1 = new Book
        {
            Title = "The Great Gatsby",
            Isbn = "9780743273565",
            Price = 10.99m,
            YearPublished = 1925
        };
        var bookInsertResult1 = await book1.InsertAsync();
        bookInsertResult1.ShouldBeSuccess();
        book1 = bookInsertResult1.Value;

        var book2 = new Book
        {
            Title = "1984",
            Isbn = "9780451524935",
            Price = 12.99m,
            YearPublished = 1949
        };
        var bookInsertResult2 = await book2.InsertAsync();
        bookInsertResult2.ShouldBeSuccess();
        book2 = bookInsertResult2.Value;

        var order = new Order
        {
            CustomerId = customer.Id,
            DateSubmitted = DateTimeOffset.UtcNow,
            Status = OrderStatus.New,
            Subtotal = 23.98m,
            Shipping = 5.00m,
            Tax = 2.00m,
            OrderBooks =
            [
                new OrderBook { BookId = book1.Id, Quantity = 1, UnitPrice = book1.Price },
            new OrderBook { BookId = book2.Id, Quantity = 2, UnitPrice = book2.Price }
            ]
        };

        // Act
        var insertResult = await order.InsertAsync();

        // Assert
        insertResult.ShouldBeSuccess();
        insertResult.Value.ShouldNotBeNull();
        insertResult.Value.Id.ShouldBe(order.Id);

        var findResult = await Order.FindOneAsync(order.Id);

        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
        findResult.Value.OrderBooks.Count.ShouldBe(2);
        findResult.Value.OrderBooks.ShouldContain(ob => ob.Book.Title == "The Great Gatsby" && ob.Quantity == 1);
        findResult.Value.OrderBooks.ShouldContain(ob => ob.Book.Title == "1984" && ob.Quantity == 2);
    }

    [Fact]
    public async Task UpdateOrderCustomer_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = EmailAddressStub.Create("jane.doe@example.com"),
            Title = "Ms."
        };
        var customerInsertResult = await customer.InsertAsync();
        customerInsertResult.ShouldBeSuccess();
        customer = customerInsertResult.Value;

        var book1 = new Book
        {
            Title = "To Kill a Mockingbird",
            Isbn = "9780446310789",
            Price = 14.99m,
            YearPublished = 1960
        };
        var book1InsertResult = await book1.InsertAsync();
        book1InsertResult.ShouldBeSuccess();
        book1 = book1InsertResult.Value;

        var book2 = new Book
        {
            Title = "Animal Farm",
            Isbn = "9780446310112",
            Price = 12.99m,
            YearPublished = 1945
        };
        var book2InsertResult = await book2.InsertAsync();
        book2InsertResult.ShouldBeSuccess();
        book2 = book2InsertResult.Value;

        var order = new Order
        {
            //CustomerId = customer.Id, // or set Customer = customer,
            Customer = customer,
            DateSubmitted = DateTimeOffset.UtcNow,
            Status = OrderStatus.New,
            Subtotal = 14.99m + (12.99m * 2),
            Shipping = 4.00m,
            Tax = 2.50m,
            OrderBooks =
            [
                new OrderBook { BookId = book1.Id, Quantity = 1, UnitPrice = book1.Price },
                new OrderBook { BookId = book2.Id, Quantity = 2, UnitPrice = book2.Price }
            ]
        };
        var insertResult = await order.InsertAsync();
        insertResult.ShouldBeSuccess();
        order = (await Order.FindOneAsync(order.Id)).Value; // ensure we have the latest state from the database

        // Update order + orderbook + customer
        order.Status = OrderStatus.Processing;
        order.Subtotal = (12.99m * 3) + (12.99m * 2);
        order.OrderBooks.First().Quantity = 3;
        order.OrderBooks.First().UnitPrice = 12.99m;
        order.Customer.FirstName = "Johnny";

        // Act
        var updateResult = await order.UpdateAsync();

        // Assert
        updateResult.ShouldBeSuccess();
        var findResult = await Order.FindOneAsync(order.Id); // re-fetch to ensure we have the updated state

        findResult.ShouldBeSuccess();
        findResult.Value.Status.ShouldBe(OrderStatus.Processing);
        findResult.Value.Subtotal.ShouldBe((12.99m * 3) + (12.99m * 2));
        findResult.Value.OrderBooks.Count.ShouldBe(2);
        findResult.Value.OrderBooks.Sum(ob => ob.Quantity).ShouldBe(5);
        findResult.Value.OrderBooks.First().Quantity.ShouldBe(3);
        findResult.Value.OrderBooks.First().UnitPrice.ShouldBe(12.99m);
        findResult.Value.Customer.FirstName.ShouldBe("Johnny");
    }

    [Fact]
    public async Task UpdateOrder_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = EmailAddressStub.Create("jane.doe@example.com"),
            Title = "Ms."
        };
        var customerInsertResult = await customer.InsertAsync();
        customerInsertResult.ShouldBeSuccess();
        customer = customerInsertResult.Value;

        var book1 = new Book
        {
            Title = "To Kill a Mockingbird",
            Isbn = "9780446310789",
            Price = 14.99m,
            YearPublished = 1960
        };
        var book1InsertResult = await book1.InsertAsync();
        book1InsertResult.ShouldBeSuccess();
        book1 = book1InsertResult.Value;

        var book2 = new Book
        {
            Title = "Animal Farm",
            Isbn = "9780446310112",
            Price = 12.99m,
            YearPublished = 1945
        };
        var book2InsertResult = await book2.InsertAsync();
        book2InsertResult.ShouldBeSuccess();
        book2 = book2InsertResult.Value;

        var order = new Order
        {
            //CustomerId = customer.Id, // or set Customer = customer,
            Customer = customer,
            DateSubmitted = DateTimeOffset.UtcNow,
            Status = OrderStatus.New,
            Subtotal = 14.99m + (12.99m * 2),
            Shipping = 4.00m,
            Tax = 2.50m,
            OrderBooks =
            [
                new OrderBook { BookId = book1.Id, Quantity = 1, UnitPrice = book1.Price },
                new OrderBook { BookId = book2.Id, Quantity = 2, UnitPrice = book2.Price }
            ]
        };
        var insertResult = await order.InsertAsync();
        insertResult.ShouldBeSuccess();
        order = (await Order.FindOneAsync(order.Id)).Value; // ensure we have the latest state from the database

        // Update order + orderbook + customer
        order.Status = OrderStatus.Processing;
        order.Subtotal = (12.99m * 3) + (12.99m * 2);
        order.OrderBooks.First().Quantity = 3;
        order.OrderBooks.First().UnitPrice = 12.99m;
        order.Customer.FirstName = "Johnny";

        // Act
        var updateResult = await order.UpdateAsync();

        // Assert
        updateResult.ShouldBeSuccess();
        var findResult = await Order.FindOneAsync(order.Id); // re-fetch to ensure we have the updated state

        findResult.ShouldBeSuccess();
        findResult.Value.Status.ShouldBe(OrderStatus.Processing);
        findResult.Value.Subtotal.ShouldBe((12.99m * 3) + (12.99m * 2));
        findResult.Value.OrderBooks.Count.ShouldBe(2);
        findResult.Value.OrderBooks.Sum(ob => ob.Quantity).ShouldBe(5);
        findResult.Value.OrderBooks.First().Quantity.ShouldBe(3);
        findResult.Value.OrderBooks.First().UnitPrice.ShouldBe(12.99m);
        findResult.Value.Customer.FirstName.ShouldBe("Johnny");

        // ---- second update to ensure collection merge strategy works correctly ----
        // Update order + orderbook + customer
        order = (await Order.FindOneAsync(order.Id)).Value; // ensure we have the latest state from the database
        order.Status = OrderStatus.Shipped;
        order.Subtotal = (12.99m * 3);
        order.OrderBooks.Remove(order.OrderBooks.Last());
        updateResult = await order.UpdateAsync();

        // Assert
        updateResult.ShouldBeSuccess();
        findResult = await Order.FindOneAsync(order.Id); // re-fetch to ensure we have the updated state

        findResult.ShouldBeSuccess();
        findResult.Value.Status.ShouldBe(OrderStatus.Shipped);
        findResult.Value.Subtotal.ShouldBe((12.99m * 3));
        findResult.Value.OrderBooks.Count.ShouldBe(1);
        findResult.Value.OrderBooks.Sum(ob => ob.Quantity).ShouldBe(3);
    }

    [Fact]
    public async Task RemoveBookFromOrder_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "Remove",
            LastName = "Book",
            Email = EmailAddressStub.Create("remove.book@example.com"),
            Title = "Mr."
        };
        var customerInsertResult = await customer.InsertAsync();
        customerInsertResult.ShouldBeSuccess();
        customer = customerInsertResult.Value;

        var book1 = new Book
        {
            Title = "Book One",
            Isbn = "1111111111111",
            Price = 9.99m,
            YearPublished = 2000
        };
        var book2 = new Book
        {
            Title = "Book Two",
            Isbn = "2222222222222",
            Price = 19.99m,
            YearPublished = 2001
        };

        var bookInsertResult1 = await book1.InsertAsync();
        var bookInsertResult2 = await book2.InsertAsync();
        bookInsertResult1.ShouldBeSuccess();
        bookInsertResult2.ShouldBeSuccess();
        book1 = bookInsertResult1.Value;
        book2 = bookInsertResult2.Value;

        var order = new Order
        {
            CustomerId = customer.Id,
            DateSubmitted = DateTimeOffset.UtcNow,
            Status = OrderStatus.New,
            Subtotal = 29.98m,
            Shipping = 5.00m,
            Tax = 2.00m,
            OrderBooks =
            [
                new OrderBook { BookId = book1.Id, Quantity = 1, UnitPrice = book1.Price },
                new OrderBook { BookId = book2.Id, Quantity = 1, UnitPrice = book2.Price }
            ]
        };

        var insertResult = await order.InsertAsync();
        insertResult.ShouldBeSuccess();
        order = insertResult.Value; // important to continue with the inserted order instance, with the fresh db state

        // Act: remove book2 from the order
        var orderBookToRemove = order.OrderBooks.First(ob => ob.BookId == book2.Id);
        order.OrderBooks.Remove(orderBookToRemove);

        var updateResult = await order.UpdateAsync(); // uses OrderUpdateStrategy to detect removed books in a disconnected entity state

        // Assert
        updateResult.ShouldBeSuccess();

        var findResult = await Order.FindOneAsync(order.Id);

        findResult.ShouldBeSuccess();
        findResult.Value.OrderBooks.Count.ShouldBe(1);
        findResult.Value.OrderBooks.First().Book.Title.ShouldBe("Book One");
    }

    [Fact]
    public async Task DeleteOrder_Test()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "DeleteMe",
            LastName = "Doe",
            Email = EmailAddressStub.Create("deleteme.doe@example.com"),
            Title = "Mr."
        };
        var customerInsertResult = await customer.InsertAsync();
        customerInsertResult.ShouldBeSuccess();
        customer = customerInsertResult.Value;

        var book = new Book
        {
            Title = "Brave New World",
            Isbn = "9780060850524",
            Price = 11.99m,
            YearPublished = 1932
        };
        var bookInsertResult = await book.InsertAsync();
        bookInsertResult.ShouldBeSuccess();
        book = bookInsertResult.Value;

        var order = new Order
        {
            CustomerId = customer.Id,
            DateSubmitted = DateTimeOffset.UtcNow,
            Status = OrderStatus.Pending,
            Subtotal = 11.99m,
            Shipping = 3.00m,
            Tax = 1.00m,
            OrderBooks =
            [
                new OrderBook { BookId = book.Id, Quantity = 1, UnitPrice = book.Price }
            ]
        };
        var insertResult = await order.InsertAsync();
        insertResult.ShouldBeSuccess();
        order = insertResult.Value;

        // Act
        var deleteResult = await order.DeleteAsync();

        // Assert
        deleteResult.ShouldBeSuccess();
        var findResult = await Order.FindOneAsync(order.Id);
        findResult.ShouldBeFailure();
    }

    [Fact]
    public async Task FindAllOrders_Test()
    {
        // Arrange
        await this.SeedDataAsync();

        // Act
        var findAllResult = await Order.FindAllAsync();

        // Assert
        findAllResult.ShouldBeSuccess();
        findAllResult.Value.Count().ShouldBeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task FindAllOrdersFor_Test() // code generation tests (forwarders)
    {
        // Arrange
        await this.SeedDataAsync();
        var customersResult = await Customer.FindAllAsync();
        var customer = customersResult.Value.Last();

        // Act
        var findAllResult1 = await Order.FindAllForAsync(customer);
        var findAllResult2 = await Order.FindAllForAsync(OrderStatus.Shipped);

        // Assert
        findAllResult1.ShouldBeSuccess();
        findAllResult1.Value.Count().ShouldBeGreaterThanOrEqualTo(1);
        findAllResult2.ShouldBeSuccess();
        findAllResult2.Value.Count().ShouldBeGreaterThanOrEqualTo(1);
        findAllResult2.Value.ShouldAllBe(o => o.Status == OrderStatus.Shipped);
    }

    [Fact]
    public async Task FindAllOrders_WithSpecification_Test()
    {
        // Arrange
        await this.SeedDataAsync();
        var spec = new Specification<Order>(o => o.Status == OrderStatus.Shipped);

        // Act
        var findAllResult = await Order.FindAllAsync(spec);

        // Assert
        findAllResult.ShouldBeSuccess();
        findAllResult.Value.ShouldAllBe(o => o.Status == OrderStatus.Shipped);
        findAllResult.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FindAllOrders_WithGeneratedSpecification_Test()
    {
        // Arrange
        await this.SeedDataAsync();

        // Act
        var findAllResult = await Order.FindAllAsync(
            Order.Specifications.StatusEquals(OrderStatus.Shipped)); // this specification is generated via source generator

        // Assert
        findAllResult.ShouldBeSuccess();
        findAllResult.Value.ShouldAllBe(o => o.Status == OrderStatus.Shipped);
        findAllResult.Value.Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task InsertSupplier_Validator_Test()
    {
        // Arrange
        var sut = new Supplier
        {
            Name = "Penguin Random House",
            Email = "test@supplier.com",
            Rating = 5
        };

        // Act
        var insertResult = await sut.InsertAsync();

        // Assert
        insertResult.ShouldBeSuccess();
        insertResult.Value.ShouldNotBeNull();
        insertResult.Value.Id.ShouldBe(sut.Id);
        var findResult = await Supplier.FindOneAsync(sut.Id);
        findResult.ShouldBeSuccess();
        findResult.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task InsertSupplier_ValidatorInvalid_Test()
    {
        // Arrange
        var sut = new Supplier
        {
            Name = new string('A', 101), // Exceeds max length
            Email = "invalid-email", // Invalid format
            Rating = 6, // Out of range
        };

        // Act
        var insertResult = await sut.InsertAsync();

        // Assert
        insertResult.ShouldBeFailure();
        var findResult = await Supplier.FindOneAsync(sut.Id);
        findResult.ShouldBeFailure();
    }

    private async Task SeedDataAsync()
    {
        // Seed customers
        var customer1 = new Customer { FirstName = "John", LastName = "Doe", Email = EmailAddressStub.Create("john.doe@example.com"), Title = "Mr." };
        var customer2 = new Customer { FirstName = "Jane", LastName = "Doe", Email = EmailAddressStub.Create("jane.doe@example.com"), Title = "Ms." };
        var customer3 = new Customer { FirstName = "Alice", LastName = "Smith", Email = EmailAddressStub.Create("alice.smith@example.com"), Title = "Ms." };
        var customer1Id = (await customer1.InsertAsync()).Value.Id;
        var customer2Id = (await customer2.InsertAsync()).Value.Id;
        var customer3Id = (await customer3.InsertAsync()).Value.Id;

        // Seed authors (4 total, some with multiple books)
        var author1Id = (await new Author { FirstName = "F. Scott", LastName = "Fitzgerald", Title = "Mr." }.InsertAsync()).Value.Id;
        var author2Id = (await new Author { FirstName = "George", LastName = "Orwell", Title = "Mr." }.InsertAsync()).Value.Id;
        var author3Id = (await new Author { FirstName = "Harper", LastName = "Lee", Title = "Ms." }.InsertAsync()).Value.Id;
        var author4Id = (await new Author { FirstName = "Aldous", LastName = "Huxley", Title = "Mr." }.InsertAsync()).Value.Id;

        // Seed suppliers (3 total)
        var supplier1Id = (await new Supplier { Name = "Penguin Random House", Email = "ph@example.com", Rating = 5 }.InsertAsync()).Value.Id;
        var supplier2Id = (await new Supplier { Name = "HarperCollins", Email = "hc@example.com", Rating = 4 }.InsertAsync()).Value.Id;
        var supplier3Id = (await new Supplier { Name = "Simon & Schuster", Email = "ss@example.com", Rating = 3 }.InsertAsync()).Value.Id;

        // Seed books (8 total, authors with multiple books)
        var books = new List<Book>
        {
            new() { Title = "The Great Gatsby", Isbn = "9780743273565", Price = 10.99m, YearPublished = 1925, AuthorId = author1Id, SupplierId = supplier1Id },
            new() { Title = "Tender Is the Night", Isbn = "9780684801544", Price = 13.99m, YearPublished = 1934, AuthorId = author1Id, SupplierId = supplier1Id }, // Multiple for Fitzgerald
            new() { Title = "1984", Isbn = "9780451524935", Price = 12.99m, YearPublished = 1949, AuthorId = author2Id, SupplierId = supplier2Id },
            new() { Title = "Animal Farm", Isbn = "9780451526342", Price = 9.99m, YearPublished = 1945, AuthorId = author2Id, SupplierId = supplier2Id }, // Multiple for Orwell
            new() { Title = "To Kill a Mockingbird", Isbn = "9780446310789", Price = 14.99m, YearPublished = 1960, AuthorId = author3Id, SupplierId = supplier1Id },
            new() { Title = "Go Set a Watchman", Isbn = "9780062409850", Price = 15.99m, YearPublished = 2015, AuthorId = author3Id, SupplierId = supplier3Id }, // Multiple for Lee
            new() { Title = "Brave New World", Isbn = "9780060850524", Price = 11.99m, YearPublished = 1932, AuthorId = author4Id, SupplierId = supplier2Id },
            new() { Title = "Island", Isbn = "9780061561795", Price = 12.99m, YearPublished = 1962, AuthorId = author4Id, SupplierId = supplier3Id } // Multiple for Huxley
        };
        var bookIds = new List<Guid>();
        foreach (var book in books)
        {
            var result = await book.InsertAsync();
            result.ShouldBeSuccess();
            bookIds.Add(result.Value.Id);
        }

        // Seed orders with OrderBooks

        foreach (var config in new[]
        {
            new { CustomerId = customer1Id, BookIndexes = new[] { 0, 1, 2 }, Subtotal = 10.99m + 13.99m + 14.99m, Status = OrderStatus.New, DaysOffset = -2 },
            new { CustomerId = customer1Id, BookIndexes = new[] { 3, 4 }, Subtotal = 12.99m + 9.99m, Status = OrderStatus.Shipped, DaysOffset = -1 },
            new { CustomerId = customer1Id, BookIndexes = new[] { 5, 6 }, Subtotal = 15.99m + 11.99m, Status = OrderStatus.Pending, DaysOffset = 0 },
            new { CustomerId = customer2Id, BookIndexes = new[] { 1, 2, 3 }, Subtotal = 13.99m + 14.99m + 12.99m, Status = OrderStatus.New, DaysOffset = -2 },
            new { CustomerId = customer2Id, BookIndexes = new[] { 4, 5 }, Subtotal = 9.99m + 15.99m, Status = OrderStatus.Shipped, DaysOffset = -1 },
            new { CustomerId = customer2Id, BookIndexes = new[] { 6, 7 }, Subtotal = 11.99m + 12.99m, Status = OrderStatus.Processing, DaysOffset = 0 },
            new { CustomerId = customer3Id, BookIndexes = new[] { 0, 7 }, Subtotal = 10.99m + 12.99m, Status = OrderStatus.New, DaysOffset = -3 },
            new { CustomerId = customer3Id, BookIndexes = new[] { 2, 4 }, Subtotal = 14.99m + 9.99m, Status = OrderStatus.Submitted, DaysOffset = -2 }
        })
        {
            var orderBooks = new List<OrderBook>();
            foreach (var bookIndex in config.BookIndexes)
            {
                var bookResult = await Book.FindOneAsync(bookIds[bookIndex]);
                bookResult.ShouldBeSuccess();
                var book = bookResult.Value;
                orderBooks.Add(new OrderBook { BookId = book.Id, Quantity = bookIndex % 2 + 1, UnitPrice = book.Price }); // Vary quantity
            }

            var order = new Order
            {
                CustomerId = config.CustomerId,
                DateSubmitted = DateTimeOffset.UtcNow.AddDays(config.DaysOffset),
                Status = config.Status,
                Subtotal = config.Subtotal,
                Shipping = config.BookIndexes.Length > 1 ? 5.00m : 4.00m,
                Tax = config.Subtotal * 0.08m,
                OrderBooks = orderBooks
            };

            var orderInsertResult = await order.InsertAsync();
            orderInsertResult.ShouldBeSuccess();
        }

        // Seed reviews
        foreach (var config in new[]
        {
            new { CustomerId = customer1Id, BookId = bookIds[0], Title = "Great Classic", Body = "A timeless masterpiece of American literature.", Rating = 5, State = 1 },
            new { CustomerId = customer1Id, BookId = bookIds[1], Title = "Thought Provoking", Body = "Orwell's vision is more relevant than ever.", Rating = 5, State = 1 },
            new { CustomerId = customer2Id, BookId = bookIds[2], Title = "Important Read", Body = "A powerful story about justice and morality.", Rating = 4, State = 1 },
            new { CustomerId = customer2Id, BookId = bookIds[3], Title = "Dystopian Vision", Body = "Huxley's world is both fascinating and terrifying.", Rating = 4, State = 1 },
            new { CustomerId = customer1Id, BookId = bookIds[4], Title = "Must Read", Body = "Everyone should read this book at least once.", Rating = 5, State = 1 },
            new { CustomerId = customer2Id, BookId = bookIds[5], Title = "Beautiful Prose", Body = "Fitzgerald's writing is simply beautiful.", Rating = 4, State = 1 },
            new { CustomerId = customer3Id, BookId = bookIds[6], Title = "Classic Dystopia", Body = "A must-read for fans of the genre.", Rating = 5, State = 1 },
            new { CustomerId = customer3Id, BookId = bookIds[7], Title = "Philosophical Gem", Body = "Deep and thought-provoking.", Rating = 4, State = 1 }
        })
        {
            var review = new Review
            {
                CustomerId = config.CustomerId,
                BookId = config.BookId,
                Title = config.Title,
                Body = config.Body,
                Rating = config.Rating,
                State = config.State
            };

            var reviewInsertResult = await review.InsertAsync();
            reviewInsertResult.ShouldBeSuccess();
        }
    }

    public static async Task<Order> OrderMergeStrategy(DbContext context, Order order, CancellationToken cancellationToken)
    {
        var existingOrder = await context.Set<Order>()
            .Include(o => o.OrderBooks)
            .FirstOrDefaultAsync(o => o.Id == order.Id, cancellationToken);

        if (existingOrder == null)
        {
            return order; // fallback to normal insert
        }

        // Update scalar properties
        context.Entry(existingOrder).CurrentValues.SetValues(order);

        // Reconcile Customer reference
        if (order.Customer != null)
        {
            if (existingOrder.Customer != null && existingOrder.Customer.Id == order.Customer.Id)
            {
                // Update scalar properties
                context.Entry(existingOrder.Customer).CurrentValues.SetValues(order.Customer);
            }
            else
            {
                // Replace with new Customer (update FK and attach)
                existingOrder.Customer = order.Customer;
                existingOrder.CustomerId = order.Customer.Id;
                context.Attach(order.Customer); // Ensure new Customer is tracked
            }
        }

        // Reconcile OrderBooks references
        foreach (var existingOrderBook in existingOrder.OrderBooks.ToList())
        {
            if (!order.OrderBooks.Any(ob => ob.BookId == existingOrderBook.BookId))
            {
                context.Remove(existingOrderBook); // mark as deleted
            }
        }

        foreach (var incomingOrderBook in order.OrderBooks)
        {
            var existingOrderBook = existingOrder.OrderBooks.FirstOrDefault(ob => ob.BookId == incomingOrderBook.BookId);
            if (existingOrderBook == null)
            {
                existingOrder.OrderBooks.Add(incomingOrderBook); // new relation
            }
            else
            {
                // Update scalar properties
                context.Entry(existingOrderBook).CurrentValues.SetValues(incomingOrderBook); // update quantity/unitprice
            }
        }

        return existingOrder;
    }
}

public class CustomerCreatedDomainEvent(Customer customer) : DomainEventBase
{
    public CustomerId CustomerId { get; } = customer.Id;
}

public class CustomerChangedDomainEvent(Customer customer) : ChangedDomainEventBase<Customer>()
{
    public CustomerId CustomerId { get; } = customer.Id;
}

public class CustomerCreatedHandler(ILogger logger) : NotificationHandlerBase<CustomerCreatedDomainEvent>
{
    protected override async Task<Result> HandleAsync(CustomerCreatedDomainEvent notification, PublishOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Delay longer than the timeout
        logger.LogInformation($"CUSTOMER Created ================ {notification.CustomerId}");
        return Result.Success();
    }
}

public class CustomerChangedHandler(ILogger logger) : NotificationHandlerBase<CustomerChangedDomainEvent>
{
    protected override async Task<Result> HandleAsync(CustomerChangedDomainEvent notification, PublishOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Delay longer than the timeout
        logger.LogInformation($"CUSTOMER Changed ================ {notification.CustomerId}");
        return Result.Success();
    }
}

public abstract class ChangedDomainEventBase<TEntity>() : DomainEventBase
    where TEntity : IEntity
{
    /// <summary>
    /// Gets the list of property changes (batched).
    /// </summary>
    public List<PropertyChange> Changes { get; } = [];

    /// <summary>
    /// Adds or updates a property change. If the property already exists, overwrites with the latest values.
    /// </summary>
    public void AddOrUpdateChange(string propertyName, object oldValue, object newValue)
    {
        var existing = this.Changes.FirstOrDefault(c => c.PropertyName == propertyName);
        if (existing != null)
        {
            var updated = existing with { OldValue = existing.OldValue ?? oldValue, NewValue = newValue };
            var index = this.Changes.IndexOf(existing);
            this.Changes[index] = updated;
        }
        else
        {
            this.Changes.Add(new PropertyChange(propertyName, oldValue, newValue));
        }
    }

    public record PropertyChange(string PropertyName, object OldValue, object NewValue);
}

//public static class CustomerExtensions
//{
//    public static Task<Result<IEnumerable<Customer>>> FindAllByFirstNameAsync(
//        this ActiveEntity<Customer, CustomerId> _,
//        string name)
//    {
//        return Customer.WithProviderAsync(async (provider) =>
//            await provider.FindAllAsync(new Specification<Customer>(c => c.FirstName == name)));
//    }

//    public static Task<Result<IEnumerable<Customer>>> FindAllByFirstNameAsync(string name)
//    {
//        return Customer.WithProviderAsync(provider =>
//            provider.FindAllAsync(new Specification<Customer>(c => c.FirstName == name)));
//    }
//}

//public static class CustomerQueryExtensions
//{
//    public static Task<Result<IEnumerable<Customer>>> FindAllByFirstNameAsync(
//        this ActiveEntity<Customer, CustomerId> _, string name)
//    {
//        return Customer.WithProviderAsync(provider =>
//            provider.FindAllAsync(new Specification<Customer>(c => c.FirstName == name)));
//    }
//}