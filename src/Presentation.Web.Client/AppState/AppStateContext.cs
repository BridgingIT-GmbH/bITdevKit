// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Supplies host-established opaque identity and workspace partitions.</summary>
/// <param name="PrincipalKey">The principal key, or null to use the user-context provider.</param>
/// <param name="WorkspaceKey">The workspace key, or null for no workspace.</param>
/// <remarks>These keys partition state and do not establish authorization.</remarks>
/// <example>
/// <code><![CDATA[
/// // These opaque keys have already been accepted by the host's context owner.
/// await manager.ChangeContext(new AppStateContext(confirmedPrincipalKey, confirmedWorkspaceKey));
/// ]]></code>
/// </example>
public sealed record AppStateContext(string PrincipalKey = null, string WorkspaceKey = null);
