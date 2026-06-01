// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

public sealed class JobsPurgeHistoryJob(IJobSchedulerMaintenanceService maintenance) : JobBase<JobPurgeHistoryJobData>
{
    public override async Task<Result> ExecuteAsync(IJobExecutionContext<JobPurgeHistoryJobData> context, CancellationToken cancellationToken = default)
    {
        var report = await maintenance.PurgeHistoryAsync(context.Data ?? new JobPurgeHistoryJobData(), cancellationToken).ConfigureAwait(false);
        MaintenanceJobWriter.WriteReport(context, report);
        return Result.Success();
    }
}
