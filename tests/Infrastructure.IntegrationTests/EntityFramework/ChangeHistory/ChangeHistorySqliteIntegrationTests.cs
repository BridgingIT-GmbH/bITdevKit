// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.ChangeHistory;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

[IntegrationTest("Infrastructure")]
public class ChangeHistorySqliteIntegrationTests
{
    [Fact]
    public async Task UpdateAsync_WhenEntitySaveFails_DoesNotCommitEntityOrHistoryRows()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required));
        database.Context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "first", UniqueCode = "A" },
            new Customer { Id = Guid.NewGuid(), Name = "second", UniqueCode = "B" });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var entity = await database.Context.Customers.AsNoTracking().SingleAsync(e => e.UniqueCode == "A");
        entity.Name = "changed";
        entity.UniqueCode = "B";
        var repository = CreateChangeHistoryRepository(database.Context, database.Options);

        await Should.ThrowAsync<DbUpdateException>(() => repository.UpdateAsync(entity));
        database.Context.ChangeTracker.Clear();

        var unchanged = await database.Context.Customers.AsNoTracking().SingleAsync(e => e.Id == entity.Id);
        unchanged.Name.ShouldBe("first");
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ChangeHistoryEntry_WithSqlite_CreatesTableColumnsAndIndexes()
    {
        await using var database = await CreateDatabaseAsync();

        var tables = await database.Context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name = '__ChangeHistory_Entries'").ToListAsync();
        var columns = await database.Context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('__ChangeHistory_Entries')").ToListAsync();
        var indexes = await database.Context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = '__ChangeHistory_Entries'").ToListAsync();

        tables.ShouldContain("__ChangeHistory_Entries");
        columns.ShouldContain(nameof(ChangeHistoryEntry.BulkOperationId));
        columns.ShouldContain(nameof(ChangeHistoryEntry.AffectedEntityCount));
        columns.ShouldContain(nameof(ChangeHistoryEntry.Properties));
        columns.ShouldContain(nameof(ChangeHistoryEntry.ActivityParentId));
        indexes.Count.ShouldBeGreaterThanOrEqualTo(7);
    }

    [Theory]
    [InlineData("/_bdk/api/change-history", "get")]
    [InlineData("/_bdk/api/change-history/00000000-0000-0000-0000-000000000001/change-sets/00000000-0000-0000-0000-000000000002/restore", "post")]
    public async Task Endpoints_WithAuthorizationPolicyAndUnauthorizedUser_DenyRequest(string path, string method)
    {
        await using var database = await CreateDatabaseAsync(options => options
            .UseReadAuthorizationPolicy("History.Read")
            .UseRestoreAuthorizationPolicy("History.Restore")
            .Track<Customer>());
        await using var application = await CreateApplicationAsync(database, requireAllowedClaim: true);
        var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "deny");

        var response = method == "post"
            ? await client.PostAsJsonAsync(path, new ChangeHistoryRestoreRequestModel())
            : await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreEndpoint_WithValidRequest_InvokesRestoreAndPersistsRows()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .UseRestoreAuthorizationPolicy("History.Restore")
            .Track<Customer>()
                .AllowRestore(e => e.Name)
                .UseValidatedSetter());
        var customer = new Customer { Id = Guid.NewGuid(), Name = "new", UniqueCode = "RESTORE" };
        var changeSetId = Guid.NewGuid();
        database.Context.Customers.Add(customer);
        database.Context.Set<ChangeHistoryEntry>().Add(CreateScalarHistory(customer, changeSetId, "old", "new"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        await using var application = await CreateApplicationAsync(database, requireAllowedClaim: true);
        var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "allow");

        var response = await client.PostAsJsonAsync($"/_bdk/api/change-history/{customer.Id}/change-sets/{changeSetId}/restore", new ChangeHistoryRestoreRequestModel { Reason = "undo" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        database.Context.ChangeTracker.Clear();
        var restored = await database.Context.Customers.SingleAsync(e => e.Id == customer.Id);
        restored.Name.ShouldBe("old");
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString())).ShouldBe(1);
    }

    [Fact]
    public async Task QueryService_WithSqlite_GroupsRowsByChangeSet()
    {
        await using var database = await CreateDatabaseAsync();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "new", UniqueCode = "QUERY" };
        var changeSetId = Guid.NewGuid();
        database.Context.Set<ChangeHistoryEntry>().AddRange(
            CreateScalarHistory(customer, changeSetId, "old", "new"),
            CreateHistory(customer, changeSetId, nameof(Customer.UniqueCode), "A", "B"));
        await database.Context.SaveChangesAsync();

        var result = await new ChangeHistoryQueryService<ChangeHistoryTestDbContext>(database.Context).FindAllChangeSetsAsync(new ChangeHistoryFindAllQuery
        {
            EntityType = nameof(Customer),
            EntityId = customer.Id.ToString()
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Value.Single().Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task QueryService_WithSqlite_PagesGroupedChangeSetKeysBeforeLoadingRows()
    {
        await using var database = await CreateDatabaseAsync();
        var customer = new Customer { Id = Guid.NewGuid(), Name = "new", UniqueCode = "PAGE" };
        var firstChangeSetId = Guid.NewGuid();
        var secondChangeSetId = Guid.NewGuid();
        var thirdChangeSetId = Guid.NewGuid();
        database.Context.Customers.Add(customer);
        database.Context.Set<ChangeHistoryEntry>().AddRange(
            CreateScalarHistory(customer, firstChangeSetId, "one", "two", DateTimeOffset.UtcNow.AddMinutes(-3)),
            CreateHistory(customer, firstChangeSetId, nameof(Customer.UniqueCode), "A", "B", DateTimeOffset.UtcNow.AddMinutes(-3)),
            CreateScalarHistory(customer, secondChangeSetId, "two", "three", DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateScalarHistory(customer, thirdChangeSetId, "three", "four", DateTimeOffset.UtcNow.AddMinutes(-1)));
        await database.Context.SaveChangesAsync();

        var result = await new ChangeHistoryQueryService<ChangeHistoryTestDbContext>(database.Context).FindAllChangeSetsAsync(new ChangeHistoryFindAllQuery
        {
            EntityType = nameof(Customer),
            EntityId = customer.Id.ToString(),
            OrderAscending = true,
            Page = 2,
            PageSize = 1
        });

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        var changeSet = result.Value.Single();
        changeSet.ChangeSetId.ShouldBe(secondChangeSetId);
        changeSet.Rows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateSetAsync_WhenRequiredLimitExceeded_ThrowsBeforeUpdateAndHistoryRows()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureUpdateSet(ChangeHistoryCaptureMode.Required, maxAffectedRows: 1));
        database.Context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "one", UniqueCode = "R1" },
            new Customer { Id = Guid.NewGuid(), Name = "two", UniqueCode = "R2" });
        await database.Context.SaveChangesAsync();
        var repository = CreateChangeHistoryRepository(database.Context, database.Options);

        await Should.ThrowAsync<InvalidOperationException>(() => repository.UpdateSetAsync(set => set.Set(e => e.Name, "bulk")));

        (await database.Context.Set<ChangeHistoryEntry>().CountAsync()).ShouldBe(0);
        (await database.Context.Customers.CountAsync(e => e.Name == "bulk")).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateSetAsync_WhenBestEffortLimitExceeded_PersistsSummaryRow()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureUpdateSet(ChangeHistoryCaptureMode.BestEffort, maxAffectedRows: 1));
        database.Context.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), Name = "one", UniqueCode = "B1" },
            new Customer { Id = Guid.NewGuid(), Name = "two", UniqueCode = "B2" });
        await database.Context.SaveChangesAsync();
        var repository = CreateChangeHistoryRepository(database.Context, database.Options);

        var affected = await repository.UpdateSetAsync(set => set.Set(e => e.Name, "bulk"));

        affected.ShouldBe(2);
        var row = await database.Context.Set<ChangeHistoryEntry>().SingleAsync();
        row.CaptureStatus.ShouldBe(ChangeHistoryCaptureStatus.Summary.ToString());
        row.AffectedEntityCount.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_WhenRequiredBaselineMissing_ThrowsBeforeInsertOrHistoryRows()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required));
        var repository = CreateChangeHistoryRepository(database.Context, database.Options);
        var entity = new Customer { Id = Guid.NewGuid(), Name = "missing", UniqueCode = "MISS" };

        await Should.ThrowAsync<InvalidOperationException>(() => repository.UpdateAsync(entity));

        (await database.Context.Customers.CountAsync()).ShouldBe(0);
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task UpdateAsync_WithEfChangeTracker_CapturesDirectMutation()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureDirectMutations(ChangeHistoryCaptureStrategy.EfChangeTracker, ChangeHistoryCaptureMode.Required));
        var entity = new Customer { Id = Guid.NewGuid(), Name = "old", UniqueCode = "EF" };
        database.Context.Customers.Add(entity);
        await database.Context.SaveChangesAsync();
        entity.Name = "new";
        var repository = CreateChangeHistoryRepository(database.Context, database.Options);

        await repository.UpdateAsync(entity);

        var row = await database.Context.Set<ChangeHistoryEntry>().SingleAsync(e => e.PropertyName == nameof(Customer.Name));
        row.CaptureSource.ShouldBe(ChangeHistoryCaptureSource.EfChangeTracker.ToString());
        row.OldValue.ShouldBe("\"old\"");
        row.NewValue.ShouldBe("\"new\"");
    }

    [Fact]
    public async Task UpsertAsync_WithExistingAndNewEntities_CapturesUpdateAndCreateHistory()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureCreates()
                .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required));
        var existing = new Customer { Id = Guid.NewGuid(), Name = "old", UniqueCode = "UP1" };
        database.Context.Customers.Add(existing);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var repository = CreateChangeHistoryRepository(database.Context, database.Options);

        var updateResult = await repository.UpsertAsync(new Customer { Id = existing.Id, Name = "new", UniqueCode = "UP1" });
        var createResult = await repository.UpsertAsync(new Customer { Id = Guid.NewGuid(), Name = "created", UniqueCode = "UP2" });

        updateResult.action.ShouldBe(RepositoryActionResult.Updated);
        createResult.action.ShouldBe(RepositoryActionResult.Inserted);
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Update.ToString())).ShouldBe(1);
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Create.ToString())).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RestoreCommand_WithChangeHistoryRepository_PersistsRestoreRowsWithoutUpdateCaptureRows()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required)
                .AllowRestore(e => e.Name)
                .UseValidatedSetter());
        var customer = new Customer { Id = Guid.NewGuid(), Name = "new", UniqueCode = "RESTORE-NODUP" };
        var changeSetId = Guid.NewGuid();
        database.Context.Customers.Add(customer);
        database.Context.Set<ChangeHistoryEntry>().Add(CreateScalarHistory(customer, changeSetId, "old", "new"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var repository = new RepositoryChangeHistoryBehavior<Customer, ChangeHistoryTestDbContext>(
            NullLoggerFactory.Instance,
            database.Context,
            new IncludeCustomerRepository(database.Context),
            database.Options);
        var handler = new ChangeHistoryRestoreCommandHandler<Customer, ChangeHistoryTestDbContext>(
            database.Context,
            repository,
            database.Options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<Customer>(customer.Id, changeSetId));

        result.IsSuccess.ShouldBeTrue();
        database.Context.ChangeTracker.Clear();
        var restored = await database.Context.Customers.SingleAsync(e => e.Id == customer.Id);
        restored.Name.ShouldBe("old");
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Update.ToString())).ShouldBe(1);
        (await database.Context.Set<ChangeHistoryEntry>().CountAsync(e => e.Operation == ChangeHistoryOperation.Restore.ToString())).ShouldBe(1);
    }

    [Fact]
    public async Task RestoreCommand_WithCollectionMembershipRows_ReplaysMembershipAgainstSqliteEntity()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureCollection(e => e.Tags, tag => tag.Id));
        var keptId = Guid.NewGuid();
        var addedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "customer",
            UniqueCode = "COL",
            Tags =
            [
                new CustomerTag { Id = keptId, Value = "new" },
                new CustomerTag { Id = addedId, Value = "added" }
            ]
        };
        var changeSetId = Guid.NewGuid();
        database.Context.Customers.Add(customer);
        database.Context.Set<ChangeHistoryEntry>().AddRange(
            CreateCollectionHistory(customer, changeSetId, keptId, "old", "new"),
            CreateCollectionHistory(customer, changeSetId, addedId, null, "added", "Added"),
            CreateCollectionHistory(customer, changeSetId, removedId, "removed", null, "Removed"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var handler = CreateRestoreHandler(database.Context, database.Options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<Customer>(customer.Id, changeSetId));

        result.IsSuccess.ShouldBeTrue();
        database.Context.ChangeTracker.Clear();
        var restored = await database.Context.Customers.Include(e => e.Tags).SingleAsync(e => e.Id == customer.Id);
        restored.Tags.ShouldContain(tag => tag.Id == keptId && tag.Value == "old");
        restored.Tags.ShouldNotContain(tag => tag.Id == addedId);
        restored.Tags.ShouldContain(tag => tag.Id == removedId && tag.Value == "removed");
    }

    [Fact]
    public async Task RestoreCommand_WithGraphRowsAndEfMetadata_RestoresGraphWithoutExplicitIdentityRule()
    {
        await using var database = await CreateDatabaseAsync(options => options
            .Track<Customer>()
                .CaptureGraph(nameof(Customer.Orders)));
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "customer",
            UniqueCode = "GRAPH",
            Orders = [new CustomerOrder { Id = orderId, Number = "SO-1", Items = [new CustomerOrderItem { Id = itemId, Sku = "A", Quantity = 5 }] }]
        };
        var changeSetId = Guid.NewGuid();
        database.Context.Customers.Add(customer);
        database.Context.Set<ChangeHistoryEntry>().Add(CreateGraphHistory(customer, changeSetId, orderId, itemId, 1, 5));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var handler = CreateRestoreHandler(database.Context, database.Options);

        var result = await handler.HandleAsync(new ChangeHistoryRestoreCommand<Customer>(customer.Id, changeSetId));

        result.IsSuccess.ShouldBeTrue();
        database.Context.ChangeTracker.Clear();
        var restored = await database.Context.Customers.Include(e => e.Orders).ThenInclude(e => e.Items).SingleAsync(e => e.Id == customer.Id);
        restored.Orders.Single().Items.Single().Quantity.ShouldBe(1);
    }

    [Fact]
    public void AddChangeHistory_WithInvalidDomainLogicRestorePolicy_ThrowsDuringConfiguration()
    {
        var services = new ServiceCollection();

        var action = () => services.AddChangeHistory(options => options
            .Track<Customer>()
            .AllowRestore(e => e.Name));

        action.ShouldThrow<InvalidOperationException>();
    }

    private static async Task<TestDatabase> CreateDatabaseAsync(Action<ChangeHistoryOptions> configure = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ChangeHistoryTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ChangeHistoryTestDbContext(dbOptions);
        await context.Database.EnsureCreatedAsync();
        var options = new ChangeHistoryOptions();
        configure?.Invoke(options);
        options.Validate();

        return new TestDatabase(connection, context, options);
    }

    private static RepositoryChangeHistoryBehavior<Customer, ChangeHistoryTestDbContext> CreateChangeHistoryRepository(
        ChangeHistoryTestDbContext context,
        ChangeHistoryOptions options)
        => new(
            NullLoggerFactory.Instance,
            context,
            new EntityFrameworkRepositoryWrapper<Customer, ChangeHistoryTestDbContext>(NullLoggerFactory.Instance, context),
            options);

    private static ChangeHistoryRestoreCommandHandler<Customer, ChangeHistoryTestDbContext> CreateRestoreHandler(
        ChangeHistoryTestDbContext context,
        ChangeHistoryOptions options)
        => new(
            context,
            new IncludeCustomerRepository(context),
            options);

    private static async Task<WebApplication> CreateApplicationAsync(TestDatabase database, bool requireAllowedClaim)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("History.Read", policy => policy.RequireClaim("allowed", "true"));
            options.AddPolicy("History.Restore", policy => policy.RequireClaim("allowed", "true"));
            if (!requireAllowedClaim)
            {
                options.AddPolicy("AllowAll", policy => policy.RequireAuthenticatedUser());
            }
        });
        builder.Services.AddSingleton(database.Connection);
        builder.Services.AddDbContext<ChangeHistoryTestDbContext>((provider, options) => options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        builder.Services.AddSingleton(database.Options);
        builder.Services.AddScoped<IGenericRepository<Customer>, EntityFrameworkRepositoryWrapper<Customer, ChangeHistoryTestDbContext>>(provider =>
            new EntityFrameworkRepositoryWrapper<Customer, ChangeHistoryTestDbContext>(NullLoggerFactory.Instance, provider.GetRequiredService<ChangeHistoryTestDbContext>()));
        builder.Services.AddRequester();
        builder.Services.AddChangeHistoryEndpoints<Customer, ChangeHistoryTestDbContext>();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEndpoints();
        await app.StartAsync();

        return app;
    }

    private static ChangeHistoryEntry CreateScalarHistory(
        Customer customer,
        Guid changeSetId,
        string oldValue,
        string newValue,
        DateTimeOffset? changedDate = null)
        => CreateHistory(customer, changeSetId, nameof(Customer.Name), oldValue, newValue, changedDate);

    private static ChangeHistoryEntry CreateHistory(
        Customer customer,
        Guid changeSetId,
        string propertyName,
        string oldValue,
        string newValue,
        DateTimeOffset? changedDate = null)
    {
        var effectiveChangedDate = changedDate ?? DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChangeSetId = changeSetId,
            EntityType = nameof(Customer),
            EntityClrType = typeof(Customer).AssemblyQualifiedName,
            EntityId = customer.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = propertyName,
            PathKind = "Scalar",
            ValueClrType = typeof(string).AssemblyQualifiedName,
            OldValue = oldValue is null ? null : $"\"{oldValue}\"",
            NewValue = newValue is null ? null : $"\"{newValue}\"",
            Operation = ChangeHistoryOperation.Update.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            ChangedDate = effectiveChangedDate,
            ChangedDateTicks = effectiveChangedDate.UtcTicks
        };
    }

    private static ChangeHistoryEntry CreateCollectionHistory(Customer customer, Guid changeSetId, Guid itemId, string oldValue, string newValue, string action = null)
    {
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChangeSetId = changeSetId,
            EntityType = nameof(Customer),
            EntityClrType = typeof(Customer).AssemblyQualifiedName,
            EntityId = customer.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = $"Tags[{itemId}].Value",
            PropertyPath = $"Tags[{itemId}].Value",
            PathKind = ChangeHistoryCapturePathKind.Collection.ToString(),
            CollectionAction = action,
            CollectionItemId = itemId.ToString(),
            ValueClrType = typeof(string).AssemblyQualifiedName,
            OldValue = oldValue is null ? null : $"\"{oldValue}\"",
            NewValue = newValue is null ? null : $"\"{newValue}\"",
            Operation = ChangeHistoryOperation.CollectionChanged.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks
        };
    }

    private static ChangeHistoryEntry CreateGraphHistory(Customer customer, Guid changeSetId, Guid orderId, Guid itemId, int oldValue, int newValue)
    {
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChangeSetId = changeSetId,
            EntityType = nameof(Customer),
            EntityClrType = typeof(Customer).AssemblyQualifiedName,
            EntityId = customer.Id.ToString(),
            EntityIdType = typeof(Guid).AssemblyQualifiedName,
            PropertyName = $"Orders[{orderId}].Items[{itemId}].Quantity",
            PropertyPath = $"Orders[{orderId}].Items[{itemId}].Quantity",
            PathKind = ChangeHistoryCapturePathKind.Graph.ToString(),
            CollectionItemId = itemId.ToString(),
            ValueClrType = typeof(int).AssemblyQualifiedName,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString(),
            Operation = ChangeHistoryOperation.GraphChanged.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.RepositorySnapshot.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.RepositorySnapshot.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks
        };
    }

    private sealed class ChangeHistoryTestDbContext(DbContextOptions<ChangeHistoryTestDbContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers { get; set; }

        public DbSet<ChangeHistoryEntry> ChangeHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.HasIndex(e => e.UniqueCode).IsUnique();
                builder.OwnsOne(e => e.BillingAddress);
                builder.OwnsMany(e => e.Tags, tags =>
                {
                    tags.WithOwner().HasForeignKey("CustomerId");
                    tags.HasKey(e => e.Id);
                });
                builder.OwnsMany(e => e.Orders, orders =>
                {
                    orders.WithOwner().HasForeignKey("CustomerId");
                    orders.HasKey(e => e.Id);
                    orders.OwnsMany(e => e.Items, items =>
                    {
                        items.WithOwner().HasForeignKey("OrderId");
                        items.HasKey(e => e.Id);
                    });
                });
            });
        }
    }

    private sealed class Customer : Entity<Guid>
    {
        public string Name { get; set; }

        public string UniqueCode { get; set; }

        public Address BillingAddress { get; set; }

        public List<CustomerTag> Tags { get; set; } = [];

        public List<CustomerOrder> Orders { get; set; } = [];
    }

    private sealed class Address
    {
        public string City { get; set; }
    }

    private sealed class CustomerTag
    {
        public Guid Id { get; set; }

        public string Value { get; set; }
    }

    private sealed class CustomerOrder
    {
        public Guid Id { get; set; }

        public string Number { get; set; }

        public List<CustomerOrderItem> Items { get; set; } = [];
    }

    private sealed class CustomerOrderItem
    {
        public Guid Id { get; set; }

        public string Sku { get; set; }

        public int Quantity { get; set; }
    }

    private sealed class TestDatabase(SqliteConnection connection, ChangeHistoryTestDbContext context, ChangeHistoryOptions options) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;

        public ChangeHistoryTestDbContext Context { get; } = context;

        public ChangeHistoryOptions Options { get; } = options;

        public async ValueTask DisposeAsync()
        {
            await this.Context.DisposeAsync();
            await this.Connection.DisposeAsync();
        }
    }

    private sealed class IncludeCustomerRepository : EntityFrameworkRepositoryWrapper<Customer, ChangeHistoryTestDbContext>
    {
        private readonly ChangeHistoryTestDbContext context;

        public IncludeCustomerRepository(ChangeHistoryTestDbContext context)
            : base(NullLoggerFactory.Instance, context)
        {
            this.context = context;
        }

        public override async Task<Customer> FindOneAsync(
            object id,
            IFindOptions<Customer> options = null,
            CancellationToken cancellationToken = default)
        {
            var query = this.context.Customers
                .Include(e => e.Tags)
                .Include(e => e.Orders)
                .ThenInclude(e => e.Items)
                .AsQueryable();
            if (options?.NoTracking == true)
            {
                query = query.AsNoTracking();
            }

            return await query.SingleOrDefaultAsync(e => e.Id == (Guid)id, cancellationToken);
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "integration-test")
            };
            if (this.Request.Headers.TryGetValue("X-Test-Auth", out var value) && value == "allow")
            {
                claims.Add(new Claim("allowed", "true"));
            }

            var identity = new ClaimsIdentity(claims, this.Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, this.Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
