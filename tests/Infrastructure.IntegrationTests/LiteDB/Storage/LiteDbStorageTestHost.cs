// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Storage;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>Owns a disposable native storage namespace and deterministic operation clock.</summary>
public sealed class LiteDbStorageTestHost : IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-storage-");
    /// <summary>Registers native storage using an explicit disposable-data prerelease acknowledgement.</summary>
    /// <param name="file">An optional external file for reopen tests.</param>
    /// <param name="configure">Additional registrations.</param>
    public LiteDbStorageTestHost(string file = null, Action<IServiceCollection> configure = null)
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        this.File = file ?? Path.Combine(this.directory.FullName, "storage.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(this.Clock);
        services.AddLiteDbDatabase(options => options.ConnectionString = this.File);
        services.AddLiteDbDocumentStore();
        services.AddLiteDbFileStorage();
        services.AddStoragePermalinks().UseLiteDb();
        services.AddCaching().WithLiteDbDocumentStoreProvider();
        services.AddFileStorage(factory =>
        {
            factory.RegisterProvider("first", files => files.UseLiteDb("first"));
            factory.RegisterProvider("second", files => files.UseLiteDb("second"));
            factory.RegisterProvider("linked", files => files.UseLiteDb("linked").WithPermalinks());
        });
        configure?.Invoke(services);
        this.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.Scope = this.Services.CreateAsyncScope();
    }

    /// <summary>Gets the file used by the native owner.</summary>
    public string File { get; }

    /// <summary>Gets the root services.</summary>
    public ServiceProvider Services { get; }

    /// <summary>Gets the active consumer scope.</summary>
    public AsyncServiceScope Scope { get; }

    /// <summary>Gets the test clock.</summary>
    public StorageClock Clock { get; } = new();
    /// <summary>Gets the document provider.</summary>
    public LiteDbDocumentStoreProvider Documents => this.Services.GetRequiredService<LiteDbDocumentStoreProvider>();
    /// <summary>Gets the registry provider.</summary>
    public IStoragePermalinkRegistryProvider Permalinks => this.Services.GetRequiredService<IStoragePermalinkRegistryProvider>();
    /// <summary>Gets the distributed-cache facade.</summary>
    public IDistributedCache Cache => this.Scope.ServiceProvider.GetRequiredService<IDistributedCache>();
    /// <summary>Gets the existing Common Caching adapter.</summary>
    public ICacheProvider CommonCache => this.Scope.ServiceProvider.GetRequiredService<ICacheProvider>();
    /// <summary>Resolves a provider through the existing file factory.</summary>
    /// <param name="name">The configured provider name.</param>
    /// <returns>The selected provider and its behaviors.</returns>
    public IFileStorageProvider Files(string name = "first") => this.Services.GetRequiredService<IFileStorageProviderFactory>().CreateProvider(name);
    /// <summary>Releases scoped clients and the native owner before deleting disposable files.</summary>
    /// <returns>The cleanup operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.Scope.DisposeAsync();
        await this.Services.DisposeAsync();
        this.directory.Delete(true);
    }
}

/// <summary>Provides deterministic expiration and fence timestamps.</summary>
public sealed class StorageClock : TimeProvider
{
    /// <summary>Gets or sets the current UTC instant.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => this.Now;
}
