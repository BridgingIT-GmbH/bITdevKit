// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Interface for DbContext implementations to support FileMonitoring persistence.
/// Existing application DbContexts can implement this to include the FileEvents DbSet.
/// </summary>
public interface IFileMonitoringContext
{
    /// <summary>
    /// Gets or sets the DbSet for FileEventEntity, representing the "__Storage_FileEvents" table.
    /// </summary>
    DbSet<FileEventEntity> FileEvents { get; set; }
}