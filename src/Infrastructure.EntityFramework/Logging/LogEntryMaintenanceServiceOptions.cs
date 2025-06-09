// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;

public class LogEntryMaintenanceServiceOptions : OptionsBase
{
    public bool Enabled { get; set; } = true;

    public TimeSpan StartupDelay { get; set; } = new(0, 0, 15);

    public TimeSpan ProcessingInterval { get; set; } = new(0, 0, 5);

    public TimeSpan CleanupInterval { get; set; } = new(0, 59, 59);

    /// <summary>
    /// The number of days; logs older than this will be archived by the background timer. Default is 7.
    /// </summary>
    public int CleanupArchiveOlderThanDays { get; set; } = 7;

    /// <summary>
    /// The number of days; archived logs older than this will be deleted by the background timer. Default is 30.
    /// </summary>
    public int CleanupDeleteOlderThanDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of items to process in a single batch.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 1000;
}