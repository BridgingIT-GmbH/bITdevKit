// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents jobs release expired leases job.
/// </summary>
/// <param name="maintenance">The maintenance used by the operation.</param>
public sealed class JobsReleaseExpiredLeasesJob(IJobSchedulerMaintenanceService maintenance) : JobBase<JobReleaseExpiredLeasesJobData>
{
    /// <inheritdoc/>
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<JobReleaseExpiredLeasesJobData> context, CancellationToken cancellationToken = default)
    {
        var report = await maintenance.ReleaseExpiredLeasesAsync(context.Data ?? new JobReleaseExpiredLeasesJobData(), cancellationToken).ConfigureAwait(false);
        MaintenanceJobWriter.WriteReport(context, report);
        return Result.Success();
    }
}
