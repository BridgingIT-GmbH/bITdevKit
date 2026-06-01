// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Provides provider-neutral maintenance operations over scheduler state.
/// </summary>
public interface IJobSchedulerMaintenanceService
{
    /// <summary>
    /// Archives completed, failed, or cancelled occurrences after a retention window.
    /// </summary>
    /// <param name="options">The archive options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance report.</returns>
    Task<JobMaintenanceReport> ArchiveOccurrencesAsync(JobArchiveOccurrencesJobData options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges retained occurrences and their dependent persisted records.
    /// </summary>
    /// <param name="request">The purge request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance report.</returns>
    Task<JobMaintenanceReport> PurgeOccurrencesAsync(JobPurgeOccurrencesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges archived execution history that is older than the configured retention window.
    /// </summary>
    /// <param name="options">The purge options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance report.</returns>
    Task<JobMaintenanceReport> PurgeHistoryAsync(JobPurgeHistoryJobData options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases expired leases and repairs the affected occurrences.
    /// </summary>
    /// <param name="options">The lease-release options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance report.</returns>
    Task<JobMaintenanceReport> ReleaseExpiredLeasesAsync(JobReleaseExpiredLeasesJobData options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers stale occurrences that no longer have a valid active lease.
    /// </summary>
    /// <param name="options">The stuck-occurrence recovery options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance report.</returns>
    Task<JobMaintenanceReport> RecoverStuckOccurrencesAsync(JobRecoverStuckOccurrencesJobData options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects runtime-state rows that no longer correspond to active code-first registrations.
    /// </summary>
    /// <param name="options">The orphan-detection options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance report.</returns>
    Task<JobMaintenanceReport> DetectOrphanedRuntimeStateAsync(JobDetectOrphanedRuntimeStateJobData options, CancellationToken cancellationToken = default);
}