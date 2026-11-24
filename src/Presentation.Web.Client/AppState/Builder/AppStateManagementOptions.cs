// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Collects application-wide state configuration without retaining live state.</summary>
/// <example>
/// <code><![CDATA[
/// var registrations = scope.ServiceProvider.GetRequiredService<AppStateManagementOptions>();
/// Debug.Assert(registrations.StateConfigurations.ContainsKey(typeof(CatalogViewState)));
/// ]]></code>
/// </example>
public class AppStateManagementOptions
{
    /// <summary>Gets the registered concrete-state configurations.</summary>
    public Dictionary<Type, AppStateOptions> StateConfigurations { get; } = [];

    /// <summary>Gets or sets the application namespace for persistence.</summary>
    public string ApplicationId { get; set; } = "default";

    /// <summary>Gets or sets the default delay for later state registrations.</summary>
    public TimeSpan DefaultDebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets or sets the provider used only when no per-state provider was selected.</summary>
    public Type DefaultStorageProvider { get; set; }

    /// <summary>Gets the provider types already registered by these builders.</summary>
    public HashSet<Type> RegisteredProviders { get; } = [];
}
