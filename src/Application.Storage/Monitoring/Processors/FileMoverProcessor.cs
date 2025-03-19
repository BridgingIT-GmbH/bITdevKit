// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Moves files to a specified destination directory based on the file event, preserving the original structure.
/// </summary>
public class FileMoverProcessor(ILogger<FileMoverProcessor> logger) : IFileEventProcessor
{
    private readonly ILogger<FileMoverProcessor> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string ProcessorName => nameof(FileMoverProcessor);

    public bool IsEnabled { get; set; } = true;

    public IEnumerable<IProcessorBehavior> Behaviors => [];

    public string DestinationRoot { get; set; } // Public property for configuration

    public async Task ProcessAsync(ProcessingContext context, CancellationToken token)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (string.IsNullOrEmpty(this.DestinationRoot))
        {
            throw new InvalidOperationException("DestinationRoot must be configured for FileMoverProcessor.");
        }
        token.ThrowIfCancellationRequested();

        var fileEvent = context.FileEvent;
        var sourceProvider = context.GetItem<IFileStorageProvider>("StorageProvider");
        if (sourceProvider == null)
        {
            throw new InvalidOperationException("StorageProvider not available in ProcessingContext.");
        }

        var destinationPath = Path.Combine(this.DestinationRoot, fileEvent.FilePath);
        var destinationDir = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrEmpty(destinationDir))
        {
            await sourceProvider.CreateDirectoryAsync(destinationDir, token);
        }

        try
        {
            var moveResult = await sourceProvider.MoveFileAsync(fileEvent.FilePath, destinationPath, null, token);
            if (moveResult.IsSuccess)
            {
                this.logger.LogInformation(
                    "{LogKey} filemonitoring: file moved successfully {SourcePath} to {DestinationPath}",
                    Constants.LogKey,
                    fileEvent.FilePath,
                    destinationPath);
            }
            else
            {
                throw new IOException($"Failed to move file: {moveResult.Messages.FirstOrDefault()}");
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "{LogKey} filemonitoring: failed to move file {SourcePath} to {DestinationPath}",
                Constants.LogKey,
                fileEvent.FilePath,
                destinationPath);
            throw;
        }
    }
}