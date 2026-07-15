// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class PlaceholderEntityBulkInsertProviderTests
{
    [Fact]
    public async Task InsertAsync_PostgresContext_UsesPlaceholderProviderAndReturnsNotImplementedFailure()
    {
        // Arrange
        using var serviceProvider = CreatePostgresServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IEntityBulkInsertProvider>().ToList();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertPersonStub>>();

        // Act
        var result = await sut.InsertAsync([CreateEntity()]);

        // Assert
        providers.ShouldHaveSingleItem().ShouldBeOfType<PostgresEntityBulkInsertProvider>();
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error.Message.Contains("PostgreSQL entity bulk insert is not implemented yet.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InsertAsync_SqliteContext_UsesPlaceholderProviderAndReturnsNotImplementedFailure()
    {
        // Arrange
        using var serviceProvider = CreateSqliteServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var providers = scope.ServiceProvider.GetServices<IEntityBulkInsertProvider>().ToList();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<BulkInsertPersonStub>>();

        // Act
        var result = await sut.InsertAsync([CreateEntity()]);

        // Assert
        providers.ShouldHaveSingleItem().ShouldBeOfType<SqliteEntityBulkInsertProvider>();
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error.Message.Contains("SQLite entity bulk insert is not implemented yet.", StringComparison.Ordinal));
    }

    private static ServiceProvider CreatePostgresServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresDbContext<StubDbContext>("Host=localhost;Database=bulk;Username=bulk;Password=bulk");
        services.AddEntityFrameworkRepository<BulkInsertPersonStub, StubDbContext>()
            .WithBulkInsert();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ServiceProvider CreateSqliteServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqliteDbContext<StubDbContext>("Data Source=:memory:");
        services.AddEntityFrameworkRepository<BulkInsertPersonStub, StubDbContext>()
            .WithBulkInsert();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static BulkInsertPersonStub CreateEntity()
    {
        return new BulkInsertPersonStub
        {
            FirstName = "Bulk",
            LastName = "Placeholder",
            Age = 42
        };
    }
}
