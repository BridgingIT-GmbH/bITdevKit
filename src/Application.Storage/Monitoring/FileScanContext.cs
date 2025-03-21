// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the context of a scan operation, including detected changes.
/// </summary>
public class FileScanContext
{
    /// <summary>
    /// Gets or sets the name of the monitored location where the scan occurred.
    /// </summary>
    public string LocationName { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the scan operation started.
    /// Defaults to the current UTC time when the context is created.
    /// </summary>
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the scan operation completed.
    /// Null if the scan is still in progress.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the collection of file events detected during the scan.
    /// </summary>
    public List<FileEvent> Events { get; set; } = [];
}