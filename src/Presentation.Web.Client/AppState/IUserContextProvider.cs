// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>
/// Defines the contract for user context providers, generating unique identifiers for state isolation.
/// </summary>
/// <example>
/// <code><![CDATA[
/// IUserContextProvider context = new LocalStorageUserContextProvider(jsRuntime, null, "catalog-console");
/// var first = await context.GetUserContextId();
/// Debug.Assert(first == await context.GetUserContextId());
/// ]]></code>
/// </example>
public interface IUserContextProvider
{
    /// <summary>
    /// Gets or generates a unique identifier for the current user context.
    /// </summary>
    /// <returns>A string representing the user context ID.</returns>
    /// <example>
    /// <code><![CDATA[
    /// var first = await userContext.GetUserContextId();
    /// Debug.Assert(first == await userContext.GetUserContextId());
    /// ]]></code>
    /// </example>
    Task<string> GetUserContextId();

    /// <summary>
    /// Clears the stored user context ID, if any.
    /// </summary>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    /// <example>
    /// <code><![CDATA[
    /// await userContext.Clear();
    /// var replacement = await userContext.GetUserContextId();
    /// Debug.Assert(!string.IsNullOrEmpty(replacement));
    /// ]]></code>
    /// </example>
    Task Clear();
}
