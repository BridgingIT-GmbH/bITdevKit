// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using Microsoft.JSInterop;

/// <summary>Provides the historical custom-storage example with owner-local copied memory values.</summary>
/// <remarks>This example is not durable storage. Implement IAppStateStoreProvider for external storage.</remarks>
/// <example>
/// <code><![CDATA[
/// // The built-in example provider is copied in-memory storage, not durable storage.
/// services.AddAppState().AddCustomStateStoreProvider<CustomStorageStateStoreProvider>()
///     .AddState<CatalogViewState>().WithIdentity("catalog", "list").Done();
/// ]]></code>
/// </example>
public class CustomStorageStateStoreProvider : SessionScopedStateStoreProvider
{
    /// <summary>Creates the provider without browser dependencies.</summary>
    /// <example>
    /// <code><![CDATA[
    /// var storage = new CustomStorageStateStoreProvider();
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel());
    /// ]]></code>
    /// </example>
    public CustomStorageStateStoreProvider() { }

    /// <summary>Preserves the historical constructor without owning its dependencies.</summary>
    /// <param name="debugger">The optional observer.</param>
    /// <param name="jsRuntime">The optional browser runtime.</param>
    /// <example>
    /// <code><![CDATA[
    /// var storage = new CustomStorageStateStoreProvider(debugger, jsRuntime);
    /// await storage.SaveAsync("catalog-list", "anonymous:preview", new CatalogViewModel());
    /// ]]></code>
    /// </example>
    public CustomStorageStateStoreProvider(AppStateDebugger debugger, IJSRuntime jsRuntime) : base(debugger, jsRuntime) { }
}
