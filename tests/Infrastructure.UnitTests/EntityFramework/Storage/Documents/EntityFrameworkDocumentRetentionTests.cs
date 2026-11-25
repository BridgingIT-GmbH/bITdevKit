// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises EF document retention when a delete in a batch fails.</summary>
/// <example><code>await new EntityFrameworkDocumentRetentionTests().SweepExpiredAsync_WhenDeleteFails_OnlyRecoversFromContention(1205, true);</code></example>
[UnitTest("Infrastructure")]
public sealed class EntityFrameworkDocumentRetentionTests
{
    /// <summary>Verifies that recoverable failures leave the affected row due and permanent failures fail the sweep.</summary>
    /// <example><code>await tests.SweepExpiredAsync_WhenDeleteFails_OnlyRecoversFromContention(547, false);</code></example>
    [Theory]
    [InlineData(1205, true)]
    [InlineData(0, true)]
    [InlineData(547, false)]
    public async Task SweepExpiredAsync_WhenDeleteFails_OnlyRecoversFromContention(int number, bool recoverable)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(10);
        var type = DocumentTypeIdentity.For<Receipt>();
        var victim = new DocumentKey("archive", "victim");
        var older = new DocumentKey("archive", "older");
        var newer = new DocumentKey("archive", "newer");
        var fault = new DeleteFailure(victim, cutoff.AddHours(1), number == 0
            ? null
            : new DbUpdateException("Injected document delete failure.", SqlServerTestException.Create(number)));

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection()
            .AddDbContext<RetentionContext>(options => options.UseSqlite(connection).AddInterceptors(fault))
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RetentionContext>();
        await context.Database.EnsureCreatedAsync();
        var sut = new EntityFrameworkDocumentStoreProvider<RetentionContext>(services.GetRequiredService<IServiceScopeFactory>());
        var client = new DocumentStoreClient<Receipt>(sut);
        foreach (var (key, minutesAgo) in new[] { (victim, 3), (older, 2), (newer, 1) })
        {
            var written = await client.UpsertAsync(key, new(), new() { Expiration = ExpirationChange.At(cutoff.AddMinutes(-minutesAgo)) });
            written.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, written.Errors.Select(error => error.Message)));
        }

        var request = new DocumentRetentionSweepRequest { DocumentType = type, VisibilityCutoff = cutoff, BatchSize = 2, MaxBatches = 3 };
        var result = await sut.SweepExpiredAsync(request);
        var remaining = await context.StorageDocuments.AsNoTracking().Select(row => row.RowKey).ToArrayAsync();

        if (!recoverable)
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.Single().ShouldBeOfType<DocumentStoreProviderError>();
            remaining.Length.ShouldBe(3);
            return;
        }

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        result.Value.DeletedCount.ShouldBe(2);
        result.Value.DeletedKeys.ShouldBe([older, newer]);
        result.Value.HasMore.ShouldBeTrue();
        remaining.ShouldBe([victim.RowKey]);

        fault.Enabled = false;
        var later = await sut.SweepExpiredAsync(request);
        later.IsSuccess.ShouldBeTrue();
        if (number == 0)
        {
            later.Value.DeletedKeys.ShouldBeEmpty();
            var revived = await context.StorageDocuments.AsNoTracking().SingleAsync();
            revived.RowKey.ShouldBe(victim.RowKey);
            revived.ExpiresAtUnixMilliseconds.ShouldBe(cutoff.AddHours(1).ToUnixTimeMilliseconds());
        }
        else
        {
            later.Value.DeletedKeys.ShouldBe([victim]);
            (await context.StorageDocuments.AnyAsync()).ShouldBeFalse();
        }
    }

    private sealed class Receipt
    {
        /// <summary>Gets the serialized test receipt amount.</summary>
        /// <example><code>var receipt = new Receipt { Amount = 10 };</code></example>
        public int Amount { get; init; } = 10;
    }

    private sealed class RetentionContext(DbContextOptions<RetentionContext> options) : DbContext(options), IDocumentStoreContext
    {
        /// <summary>Gets the physical records inspected after a sweep.</summary>
        /// <example><code>var rows = await context.StorageDocuments.ToListAsync();</code></example>
        public DbSet<StorageDocument> StorageDocuments { get; set; }
    }

    private sealed class DeleteFailure(DocumentKey victim, DateTimeOffset revivedExpiration, Exception failure) : SaveChangesInterceptor
    {
        /// <summary>Gets or sets whether to fail deletion of the selected record.</summary>
        /// <example><code>fault.Enabled = false;</code></example>
        public bool Enabled { get; set; } = true;

        /// <summary>Fails a selected delete with a SQL error or revives the row to cause an optimistic concurrency conflict.</summary>
        /// <example><code>options.AddInterceptors(new DeleteFailure(key, expiresAt, exception));</code></example>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            var deleted = this.Enabled ? context.ChangeTracker.Entries<StorageDocument>().SingleOrDefault(entry =>
                entry.State == EntityState.Deleted && entry.Entity.PartitionKey == victim.PartitionKey && entry.Entity.RowKey == victim.RowKey) : null;
            if (deleted is null)
            {
                return result;
            }

            if (failure is not null)
            {
                throw failure;
            }

            this.Enabled = false;
            var expiry = revivedExpiration.ToUnixTimeMilliseconds();
            var version = Guid.NewGuid();
            await context.Set<StorageDocument>().Where(row => row.Id == deleted.Entity.Id)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(row => row.ExpiresAtUnixMilliseconds, expiry)
                    .SetProperty(row => row.ConcurrencyVersion, version), cancellationToken);
            return result;
        }
    }
}
