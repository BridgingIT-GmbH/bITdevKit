// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

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

    public Task ProcessAsync(FileProcessingContext context, CancellationToken token)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        token.ThrowIfCancellationRequested();

        this.logger.LogInformation(
            "{LogKey} filemonitoring: file event processed (Location={LocationName}, Path={FilePath}, Type={EventType}, Size={FileSize}, Checksum={Checksum})",
            Constants.LogKey,
            context.FileEvent.LocationName,
            context.FileEvent.FilePath,
            context.FileEvent.EventType,
            context.FileEvent.FileSize,
            context.FileEvent.Checksum);

        return Task.CompletedTask;
    }
}