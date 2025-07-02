namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.EntityFrameworkCore;
using System.Threading;

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
    private readonly SemaphoreSlim semaphore = new(1, 1); // Allows only one thread at a time

    public async Task<FileEvent> GetFileEventAsync(
        string filePath,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? tillDate = null,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            var entity = await this.context.FileEvents
                .Where(e => e.FilePath == filePath)
                .WhereExpressionIf(e => e.DetectedDate >= fromDate, fromDate != null)
                .WhereExpressionIf(e => e.DetectedDate <= tillDate, tillDate != null)
                .OrderByDescending(e => e.DetectedDate)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return this.MapToDomain(entity);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<FileEvent> GetFileEventAsync(
        string locationName,
        string filePath,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? tillDate = null,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            var entity = await this.context.FileEvents
                .Where(e => e.LocationName == locationName && e.FilePath == filePath)
                .WhereExpressionIf(e => e.DetectedDate >= fromDate, fromDate != null)
                .WhereExpressionIf(e => e.DetectedDate <= tillDate, tillDate != null)
                .OrderByDescending(e => e.DetectedDate)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            return this.MapToDomain(entity);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<IEnumerable<FileEvent>> GetFileEventsAsync(
        string filePath,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? tillDate = null,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            var entities = await this.context.FileEvents
                .Where(e => e.FilePath == filePath)
                .WhereExpressionIf(e => e.DetectedDate >= fromDate, fromDate != null)
                .WhereExpressionIf(e => e.DetectedDate <= tillDate, tillDate != null)
                .OrderByDescending(e => e.DetectedDate)
                .ToListAsync(cancellationToken: cancellationToken);

            return entities.Select(this.MapToDomain);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<List<FileEvent>> GetFileEventsForLocationAsync(
        string locationName,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? tillDate = null,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            var entities = await this.context.FileEvents
                .Where(e => e.LocationName == locationName)
                .WhereExpressionIf(e => e.DetectedDate >= fromDate, fromDate != null)
                .WhereExpressionIf(e => e.DetectedDate <= tillDate, tillDate != null)
                .OrderByDescending(e => e.DetectedDate)
                .ToListAsync(cancellationToken: cancellationToken);

            return entities.Select(this.MapToDomain).ToList();
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<List<string>> GetPresentFilesAsync(
        string locationName,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            var latestEvents = this.context.FileEvents
                .Where(e1 => e1.LocationName == locationName)
                .Where(e1 => !this.context.FileEvents
                    .Where(e2 => e2.LocationName == locationName && e2.FilePath == e1.FilePath)
                    .Any(e2 => e2.DetectedDate > e1.DetectedDate));

            return await latestEvents
                .Where(e => e.EventType != (int)FileEventType.Deleted)
                .Select(e => e.FilePath)
                .Distinct()
                .ToListAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task StoreEventAsync(
        FileEvent fileEvent,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            var entity = this.MapToEntity(fileEvent);
            this.context.FileEvents.Add(entity);

            await this.context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task StoreProcessingResultAsync(
        FileProcessingResult result,
        CancellationToken cancellationToken = default)
    {
        await this.semaphore.WaitAsync(cancellationToken);
        try
        {
            // Placeholder: ProcessingResult storage not yet fully defined.
            await Task.CompletedTask;
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    private FileEvent MapToDomain(FileEventEntity entity) =>
        entity == null
            ? null
            : new FileEvent
            {
                Id = entity.Id,
                ScanId = entity.ScanId ?? Guid.Empty,
                LocationName = entity.LocationName,
                FilePath = entity.FilePath,
                EventType = (FileEventType)entity.EventType,
                FileSize = entity.FileSize,
                DetectedDate = entity.DetectedDate,
                LastModifiedDate = entity.LastModifiedDate,
                Checksum = entity.Checksum,
                Properties = entity.Properties
            };

    private FileEventEntity MapToEntity(FileEvent fileEvent) =>
        new()
        {
            Id = fileEvent.Id,
            ScanId = fileEvent.ScanId,
            LocationName = fileEvent.LocationName,
            FilePath = fileEvent.FilePath,
            EventType = (int)fileEvent.EventType,
            FileSize = fileEvent.FileSize,
            DetectedDate = fileEvent.DetectedDate,
            LastModifiedDate = fileEvent.LastModifiedDate,
            Checksum = fileEvent.Checksum,
            Properties = fileEvent.Properties
        };
}