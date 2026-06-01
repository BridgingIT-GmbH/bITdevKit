// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

internal static class MaintenanceJobWriter
{
    internal static void WriteReport<TData>(IJobExecutionContext<TData> context, JobMaintenanceReport report)
    {
        context.Messages.Add($"{report.Operation}: matched={report.MatchedCount}, processed={report.ProcessedCount}, remaining={report.RemainingCount}, dryRun={report.DryRun}".ToLowerInvariant());
        foreach (var diagnostic in report.Diagnostics)
        {
            context.Messages.Add(diagnostic);
        }

        foreach (var affectedId in report.AffectedIds.Take(10))
        {
            context.Messages.Add($"affected={affectedId}");
        }
    }
}