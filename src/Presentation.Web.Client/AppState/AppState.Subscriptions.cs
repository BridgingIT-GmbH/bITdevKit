// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

public abstract partial class AppState<TState>
{
    /// <summary>Observes changes to one selected value with explicit renderer dispatch and ownership.</summary>
    /// <typeparam name="TSelected">The selected value type.</typeparam>
    /// <param name="selector">Selects a value from independent snapshots.</param>
    /// <param name="callback">Handles changed selections. Check the version after asynchronous work.</param>
    /// <param name="comparer">Value equality; provide structural equality for collections when needed.</param>
    /// <param name="dispatch">The component's InvokeAsync delegate, or null for direct invocation.</param>
    /// <returns>An owned handle; dispose it when the consumer unmounts.</returns>
    /// <example>
    /// <code><![CDATA[
    /// using var subscription = state.Subscribe(model => model.Page, (change, token) =>
    /// {
    ///     if (state.IsCurrent(change.Version))
    ///     {
    ///         Console.WriteLine($"Catalog page: {change.Current}");
    ///     }
    ///
    ///     return Task.CompletedTask;
    /// }, dispatch: work => InvokeAsync(work));
    /// await state.SetPage(2);
    /// ]]></code>
    /// </example>
    public AppStateSubscription Subscribe<TSelected>(Func<TState, TSelected> selector,
        Func<AppStateSelectionChange<TSelected>, CancellationToken, Task> callback,
        IEqualityComparer<TSelected> comparer = null, Func<Func<Task>, Task> dispatch = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(callback);
        comparer ??= EqualityComparer<TSelected>.Default;
        dispatch ??= static work => work();
        var subscriptionSync = new object();
        var previous = selector(this.TakeSnapshot());
        var lastRevision = this.Version.Revision;
        CancellationTokenSource active = null;
        var ended = false;
        AppStateSubscription handle = null;
        void Changed(IStateChangeMetadata metadata)
        {
            var selected = selector((TState)metadata.NewValue);
            AppStateSelectionChange<TSelected> change;
            CancellationTokenSource source;
            CancellationTokenSource obsolete;
            lock (subscriptionSync)
            {
                if (ended || metadata.Version.Revision <= lastRevision) { return; }

                lastRevision = metadata.Version.Revision;

                var isContextReset = metadata.Operation is "ContextReset" or "Reset";
                if (comparer.Equals(previous, selected) && !isContextReset) { return; }

                obsolete = active;
                source = CancellationTokenSource.CreateLinkedTokenSource(this.lifetime.Token);
                active = source;
                change = new(metadata.Operation == "ContextReset" ? default : previous, selected, metadata.Version);
                previous = typeof(TSelected).IsValueType || selected is string
                    ? selected : selector(this.CloneState((TState)metadata.NewValue));
            }

            this.CancelOwned(obsolete);
            this.Observe(DispatchAsync());
            async Task DispatchAsync()
            {
                try
                {
                    await dispatch(async () =>
                    {
                        lock (subscriptionSync)
                        {
                            if (ended || source.IsCancellationRequested || !this.IsGenerationCurrent(change.Version)) { return; }
                        }

                        await callback(change, source.Token);
                    });
                }
                catch (OperationCanceledException) when (source.IsCancellationRequested) { }
                finally
                {
                    lock (subscriptionSync)
                    {
                        if (ReferenceEquals(active, source)) { active = null; }
                    }

                    source.Dispose();
                }
            }
        }

        handle = new AppStateSubscription(() =>
        {
            lock (subscriptionSync)
            {
                ended = true;
                this.CancelOwned(active);
            }

            this.StateChangeWithMetadata -= Changed;
            lock (this.sync) { this.subscriptions.Remove(handle); }
        });
        lock (this.sync)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            this.StateChangeWithMetadata += Changed;
            this.subscriptions.Add(handle);
        }

        return handle;
    }
}
