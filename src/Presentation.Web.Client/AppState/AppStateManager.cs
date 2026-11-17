// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Tracks and resets live state belonging only to the originating runtime scope.</summary>
/// <param name="serviceProvider">The originating DI scope, never used to open another scope.</param>
/// <param name="debugger">The optional owner-local debugger.</param>
/// <example>
/// <code><![CDATA[
/// var manager = scope.ServiceProvider.GetRequiredService<AppStateManager>();
/// foreach (var instance in manager.GetInstances())
/// {
///     Console.WriteLine($"{instance.StateId}: revision {instance.Revision}");
/// }
/// ]]></code>
/// </example>
public class AppStateManager(IServiceProvider serviceProvider, AppStateDebugger debugger)
{
    private readonly object sync = new();
    private readonly Dictionary<IAppState, Guid> states = new(ReferenceEqualityComparer.Instance);
    private readonly TimeProvider clock = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
    private AppStateContext currentContext = new();

    /// <summary>Gets the currently accepted host context; it does not grant authorization.</summary>
    public AppStateContext CurrentContext => Volatile.Read(ref this.currentContext);

    /// <summary>Registers an object once without retaining another runtime's services.</summary>
    /// <param name="state">The live state instance.</param>
    /// <example>
    /// <code><![CDATA[
    /// using var owned = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// manager.RegisterState(owned);
    /// ]]></code>
    /// </example>
    public void RegisterState(IAppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (this.sync)
        {
            if (this.states.ContainsKey(state)) { return; }

            if (state is IAppStateRuntime runtime)
            {
                if (runtime.PersistenceIdentity != null && this.states.Keys.OfType<IAppStateRuntime>()
                    .Any(s => s.PersistenceIdentity == runtime.PersistenceIdentity))
                {
                    throw new InvalidOperationException("A persisted state with this stable instance key is already active.");
                }

                runtime.AttachOwner(this, this.clock);
                this.states.Add(state, runtime.InstanceId);
            }
            else { this.states.Add(state, Guid.NewGuid()); }
        }
    }

    /// <summary>Removes exactly the originating object without disposing its DI-owned dependencies.</summary>
    /// <param name="state">The state registration to detach.</param>
    /// <example>
    /// <code><![CDATA[
    /// using var owned = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// manager.RegisterState(owned);
    /// manager.UnregisterState(owned);
    /// ]]></code>
    /// </example>
    public void UnregisterState(IAppState state)
    {
        lock (this.sync) { this.states.Remove(state); }
    }

    /// <summary>Gets a copied owner-local inventory of live state references.</summary>
    /// <returns>The current live registrations.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var states = manager.GetAllStates();
    /// Debug.Assert(states.Contains(state));
    /// ]]></code>
    /// </example>
    public IReadOnlyList<IAppState> GetAllStates()
    {
        lock (this.sync) { return this.states.Keys.ToArray(); }
    }

    /// <summary>Gets the assigned runtime ID of one registered object.</summary>
    /// <param name="state">The exact originating instance.</param>
    /// <returns>Its runtime identity.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var id = manager.GetInstanceId(state);
    /// Debug.Assert(id == state.InstanceId);
    /// ]]></code>
    /// </example>
    public Guid GetInstanceId(IAppState state)
    {
        lock (this.sync) { return this.states[state]; }
    }

    /// <summary>Gets safe live-instance metadata without model values.</summary>
    /// <returns>A materialized metadata snapshot.</returns>
    /// <example>
    /// <code><![CDATA[
    /// foreach (var instance in manager.GetInstances())
    /// {
    ///     Console.WriteLine($"{instance.StateType.Name}: revision {instance.Revision}");
    /// }
    /// ]]></code>
    /// </example>
    public IReadOnlyList<AppStateInstanceInfo> GetInstances()
    {
        KeyValuePair<IAppState, Guid>[] entries;
        lock (this.sync) { entries = this.states.ToArray(); }

        return entries.Select(entry => entry.Key is IAppStateRuntime state
            ? new AppStateInstanceInfo(state.InstanceId, state.StateId, state.GetType(), state.Version.Revision,
                state.Version.ContextRevision, state.PersistenceStatus)
            : new AppStateInstanceInfo(entry.Value, entry.Key.StateId, entry.Key.GetType(), 0, 0,
                AppStatePersistenceStatus.NotConfigured)).ToArray();
    }

    /// <summary>Resets originating states, retaining registrations and never clearing provider-wide storage.</summary>
    /// <returns>Cleanup of all states; aggregate failures follow all reset attempts.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await manager.ResetAll();
    /// ]]></code>
    /// </example>
    public Task ResetAll() => CompleteAllAsync(this.GetAllStates().Select(state => Capture(state.Reset)));

    /// <summary>Accepts a new host context and invalidates private values before asynchronous cleanup.</summary>
    /// <param name="context">The host-established opaque partition.</param>
    /// <param name="cancellationToken">Cancels admission or this caller's wait.</param>
    /// <returns>Cleanup of the old captured partitions.</returns>
    /// <remarks>Superseded transitions stop propagating. A legacy custom state's failed reset detaches it.
    /// Its owner must reset it successfully before registering it again.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// // The host has already accepted this identity/workspace transition.
    /// await manager.ChangeContext(new AppStateContext(confirmedPrincipalKey, confirmedWorkspaceKey),
    ///     CancellationToken.None);
    /// ]]></code>
    /// </example>
    public Task ChangeContext(AppStateContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        AppStateContext accepted;
        IAppState[] registered;
        lock (this.sync)
        {
            if (this.currentContext == context) { return Task.CompletedTask; }

            // A fresh reference distinguishes separate acceptances even if a caller reuses a context object.
            accepted = context with { };
            Volatile.Write(ref this.currentContext, accepted);
            registered = this.states.Keys.ToArray();
            debugger?.Clear();
        }

        var tasks = new List<Task>(registered.Length);
        foreach (var state in registered)
        {
            if (!ReferenceEquals(this.CurrentContext, accepted)) { break; }

            tasks.Add(Capture(() => state is IAppStateRuntime runtime
                ? runtime.ChangeContext(accepted) : ResetLegacyAsync(state)));
        }

        return CompleteAllAsync(tasks).WaitAsync(cancellationToken);

        async Task ResetLegacyAsync(IAppState state)
        {
            try { await state.Reset(); }
            catch
            {
                this.UnregisterState(state);
                throw;
            }
        }
    }

    private static Task Capture(Func<Task> action)
    {
        try { return action(); }
        catch (Exception ex) { return Task.FromException(ex); }
    }

    private static async Task CompleteAllAsync(IEnumerable<Task> operations)
    {
        var tasks = operations.ToArray();
        try { await Task.WhenAll(tasks); }
        catch { throw new AggregateException(tasks.Where(t => t.IsFaulted).SelectMany(t => t.Exception.InnerExceptions)); }
    }
}
