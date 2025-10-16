// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

/// <summary>
/// Configuration options for sequence number generation.
/// </summary>
public class SequenceNumberGeneratorOptions
{
    /// <summary>
    /// Gets or sets the timeout duration for acquiring sequence locks.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the operation timeout duration for database operations.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the minimum log level for sequence operations.
    /// Default is Information.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets per-sequence configuration overrides.
    /// Key is the sequence name, value is the sequence-specific options.
    /// </summary>
    public Dictionary<string, SequenceOptions> SequenceOverrides { get; set; } = [];
}
