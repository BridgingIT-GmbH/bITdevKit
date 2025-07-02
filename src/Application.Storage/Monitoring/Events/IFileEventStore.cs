// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the contract for storing and retrieving FileEvent instances in the FileMonitoring system.
/// Acts as a bridge between the domain layer and persistence layer, abstracting storage details.
/// </summary>
public interface IFileEventStore
{
    /// <summary>
    /// Retrieves the most recent FileEvent for a given file path.
    /// </summary>
    /// <param name="filePath">The relative file path to query.</param>
    /// <returns>The latest FileEvent, or null if none exists.</returns>
    Task<FileEvent> GetFileEventAsync(string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tetrieves a file event based on a specified location and file path.
    /// </summary>
    /// <param name="locationName">Specifies the name of the location where the file is located.</param>
    /// <param name="filePath">Indicates the path of the file for which the event is being retrieved.</param>
    /// <returns>Returns a task that represents the asynchronous operation, containing the file event.</returns>
    Task<FileEvent> GetFileEventAsync(string locationName, string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a collection of file events related to a specified file.
    /// </summary>
    /// <param name="filePath">Specifies the location of the file for which events are being retrieved.</param>
    /// <returns>Returns a task that resolves to an enumerable collection of file events.</returns>
    Task<IEnumerable<FileEvent>> GetFileEventsAsync(string filePath, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all FileEvents for a specific location.
    /// </summary>
    /// <param name="locationName">The name of the monitored location.</param>
    /// <returns>A list of FileEvents for the location.</returns>
    Task<List<FileEvent>> GetFileEventsForLocationAsync(string locationName, DateTimeOffset? fromDate = null, DateTimeOffset? tillDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of file paths currently considered "present" (not deleted) in a location.
    /// Used for deletion detection during scans.
    /// </summary>
    /// <param name="locationName">The name of the monitored location.</param>
    /// <returns>A list of present file paths.</returns>
    Task<List<string>> GetPresentFilesAsync(string locationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new FileEvent in the persistence layer.
    /// </summary>
    /// <param name="fileEvent">The FileEvent to store.</param>
    Task StoreEventAsync(FileEvent fileEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the result of processing a FileEvent by a processor.
    /// </summary>
    /// <param name="result">The ProcessingResult to store.</param>
    Task StoreProcessingResultAsync(FileProcessingResult result, CancellationToken cancellationToken = default);
}