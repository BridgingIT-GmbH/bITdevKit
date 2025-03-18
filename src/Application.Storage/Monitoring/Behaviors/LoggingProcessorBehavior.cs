// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Logs processor execution details before and after processing a file event.
/// </summary>
public class LoggingProcessorBehavior(ILogger<LoggingProcessorBehavior> logger) : IProcessorBehavior
{
    private readonly ILogger<LoggingProcessorBehavior> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task BeforeProcessAsync(ProcessingContext context, CancellationToken token)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        token.ThrowIfCancellationRequested();

        this.logger.LogDebug(
            "{LogKey} filemonitoring: starting processing for file event (Location={LocationName}, Path={FilePath}, Type={EventType})",
            Constants.LogKey,
            context.FileEvent.LocationName,
            context.FileEvent.FilePath,
            context.FileEvent.EventType);

        await Task.CompletedTask;
    }

    public async Task AfterProcessAsync(ProcessingContext context, Result<bool> result, CancellationToken token)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        token.ThrowIfCancellationRequested();

        if (result.IsSuccess)
        {
            this.logger.LogDebug(
                "{LogKey} filemonitoring: completed processing for file event (Location={LocationName}, Path={FilePath}, Type={EventType})",
                Constants.LogKey,
                context.FileEvent.LocationName,
                context.FileEvent.FilePath,
                context.FileEvent.EventType);
        }
        else
        {
            this.logger.LogWarning(
                "{LogKey} filemonitoring: failed processing for file event (Location={LocationName}, Path={FilePath}, Type={EventType}, Error={ErrorMessage})",
                Constants.LogKey,
                context.FileEvent.LocationName,
                context.FileEvent.FilePath,
                context.FileEvent.EventType,
                result.Errors.FirstOrDefault()?.Message);
        }

        await Task.CompletedTask;
    }
}
