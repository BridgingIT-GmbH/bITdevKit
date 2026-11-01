// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Runs real handlers/repositories against an independently owned SQLite database.</summary>
/// <example><code>var factory = new DoFiestaApplicationFactory(); await factory.InitializeAsync();</code></example>
public sealed class DoFiestaApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    /// <summary>The unique fictional authenticated user for this isolated host.</summary>
    /// <example><code>client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);</code></example>
    public string UserId { get; } = "ndjson-" + Guid.NewGuid().ToString("N");

    /// <summary>The identities granted per-item Read permission.</summary>
    /// <example><code>received.Count.ShouldBe(factory.ReadableIds.Count);</code></example>
    public HashSet<string> ReadableIds { get; } = [];

    /// <summary>Starts the test database and seeds 205 source rows with alternating read grants.</summary>
    /// <example><code>await factory.InitializeAsync();</code></example>
    public async Task InitializeAsync()
    {
        await this.connection.OpenAsync();
        await using var scope = this.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await context.Database.EnsureCreatedAsync();
        this.Services.GetService<IDatabaseReadyService>()?.SetReady(nameof(CoreDbContext));
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
    }

    /// <summary>Disposes the host before closing this factory's in-memory database.</summary>
    /// <example><code>await factory.DisposeAsync();</code></example>
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await this.connection.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Fatal"
        }));
        builder.ConfigureServices(services =>
        {
            for (var i = services.Count - 1; i >= 0; i--)
            {
                var type = services[i].ServiceType;
                if (type == typeof(CoreDbContext) || type == typeof(DbContextOptions<CoreDbContext>) || type == typeof(DbContextOptions) || type == typeof(IDbContextResolver) || type == typeof(IHostedService) || type.Name.Contains("StartupTaskDefinition") || services[i].ImplementationType?.Name.Contains("Seeder") == true)
                {
                    services.RemoveAt(i);
                }
            }

            services.AddScoped(services => new DbContextOptionsBuilder<CoreDbContext>().UseSqlite(this.connection)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
                .AddInterceptors(services.GetRequiredService<ChangeHistoryAuditSaveInterceptor<CoreDbContext>>(),
                    services.GetRequiredService<AuditTransactionObserver<CoreDbContext>>()).Options);
            services.AddScoped<CoreDbContext>();
            services.AddScoped<IDbContextResolver, DbContextResolver>();
            // These test hosts omit background workers; use confirmed inline delivery.
            services.Replace(ServiceDescriptor.Singleton<BridgingIT.DevKit.Application.Auditing.IAuditingStorageProvider>(provider =>
                new BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing.EntityFrameworkAuditStorageProvider<CoreDbContext>(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing.AuditEntityFrameworkOptions>() with { Buffered = false },
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditingOptions>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditFieldCatalog>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditIntegrityReadPolicy>(),
                    provider.GetRequiredService<BridgingIT.DevKit.Application.Auditing.AuditRetentionOptions>(),
                    provider.GetRequiredService<TimeProvider>())));
            services.Replace(ServiceDescriptor.Singleton<BridgingIT.DevKit.Domain.Repositories.ISequenceNumberGenerator>(
                new BridgingIT.DevKit.Domain.Repositories.InMemorySequenceNumberGenerator(null)));
            var dashboard = services.First(descriptor => descriptor.ServiceType == typeof(BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardEndpointsOptions)).ImplementationInstance
                as BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardEndpointsOptions;
            dashboard.RequireAuthenticationSchemes = [TestAuthenticationHandler.SCHEME];
            dashboard.Authentication.Scheme = TestAuthenticationHandler.SCHEME;
            dashboard.Authentication.Kind = BridgingIT.DevKit.Presentation.Web.Dashboard.DashboardAuthenticationRegistrationKind.ExistingScheme;
            services.AddAuthentication(o => { o.DefaultAuthenticateScheme = TestAuthenticationHandler.SCHEME; o.DefaultChallengeScheme = TestAuthenticationHandler.SCHEME; })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SCHEME, _ => { });
        });
    }

    // Keep production SQL Server configuration unchanged. Translate provider-only
    // storage annotations for this relational test store after the real model builds.
    private sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            foreach (var sequence in modelBuilder.Model.GetSequences().ToArray())
            {
                modelBuilder.Model.RemoveSequence(sequence.Name, sequence.ModelSchema);
            }

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.GetColumnType()?.Contains("max", StringComparison.OrdinalIgnoreCase) == true) { property.SetColumnType("TEXT"); }

                    if (property.GetDefaultValueSql() is { } sql && (sql.Contains("getutcdate", StringComparison.OrdinalIgnoreCase) || sql.Contains("sysutcdatetime", StringComparison.OrdinalIgnoreCase))) { property.SetDefaultValueSql("CURRENT_TIMESTAMP"); }

                    if (property.GetDefaultValueSql() is { } providerDefault && (providerDefault.Contains("newsequentialid", StringComparison.OrdinalIgnoreCase) || providerDefault.Contains("NEXT VALUE FOR", StringComparison.OrdinalIgnoreCase))) { property.SetDefaultValueSql(null); }

                    if (property.GetDefaultValueSql()?.Contains("NEWID()", StringComparison.OrdinalIgnoreCase) == true) { property.SetDefaultValueSql("(lower(hex(randomblob(16))))"); }

                    if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateTimeOffset)) { property.SetValueConverter(new DateTimeOffsetToBinaryConverter()); }
                }
            }
        }
    }
}
