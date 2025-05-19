namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Builds the ManagedState system configuration with a fluent API.
/// </summary>
/// <remarks>
/// Initializes a new instance of the StateManagementBuilder.
/// </remarks>
/// <param name="services">The IServiceCollection to configure.</param>
/// <exception cref="ArgumentNullException">Thrown if services is null.</exception>
public class AppStateManagementBuilder(IServiceCollection services)
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly AppStateManagementOptions options = new AppStateManagementOptions();

    /// <summary>
    /// Gets the IServiceCollection being configured.
    /// </summary>
    public IServiceCollection Services => this.services;

    /// <summary>
    /// Configures debugging options for the ManagedState system.
    /// </summary>
    /// <param name="configure">An action to configure the DebugOptions.</param>
    /// <returns>The current StateManagementBuilder for chaining.</returns>
    public AppStateManagementBuilder WithDebugging(Action<AppStateDebugOptions> configure)
    {
        var debugOptions = new AppStateDebugOptions();
        configure?.Invoke(debugOptions);
        this.services.AddSingleton(debugOptions);
        return this;
    }

    /// <summary>
    /// Configures the default debounce delay for all states in the ManagedState system.
    /// </summary>
    /// <param name="delay">The default delay interval before saving state after a change.</param>
    /// <returns>The current StateManagementBuilder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if delay is negative.</exception>
    public AppStateManagementBuilder WithDefaultDebounceDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Debounce delay cannot be negative.");
        }

        foreach (var config in this.options.StateConfigurations.Values)
        {
            config.DebounceDelay = delay;
        }
        // Set the default for future states
        this.options.DefaultDebounceDelay = delay;
        return this;
    }

    /// <summary>
    /// Adds a specific state type to the ManagedState system with custom configuration.
    /// </summary>
    /// <typeparam name="TState">The type of the state class, must inherit from ManagedState.</typeparam>
    /// <param name="lifetime">The service lifetime for the state (default is Scoped).</param>
    /// <returns>A StateBuilder for configuring the specific state.</returns>
    public AppStateBuilder AddState<TState>(ServiceLifetime lifetime = ServiceLifetime.Scoped) where TState : class, IAppState
    {
        var config = new AppStateOptions
        {
            DebounceDelay = this.options.DefaultDebounceDelay // Apply the default debounce delay
        };
        this.options.StateConfigurations[typeof(TState)] = config;

        // Register the state as its concrete type (TState)
        this.services.Add(new ServiceDescriptor(typeof(TState), factory, lifetime));

        // Also register the state as IAppState so GetServices<IAppState>() can resolve it
        this.services.Add(new ServiceDescriptor(typeof(IAppState), factory, lifetime));

        return new AppStateBuilder(this.services, config, this);

        // Factory to create the state instance
        object factory(IServiceProvider provider)
        {
            var logger = provider.GetRequiredService<ILogger<TState>>();
            var storageProvider = config.StorageProvider != null
                ? (IAppStateStoreProvider)provider.GetService(config.StorageProvider)
                : null;
            var userContextProvider = provider.GetService<IUserContextProvider>();
            var debugger = provider.GetService<AppStateDebugger>();
            var instance = (IAppState)Activator.CreateInstance(typeof(TState), logger, config, storageProvider, userContextProvider, debugger);

            // Register the state with the StateManager
            var stateManager = provider.GetRequiredService<AppStateManager>();
            stateManager.RegisterState(instance);

            return instance;
        }
    }

    /// <summary>
    /// Adds a custom state store provider for all states that require persistence, overriding the default.
    /// </summary>
    /// <typeparam name="TProvider">The type of the custom state store provider.</typeparam>
    /// <param name="configureServices">An optional action to configure additional services for the provider.</param>
    /// <returns>The current StateManagementBuilder for chaining.</returns>
    public AppStateManagementBuilder AddCustomStateStoreProvider<TProvider>(Action<IServiceCollection> configureServices = null)
        where TProvider : class, IAppStateStoreProvider
    {
        configureServices?.Invoke(this.services);
        // Remove the default ComponentScopedStateStoreProvider if it exists
        var defaultDescriptor = this.services.FirstOrDefault(d => d.ServiceType == typeof(IAppStateStoreProvider) && d.ImplementationType == typeof(ComponentScopedStateStoreProvider));
        if (defaultDescriptor != null)
        {
            this.services.Remove(defaultDescriptor);
        }
        this.services.AddScoped<IAppStateStoreProvider, TProvider>();
        return this;
    }
}