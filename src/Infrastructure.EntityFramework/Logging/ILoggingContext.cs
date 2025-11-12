// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines the interface for the logging database context.
/// </summary>
public interface ILoggingContext
{
    /// <summary>
    /// Gets or sets the DbSet for log entries in the LogEntries table.
    /// </summary>
    DbSet<LogEntry> LogEntries { get; set; }
}