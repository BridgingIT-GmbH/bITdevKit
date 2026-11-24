// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Collects state registrations using ordinary DI lifetimes and explicit ownership.</summary>
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
public class AppStateManagementBuilder
{
    private readonly AppStateManagementOptions options;

    /// <summary>Creates a builder over the collection's shared registration options.</summary>
    /// <param name="services">The runtime service collection.</param>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState();
    /// var builder = new AppStateManagementBuilder(services);
    /// builder.AddState<CatalogViewState>().AsComponentScoped().Done();
    /// ]]></code>
    /// </example>
    public AppStateManagementBuilder(IServiceCollection services)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
        this.options = services.FirstOrDefault(d => d.ServiceType == typeof(AppStateManagementOptions))?.ImplementationInstance
            as AppStateManagementOptions;
        if (this.options == null)
        {
            this.options = new AppStateManagementOptions();
            services.AddSingleton(this.options);
        }
    }

    /// <summary>Gets the native service collection for additional registrations.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Sets a stable application namespace before resolving state.</summary>
    /// <param name="applicationId">An application ID unique among apps sharing browser storage.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().WithApplicationId("catalog-console");
    /// ]]></code>
    /// </example>
    public AppStateManagementBuilder WithApplicationId(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        this.options.ApplicationId = applicationId;
        return this;
    }

    /// <summary>Configures bounded optional state diagnostics.</summary>
    /// <param name="configure">Updates the shared diagnostic settings.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().WithDebugging(options =>
    /// {
    ///     options.LoggingEnabled = true;
    ///     options.StateChangesTracked = true;
    ///     options.MaxTrackedChanges = 100;
    /// });
    /// ]]></code>
    /// </example>
    public AppStateManagementBuilder WithDebugging(Action<AppStateDebugOptions> configure)
    {
        var settings = this.Services.First(d => d.ServiceType == typeof(AppStateDebugOptions)).ImplementationInstance as AppStateDebugOptions;
        configure?.Invoke(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.MaxTrackedChanges);
        return this;
    }

    /// <summary>Sets the debounce delay for existing and future state registrations.</summary>
    /// <param name="delay">A nonnegative delay; zero disables waiting.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().WithDefaultDebounceDelay(TimeSpan.FromMilliseconds(300));
    /// ]]></code>
    /// </example>
    public AppStateManagementBuilder WithDefaultDebounceDelay(TimeSpan delay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        this.options.DefaultDebounceDelay = delay;
        foreach (var config in this.options.StateConfigurations.Values) { config.DebounceDelay = delay; }

        return this;
    }

    /// <summary>Registers a concrete state and its interface alias without creating duplicate scoped objects.</summary>
    /// <typeparam name="TState">The authored state service.</typeparam>
    /// <param name="lifetime">Scoped or Transient. Singleton user state is rejected.</param>
    /// <returns>The state-specific builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    /// .AsComponentScoped().WithHistory(10).Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder AddState<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TState>(ServiceLifetime lifetime = ServiceLifetime.Scoped) where TState : class, IAppState
    {
        if (lifetime == ServiceLifetime.Singleton) { throw new ArgumentException("AppState cannot use a singleton lifetime.", nameof(lifetime)); }

        var configuration = new AppStateOptions { DebounceDelay = this.options.DefaultDebounceDelay };
        if (!this.options.StateConfigurations.TryAdd(typeof(TState), configuration))
        {
            throw new ArgumentException($"State {typeof(TState).FullName} is already registered.", nameof(TState));
        }

        this.Services.Add(new ServiceDescriptor(typeof(TState), provider => CreateState<TState>(provider, configuration), lifetime));
        this.Services.Add(new ServiceDescriptor(typeof(IAppState), provider => provider.GetRequiredService<TState>(), lifetime));
        return new AppStateBuilder(this.Services, configuration, this);
    }

    /// <summary>Constructs and tracks a state using only the originating runtime's dependencies.</summary>
    /// <typeparam name="TState">The concrete state service.</typeparam>
    /// <param name="provider">The current DI scope.</param>
    /// <param name="configuration">The registration options, copied before creation.</param>
    /// <returns>The newly tracked state.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var configuration = new AppStateOptions
    /// {
    ///     InstanceKey = "manual-pane", PersistenceType = AppStatePersistenceType.SessionScoped,
    ///     StorageProvider = typeof(SessionScopedStateStoreProvider),
    ///     JsonTypeInfo = CatalogJson.Default.CatalogViewModel
    /// };
    /// await using var owned = AppStateManagementBuilder.CreateState<CatalogViewState>(
    ///     scope.ServiceProvider, configuration);
    /// await owned.Load();
    /// ]]></code>
    /// </example>
    public static TState CreateState<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TState>(IServiceProvider provider, AppStateOptions configuration) where TState : class, IAppState
    {
        var all = provider.GetRequiredService<AppStateManagementOptions>();
        var options = configuration.Copy();
        options.ApplicationId = all.ApplicationId;
        if (options.StorageProvider == null && all.DefaultStorageProvider != null)
        {
            options.StorageProvider = all.DefaultStorageProvider;
            options.PersistenceType = AppStatePersistenceType.CustomStorage;
        }

        var storage = options.StorageProvider != null
            ? (IAppStateStoreProvider)provider.GetRequiredService(options.StorageProvider) : null;
        // Explicit options/provider arguments preserve the historical constructor and permit extra DI dependencies.
        var state = storage != null
            ? ActivatorUtilities.CreateInstance<TState>(provider, options, storage)
            : ActivatorUtilities.CreateInstance<TState>(provider, options);
        var manager = provider.GetRequiredService<AppStateManager>();
        try { manager.RegisterState(state); }
        catch { (state as IDisposable)?.Dispose(); throw; }

        return state;
    }

    /// <summary>Selects an application default for states without an explicit provider.</summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="configureServices">Optional native dependency registrations.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState()
    /// .AddCustomStateStoreProvider<CustomStorageStateStoreProvider>()
    /// .AddState<CatalogViewState>().WithIdentity("catalog", "list").Done();
    /// ]]></code>
    /// </example>
    public AppStateManagementBuilder AddCustomStateStoreProvider<TProvider>(Action<IServiceCollection> configureServices = null)
        where TProvider : class, IAppStateStoreProvider
    {
        configureServices?.Invoke(this.Services);
        this.RegisterProvider<TProvider>();
        this.options.DefaultStorageProvider = typeof(TProvider);
        return this;
    }

    /// <summary>Registers a provider once with an interface alias to the same scoped instance.</summary>
    /// <typeparam name="TProvider">The selected provider.</typeparam>
    /// <example>
    /// <code><![CDATA[
    /// var builder = services.AddAppState();
    /// builder.RegisterProvider<SessionScopedStateStoreProvider>();
    /// using var provider = services.BuildServiceProvider();
    /// using var scope = provider.CreateScope();
    /// var concrete = scope.ServiceProvider.GetRequiredService<SessionScopedStateStoreProvider>();
    /// var alias = scope.ServiceProvider.GetRequiredService<IAppStateStoreProvider>();
    /// Debug.Assert(ReferenceEquals(concrete, alias));
    /// ]]></code>
    /// </example>
    public void RegisterProvider<TProvider>() where TProvider : class, IAppStateStoreProvider
    {
        this.Services.TryAddScoped<TProvider>();
        if (this.options.RegisteredProviders.Add(typeof(TProvider)))
        {
            this.Services.AddScoped<IAppStateStoreProvider>(provider => provider.GetRequiredService<TProvider>());
        }
    }
}
