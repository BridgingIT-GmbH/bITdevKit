// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Client.AppState;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies deterministic registration, provider aliases, and invalid configuration.</summary>
public class AppStateRegistrationTests
{
    /// <summary>Infrastructure registration is idempotent and state duplicates are rejected.</summary>
    [Fact]
    public void AddAppState_RepeatedInfrastructure_DoesNotDuplicateOwners()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().Done();
        var builder = services.AddAppState();
        services.Count(d => d.ServiceType == typeof(AppStateManager)).ShouldBe(1);
        Should.Throw<ArgumentException>(() => builder.AddState<CatalogViewState>());
    }

    /// <summary>Singleton state cannot capture circuit-local dependencies.</summary>
    [Fact]
    public void AddState_Singleton_RejectsUnsafeLifetime()
    {
        var services = new ServiceCollection().AddLogging();
        Should.Throw<ArgumentException>(() => services.AddAppState().AddState<CatalogViewState>(ServiceLifetime.Singleton));
    }

    /// <summary>Provider aliases resolve the same scoped concrete object.</summary>
    [Fact]
    public void WithPersistence_MemoryProvider_UsesOneScopedInstance()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().AsInMemoryScoped().Done();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SessionScopedStateStoreProvider>()
            .ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<IAppStateStoreProvider>());
        scope.ServiceProvider.GetRequiredService<CatalogViewState>().ShouldNotBeNull();
    }

    /// <summary>Stable instance collisions are diagnosed before one state can overwrite another.</summary>
    [Fact]
    public void Create_DuplicatePersistedKey_RejectsCompetingOwner()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAppState().AddState<CatalogViewState>().AsInMemoryScoped().Done();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
        using var first = factory.Create<CatalogViewState>("pane");
        Should.Throw<InvalidOperationException>(() => factory.Create<CatalogViewState>("pane"));
        first.Dispose();
        using var remounted = factory.Create<CatalogViewState>("pane");
        remounted.InstanceId.ShouldNotBe(first.InstanceId);
    }
}
