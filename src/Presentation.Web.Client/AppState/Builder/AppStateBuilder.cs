namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builds configuration for a specific state type within the ManagedState system.
/// </summary>
/// <remarks>
/// Initializes a new instance of the StateBuilder.
/// </remarks>
/// <param name="services">The IServiceCollection to configure.</param>
/// <param name="options">The configuration options for this state.</param>
/// <param name="parentBuilder">The parent StateManagementBuilder for chaining.</param>
/// <exception cref="ArgumentNullException">Thrown if services, options, or parentBuilder is null.</exception>
public class AppStateBuilder(IServiceCollection services, AppStateOptions options, AppStateManagementBuilder parentBuilder)
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly AppStateOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly AppStateManagementBuilder parentBuilder = parentBuilder ?? throw new ArgumentNullException(nameof(parentBuilder));

    /// <summary>
    /// Gets the IServiceCollection being configured.
    /// </summary>
    public IServiceCollection Services => this.services;

    /// <summary>
    /// Configures the state to be component-scoped, with no persistence beyond the component lifetime.
    /// </summary>
    /// <returns>The current StateBuilder for chaining.</returns>
    public AppStateBuilder AsComponentScoped()
    {
        this.options.PersistenceType = AppStatePersistenceType.ComponentScoped;
        // Register ComponentScopedStateStoreProvider if not already overridden globally
        if (!this.services.Any(d => d.ServiceType == typeof(IAppStateStoreProvider) && d.ImplementationType == typeof(ComponentScopedStateStoreProvider)))
        {
            this.services.AddScoped<IAppStateStoreProvider, ComponentScopedStateStoreProvider>();
        }
        this.options.StorageProvider = typeof(ComponentScopedStateStoreProvider);
        return this;
    }

    /// <summary>
    /// Configures the state to be in-memory scoped, sharing it across a user session without persistence beyond the session.
    /// </summary>
    /// <returns>The current StateBuilder for chaining.</returns>
    public AppStateBuilder AsInMemoryScoped()
    {
        this.options.PersistenceType = AppStatePersistenceType.SessionScoped;
        // Register SessionScopedStateStoreProvider if not already overridden globally
        if (!this.services.Any(d => d.ServiceType == typeof(IAppStateStoreProvider) && d.ImplementationType == typeof(SessionScopedStateStoreProvider)))
        {
            this.services.AddScoped<IAppStateStoreProvider, SessionScopedStateStoreProvider>();
        }
        this.options.StorageProvider = typeof(SessionScopedStateStoreProvider);
        return this;
    }

    /// <summary>
    /// Configures the state to be localStorage-scoped, persisting it in the browser's localStorage to survive refreshes.
    /// </summary>
    /// <returns>The current StateBuilder for chaining.</returns>
    public AppStateBuilder AsLocalStorageScoped()
    {
        this.options.PersistenceType = AppStatePersistenceType.LocalStorage;
        // Register LocalStorageStateStoreProvider if not already overridden globally
        if (!this.services.Any(d => d.ServiceType == typeof(IAppStateStoreProvider) && d.ImplementationType == typeof(LocalStorageStateStoreProvider)))
        {
            this.services.AddScoped<IAppStateStoreProvider, LocalStorageStateStoreProvider>();
            this.services.AddScoped<LocalStorageStateStoreProvider>();
        }
        this.options.StorageProvider = typeof(LocalStorageStateStoreProvider);
        return this;
    }

    public AppStateBuilder Enabled(bool value)
    {
        this.options.Enabled = value;
        return this;
    }

    /// <summary>
    /// Enables history tracking for the state with a specified maximum number of items.
    /// </summary>
    /// <param name="maxItems">The maximum number of history items to retain (default is 10).</param>
    /// <returns>The current StateBuilder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if maxItems is less than or equal to zero.</exception>
    public AppStateBuilder WithHistory(int maxItems = 10)
    {
        if (maxItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Maximum history items must be greater than zero.");
        }

        this.options.HistoryEnabled = true;
        this.options.MaxHistoryItems = maxItems;
        return this;
    }

    /// <summary>
    /// Configures the state to use a specific persistence provider.
    /// </summary>
    /// <typeparam name="TProvider">The type of the state store provider.</typeparam>
    /// <returns>The current StateBuilder for chaining.</returns>
    public AppStateBuilder WithPersistence<TProvider>() where TProvider : class, IAppStateStoreProvider
    {
        if (typeof(TProvider) == typeof(LocalStorageStateStoreProvider))
        {
            this.options.PersistenceType = AppStatePersistenceType.LocalStorage;
        }
        else if (typeof(TProvider) == typeof(SessionScopedStateStoreProvider))
        {
            this.options.PersistenceType = AppStatePersistenceType.SessionScoped;
        }
        else if (typeof(TProvider) == typeof(ComponentScopedStateStoreProvider))
        {
            this.options.PersistenceType = AppStatePersistenceType.ComponentScoped;
        }
        else
        {
            this.options.PersistenceType = AppStatePersistenceType.CustomStorage;
        }

        this.options.StorageProvider = typeof(TProvider);

        // Register the provider if not already registered globally or for another state
        if (!this.services.Any(d => d.ServiceType == typeof(IAppStateStoreProvider) && d.ImplementationType == typeof(TProvider)))
        {
            this.services.AddScoped<IAppStateStoreProvider, TProvider>();
        }

        return this;
    }

    /// <summary>
    /// Configures the debounce delay for saving state to the storage provider.
    /// </summary>
    /// <param name="delay">The delay interval before saving state after a change.</param>
    /// <returns>The current StateBuilder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if delay is negative.</exception>
    public AppStateBuilder WithDebounceDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Debounce delay cannot be negative.");
        }

        this.options.DebounceDelay = delay;
        return this;
    }

    /// <summary>
    /// Completes the configuration of the current state and returns to the parent builder for further state additions.
    /// </summary>
    /// <returns>The parent StateManagementBuilder for chaining.</returns>
    public AppStateManagementBuilder Done()
    {
        return this.parentBuilder;
    }
}