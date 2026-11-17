// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Owns a subscription without owning its state or injected services.</summary>
/// <param name="unsubscribe">The idempotent cleanup callback.</param>
/// <example>
/// <code><![CDATA[
/// Action<object> changed = value => Console.WriteLine(((CatalogViewModel)value).Page);
/// state.StateChanged += changed;
/// using var subscription = new AppStateSubscription(() => state.StateChanged -= changed);
/// await state.SetPage(2);
/// ]]></code>
/// </example>
public sealed class AppStateSubscription(Action unsubscribe) : IDisposable
{
    private Action unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));

    /// <summary>Releases the subscription and cancels its obsolete callbacks.</summary>
    /// <example>
    /// <code><![CDATA[
    /// var subscription = state.Subscribe(model => model.Page,
    /// (change, token) => Task.CompletedTask);
    /// subscription.Dispose(); // Dispose when this consumer unmounts.
    /// ]]></code>
    /// </example>
    public void Dispose() => Interlocked.Exchange(ref this.unsubscribe, null)?.Invoke();
}
