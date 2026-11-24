// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

/// <summary>Creates independent component states using the originating runtime's dependencies.</summary>
/// <param name="services">The current runtime scope, not a component-created child scope.</param>
/// <param name="registrations">The registered state configuration.</param>
/// <param name="manager">The owner-local inventory.</param>
/// <example>
/// <code><![CDATA[
/// var factory = scope.ServiceProvider.GetRequiredService<AppStateFactory>();
/// await using var left = factory.Create<CatalogViewState>("left-pane");
/// await using var right = factory.Create<CatalogViewState>("right-pane");
/// Debug.Assert(left.InstanceId != right.InstanceId);
/// ]]></code>
/// </example>
public sealed class AppStateFactory(IServiceProvider services, AppStateManagementOptions registrations, AppStateManager manager)
{
    /// <summary>Creates a leased state without changing DI service lifetimes.</summary>
    /// <typeparam name="TState">The configured concrete state type.</typeparam>
    /// <param name="instanceKey">A stable key required for persisted state; null creates an ephemeral local identity.</param>
    /// <returns>An independently disposable state owner.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await using var pane = factory.Create<CatalogViewState>("left-pane");
    /// await pane.State.Load();
    /// ]]></code>
    /// </example>
    public AppStateLease<TState> Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TState>(string instanceKey = null) where TState : class, IAppState
    {
        if (!registrations.StateConfigurations.TryGetValue(typeof(TState), out var configuration))
        {
            throw new InvalidOperationException($"State {typeof(TState).FullName} is not registered.");
        }

        if (configuration.Enabled && (configuration.PersistenceType != AppStatePersistenceType.ComponentScoped || configuration.StorageProvider == null && registrations.DefaultStorageProvider != null)
            && string.IsNullOrWhiteSpace(instanceKey))
        {
            throw new ArgumentException("Persisted component state requires a stable instance key.", nameof(instanceKey));
        }

        var options = configuration.Copy();
        options.ApplicationId = registrations.ApplicationId;
        options.InstanceKey = instanceKey ?? Guid.NewGuid().ToString("N");
        var state = AppStateManagementBuilder.CreateState<TState>(services, options);
        var id = manager.GetInstanceId(state);
        return new(state, id, () => manager.UnregisterState(state));
    }
}
