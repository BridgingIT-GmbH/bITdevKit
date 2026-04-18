// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// Represents the result of triggering a monitoring scan for a provider-backed location.
/// </summary>
public class FileStorageFileEventScanResponseModel
{
    /// <summary>
    /// Gets or sets the file storage provider registration name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the monitored location name that was scanned.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the completed scan.
    /// </summary>
    public Guid ScanId { get; set; }

    /// <summary>
    /// Gets or sets the scan start timestamp.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the scan completion timestamp.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the number of events detected during the scan.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Gets or sets the events detected during the scan.
    /// </summary>
    public FileStorageFileEventModel[] Events { get; set; } = [];
}
