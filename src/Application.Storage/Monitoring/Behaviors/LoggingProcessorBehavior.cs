// File: BridgingIT.DevKit.Application.FileMonitoring/SampleProcessorsAndBehaviors.cs
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
            "Starting processing for file event: Location={LocationName}, Path={FilePath}, Type={EventType}",
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
                "Completed processing for file event: Location={LocationName}, Path={FilePath}, Type={EventType}",
                context.FileEvent.LocationName,
                context.FileEvent.FilePath,
                context.FileEvent.EventType);
        }
        else
        {
            this.logger.LogWarning(
                "Failed processing for file event: Location={LocationName}, Path={FilePath}, Type={EventType}, Error={ErrorMessage}",
                context.FileEvent.LocationName,
                context.FileEvent.FilePath,
                context.FileEvent.EventType,
                result.Errors.FirstOrDefault()?.Message);
        }

        await Task.CompletedTask;
    }
}
