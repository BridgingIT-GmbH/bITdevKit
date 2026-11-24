// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

/// <summary>Describes a live owner-local registration without exposing its values.</summary>
/// <param name="InstanceId">The runtime identity, distinct from durable storage keys.</param>
/// <param name="StateId">The logical state identity.</param>
/// <param name="StateType">The concrete registered service type.</param>
/// <param name="Revision">The accepted edit revision.</param>
/// <param name="ContextRevision">The current invalidation generation.</param>
/// <param name="PersistenceStatus">The current persistence status.</param>
/// <example>
/// <code><![CDATA[
/// AppStateInstanceInfo instance = manager.GetInstances().First();
/// Console.WriteLine($"{instance.StateType.Name}: {instance.PersistenceStatus}");
/// ]]></code>
/// </example>
public sealed record AppStateInstanceInfo(Guid InstanceId, string StateId, Type StateType,
    long Revision, long ContextRevision, AppStatePersistenceStatus PersistenceStatus);
