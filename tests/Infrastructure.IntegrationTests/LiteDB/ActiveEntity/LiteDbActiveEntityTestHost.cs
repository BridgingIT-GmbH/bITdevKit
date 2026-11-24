// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>Creates an isolated, disposable LiteDB host without external-service fixtures.</summary>
public sealed class LiteDbActiveEntityTestHost : IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-litedb-activeentity-");
    private readonly AsyncServiceScope scope;

    /// <summary>Registers the test entities against one owned database and creates a scoped provider.</summary>
    /// <param name="configure">Optional ledger provider settings.</param>
    /// <param name="file">An external file to preserve across host instances, or null for an isolated temporary file.</param>
    /// <param name="configureDatabase">Optional database and mapper settings.</param>
    /// <param name="configureServices">Additional services or behaviors for a scenario.</param>
    public LiteDbActiveEntityTestHost(Action<LiteDbActiveEntityProviderOptions<LedgerEntry>> configure = null,
        string file = null, Action<LiteDbDatabaseOptions> configureDatabase = null, Action<IServiceCollection> configureServices = null)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        this.File = file ?? Path.Combine(this.directory.FullName, "ledger.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLiteDbDatabase(options =>
        {
            options.ConnectionString = this.File;
            configureDatabase?.Invoke(options);
        });
        services.AddActiveEntity(configuration =>
        {
            configuration.For<LedgerEntry, Guid>().UseLiteDbProvider(options =>
            {
                options.CollectionName = "ledger";
                configure?.Invoke(options);
            });
            configuration.For<TypedLedgerEntry, TypedLedgerEntryId>().UseLiteDbProvider();
            configuration.For<IntegerLedgerEntry, IntegerLedgerEntryId>().UseLiteDbProvider();
            configuration.For<LongLedgerEntry, LongLedgerEntryId>().UseLiteDbProvider();
            configuration.For<StringLedgerEntry, StringLedgerEntryId>().UseLiteDbProvider();
            configuration.For<LedgerItem<Guid>, Guid>().UseLiteDbProvider(options => options.CollectionName = "references");
            configuration.For<LedgerItem<int>, int>().UseLiteDbProvider();
            configuration.For<LedgerItem<long>, long>().UseLiteDbProvider();
            configuration.For<LedgerItem<string>, string>().UseLiteDbProvider();
        });
        configureServices?.Invoke(services);
        this.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.scope = this.Services.CreateAsyncScope();
        this.Provider = this.Get<LedgerEntry, Guid>();
    }

    /// <summary>Gets the physical database path used by the host.</summary>
    public string File { get; }

    /// <summary>Gets the root service provider, which owns the native database worker.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Gets the scoped ledger provider under test.</summary>
    public IActiveEntityEntityProvider<LedgerEntry, Guid> Provider { get; }

    /// <summary>Resolves another entity provider in the host's existing transaction session.</summary>
    /// <typeparam name="TEntity">The test entity type.</typeparam>
    /// <typeparam name="TId">Its identifier type.</typeparam>
    /// <returns>The provider in the host's DI scope.</returns>
    public IActiveEntityEntityProvider<TEntity, TId> Get<TEntity, TId>() where TEntity : class, IEntity =>
        this.scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<TEntity, TId>>();

    /// <summary>Inserts fictional ledger data and requires every insert to succeed.</summary>
    /// <param name="values">Names and amounts to persist.</param>
    /// <returns>A task representing seed completion.</returns>
    public async Task SeedAsync(params (string Name, int Amount)[] values)
    {
        foreach (var value in values)
        {
            var result = await this.Provider.InsertAsync(new LedgerEntry { Name = value.Name, Amount = value.Amount });
            result.IsSuccess.ShouldBeTrue(string.Join(",", result.Errors.Select(e => e.Message)));
        }
    }

    /// <summary>Sets the serialized test's global ActiveEntity host until the returned guard is disposed.</summary>
    /// <returns>A guard restoring the previous global service provider.</returns>
    public IDisposable ActivateGlobal()
    {
        var previous = ActiveEntityConfigurator.GetGlobalServiceProvider();
        ActiveEntityConfigurator.SetGlobalServiceProvider(this.Services);
        return new RestoreGlobal(previous);
    }

    private sealed class RestoreGlobal(IServiceProvider previous) : IDisposable
    {
        public void Dispose()
        {
            // Test-only restoration also handles an initially unconfigured global provider.
            typeof(ActiveEntityConfigurator).GetField("serviceProvider", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, previous);
        }
    }

    /// <summary>Releases the scope, stops the database worker, and deletes this host's private temporary directory.</summary>
    /// <returns>A task representing cleanup.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.scope.DisposeAsync();
        await this.Services.DisposeAsync();
        this.directory.Delete(true);
    }
}
