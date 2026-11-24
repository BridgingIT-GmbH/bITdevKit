// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Enumerates the possible persistence types for a app state.
/// </summary>
/// <example>
/// <code><![CDATA[
/// var options = new AppStateOptions
/// {
///     PersistenceType = AppStatePersistenceType.SessionScoped,
///     StorageProvider = typeof(SessionScopedStateStoreProvider)
/// };
/// ]]></code>
/// </example>
public enum AppStatePersistenceType
{
    /// <summary>
    /// Disables persistence; state lifetime follows its DI registration or explicit owner.
    /// </summary>
    ComponentScoped,

    /// <summary>
    /// Selects copied in-memory storage within the resolving DI scope.
    /// </summary>
    SessionScoped,

    /// <summary>
    /// State is persisted in browser localStorage, survives refreshes.
    /// </summary>
    LocalStorage,

    /// <summary>
    /// State is persisted using a custom provider (e.g., server-side storage).
    /// </summary>
    CustomStorage
}
