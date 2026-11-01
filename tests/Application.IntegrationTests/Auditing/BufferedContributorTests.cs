// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.IntegrationTests.Auditing;

using System.Security.Cryptography;
using System.Text;
using Application.Auditing;
using Application.DataPorter;
using Infrastructure.EntityFramework.Auditing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

public sealed class BufferedContributorTests
{
    [Fact]
    public async Task DataPorter_RealBufferedEfDelivery_BoundedFlushDoesNotCancelAcceptedEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), "auditing-contributor-" + Guid.NewGuid().ToString("N") + ".db");
        var gate = new WriteGate();
        try
        {
            using var host = new HostBuilder().ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddSingleton<KeyMaterial>();
                services.AddDbContext<SqliteAuditMigrationContext>(options => options.UseSqlite("Data Source=" + path).AddInterceptors(gate));
                services.AddAuditing().WithHmacSha256<Keys>().WithEntityFrameworkProvider<SqliteAuditMigrationContext>(options => options.Buffered(value => value with { BatchSize = 1, RetryCount = 0 }));
                services.AddDataPorter().WithAuditing();
                services.AddSingleton<IDataPorterProvider, CsvDataPorterProvider>();
            }).Build();
            await using (var migration = host.Services.CreateAsyncScope())
            {
                await migration.ServiceProvider.GetRequiredService<SqliteAuditMigrationContext>().Database.MigrateAsync();
            }

            await host.StartAsync();
            try
            {
                var storage = host.Services.GetRequiredService<IAuditingStorageProvider>();
                try
                {
                    await using var source = host.Services.CreateAsyncScope();
                    using var output = new MemoryStream();
                    var result = await source.ServiceProvider.GetRequiredService<IDataExporter>().ExportAsync(new[] { new Customer() }, output, new ExportOptions { Format = Format.Csv });
                    result.IsSuccess.ShouldBeTrue();
                    host.Services.GetRequiredService<AuditRuntime>().SnapshotFailures().ShouldBeEmpty();
                    await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    using var shortWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    var pending = await storage.FlushAsync(shortWait.Token);
                    pending.IsFailure.ShouldBeTrue();
                    var boundary = pending.Errors.OfType<AuditFlushIncompleteError>().Single().FlushResult;
                    boundary.Pending.ShouldBe(1);
                    boundary.Persisted.ShouldBe(0);
                    await using var read = host.Services.CreateAsyncScope();
                    (await read.ServiceProvider.GetRequiredService<SqliteAuditMigrationContext>().AuditRecords.CountAsync()).ShouldBe(0);
                }
                finally { gate.Release.TrySetResult(); }

                using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var confirmed = await storage.FlushAsync(bounded.Token);
                confirmed.IsSuccess.ShouldBeTrue();
                confirmed.Value.Persisted.ShouldBe(1);
                confirmed.Value.Pending.ShouldBe(0);
                var plan = host.Services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
                var summary = (await storage.SearchAsync(plan, bounded.Token)).Value.ShouldHaveSingleItem();
                var record = (await storage.FindAsync(summary.AuditId, bounded.Token)).Value;
                record.Kind.ShouldBe("DataPorter");
                record.IntegrityProtection.ShouldNotBeNull();
                Encoding.UTF8.GetString(AuditRecordCodec.Encode(record)).ShouldNotContain("private");
            }
            finally
            {
                gate.Release.TrySetResult();
                await host.StopAsync();
            }
        }
        finally
        {
            gate.Release.TrySetResult();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
    public sealed class Customer { public string Name { get; set; } = "private-source-row"; }
    public sealed class KeyMaterial : IDisposable
    {
        public byte[] Bytes { get; } = RandomNumberGenerator.GetBytes(32);
        public void Dispose() => CryptographicOperations.ZeroMemory(this.Bytes);
    }
    public sealed class Keys(KeyMaterial material) : IAuditIntegrityKeyProvider
    {
        public Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default) => this.ResolveAsync("contributor-test", cancellationToken);
        public Task<Result<AuditIntegrityKey>> ResolveAsync(string keyId, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditIntegrityKey>.Success(new(keyId, material.Bytes)));
    }
    public sealed class WriteGate : SaveChangesInterceptor
    {
        private int calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this.calls) == 1)
            {
                this.Entered.TrySetResult();
                await this.Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
