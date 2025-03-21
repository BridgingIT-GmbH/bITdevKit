// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implements IFileEventStore using EF Core, storing FileEvent instances as FileEventEntity in a DbContext.
/// Requires a TContext that implements IFileMonitoringContext for compatibility with existing application DbContexts.
/// </summary>
/// <typeparam name="TContext">The DbContext type, must implement IFileMonitoringContext.</typeparam>
/// <remarks>
/// Initializes a new instance of the EntityFrameworkFileEventStore with the specified DbContext.
/// </remarks>
/// <param name="context">The DbContext instance implementing IFileMonitoringContext.</param>
public class EntityFrameworkFileEventStore<TContext>(TContext context) : IFileEventStore
    where TContext : DbContext, IFileMonitoringContext
{
    private readonly TContext context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<FileEvent> GetFileEventAsync(string filePath)
    {
        var entity = await this.context.FileEvents
            .OrderByDescending(e => e.DetectionTime)
            .FirstOrDefaultAsync(e => e.FilePath == filePath);

        return this.MapToDomain(entity);
    }

    public async Task<FileEvent> GetFileEventAsync(string locationName, string filePath)
    {
        var entity = await this.context.FileEvents
            .Where(e => e.LocationName == locationName && e.FilePath == filePath)
            .OrderByDescending(e => e.DetectionTime)
            .FirstOrDefaultAsync(e => e.FilePath == filePath);

        return this.MapToDomain(entity);
    }

    public async Task<IEnumerable<FileEvent>> GetFileEventsAsync(string filePath)
    {
        var entities = await this.context.FileEvents
            .Where(e => e.FilePath == filePath)
            .OrderByDescending(e => e.DetectionTime).ToListAsync();

        return entities.Select(this.MapToDomain);
    }

    /// <summary>
    /// Retrieves a list of file events associated with a specific location asynchronously.
    /// </summary>
    /// <param name="locationName">Specifies the name of the location for which file events are being retrieved.</param>
    /// <returns>A list of file events mapped to the domain model.</returns>
    public async Task<List<FileEvent>> GetFileEventsForLocationAsync(string locationName)
    {
        var entities = await this.context.FileEvents
            .Where(e => e.LocationName == locationName)
            .OrderByDescending(e => e.DetectionTime).ToListAsync();

        return entities.Select(this.MapToDomain).ToList();
    }

    /// <summary>
    /// Retrieves a list of file paths that are present at a specified location, excluding deleted files.
    /// </summary>
    /// <param name="locationName">Specifies the location to filter the file events.</param>
    /// <returns>A list of file paths that are currently present at the specified location.</returns>
    public async Task<List<string>> GetPresentFilesAsync(string locationName)
    {
        var latestEvents = this.context.FileEvents
            .Where(e1 => e1.LocationName == locationName)
            .Where(e1 => !this.context.FileEvents
                .Where(e2 => e2.LocationName == locationName && e2.FilePath == e1.FilePath)
                .Any(e2 => e2.DetectionTime > e1.DetectionTime));


        return await latestEvents
            .Where(e => e.EventType != (int)FileEventType.Deleted)
            .Select(e => e.FilePath)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// Stores a file event in the database asynchronously. It maps the provided event to an entity and saves it.
    /// </summary>
    /// <param name="fileEvent">An object representing the details of the file event to be stored.</param>
    /// <returns>This method does not return a value.</returns>
    public async Task StoreEventAsync(FileEvent fileEvent)
    {
        var entity = this.MapToEntity(fileEvent);
        this.context.FileEvents.Add(entity);

        await this.context.SaveChangesAsync();
    }

    /// <summary>
    /// Stores the processing result asynchronously, with current implementation logging or skipping persistence.
    /// </summary>
    /// <param name="result">Contains the outcome of a processing operation to be stored or logged.</param>
    /// <returns>Completes a task indicating the operation has finished.</returns>
    public async Task StoreProcessingResultAsync(ProcessingResult result)
    {
        // Placeholder: ProcessingResult storage not yet fully defined.
        // For now, we'll log it or skip persistence until Step 7 clarifies requirements.
        // If persisted, it could be a separate DbSet or embedded in FileEventEntity.
        await Task.CompletedTask;
    }

    private FileEvent MapToDomain(FileEventEntity entity) =>
        entity == null
            ? null
            : new FileEvent
            {
                Id = entity.Id,
                LocationName = entity.LocationName,
                FilePath = entity.FilePath,
                EventType = (FileEventType)entity.EventType,
                DetectionTime = entity.DetectionTime,
                FileSize = entity.FileSize,
                LastModified = entity.LastModified,
                Checksum = entity.Checksum
            };

    private FileEventEntity MapToEntity(FileEvent fileEvent) =>
        new FileEventEntity
        {
            Id = fileEvent.Id,
            LocationName = fileEvent.LocationName,
            FilePath = fileEvent.FilePath,
            EventType = (int)fileEvent.EventType,
            DetectionTime = fileEvent.DetectionTime,
            FileSize = fileEvent.FileSize,
            LastModified = fileEvent.LastModified,
            Checksum = fileEvent.Checksum
        };
}