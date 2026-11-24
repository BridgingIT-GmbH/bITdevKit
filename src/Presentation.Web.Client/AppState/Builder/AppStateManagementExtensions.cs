// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>Registers runtime-local state services without requiring browser or authentication services.</summary>
/// <example>
/// <code><![CDATA[
/// services.AddAppState()
///     .WithApplicationId("catalog-console")
///     .WithDebugging(debug =>
///     {
///         debug.LoggingEnabled = true;
///         debug.StateChangesTracked = true;
///         debug.MaxTrackedChanges = 100;
///     })
///     .WithDefaultDebounceDelay(TimeSpan.FromMilliseconds(300))
///     .AddState<CatalogViewState>()
///         .WithIdentity("catalog", "list")
///         .WithJsonTypeInfo(CatalogJson.Default.CatalogViewModel)
///         .AsInMemoryScoped()
///         .WithHistory(10)
///         .Done();
/// ]]></code>
/// </example>
public static class AppStateManagementExtensions
{
    /// <summary>Adds idempotent infrastructure and returns ordinary fluent state configuration.</summary>
    /// <param name="services">The originating runtime's service collection.</param>
    /// <returns>The shared configuration builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState()
    ///     .WithApplicationId("catalog-console")
    ///     .WithDebugging(debug =>
    ///     {
    ///         debug.LoggingEnabled = true;
    ///         debug.StateChangesTracked = true;
    ///         debug.MaxTrackedChanges = 100;
    ///     })
    ///     .WithDefaultDebounceDelay(TimeSpan.FromMilliseconds(300))
    ///     .AddState<CatalogViewState>()
    ///         .WithIdentity("catalog", "list")
    ///         .WithJsonTypeInfo(CatalogJson.Default.CatalogViewModel)
    ///         .AsInMemoryScoped()
    ///         .WithHistory(10)
    ///         .Done();
    /// ]]></code>
    /// </example>
    public static AppStateManagementBuilder AddAppState(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton(new AppStateDebugOptions());
        services.TryAddScoped(provider => new AppStateDebugger(provider.GetRequiredService<ILogger<AppStateDebugger>>(),
            provider.GetService<IJSRuntime>(), provider.GetRequiredService<AppStateDebugOptions>()));
        services.TryAddScoped<IUserContextProvider>(provider => new LocalStorageUserContextProvider(provider.GetService<IJSRuntime>(),
            provider.GetService<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(),
            provider.GetRequiredService<AppStateManagementOptions>().ApplicationId));
        services.TryAddScoped(provider => new LocalStorageStateStoreProvider(provider.GetService<IJSRuntime>(),
            provider.GetRequiredService<AppStateDebugger>()));
        services.TryAddScoped<AppStateManager>();
        services.TryAddScoped<AppStateFactory>();
        return new AppStateManagementBuilder(services);
    }
}
