// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines debugging options for the state management system.
/// </summary>
/// <example>
/// <code><![CDATA[
/// services.AddAppState().WithDebugging(options =>
/// {
///     options.StateChangesTracked = true;
///     options.MaxTrackedChanges = 100;
///     options.ValueProjection = value => (value as CatalogViewModel)?.SelectedIds.Count;
/// });
/// ]]></code>
/// </example>
public class AppStateDebugOptions
{
    /// <summary>Gets or sets the positive bounded tracking capacity. Default is 100.</summary>
    public int MaxTrackedChanges { get; set; } = 100;

    /// <summary>Gets or sets an explicit safe value projection. Null retains no model values.</summary>
    public Func<object, object> ValueProjection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled for state changes.
    /// </summary>
    public bool LoggingEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether state changes should be tracked in memory.
    /// </summary>
    public bool StateChangesTracked { get; set; }
}
