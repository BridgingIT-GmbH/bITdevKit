// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

/// <summary>
/// Data transfer object for representing log statistics, including counts by level and time intervals.
/// </summary>
public class LogEntryStatisticsModel
{
    /// <summary>
    /// Gets or sets the counts of log entries by level (e.g., Information, Error).
    /// </summary>
    public Dictionary<LogLevel, int> LevelCounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the counts of log entries by time interval and level.
    /// Keys are the start times of intervals, values are dictionaries of level counts.
    /// </summary>
    public Dictionary<DateTimeOffset, Dictionary<LogLevel, int>> TimeIntervalCounts { get; set; } = [];
}
