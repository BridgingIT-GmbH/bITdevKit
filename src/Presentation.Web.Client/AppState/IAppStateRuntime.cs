// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Connects managed state to its explicit runtime owner.</summary>
/// <remarks>Ordinary consumers use the state or a factory lease. Custom implementations can opt into owner lifecycle integration.</remarks>
/// <example>
/// <code><![CDATA[
/// IAppStateRuntime runtime = state;
/// var version = runtime.Version;
/// Debug.Assert(version.InstanceId == runtime.InstanceId);
/// ]]></code>
/// </example>
public interface IAppStateRuntime : IAppState
{
    /// <summary>Gets the stable identity of this live instance.</summary>
    Guid InstanceId { get; }

    /// <summary>Gets the current edit and context revisions.</summary>
    AppStateVersion Version { get; }

    /// <summary>Gets the persistence status of this owner.</summary>
    AppStatePersistenceStatus PersistenceStatus { get; }

    /// <summary>Gets the local identity used to reject competing active persisted registrations.</summary>
    string PersistenceIdentity { get; }

    /// <summary>Attaches runtime services without transferring dependency ownership.</summary>
    /// <param name="owner">The originating scoped manager.</param>
    /// <param name="timeProvider">The clock used for owned delays.</param>
    /// <example>
    /// <code><![CDATA[
    /// using var owned = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// IAppStateRuntime runtime = owned;
    /// runtime.AttachOwner(manager, TimeProvider.System);
    /// manager.RegisterState(owned);
    /// ]]></code>
    /// </example>
    void AttachOwner(AppStateManager owner, TimeProvider timeProvider);

    /// <summary>Invalidates old values before returning the asynchronous cleanup task.</summary>
    /// <param name="context">The accepted host context.</param>
    /// <returns>Cleanup of the old captured storage partition.</returns>
    /// <remarks>For attached states, the manager supplies its accepted context instance. Superseded owner calls are ignored.
    /// Standalone states may change context directly.</remarks>
    /// <example>
    /// <code><![CDATA[
    /// await using var owned = new CatalogViewState(NullLogger<CatalogViewState>.Instance, new AppStateOptions());
    /// IAppStateRuntime runtime = owned;
    /// await runtime.ChangeContext(new AppStateContext(confirmedPrincipalKey, confirmedWorkspaceKey));
    /// ]]></code>
    /// </example>
    Task ChangeContext(AppStateContext context);
}
