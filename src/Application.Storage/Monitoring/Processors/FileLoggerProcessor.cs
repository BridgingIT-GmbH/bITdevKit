// File: BridgingIT.DevKit.Application.FileMonitoring/SampleProcessorsAndBehaviors.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Logs file events to the ILogger, capturing event details for auditing or debugging.
/// </summary>
public class FileLoggerProcessor(ILogger<FileLoggerProcessor> logger) : IFileEventProcessor
{
    private readonly ILogger<FileLoggerProcessor> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string ProcessorName => nameof(FileLoggerProcessor);

    public bool IsEnabled { get; set; } = true;

    public IEnumerable<IProcessorBehavior> Behaviors => [];

    public Task ProcessAsync(ProcessingContext context, CancellationToken token)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        token.ThrowIfCancellationRequested();

        var fileEvent = context.FileEvent;
        this.logger.LogInformation(
            "File event processed: Location={LocationName}, Path={FilePath}, Type={EventType}, Size={FileSize}, Checksum={Checksum}",
            fileEvent.LocationName,
            fileEvent.FilePath,
            fileEvent.EventType,
            fileEvent.FileSize,
            fileEvent.Checksum);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Custom exception to signal a retry operation within the RetryProcessorBehavior.
/// </summary>
public class RetryException(string message) : Exception(message)
{
}