// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Owns one explicitly created state, leaving injected services owned by DI.</summary>
/// <typeparam name="TState">The concrete state type.</typeparam>
/// <param name="state">The independently created state.</param>
/// <param name="instanceId">Its runtime identity.</param>
/// <param name="unregister">Removes its manager registration.</param>
/// <example>
/// <code><![CDATA[
/// await using var pane = factory.Create<CatalogViewState>("left-pane");
/// await pane.State.Load();
/// var next = pane.State.TakeSnapshot();
/// next.Page = 2;
/// await pane.State.RestoreSnapshot(next);
/// ]]></code>
/// </example>
public sealed class AppStateLease<TState>(TState state, Guid instanceId, Action unregister) : IDisposable, IAsyncDisposable
    where TState : class, IAppState
{
    private int disposed;

    /// <summary>Gets the leased state; do not use it after releasing the lease.</summary>
    public TState State { get; } = state;

    /// <summary>Gets the nondurable live identity.</summary>
    public Guid InstanceId { get; } = instanceId;

    /// <summary>Detaches the state and releases only its owned resources.</summary>
    /// <exception cref="InvalidOperationException">The state supports only asynchronous disposal. Call DisposeAsync instead.</exception>
    /// <remarks>A rejected synchronous disposal leaves the lease registered and available for asynchronous cleanup.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// var pane = factory.Create<CatalogViewState>("left-pane");
    /// await pane.State.Load();
    /// pane.Dispose();
    /// ]]></code>
    /// </example>
    public void Dispose()
    {
        if (this.State is IAsyncDisposable and not IDisposable && Volatile.Read(ref this.disposed) == 0)
        {
            throw new InvalidOperationException("The state requires asynchronous cleanup. Call DisposeAsync instead.");
        }

        if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

        unregister();
        (this.State as IDisposable)?.Dispose();
    }

    /// <summary>Detaches immediately, then awaits bounded state cleanup when supported.</summary>
    /// <returns>Cleanup without disposal of injected dependencies.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var pane = factory.Create<CatalogViewState>("left-pane");
    /// await pane.State.Load();
    /// await pane.DisposeAsync();
    /// ]]></code>
    /// </example>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

        unregister();
        if (this.State is IAsyncDisposable asyncDisposable) { await asyncDisposable.DisposeAsync(); }
        else { (this.State as IDisposable)?.Dispose(); }
    }
}
