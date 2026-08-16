// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents jobs detect orphaned runtime state job.
/// </summary>
/// <param name="maintenance">The maintenance used by the operation.</param>
public sealed class JobsDetectOrphanedRuntimeStateJob(IJobSchedulerMaintenanceService maintenance) : JobBase<JobDetectOrphanedRuntimeStateJobData>
{
    /// <inheritdoc/>
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<JobDetectOrphanedRuntimeStateJobData> context, CancellationToken cancellationToken = default)
    {
        var report = await maintenance.DetectOrphanedRuntimeStateAsync(context.Data ?? new JobDetectOrphanedRuntimeStateJobData(), cancellationToken).ConfigureAwait(false);
        MaintenanceJobWriter.WriteReport(context, report);
        return Result.Success();
    }
}
