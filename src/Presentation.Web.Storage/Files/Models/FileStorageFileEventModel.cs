// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents a file event exposed through the provider-scoped storage REST API.
/// </summary>
public class FileStorageFileEventModel
{
    /// <summary>
    /// Gets or sets the unique identifier of the file event.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the scan identifier that produced the event.
    /// </summary>
    public Guid ScanId { get; set; }

    /// <summary>
    /// Gets or sets the monitored location name.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the affected file path relative to the monitored location.
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// Gets or sets the detected event type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was detected.
    /// </summary>
    public DateTimeOffset DetectedDate { get; set; }

    /// <summary>
    /// Gets or sets the detected file size in bytes when available.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Gets or sets the last modified timestamp when available.
    /// </summary>
    public DateTimeOffset? LastModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the checksum captured for the event when available.
    /// </summary>
    public string Checksum { get; set; }
}
