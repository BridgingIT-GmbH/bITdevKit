// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Globalization;
using System.Text;
using BridgingIT.DevKit.Common;

/// <summary>Builds unambiguous versioned keys for safe optional preferences.</summary>
/// <example>
/// <code><![CDATA[
/// var key = AppStateStorageKey.Create("catalog-console", "catalog", "list", "left-pane",
///     "user:operator", "workspace:north", 1);
/// Debug.Assert(key.StartsWith(AppStateStorageKey.Prefix, StringComparison.Ordinal));
/// ]]></code>
/// </example>
public static class AppStateStorageKey
{
    /// <summary>Gets the prefix reserved for canonical AppState storage keys.</summary>
    public const string Prefix = "bdk:appstate:1:";

    /// <summary>Encodes every independently owned partition dimension.</summary>
    /// <param name="application">The application identity.</param>
    /// <param name="module">The logical module identity.</param>
    /// <param name="state">The logical state or view identity.</param>
    /// <param name="instance">The stable consumer instance key.</param>
    /// <param name="principal">The identity-kind-qualified opaque principal key.</param>
    /// <param name="workspace">The workspace partition.</param>
    /// <param name="schema">The positive persisted schema version.</param>
    /// <returns>The canonical physical storage key.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var key = AppStateStorageKey.Create("catalog-console", "catalog", "list", "left-pane",
    /// "user:operator", "workspace:north", 1);
    /// Debug.Assert(key.StartsWith(AppStateStorageKey.Prefix, StringComparison.Ordinal));
    /// ]]></code>
    /// </example>
    public static string Create(string application, string module, string state, string instance,
        string principal, string workspace, int schema)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schema);
        return Prefix + string.Join(":", new[] { application, module, state, instance, principal, workspace }

            .Select(value => Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(value ?? string.Empty))))
            + ":" + schema.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Preserves old provider calls while using canonical keys without a suffix.</summary>
    /// <param name="stateId">The logical or canonical state key.</param>
    /// <param name="userContextId">The legacy user partition.</param>
    /// <returns>The exact physical key.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var legacy = AppStateStorageKey.Resolve("catalog-list", "anonymous:preview");
    /// Debug.Assert(legacy == "catalog-list_anonymous:preview");
    /// ]]></code>
    /// </example>
    public static string Resolve(string stateId, string userContextId) =>
        stateId.StartsWith(Prefix, StringComparison.Ordinal) ? stateId : $"{stateId}_{userContextId}";
}
