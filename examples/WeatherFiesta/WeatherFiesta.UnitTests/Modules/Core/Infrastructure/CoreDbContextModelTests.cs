// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Infrastructure;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

/// <summary>Checks the module-owned cache schema and independent contexts from its SQL Server registration.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CoreDbContextModelTests</code></example>
public sealed class CoreDbContextModelTests
{
    /// <summary>Factory calls return separate contexts with the enriched annotated cache model and no automatic retries.</summary>
    [Fact]
    public void CreateDbContext_RepeatedCalls_ReturnsIndependentCacheCapableContexts()
    {
        var services = new ServiceCollection();
        services.AddSqlServerDbContext<CoreDbContext>("Server=localhost;Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True")
            .WithDbContextFactory();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var sut = provider.GetRequiredService<IDbContextFactory<CoreDbContext>>();

        using var first = sut.CreateDbContext();
        using var second = sut.CreateDbContext();

        first.ShouldNotBeSameAs(second);
        first.ShouldBeAssignableTo<IApplicationCachingContext>();
        first.Model.FindEntityType(typeof(CacheEntryEntity)).GetTableName().ShouldBe("__Caching_Entries");
        first.Model.GetEntityTypes().Count(entity => entity.ClrType.Namespace == typeof(CacheEntryEntity).Namespace).ShouldBe(12);
        first.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeFalse();
    }
}