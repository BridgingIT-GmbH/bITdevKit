// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

public sealed partial class StorageDimensionsMutationProofTests
{
    /// <summary>Verifies that pooled SQL Server contexts can update and replace registered document dimensions.</summary>
    /// <example><code>await tests.DocumentDimensionMutations_WithPooledSqlServerContext_CommitNewNames();</code></example>
    [Fact]
    public async Task DocumentDimensionMutations_WithPooledSqlServerContext_CommitNewNames()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sql.StartAsync();
        await using var services = await CreateDocumentServicesAsync(sql.GetConnectionString(), "pooled_document_dimensions");
        var sut = new DocumentStoreClient<Receipt>(new EntityFrameworkDocumentStoreProvider<MutationContext>(
            services.GetRequiredService<IServiceScopeFactory>()));
        var key = new DocumentKey("archive", "receipt");
        var created = await sut.UpsertAsync(key, new() { Amount = 1 });
        created.IsSuccess.ShouldBeTrue(Errors(created));

        var updated = await sut.UpdateDimensionsAsync(new()
        {
            Key = key,
            IfMatchETag = created.Value.ETag,
            Dimensions = DocumentValues(("FiscalYear", 2026))
        });
        updated.IsSuccess.ShouldBeTrue(Errors(updated));

        var overwritten = await sut.UpsertAsync(key, new() { Amount = 2 }, new()
        {
            IfMatchETag = updated.Value.ETag,
            Dimensions = DocumentValues(("Approved", true))
        });
        overwritten.IsSuccess.ShouldBeTrue(Errors(overwritten));

        var stored = await sut.GetAsync(key);
        stored.IsSuccess.ShouldBeTrue(Errors(stored));
        stored.Value.Dimensions.Count.ShouldBe(1);
        stored.Value.Dimensions["Approved"].Value.ShouldBe(true);
    }

    /// <summary>Verifies that paging visits every key that compares equal under a case-insensitive SQL Server collation.</summary>
    /// <example><code>await tests.DocumentPaging_WithCollationEquivalentKeys_ReturnsEveryKey();</code></example>
    [Fact]
    public async Task DocumentPaging_WithCollationEquivalentKeys_ReturnsEveryKey()
    {
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("MSSQL_COLLATION", "SQL_Latin1_General_CP1_CI_AS")
            .Build();
        await sql.StartAsync();
        await using var services = await CreateDocumentServicesAsync(sql.GetConnectionString(), "document_paging", pooled: false);
        var sut = new DocumentStoreClient<Receipt>(new EntityFrameworkDocumentStoreProvider<MutationContext>(
            services.GetRequiredService<IServiceScopeFactory>()), options: new DocumentStoreOptions { AllowFullScans = true });
        DocumentKey[] keys = [new("Archive", "A"), new("Archive", "a"), new("archive", "A"), new("Archive", "B")];
        foreach (var key in keys)
        {
            var stored = await sut.UpsertAsync(key, new() { Amount = 1 }, new() { Dimensions = DocumentValues(("Approved", true)) });
            stored.IsSuccess.ShouldBeTrue(Errors(stored));
        }

        var approved = DocumentDimensionPredicate.Create("Approved", DocumentDimensionOperator.Equal, true).Value;
        var dimensionFilter = DocumentDimensionFilter.Where(approved);
        var listed = new List<DocumentKey>();
        var found = new List<DocumentKey>();
        string listToken = null;
        string findToken = null;
        for (var page = 0; page < keys.Length; page++)
        {
            if (page == 0 || listToken is not null)
            {
                var result = await sut.ListPageAsync(new() { AllowFullScan = true, Take = 1, ContinuationToken = listToken });
                result.IsSuccess.ShouldBeTrue(Errors(result));
                listed.AddRange(result.Value.Items);
                listToken = result.Value.ContinuationToken;
            }

            if (page == 0 || findToken is not null)
            {
                var result = await sut.FindPageAsync(new() { DimensionFilter = dimensionFilter, Take = 1, ContinuationToken = findToken });
                result.IsSuccess.ShouldBeTrue(Errors(result));
                found.AddRange(result.Value.Items.Select(item => item.Key));
                findToken = result.Value.ContinuationToken;
            }
        }

        listed.ShouldBe(keys, ignoreOrder: true);
        found.ShouldBe(listed);
        listToken.ShouldBeNull();
        findToken.ShouldBeNull();
    }

    private static async Task<ServiceProvider> CreateDocumentServicesAsync(string connection, string database, bool pooled = true)
    {
        var connectionString = new SqlConnectionStringBuilder(connection) { InitialCatalog = database }.ConnectionString;
        var registrations = new ServiceCollection();
        if (pooled)
        {
            registrations.AddDbContextPool<MutationContext>(options => options.UseSqlServer(connectionString), poolSize: 2);
        }
        else
        {
            registrations.AddDbContext<MutationContext>(options => options.UseSqlServer(connectionString));
        }

        var services = registrations.BuildServiceProvider();
        try
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<MutationContext>().Database.EnsureCreatedAsync();
            return services;
        }
        catch
        {
            await services.DisposeAsync();
            throw;
        }
    }
}
