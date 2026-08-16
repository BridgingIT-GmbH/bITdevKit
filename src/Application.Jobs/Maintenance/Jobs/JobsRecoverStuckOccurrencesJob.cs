// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Represents jobs recover stuck occurrences job.
/// </summary>
/// <param name="maintenance">The maintenance used by the operation.</param>
public sealed class JobsRecoverStuckOccurrencesJob(IJobSchedulerMaintenanceService maintenance) : JobBase<JobRecoverStuckOccurrencesJobData>
{
    /// <inheritdoc/>
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<JobRecoverStuckOccurrencesJobData> context, CancellationToken cancellationToken = default)
    {
        var report = await maintenance.RecoverStuckOccurrencesAsync(context.Data ?? new JobRecoverStuckOccurrencesJobData(), cancellationToken).ConfigureAwait(false);
        MaintenanceJobWriter.WriteReport(context, report);
        return Result.Success();
    }
}
