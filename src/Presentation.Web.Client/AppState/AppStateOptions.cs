// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines configuration options for a managed state instance.
/// </summary>
/// <example>
/// <code><![CDATA[
/// var options = new AppStateOptions
/// {
///     ApplicationId = "catalog-console", ModuleId = "catalog", StateName = "list",
///     InstanceKey = "left-pane", HistoryEnabled = true, MaxHistoryItems = 10,
///     JsonTypeInfo = CatalogJson.Default.CatalogViewModel
/// };
/// using var state = new CatalogViewState(NullLogger<CatalogViewState>.Instance, options);
/// ]]></code>
/// </example>
public class AppStateOptions
{
    /// <summary>Gets or sets the application namespace for persisted keys.</summary>
    public string ApplicationId { get; set; } = "default";

    /// <summary>Gets or sets the logical owner namespace.</summary>
    public string ModuleId { get; set; } = "host";

    /// <summary>Gets or sets an explicit stable state name; null uses StateId.</summary>
    public string StateName { get; set; }

    /// <summary>Gets or sets the stable consumer instance key.</summary>
    public string InstanceKey { get; set; } = "default";

    /// <summary>Gets or sets the positive persisted schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets generated metadata for model cloning and persistence.</summary>
    public System.Text.Json.Serialization.Metadata.JsonTypeInfo JsonTypeInfo { get; set; }

    /// <summary>Gets or sets the exact legacy state key selected for preference import.</summary>
    public string LegacyStateId { get; set; }

    /// <summary>Gets or sets the owner-confirmed legacy user partition.</summary>
    public string LegacyUserContextId { get; set; }

    /// <summary>Gets or sets the explicitly confirmed target workspace for legacy import; null is unscoped.</summary>
    public string LegacyWorkspaceKey { get; set; }

    /// <summary>Copies the configuration for an independently owned state instance.</summary>
    /// <returns>A shallow configuration copy; JSON metadata remains shared configuration.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var original = new AppStateOptions { InstanceKey = "left-pane" };
    /// var owned = original.Copy();
    /// owned.InstanceKey = "right-pane";
    /// Debug.Assert(original.InstanceKey == "left-pane");
    /// ]]></code>
    /// </example>
    public AppStateOptions Copy()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(this.ApplicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(this.ModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(this.InstanceKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.SchemaVersion);
        ArgumentOutOfRangeException.ThrowIfLessThan(this.DebounceDelay, TimeSpan.Zero);
        if (this.HistoryEnabled) { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(this.MaxHistoryItems); }

        return (AppStateOptions)this.MemberwiseClone();
    }

    /// <summary>
    /// Gets or sets whether optional history and persistence are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the persistence level for the state (e.g., ComponentScoped, LocalStorage).
    /// </summary>
    public AppStatePersistenceType PersistenceType { get; set; } = AppStatePersistenceType.ComponentScoped;

    /// <summary>
    /// Gets or sets the type of the storage provider, if persistence is enabled.
    /// </summary>
    public Type StorageProvider { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether history tracking (undo/redo) is enabled.
    /// </summary>
    public bool HistoryEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of history items to retain.
    /// </summary>
    public int MaxHistoryItems { get; set; }

    /// <summary>
    /// Gets or sets the debounce delay for saving state to the storage provider.
    /// </summary>
    /// <remarks>
    /// A value of TimeSpan.Zero disables debouncing, causing immediate saves.
    /// Default is 500ms.
    /// </remarks>
    public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
