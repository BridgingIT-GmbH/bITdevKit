// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.EntityFramework;

using System.Net;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies ChangeHistory dashboard registration and server-rendered reads without management REST endpoints.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~ChangeHistoryDashboardTests</code></example>
public class ChangeHistoryDashboardTests
{
    /// <summary>Dashboard reads and HTML refresh work with or without the optional management API.</summary>
    /// <example><code>await suite.Page_WithIndependentRegistration_RendersPersistedHistory(false, false, false);</code></example>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task Page_WithIndependentRegistration_RendersPersistedHistory(bool registerEndpoints, bool includeValues, bool overrideDashboard)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<HistoryDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddChangeHistory(options =>
        {
            options.Track<HistoryEntity>();
            options.Track<UnmappedHistoryEntity>();
        });
        builder.Services.AddScoped(_ => Substitute.For<IGenericRepository<HistoryEntity>>());
        if (overrideDashboard)
        {
            builder.Services.AddChangeHistoryDashboard<HistoryEntity, HistoryDbContext>(options => options.IncludeValues = includeValues);
        }

        if (registerEndpoints)
        {
            builder.Services.AddChangeHistoryEndpoints<HistoryEntity, HistoryDbContext>(options => options
                .GroupPath("/api/history")
                .IncludeValues(!includeValues));
        }

        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/admin")
            .WithPluginAssemblyContaining<DashboardEndpoints>());
        await using var app = builder.Build();
        app.MapEndpoints();
        var changeSetId = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<HistoryDbContext>();
            context.Set<ChangeHistoryEntry>().Add(new ChangeHistoryEntry
            {
                Id = Guid.NewGuid(),
                ChangeSetId = changeSetId,
                EntityType = nameof(HistoryEntity),
                EntityClrType = typeof(HistoryEntity).FullName,
                EntityId = "customer-42",
                PropertyName = "DisplayName",
                OldValue = "previous-dashboard-value",
                NewValue = "current-dashboard-value",
                Operation = "Update",
                CaptureSource = "EntityChange",
                CaptureStrategy = "EntityChange",
                CaptureStatus = "Captured",
                ChangedDate = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await app.StartAsync();
        var client = app.GetTestClient();
        foreach (var path in new[] { "/admin/change-history", "/admin/change-history/content" })
        {
            var html = await client.GetStringAsync(path + "?entityId=customer-42&changeSetId=" + changeSetId);

            html.ShouldContain("customer-42");
            html.ShouldContain("DisplayName");
            html.ShouldContain(nameof(HistoryDbContext));
            html.ShouldNotContain("could not be loaded");
            html.ShouldNotContain("No ChangeHistory entities");
            html.ShouldNotContain(nameof(UnmappedHistoryEntity));
            html.Contains("current-dashboard-value").ShouldBe(includeValues);
            html.Contains("previous-dashboard-value").ShouldBe(includeValues);
        }

        var card = await client.GetStringAsync("/admin");
        card.ShouldContain(nameof(HistoryEntity));
        card.ShouldNotContain("No ChangeHistory dashboard entities registered");
        var apiResponse = await client.GetAsync("/api/history/");
        apiResponse.StatusCode.ShouldBe(registerEndpoints ? HttpStatusCode.OK : HttpStatusCode.NotFound);
    }

    /// <summary>Dashboard restore invokes the service and preserves failures without management REST registration.</summary>
    /// <example><code>await suite.Restore_WithoutManagementEndpoints_UsesRegisteredService(true, false);</code></example>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Restore_WithoutManagementEndpoints_UsesRegisteredService(bool authorized, bool overrideDashboard)
    {
        var entityId = Guid.NewGuid();
        var changeSetId = Guid.NewGuid();
        var restoredChangeSetId = Guid.NewGuid();
        var service = Substitute.For<IChangeHistoryService<HistoryEntity, HistoryDbContext>>();
        service.RestoreAsync(Arg.Any<ChangeHistoryRestoreCommand<HistoryEntity>>(), Arg.Any<CancellationToken>())
            .Returns(authorized
                ? Result<ChangeHistoryRestoreResult>.Success(new ChangeHistoryRestoreResult(restoredChangeSetId, 1))
                : Result<ChangeHistoryRestoreResult>.Failure().WithError(new ForbiddenError("Restore denied.")));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<HistoryDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddChangeHistory(options => options.Track<HistoryEntity>());
        if (overrideDashboard)
        {
            builder.Services.AddChangeHistoryDashboard<HistoryEntity, HistoryDbContext>();
        }

        builder.Services.AddScoped(_ => service);
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/admin")
            .WithPluginAssemblyContaining<DashboardEndpoints>());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var descriptor = new ChangeHistoryDashboardDescriptor(typeof(HistoryEntity), typeof(HistoryDbContext), null);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["registration"] = descriptor.Key,
            ["entityId"] = entityId.ToString(),
            ["changeSetId"] = changeSetId.ToString(),
            ["reason"] = "Support correction"
        });

        var response = await app.GetTestClient().PostAsync("/admin/change-history/actions/restore", form);

        response.StatusCode.ShouldBe(authorized ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain(authorized ? restoredChangeSetId.ToString() : "Restore denied.");
        await service.Received(1).RestoreAsync(Arg.Is<ChangeHistoryRestoreCommand<HistoryEntity>>(command =>
            (Guid)command.EntityId == entityId && command.ChangeSetId == changeSetId && command.Reason == "Support correction"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Re-registering a pair replaces its dashboard settings without duplicate entries.</summary>
    /// <example><code>suite.AddChangeHistoryDashboard_RepeatedPair_ReplacesOptions();</code></example>
    [Fact]
    public void AddChangeHistoryDashboard_RepeatedPair_ReplacesOptions()
    {
        var services = new ServiceCollection();
        services.AddChangeHistoryDashboard<HistoryEntity, HistoryDbContext>();
        services.AddChangeHistoryDashboard<HistoryEntity, HistoryDbContext>(options => options.IncludeValues = true);

        var descriptor = (ChangeHistoryDashboardDescriptor)services.Single(service =>
            service.ServiceType == typeof(ChangeHistoryDashboardDescriptor)).ImplementationInstance;

        descriptor.Options.IncludeValues.ShouldBeTrue();
        services.ShouldNotContain(service => service.ServiceType == typeof(IEndpoints));
    }

    /// <summary>Disabled dashboard registrations add no descriptors or services.</summary>
    /// <example><code>suite.AddChangeHistoryDashboard_Disabled_DoesNotRegisterServices();</code></example>
    [Fact]
    public void AddChangeHistoryDashboard_Disabled_DoesNotRegisterServices()
    {
        var services = new ServiceCollection();

        services.AddChangeHistoryDashboard<HistoryEntity, HistoryDbContext>(enabled: false);

        services.ShouldBeEmpty();
        new ChangeHistoryDashboardOptions().IncludeValues.ShouldBeFalse();
    }

    /// <summary>Discovery matches EF models and deduplicates context metadata and explicit overrides.</summary>
    /// <example><code>await suite.Card_WithMultipleContexts_CountsOnlyMappedHistoryPairs();</code></example>
    [Fact]
    public async Task Card_WithMultipleContexts_CountsOnlyMappedHistoryPairs()
    {
        var services = new ServiceCollection();
        services.AddDbContext<HistoryDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<OtherHistoryDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddDbContext<UnrelatedDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton(new DbContextRegistration(typeof(HistoryDbContext), "InMemory"));
        services.AddSingleton(new DbContextRegistration(typeof(OtherHistoryDbContext), "InMemory"));
        services.AddChangeHistory(options =>
        {
            options.Track<HistoryEntity>();
            options.Track<UnmappedHistoryEntity>();
        });
        services.AddChangeHistoryDashboard<HistoryEntity, HistoryDbContext>();
        var options = new BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardEndpointsOptions();
        services.AddSingleton(options);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var sut = new DashboardPageProvider(options);

        var card = await sut.GetPages(context).Single().Card(context);

        card.Value.ShouldBe("2");
        card.Detail.ShouldBe(nameof(HistoryEntity));
    }

    /// <summary>Provides a tracked entity for dashboard tests.</summary>
    /// <example><code>var entity = new HistoryEntity();</code></example>
    public sealed class HistoryEntity : Entity<Guid>;

    /// <summary>Provides a tracked entity that is absent from the history context model.</summary>
    /// <example><code>options.Track&lt;UnmappedHistoryEntity&gt;();</code></example>
    public sealed class UnmappedHistoryEntity : Entity<Guid>;

    /// <summary>Stores dashboard test history in EF Core.</summary>
    /// <param name="options">The test database options.</param>
    /// <example><code>var context = new HistoryDbContext(options);</code></example>
    public sealed class HistoryDbContext(DbContextOptions<HistoryDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeHistoryEntry>();
            modelBuilder.Entity<HistoryEntity>();
        }
    }

    /// <summary>Maps the same tracked entity to a second history context.</summary>
    /// <param name="options">The test database options.</param>
    /// <example><code>var context = new OtherHistoryDbContext(options);</code></example>
    public sealed class OtherHistoryDbContext(DbContextOptions<OtherHistoryDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChangeHistoryEntry>();
            modelBuilder.Entity<HistoryEntity>();
        }
    }

    /// <summary>Maps a tracked entity without storing ChangeHistory rows.</summary>
    /// <param name="options">The test database options.</param>
    /// <example><code>var context = new UnrelatedDbContext(options);</code></example>
    public sealed class UnrelatedDbContext(DbContextOptions<UnrelatedDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnmappedHistoryEntity>();
    }
}
