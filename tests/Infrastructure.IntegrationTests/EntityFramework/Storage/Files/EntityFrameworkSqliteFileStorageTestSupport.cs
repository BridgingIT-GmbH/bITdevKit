// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;
using Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class EntityFrameworkSqliteFileStorageTestSupport : IDisposable
{
    private readonly string databasePath;

    public EntityFrameworkSqliteFileStorageTestSupport(ITestOutputHelper output)
    {
        this.databasePath = Path.Combine(AppContext.BaseDirectory, $"entity-framework-file-storage-provider-{Guid.NewGuid():N}.db");
        this.LoggerFactory = XunitLoggerFactory.Create(output);
        this.DefaultOptions = CreateOptions();

        var services = new ServiceCollection();
        services.AddSingleton(this.LoggerFactory);
        services.AddDbContext<StubDbContext>(options => options.UseSqlite($"Data Source={this.databasePath}"));

        this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
    }

    public EntityFrameworkFileStorageOptions DefaultOptions { get; }

    public ILoggerFactory LoggerFactory { get; }

    public ServiceProvider ServiceProvider { get; }

    public IFileStorageProvider CreateInMemoryProvider(string locationName = "InMemory") =>
        new LoggingFileStorageBehavior(new InMemoryFileStorageProvider(locationName), this.LoggerFactory);

    public EntityFrameworkFileStorageProvider<StubDbContext> CreateProvider(
        string locationName = "SqliteFiles",
        EntityFrameworkFileStorageOptions options = null)
        => new(this.ServiceProvider, this.LoggerFactory, locationName, options: options ?? this.DefaultOptions);

    public static EntityFrameworkFileStorageOptions CreateOptions(int pageSize = 2)
        => new EntityFrameworkFileStorageOptionsBuilder()
            .LeaseDuration(TimeSpan.Zero)
            .PageSize(pageSize)
            .Build();

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
        this.LoggerFactory.Dispose();

        try
        {
            if (File.Exists(this.databasePath))
            {
                File.Delete(this.databasePath);
            }
        }
        catch (IOException)
        {
        }
    }
}
