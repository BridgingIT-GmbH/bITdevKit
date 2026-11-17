// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Transfers copied model values under a logical instance, context and schema key.</summary>
/// <typeparam name="TState">The feature-owned model.</typeparam>
/// <param name="Key">The origin's logical handoff key; it does not grant authorization.</param>
/// <param name="Model">The independent model values approved for transfer.</param>
/// <remarks>Runtime identities, versions, history, subscriptions and services are never transferred.</remarks>
/// <example>
/// <code><![CDATA[
/// var handoff = source.CaptureHandoff();
/// var accepted = destination.TryRestoreHandoff(handoff, destination.Version);
/// Debug.Assert(accepted);
/// ]]></code>
/// </example>
public sealed record AppStateHandoff<TState>(string Key, TState Model);
