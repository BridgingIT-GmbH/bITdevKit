// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

[ExcludeFromCodeCoverage]
public class LongRunningJob(ILoggerFactory loggerFactory) : JobBase(loggerFactory)
{
    public override async Task Process(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        //var ctx = context.CancellationToken;
        //try
        //{
        for (var i = 0; i < 100; i++)
        {
            // Check if cancellation is requested
            //if (context.CancellationToken.IsCancellationRequested)
            //{
            //    this.Logger.LogWarning("{LogKey} interrupted, stopping (jobKey={JobKey})", Constants.LogKey, context.JobDetail.Key);

            //    return;
            //}
            context.CancellationToken.ThrowIfCancellationRequested();

            this.Logger.LogInformation("{LogKey} processing step {Step} (jobKey={JobKey})", Constants.LogKey, i, context.JobDetail.Key);
            await Task.Delay(5000, context.CancellationToken); // Pass the token to async operations
        }
        //}
        //catch (TaskCanceledException)
        //{
        //    this.Logger.LogWarning("{LogKey} cancelled (jobKey={JobKey})", Constants.LogKey, context.JobDetail.Key);
        //}
    }
}