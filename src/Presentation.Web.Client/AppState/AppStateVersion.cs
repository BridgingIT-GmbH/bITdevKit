// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Identifies one accepted state and context revision in a live instance.</summary>
/// <param name="InstanceId">The nondurable runtime instance identity.</param>
/// <param name="Revision">The accepted edit revision.</param>
/// <param name="ContextRevision">The reset or identity/workspace generation.</param>
/// <example>
/// <code><![CDATA[
/// var version = state.Version;
/// var next = state.TakeSnapshot();
/// await Task.Delay(1);
/// next.Page = 2;
/// var accepted = await state.TryRestoreSnapshot(next, version);
/// Debug.Assert(accepted);
/// ]]></code>
/// </example>
public readonly record struct AppStateVersion(Guid InstanceId, long Revision, long ContextRevision);
