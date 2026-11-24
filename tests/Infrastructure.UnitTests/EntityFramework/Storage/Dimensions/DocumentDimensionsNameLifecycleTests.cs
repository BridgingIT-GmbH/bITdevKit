// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Application.Storage;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

public sealed partial class StorageDimensionsMutationTests
{
    [Fact]
    public async Task DocumentNameCache_SeparatesPhysicalStoresAndClearsAfterRecreation()
    {
        var first = Path.Combine(Path.GetTempPath(), $"bdk-dim-first-{Guid.NewGuid():N}.db");
        var second = Path.Combine(Path.GetTempPath(), $"bdk-dim-second-{Guid.NewGuid():N}.db");
        var current = first;
        try
        {
            await using var services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite($"Data Source={current};Pooling=False")).BuildServiceProvider();
            var scopes = services.GetRequiredService<IServiceScopeFactory>();
            var provider = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(scopes);
            await Create(false);
            (await Write()).IsSuccess.ShouldBeTrue();
            current = second;
            await Create(true);
            (await Write()).IsSuccess.ShouldBeTrue();
            await Verify();
            current = first;
            await Create(true);
            provider.ClearDimensionNameCache();
            (await Write()).IsSuccess.ShouldBeTrue();
            await Verify();

            async Task Create(bool dummy)
            {
                await using var scope = services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                if (dummy)
                {
                    var logicalScope = DocumentTypeIdentity.For<Invoice>().Value;
                    context.Add(new StorageDocumentDimensionName { Scope = logicalScope, ScopeHash = SHA256.HashData(Encoding.UTF8.GetBytes(logicalScope)), Name = "Dummy", NameHash = SHA256.HashData("DUMMY"u8) });
                    await context.SaveChangesAsync();
                }
            }

            async Task<Result<DocumentInfo>> Write()
            {
                return await new DocumentStoreClient<Invoice>(provider).UpsertAsync(new("archive", "owner"), new Invoice { Amount = 1 }, new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["SharedName"] = 7 }).Value });
            }

            async Task Verify()
            {
                await using var scope = services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<DimensionsContext>();
                var row = await context.Set<StorageDocumentDimension>().Include(value => value.Name).SingleAsync();
                row.Name.Name.ShouldBe("SharedName");
                row.NameId.ShouldBe(2);
            }
        }
        finally
        {
            foreach (var file in new[] { first, second }) foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(file + suffix)) File.Delete(file + suffix);
        }
    }

    [Fact]
    public async Task DocumentRegistration_StopsAfterThreeUniqueConflictsAndCanRecover()
    {
        var fault = new DocumentRegistrationFault();
        await using var services = new ServiceCollection().AddDbContext<DimensionsContext>(options => options.UseSqlite(this.connection).AddInterceptors(fault)).BuildServiceProvider();
        var provider = new EntityFrameworkDocumentStoreProvider<DimensionsContext>(services.GetRequiredService<IServiceScopeFactory>());
        var failed = await Write();
        failed.IsFailure.ShouldBeTrue();
        fault.Attempts.ShouldBe(3);
        fault.Enabled = false;
        var recovered = await Write();
        recovered.IsSuccess.ShouldBeTrue(string.Join(";", recovered.Errors.Select(error => error.Message)));
        using var scope = services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<DimensionsContext>().Set<StorageDocumentDimensionName>().CountAsync()).ShouldBe(1);

        async Task<Result<DocumentInfo>> Write()
        {
            return await new DocumentStoreClient<Invoice>(provider).UpsertAsync(new("archive", "owner"), new Invoice { Amount = 1 }, new() { Dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["SharedName"] = 7 }).Value });
        }
    }

    private sealed class DocumentRegistrationFault : DbCommandInterceptor
    {
        public bool Enabled { get; set; } = true;

        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (this.Enabled && command.CommandText.Contains("INSERT INTO \"__Storage_DocumentDimensionNames\""))
            {
                this.Attempts++;
                throw new SqliteException("Injected competing registration.", 19, 2067);
            }

            return ValueTask.FromResult(result);
        }
    }
}
