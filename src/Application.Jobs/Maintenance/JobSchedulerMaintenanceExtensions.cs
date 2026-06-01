// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Provides opt-in registration helpers for built-in maintenance jobs.
/// </summary>
public static class JobSchedulerMaintenanceExtensions
{
    /// <summary>
    /// Registers the built-in maintenance jobs.
    /// </summary>
    /// <param name="context">The jobs builder context.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddJobScheduler()
    ///     .WithBuiltInMaintenanceJobs();
    /// </code>
    /// </example>
    public static JobBuilderContext WithBuiltInMaintenanceJobs(this JobBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context
            .WithJob<JobsArchiveOccurrencesJob>("jobs-archive-occurrences", job => job
                .Name("Archive Occurrences")
                .Description("Archives completed, failed, or cancelled occurrences after the configured retention window.")
                .Group("maintenance")
                .WithData<JobArchiveOccurrencesJobData>()
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<JobsPurgeHistoryJob>("jobs-purge-history", job => job
                .Name("Purge Job History")
                .Description("Purges archived job execution history older than the configured retention window.")
                .Group("maintenance")
                .WithData<JobPurgeHistoryJobData>()
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<JobsReleaseExpiredLeasesJob>("jobs-release-expired-leases", job => job
                .Name("Release Expired Leases")
                .Description("Releases expired scheduler leases and repairs the affected occurrences.")
                .Group("maintenance")
                .WithData<JobReleaseExpiredLeasesJobData>()
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<JobsRecoverStuckOccurrencesJob>("jobs-recover-stuck-occurrences", job => job
                .Name("Recover Stuck Occurrences")
                .Description("Recovers stale due or running occurrences that no longer hold an active lease.")
                .Group("maintenance")
                .WithData<JobRecoverStuckOccurrencesJobData>()
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<JobsDetectOrphanedRuntimeStateJob>("jobs-detect-orphaned-runtime-state", job => job
                .Name("Detect Orphaned Runtime State")
                .Description("Reports or removes runtime-state rows that no longer map to active registrations.")
                .Group("maintenance")
                .WithData<JobDetectOrphanedRuntimeStateJobData>()
                .AddTrigger("manual", trigger => trigger.Manual()));
    }
}