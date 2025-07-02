﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Diagnostics;

/// <summary>
/// Represents a file event detected by the FileMonitoring system in the application layer.
/// This is the domain entity used by FileMonitoringService and processors, distinct from the persistence entity.
/// </summary>
[DebuggerDisplay("Path={FilePath}, Location={LocationName}, Type={EventType.ToString()}")]
public class FileEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for the file event.
    /// Generated automatically when the event is created.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Represents a unique identifier for a scan,
    /// </summary>
    public Guid ScanId { get; set; }

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
    public DateTimeOffset DetectedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the size of the file in bytes at the time of detection.
    /// Useful for tracking file changes and validation.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp of the file, if available.
    /// Nullable to accommodate cases where metadata is unavailable (e.g., deleted files).
    /// </summary>
    public DateTimeOffset? LastModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the SHA256 checksum of the file content at the time of detection.
    /// Used for checksum-based change detection strategies.
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Gets or sets additional properties associated with the file event.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}