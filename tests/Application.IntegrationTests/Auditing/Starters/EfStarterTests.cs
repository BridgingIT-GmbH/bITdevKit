// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing.Starters;

using Application.Auditing;
using Infrastructure.EntityFramework.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;
using BridgingIT.DevKit.Domain.Repositories;

public sealed class EfStarterTests
{
    [Fact]
    public async Task HostDatabaseReady_CanCreateSchemaAfterStartupBeforeBufferedDelivery()
    {
        var file = Path.Combine(Path.GetTempPath(), "audit-ready-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var host = new HostBuilder().ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IDatabaseReadyService, DatabaseReadyService>();
                services.AddDbContext<SqliteAuditMigrationContext>(options => options.UseSqlite("Data Source=" + file + ";Pooling=false"));
                services.AddAuditing()
                    .WithRetention(new() { ArchiveAfter = TimeSpan.FromDays(30), StartupDelay = TimeSpan.FromMilliseconds(10) })
                    .WithEntityFrameworkProvider<SqliteAuditMigrationContext>(options => options
                        .StartupDelay(TimeSpan.FromMilliseconds(10)).Configure(settings => settings with { BatchSize = 1 }));
            }).Build();
            await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var readiness = host.Services.GetRequiredService<IDatabaseReadyService>();
            readiness.IsReady(nameof(SqliteAuditMigrationContext)).ShouldBeFalse();
            await using (var execution = host.Services.CreateAsyncScope())
            {
                await execution.ServiceProvider.GetRequiredService<IAuditContext>().RunAsync(
                    options => options.Key("customers.create"), (_, _) => Task.FromResult(Result.Success()));
            }

            var provider = host.Services.GetRequiredService<EntityFrameworkAuditStorageProvider<SqliteAuditMigrationContext>>();
            provider.DeliveryStatus.Pending.ShouldBe(1);
            provider.DeliveryStatus.Persisted.ShouldBe(0);
            var maintenance = provider.ArchiveBeforeAsync(DateTimeOffset.UtcNow.AddDays(-30));
            maintenance.IsCompleted.ShouldBeFalse();
            await using (var deployment = host.Services.CreateAsyncScope())
                await deployment.ServiceProvider.GetRequiredService<SqliteAuditMigrationContext>().Database.MigrateAsync();
            readiness.SetReady(nameof(SqliteAuditMigrationContext));
            (await maintenance).IsSuccess.ShouldBeTrue();
            var flushed = await provider.FlushAsync();
            flushed.IsSuccess.ShouldBeTrue();
            flushed.Value.Persisted.ShouldBe(1);
            await host.StopAsync();
        }
        finally { File.Delete(file); File.Delete(file + "-wal"); File.Delete(file + "-shm"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitReviewedSchema_WorksWithHostOwnedContext(bool buffered)
    {
        var file = Path.Combine(Path.GetTempPath(), "audit-starter-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using var runtime = new HostBuilder().ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddDbContext<SqliteAuditMigrationContext>(options => options.UseSqlite("Data Source=" + file + ";Pooling=false"));
                services.AddAuditing().WithEntityFrameworkProvider<SqliteAuditMigrationContext>(options =>
                {
                    if (!buffered) options.Inline();
                });
            }).Build();
            var host = runtime.Services;
            var missing = await host.GetRequiredService<EntityFrameworkAuditStorageProvider<SqliteAuditMigrationContext>>().CheckReadinessAsync(); missing.IsFailure.ShouldBeTrue();
            await using (var deployment = host.CreateAsyncScope()) await deployment.ServiceProvider.GetRequiredService<SqliteAuditMigrationContext>().Database.MigrateAsync();
            await runtime.StartAsync();
            Guid id;
            await using (var execution = host.CreateAsyncScope())
            {
                await using var audit = execution.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(options => options.Key("customers.update")); audit.AddResource(new("Customer", "C-42")); audit.Complete(); id = audit.AuditId;
            }

            var provider = host.GetRequiredService<IAuditingStorageProvider>(); var flush = await provider.FlushAsync(); flush.IsSuccess.ShouldBeTrue(); flush.Value.Persisted.ShouldBe(1); provider.Capabilities.IsDurable.ShouldBeTrue();
            await using var reading = host.CreateAsyncScope(); (await reading.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(id)).IsSuccess.ShouldBeTrue(); (await reading.ServiceProvider.GetRequiredService<IAuditQueryService>().GetResourceHistoryAsync(new("Customer", "C-42"))).TotalCount.ShouldBe(1);
            await runtime.StopAsync();
        }
        finally { File.Delete(file); File.Delete(file + "-wal"); File.Delete(file + "-shm"); }
    }
}
