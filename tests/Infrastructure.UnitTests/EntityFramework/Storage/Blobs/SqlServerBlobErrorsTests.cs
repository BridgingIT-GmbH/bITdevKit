// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises SQL Server blob error classification and retention recovery.</summary>
/// <example><code>new SqlServerBlobErrorsTests().ProviderError_WithSqlErrorNumber_ClassifiesFailure(1205, true);</code></example>
[UnitTest("Infrastructure")]
public sealed class SqlServerBlobErrorsTests
{
    [Theory]
    [InlineData(1205, true)]
    [InlineData(-2, true)]
    [InlineData(1222, true)]
    [InlineData(233, true)]
    [InlineData(64, true)]
    [InlineData(121, true)]
    [InlineData(10053, true)]
    [InlineData(10054, true)]
    [InlineData(10060, true)]
    [InlineData(10928, true)]
    [InlineData(10929, true)]
    [InlineData(40197, true)]
    [InlineData(40501, true)]
    [InlineData(40613, true)]
    [InlineData(49918, true)]
    [InlineData(49919, true)]
    [InlineData(49920, true)]
    [InlineData(102, false)]
    [InlineData(208, false)]
    [InlineData(547, false)]
    public void ProviderError_WithSqlErrorNumber_ClassifiesFailure(int number, bool transient)
    {
        var exception = SqlServerTestException.Create(number);

        SqlServerBlobErrors.IsTransient(exception).ShouldBe(transient);
        SqlServerBlobErrors.ProviderError(exception).IsTransient.ShouldBe(transient);
    }

    [Fact]
    public void IsTransient_WithWrappedDeadlockOrTimeout_ChecksAllInnerExceptions()
    {
        SqlServerBlobErrors.IsTransient(new DbUpdateException("Save failed.", SqlServerTestException.Create(1205))).ShouldBeTrue();
        SqlServerBlobErrors.IsTransient(new InvalidOperationException("Wrapped.", new TimeoutException())).ShouldBeTrue();
        SqlServerBlobErrors.IsTransient(new InvalidOperationException("Permanent.")).ShouldBeFalse();
        SqlServerBlobErrors.IsTransient(SqlServerTestException.Create(208, 1205)).ShouldBeTrue();
    }

    /// <summary>Checks that retention skips an aborted delete and reports permanent SQL failures.</summary>
    /// <example><code>await tests.SweepExpiredAsync_WhenDeleteFails_SkipsOnlyTransientErrors(1205, true);</code></example>
    [Theory]
    [InlineData(1205, true)]
    [InlineData(547, false)]
    public async Task SweepExpiredAsync_WhenDeleteFails_SkipsOnlyTransientErrors(int number, bool transient)
    {
        var cutoff = DateTimeOffset.UtcNow;
        var victim = new BlobKey("reports", "victim.txt");
        var other = new BlobKey("reports", "other.txt");
        var fault = new BlobDeleteFailure(victim, SqlServerTestException.Create(number));
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection()
            .AddDbContext<RetentionContext>(options => options.UseSqlite(connection).AddInterceptors(fault))
            .BuildServiceProvider();
        await using var verification = services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<RetentionContext>();
        await context.Database.EnsureCreatedAsync();
        var sut = new EntityFrameworkBlobStoreProvider<RetentionContext>(services.GetRequiredService<IServiceScopeFactory>());
        foreach (var (key, expiresAt) in new[] { (victim, cutoff.AddMinutes(-2)), (other, cutoff.AddMinutes(-1)) })
        {
            using var content = new MemoryStream([1, 2, 3]);
            var uploaded = await sut.UploadAsync(new() { Key = key, Content = content, ExpiresAt = expiresAt });
            uploaded.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, uploaded.Errors.Select(error => error.Message)));
        }

        var request = new BlobRetentionSweepRequest { ExpiresOnOrBefore = cutoff, BatchSize = 10, MaxBatches = 1 };
        var result = await sut.SweepExpiredAsync(request);
        var remaining = await context.StorageBlobs.AsNoTracking().ToArrayAsync();

        if (!transient)
        {
            result.IsFailure.ShouldBeTrue();
            var error = result.Errors.Single().ShouldBeOfType<BlobStoreProviderError>();
            error.IsTransient.ShouldBeFalse();
            remaining.Length.ShouldBe(2);
            return;
        }

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        result.Value.DeletedCount.ShouldBe(1);
        result.Value.DeletedKeys.ShouldBe([other]);
        result.Value.SkippedCount.ShouldBe(1);
        var skipped = remaining.Single();
        skipped.Name.ShouldBe(victim.Name);
        skipped.LeaseAcquiredUntil.ShouldBeNull();

        var retried = await sut.SweepExpiredAsync(request);
        retried.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, retried.Errors.Select(error => error.Message)));
        retried.Value.DeletedKeys.ShouldBe([victim]);
        (await context.StorageBlobs.AnyAsync()).ShouldBeFalse();
    }

    private sealed class RetentionContext(DbContextOptions<RetentionContext> options) : DbContext(options), IBlobStoreContext
    {
        /// <summary>Gets the rows tested by the retention sweep.</summary>
        /// <example><code>var rows = await context.StorageBlobs.ToListAsync();</code></example>
        public DbSet<StorageBlob> StorageBlobs { get; set; }

        /// <summary>Gets the content chunks deleted by the blob foreign key cascade.</summary>
        /// <example><code>var count = await context.StorageBlobChunks.CountAsync();</code></example>
        public DbSet<StorageBlobChunk> StorageBlobChunks { get; set; }
    }

    private sealed class BlobDeleteFailure(BlobKey victim, SqlException failure) : SaveChangesInterceptor
    {
        private bool injected;

        /// <summary>Injects a SQL error on the first delete of the selected blob.</summary>
        /// <example><code>options.AddInterceptors(new BlobDeleteFailure(key, sqlException));</code></example>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!this.injected && eventData.Context.ChangeTracker.Entries<StorageBlob>().Any(entry =>
                entry.State == EntityState.Deleted &&
                entry.Entity.Container == victim.Container && entry.Entity.Name == victim.Name))
            {
                this.injected = true;
                throw new DbUpdateException("Injected blob delete failure.", failure);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
