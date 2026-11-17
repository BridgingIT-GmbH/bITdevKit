// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configures persistence, history and identity for one registered AppState type.
/// </summary>
/// <remarks>
/// Use the builder returned by AppStateManagementBuilder.AddState to configure a registered state.
/// </remarks>
/// <param name="services">The IServiceCollection to configure.</param>
/// <param name="options">The configuration options for this state.</param>
/// <param name="parentBuilder">The parent StateManagementBuilder for chaining.</param>
/// <exception cref="ArgumentNullException">Thrown if services, options, or parentBuilder is null.</exception>
/// <example>
/// <code><![CDATA[
/// services.AddAppState().AddState<CatalogViewState>()
///     .AsComponentScoped().WithHistory(10).Done();
/// ]]></code>
/// </example>
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
    /// Disables persistence. DI lifetime remains unchanged; use a lease for component ownership.
    /// </summary>
    /// <returns>The current StateBuilder for chaining.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .AsComponentScoped().Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder AsComponentScoped()
    {
        this.options.PersistenceType = AppStatePersistenceType.ComponentScoped;
        this.parentBuilder.RegisterProvider<ComponentScopedStateStoreProvider>();
        this.options.StorageProvider = typeof(ComponentScopedStateStoreProvider);
        return this;
    }

    /// <summary>
    /// Selects copied in-memory storage within the resolving DI scope.
    /// </summary>
    /// <returns>The current StateBuilder for chaining.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .AsInMemoryScoped().Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder AsInMemoryScoped()
    {
        this.options.PersistenceType = AppStatePersistenceType.SessionScoped;
        this.parentBuilder.RegisterProvider<SessionScopedStateStoreProvider>();
        this.options.StorageProvider = typeof(SessionScopedStateStoreProvider);
        return this;
    }

    /// <summary>
    /// Configures the state to be localStorage-scoped, persisting it in the browser's localStorage to survive refreshes.
    /// </summary>
    /// <returns>The current StateBuilder for chaining.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .WithJsonTypeInfo(CatalogJson.Default.CatalogViewModel).AsLocalStorageScoped().Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder AsLocalStorageScoped()
    {
        this.options.PersistenceType = AppStatePersistenceType.LocalStorage;
        this.parentBuilder.RegisterProvider<LocalStorageStateStoreProvider>();
        this.options.StorageProvider = typeof(LocalStorageStateStoreProvider);
        return this;
    }

    /// <summary>
    /// Enables or disables optional history and persistence without disabling state changes or independent snapshots.
    /// </summary>
    /// <param name="value">Whether optional history and persistence are enabled.</param>
    /// <returns>The current builder for chaining.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .Enabled(false).Done();
    /// ]]></code>
    /// </example>
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
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .WithHistory(10).Done();
    /// ]]></code>
    /// </example>
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
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .WithPersistence<SessionScopedStateStoreProvider>().Done();
    /// ]]></code>
    /// </example>
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

        this.parentBuilder.RegisterProvider<TProvider>();
        return this;
    }

    /// <summary>
    /// Configures the debounce delay for saving state to the storage provider.
    /// </summary>
    /// <param name="delay">The delay interval before saving state after a change.</param>
    /// <returns>The current StateBuilder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if delay is negative.</exception>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .AsInMemoryScoped().WithDebounceDelay(TimeSpan.FromMilliseconds(300)).Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder WithDebounceDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Debounce delay cannot be negative.");
        }

        this.options.DebounceDelay = delay;
        return this;
    }

    /// <summary>Publishes a stable logical owner and persisted schema.</summary>
    /// <param name="moduleId">The logical module namespace.</param>
    /// <param name="stateName">The stable view or state identity.</param>
    /// <param name="schemaVersion">A positive schema version.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .WithIdentity("catalog", "list", schemaVersion: 2).Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder WithIdentity(string moduleId, string stateName, int schemaVersion = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        this.options.ModuleId = moduleId;
        this.options.StateName = stateName;
        this.options.SchemaVersion = schemaVersion;
        return this;
    }

    /// <summary>Supplies generated metadata for snapshots and compatible storage providers.</summary>
    /// <param name="typeInfo">Metadata describing the state model.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .WithJsonTypeInfo(CatalogJson.Default.CatalogViewModel).Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder WithJsonTypeInfo(System.Text.Json.Serialization.Metadata.JsonTypeInfo typeInfo)
    {
        this.options.JsonTypeInfo = typeInfo ?? throw new ArgumentNullException(nameof(typeInfo));
        return this;
    }

    /// <summary>Enables import of one owner-confirmed legacy preference key.</summary>
    /// <param name="stateId">The exact legacy state ID.</param>
    /// <param name="userContextId">The exact same-owner legacy user partition.</param>
    /// <param name="workspaceKey">The confirmed target workspace; null permits only unscoped import.</param>
    /// <returns>This builder.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .AsLocalStorageScoped().WithLegacyStorageKey("previous-list", confirmedPrincipalKey, confirmedWorkspaceKey).Done();
    /// ]]></code>
    /// </example>
    public AppStateBuilder WithLegacyStorageKey(string stateId, string userContextId, string workspaceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userContextId);
        this.options.LegacyStateId = stateId;
        this.options.LegacyUserContextId = userContextId;
        this.options.LegacyWorkspaceKey = workspaceKey;
        return this;
    }

    /// <summary>
    /// Completes the configuration of the current state and returns to the parent builder for further state additions.
    /// </summary>
    /// <returns>The parent StateManagementBuilder for chaining.</returns>
    /// <example>
    /// <code><![CDATA[
    /// services.AddAppState().AddState<CatalogViewState>()
    ///     .AsComponentScoped().WithHistory(10).Done();
    /// ]]></code>
    /// </example>
    public AppStateManagementBuilder Done()
    {
        return this.parentBuilder;
    }
}
