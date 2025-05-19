namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Provides extension methods for IServiceCollection to configure the ManagedState system.
/// </summary>
/// <example>
/// services.AddAppState()
///     .WithDebugging(debug =>
///     {
///         debug.EnableLogging = true;
///         debug.TrackStateChanges = true;
///     })
///     .WithDefaultDebounceDelay(TimeSpan.FromMilliseconds(300))
///     .AddState<CounterState>()
///         .AsSessionScoped()
///         .WithHistory(10).Done()
///     .AddState<FilterState>()
///         .AsLocalStorageScoped()
///         .WithDebounceDelay(TimeSpan.FromMilliseconds(500)).Done();
///     .AddCustomStateStoreProvider<CustomStorageStateStoreProvider>();
/// </example>
public static class AppStateManagementExtensions
{
    /// <summary>
    /// Adds the ManagedState system to the service collection with default configurations.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <returns>A StateManagementBuilder for further configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown if services is null.</exception>
    public static AppStateManagementBuilder AddAppState(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register core services with sane defaults
        services.AddScoped<AppStateDebugger>();
        services.AddScoped<IUserContextProvider, LocalStorageUserContextProvider>();
        // Default to ComponentScopedStateStoreProvider, ensuring no persistence unless configured
        services.AddScoped<IAppStateStoreProvider, ComponentScopedStateStoreProvider>();

        // Register StateManager as a singleton, resolving StateDebugger within a scope
        services.AddSingleton(provider =>
        {
            using (var scope = provider.CreateScope())
            {
                var debugger = scope.ServiceProvider.GetRequiredService<AppStateDebugger>();
                return new AppStateManager(provider, debugger);
            }
        });

        return new AppStateManagementBuilder(services);
    }
}