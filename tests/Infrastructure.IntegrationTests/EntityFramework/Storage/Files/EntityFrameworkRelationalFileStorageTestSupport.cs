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

public sealed class EntityFrameworkRelationalFileStorageTestSupport : IDisposable
{
    public EntityFrameworkRelationalFileStorageTestSupport(
        ITestOutputHelper output,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        this.LoggerFactory = XunitLoggerFactory.Create(output);
        this.DefaultOptions = EntityFrameworkSqliteFileStorageTestSupport.CreateOptions();

        var services = new ServiceCollection();
        services.AddSingleton(this.LoggerFactory);
        services.AddDbContext<StubDbContext>(configureDbContext);

        this.ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = this.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        dbContext.Database.EnsureCreated();
    }

    public EntityFrameworkFileStorageOptions DefaultOptions { get; }

    public ILoggerFactory LoggerFactory { get; }

    public ServiceProvider ServiceProvider { get; }

    public IFileStorageProvider CreateInMemoryProvider(string locationName = null) =>
        new LoggingFileStorageBehavior(
            new InMemoryFileStorageProvider(locationName ?? $"InMemory-{Guid.NewGuid():N}"),
            this.LoggerFactory);

    public EntityFrameworkFileStorageProvider<StubDbContext> CreateProvider(
        string locationName = null,
        EntityFrameworkFileStorageOptions options = null)
        => new(
            this.ServiceProvider,
            this.LoggerFactory,
            locationName ?? $"RelationalFiles-{Guid.NewGuid():N}",
            options: options ?? this.DefaultOptions);

    public void Dispose()
    {
        this.ServiceProvider.Dispose();
        this.LoggerFactory.Dispose();
    }
}
