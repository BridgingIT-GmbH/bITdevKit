// File: BridgingIT.DevKit.Application.FileMonitoring/FileEvent.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;

/// <summary>
/// Represents a file event detected by the FileMonitoring system in the application layer.
/// This is the domain entity used by FileMonitoringService and processors, distinct from the persistence entity.
/// </summary>
public class FileEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for the file event.
    /// Generated automatically when the event is created.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name of the monitored location (e.g., "Docs") where the event occurred.
    /// Used to associate the event with a specific storage location.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the relative file path within the monitored location (e.g., "test/file.txt").
    /// Identifies the specific file affected by the event.
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// Gets or sets the type of event that occurred (e.g., Added, Changed, Deleted).
    /// Allows processors to respond appropriately to the event type.
    /// </summary>
    public FileEventType EventType { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was detected.
    /// Defaults to the current UTC time when the event is created.
    /// </summary>
    public DateTimeOffset DetectionTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the size of the file in bytes at the time of detection.
    /// Useful for tracking file changes and validation.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp of the file, if available.
    /// Nullable to accommodate cases where metadata is unavailable (e.g., deleted files).
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>
    /// Gets or sets the SHA256 checksum of the file content at the time of detection.
    /// Used for checksum-based change detection strategies.
    /// </summary>
    public string Checksum { get; set; }
}