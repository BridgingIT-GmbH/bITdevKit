// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

/// <summary>Runs real application handlers, permission checks and central caching against an independently owned SQL Server catalog.</summary>
/// <example><code>var factory = new DoFiestaApplicationFactory(); await factory.InitializeAsync();</code></example>
public sealed class DoFiestaApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly string databaseName = "DoFiestaTest_" + Guid.NewGuid().ToString("N");
    private string connectionString;

    /// <summary>The unique fictional authenticated user for this isolated host.</summary>
    /// <example><code>client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);</code></example>
    public string UserId { get; } = "ndjson-" + Guid.NewGuid().ToString("N");

    /// <summary>The identities granted per-item Read permission.</summary>
    /// <example><code>received.Count.ShouldBe(factory.ReadableIds.Count);</code></example>
    public HashSet<string> ReadableIds { get; } = [];

    /// <summary>Starts the owned SQL Server catalog and seeds 205 source rows with alternating read grants.</summary>
    /// <example><code>await factory.InitializeAsync();</code></example>
    public async Task InitializeAsync()
    {
        await this.sqlContainer.StartAsync();
        this.connectionString = new SqlConnectionStringBuilder(this.sqlContainer.GetConnectionString())
        {
            InitialCatalog = this.databaseName
        }.ConnectionString;
        await using var scope = this.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        using (var cached = this.Services.GetRequiredService<IDbContextFactory<CoreDbContext>>().CreateDbContext())
        {
            this.RequireOwnedTarget(context);
            this.RequireOwnedTarget(cached);
        }

        await context.Database.EnsureCreatedAsync();
        context.EntityPermissions.Add(new EntityPermission { Id = Guid.NewGuid(), UserId = this.UserId, EntityType = typeof(TodoItem).FullName, Permission = Permission.List, Module = "Core" });
        for (var index = 0; index < 205; index++)
        {
            var item = new TodoItem
            {
                Id = TodoItemId.Create(Guid.Parse($"10000000-0000-0000-0000-{index + 1:000000000000}")),
                UserId = this.UserId,
                Title = $"NDJSON fixture {index:000}",
                Number = index + 1,
                Status = TodoStatus.New,
                Priority = TodoPriority.Medium,
                Assignee = EmailAddress.Create("fictional@example.test"),
                ConcurrencyVersion = Guid.NewGuid()
            };
            context.TodoItems.Add(item);
            if (index % 2 == 0)
            {
                this.ReadableIds.Add(item.Id.ToString());
                context.EntityPermissions.Add(new EntityPermission { Id = Guid.NewGuid(), UserId = this.UserId, EntityType = typeof(TodoItem).FullName, EntityId = item.Id.ToString(), Permission = Permission.Read, Module = "Core" });
            }
        }

        await context.SaveChangesAsync();
        var sequence = context.Model.GetSequences().Single(value => value.Name == "TodoItemSequence");
        var reset = await scope.ServiceProvider.GetRequiredService<ISequenceNumberGenerator>()
            .ResetSequenceAsync(sequence.Name, 206, sequence.Schema ?? context.Model.GetDefaultSchema());
        if (reset.IsFailure) { throw new InvalidOperationException("The owned fixture sequence could not be initialized."); }

        this.Services.GetService<IDatabaseReadyService>()?.SetReady(nameof(CoreDbContext));
    }

    /// <summary>Disposes the host before removing its owned SQL Server container and catalog.</summary>
    /// <example><code>await factory.DisposeAsync();</code></example>
    public new async Task DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { await this.sqlContainer.DisposeAsync(); }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Modules:Core:ConnectionStrings:Default"] = this.connectionString,
            ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Fatal"
        }));
        builder.ConfigureServices(services =>
        {
            for (var index = services.Count - 1; index >= 0; index--)
            {
                var descriptor = services[index];
                if (descriptor.ServiceType == typeof(IHostedService) || descriptor.ServiceType.Name.Contains("StartupTaskDefinition") || descriptor.ImplementationType?.Name.Contains("Seeder") == true)
                {
                    services.RemoveAt(index);
                }
            }

            // Minimal-host test configuration can arrive after module registration. Override only the
            // final connection, retaining production business interceptors and model conventions.
            services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(this.connectionString));
            services.Replace(ServiceDescriptor.Singleton<IDbContextFactory<CoreDbContext>>(provider => new SqlServerDbContextFactory<CoreDbContext>(provider,
                new DbContextOptionsBuilder<CoreDbContext>().UseSqlServer(this.connectionString).Options)));

            // Test hosts omit background workers; use confirmed inline auditing with production SQL contexts.
            services.Replace(ServiceDescriptor.Singleton<BridgingIT.DevKit.Application.Auditing.IAuditingStorageProvider>(provider =>
                new BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing.EntityFrameworkAuditStorageProvider<CoreDbContext>(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing.AuditEntityFrameworkOptions>() with { Buffered = false },
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditingOptions>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditFieldCatalog>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditIntegrityReadPolicy>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditRetentionOptions>(),
                    provider.GetRequiredService<TimeProvider>())));
            var dashboard = services.First(descriptor => descriptor.ServiceType == typeof(BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardEndpointsOptions)).ImplementationInstance
                as BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardEndpointsOptions;
            dashboard.RequireAuthenticationSchemes = [TestAuthenticationHandler.SCHEME];
            dashboard.Authentication.Scheme = TestAuthenticationHandler.SCHEME;
            dashboard.Authentication.Kind = BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardAuthenticationRegistrationKind.ExistingScheme;
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = TestAuthenticationHandler.SCHEME; options.DefaultChallengeScheme = TestAuthenticationHandler.SCHEME; })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SCHEME, _ => { });
        });
    }

    private bool IsOwnedTarget(string server, string catalog)
    {
        var expected = new SqlConnectionStringBuilder(this.connectionString);
        return string.Equals(server, expected.DataSource, StringComparison.OrdinalIgnoreCase) && string.Equals(catalog, this.databaseName, StringComparison.Ordinal);
    }

    private void RequireOwnedTarget(CoreDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (!this.IsOwnedTarget(connection.DataSource, connection.Database))
        {
            throw new InvalidOperationException("Fixture initialization requires its independently owned server and catalog.");
        }
    }

}