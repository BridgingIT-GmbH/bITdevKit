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

    /// <summary>Gets the processor name.</summary>
    public string ProcessorName => nameof(FileMoverProcessor);

    /// <summary>Gets or sets whether the processor is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets the behaviors applied around processing.</summary>
    public IEnumerable<IProcessorBehavior> Behaviors => [];

    /// <summary>Gets or sets the root directory to which files are moved.</summary>
    public string DestinationRoot { get; set; } // Public property for configuration

    /// <summary>Moves the supplied file into the configured destination root.</summary>
    /// <param name="context">The file-processing context containing the source provider and event.</param>
    /// <param name="token">The token used to cancel processing.</param>
    /// <returns>A task that represents the asynchronous move operation.</returns>
    public async Task ProcessAsync(FileProcessingContext context, CancellationToken token)
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
                    "[{LogKey}] filemonitoring: file moved successfully {SourcePath} to {DestinationPath}",
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
                "[{LogKey}] filemonitoring: failed to move file {SourcePath} to {DestinationPath}",
                Constants.LogKey,
                fileEvent.FilePath,
                destinationPath);
            throw;
        }
    }
}
