// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Groups the orchestration persistence services required by the runtime and operations layer.
/// </summary>
public interface IOrchestrationStorageProvider
{
    /// <summary>
    /// Gets the orchestration instance snapshot store.
    /// </summary>
    IOrchestrationInstanceStore Instances { get; }

    /// <summary>
    /// Gets the orchestration lease store.
    /// </summary>
    IOrchestrationLeaseStore Leases { get; }

    /// <summary>
    /// Gets the append-only orchestration history store.
    /// </summary>
    IOrchestrationHistoryStore History { get; }

    /// <summary>
    /// Gets the orchestration signal store.
    /// </summary>
    IOrchestrationSignalStore Signals { get; }

    /// <summary>
    /// Gets the orchestration timer store.
    /// </summary>
    IOrchestrationTimerStore Timers { get; }

    /// <summary>
    /// Gets the orchestration query store.
    /// </summary>
    IOrchestrationQueryStore Queries { get; }

    /// <summary>
    /// Gets the orchestration administration store.
    /// </summary>
    IOrchestrationAdministrationStore Administration { get; }

    /// <summary>
    /// Gets the serializer used for durable orchestration payloads.
    /// </summary>
    ISerializer Serializer { get; }
}
