// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Orchestrations;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines the public orchestration administration surface for maintenance and repair operations.
/// </summary>
public interface IOrchestrationAdministrationService
{
    /// <summary>
    /// Archives a terminal orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result including a status message.</returns>
    Task<Result<string>> ArchiveAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges retained orchestration data matching the supplied maintenance criteria.
    /// </summary>
    /// <param name="request">The purge request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The purge summary wrapped in the devkit result pattern.</returns>
    Task<Result<OrchestrationPurgeResult>> PurgeAsync(OrchestrationPurgeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an active lease for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result including a status message.</returns>
    Task<Result<string>> ReleaseLeaseAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues persisted timers for an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result including a status message.</returns>
    Task<Result<string>> RequeueTimersAsync(Guid instanceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the purge request used by orchestration administration endpoints and services.
/// </summary>
public class OrchestrationPurgeRequest
{
    /// <summary>
    /// Gets or sets the optional upper age filter.
    /// </summary>
    public DateTimeOffset? OlderThan { get; set; }

    /// <summary>
    /// Gets or sets the optional orchestration status filters.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the optional archive-state filter.
    /// </summary>
    public bool? IsArchived { get; set; }
}