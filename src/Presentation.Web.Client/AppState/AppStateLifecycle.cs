// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Components;

/// <summary>Owns an explicit connection between one AppState instance and native Blazor persistence.</summary>
/// <remarks>Attach once per logical instance after establishing host context. Dispose before releasing a component-owned state.
/// Only approved model values cross the render-mode boundary. This handle never owns the state or its injected services.</remarks>
/// <example>
/// <code><![CDATA[
/// using var lifecycle = AppStateLifecycle.Attach(state, persistentState,
///     CatalogJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
/// if (!lifecycle.Restored)
/// {
///     var next = state.TakeSnapshot();
///     next.Page = 1;
///     await state.RestoreSnapshot(next);
/// }
/// ]]></code>
/// </example>
public sealed class AppStateLifecycle : IDisposable
{
    private static readonly ConditionalWeakTable<PersistentComponentState, HashSet<string>> Owners = new();
    private readonly HashSet<string> reservations;
    private readonly string identity;
    private PersistingComponentStateSubscription persisting;
    private RestoringComponentStateSubscription restoring;
    private bool disposed;

    private AppStateLifecycle(HashSet<string> reservations, string identity)
    {
        this.reservations = reservations;
        this.identity = identity;
    }

    /// <summary>Gets whether native state successfully initialized this instance during attachment.</summary>
    /// <example>
    /// <code><![CDATA[
    /// if (lifecycle.Restored) { Console.WriteLine("The catalog filter was restored."); }
    /// ]]></code>
    /// </example>
    public bool Restored { get; private set; }

    /// <summary>Registers native capture and restores matching prerender or circuit snapshots before feature initialization.</summary>
    /// <typeparam name="TState">The explicitly approved model type.</typeparam>
    /// <param name="state">The originating state, resolved or leased from the current runtime.</param>
    /// <param name="persistentState">Blazor's persistence service from the current component runtime.</param>
    /// <param name="jsonTypeInfo">Generated metadata for the approved model.</param>
    /// <param name="renderMode">The native mode at this interactive boundary.</param>
    /// <param name="restoreCircuit">Whether a new circuit may restore its last native snapshot; false accepts prerender handoff only.</param>
    /// <returns>A handle whose lifetime belongs to one component or runtime owner.</returns>
    /// <remarks>Native Blazor's imperative string serialization requires JSON reflection to be enabled in the host.
    /// Feature model serialization uses the supplied metadata. Missing, corrupt or superseded snapshots retain current values.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// using var lifecycle = AppStateLifecycle.Attach(state, persistentState,
    ///     CatalogJson.Default.CatalogViewModel, RenderMode.InteractiveServer);
    /// // Recreate component subscriptions against this runtime's state after attachment.
    /// ]]></code>
    /// </example>
    public static AppStateLifecycle Attach<TState>(AppState<TState> state,
        PersistentComponentState persistentState, JsonTypeInfo<TState> jsonTypeInfo, IComponentRenderMode renderMode,
        bool restoreCircuit = true)
        where TState : class, new()
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(persistentState);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        ArgumentNullException.ThrowIfNull(renderMode);
        if (!JsonSerializer.IsReflectionEnabledByDefault)
        {
            throw new NotSupportedException("Native Blazor persistence requires JSON reflection to be enabled in the host.");
        }

        var reservations = Owners.GetValue(persistentState, static _ => new(StringComparer.Ordinal));
        var identity = state.HandoffIdentity;
        lock (reservations)
        {
            if (!reservations.Add(identity))
            {
                throw new InvalidOperationException("A lifecycle owner already exists for this logical AppState instance.");
            }
        }

        var registration = new AppStateLifecycle(reservations, identity);
        try
        {
            var version = state.Version;
            var key = state.HandoffKey;
            registration.restoring = persistentState.RegisterOnRestoring(
                () => registration.Restore(state, persistentState, jsonTypeInfo, key, version),
                new RestoreOptions
                {
                    RestoreBehavior = restoreCircuit ? RestoreBehavior.Default : RestoreBehavior.SkipLastSnapshot,
                    AllowUpdates = false
                });
            registration.persisting = persistentState.RegisterOnPersisting(
                () => registration.Persist(state, persistentState, jsonTypeInfo), renderMode);
            return registration;
        }
        catch
        {
            registration.Dispose();
            throw;
        }
    }

    /// <summary>Releases native callbacks and the logical identity reservation without disposing the state.</summary>
    /// <example>
    /// <code><![CDATA[
    /// this.lifecycle?.Dispose();
    /// this.subscription?.Dispose();
    /// // DI retains ownership of an injected state.
    /// ]]></code>
    /// </example>
    public void Dispose()
    {
        lock (this.reservations)
        {
            if (this.disposed) { return; }

            this.disposed = true;
            this.persisting.Dispose();
            this.restoring.Dispose();
            this.reservations.Remove(this.identity);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification =
        "Native Blazor serializes only System.String. The feature model uses explicit JSON metadata; the host enables native JSON reflection.")]
    private Task Persist<TState>(AppState<TState> state, PersistentComponentState persistence, JsonTypeInfo<TState> typeInfo)
        where TState : class, new()
    {
        var version = state.Version;
        if (Volatile.Read(ref this.disposed) || !state.IsCurrent(version)) { return Task.CompletedTask; }

        var snapshot = state.CaptureHandoff();
        var json = JsonSerializer.Serialize(snapshot.Model, typeInfo);
        if (!Volatile.Read(ref this.disposed) && state.IsCurrent(version)) { persistence.PersistAsJson(snapshot.Key, json); }

        return Task.CompletedTask;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification =
        "Native Blazor deserializes only System.String. The feature model uses explicit JSON metadata; the host enables native JSON reflection.")]
    private void Restore<TState>(AppState<TState> state, PersistentComponentState persistence,
        JsonTypeInfo<TState> typeInfo, string key, AppStateVersion version) where TState : class, new()
    {
        if (Volatile.Read(ref this.disposed)) { return; }

        try
        {
            if (persistence.TryTakeFromJson<string>(key, out var json) && json != null)
            {
                var model = JsonSerializer.Deserialize(json, typeInfo);
                if (model != null) { this.Restored = state.TryRestoreHandoff(new(key, model), version); }
            }
        }
        catch (JsonException) { }
    }

}
