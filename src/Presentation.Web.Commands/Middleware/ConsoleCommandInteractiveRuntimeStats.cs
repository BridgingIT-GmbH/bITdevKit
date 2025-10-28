// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Holds runtime statistics captured for the interactive status command.
/// </summary>
public sealed class ConsoleCommandInteractiveRuntimeStats
{
    /// <summary>Gets the timestamp when the application started collecting stats.</summary>
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>Total number of HTTP requests observed.</summary>
    public long TotalRequests;

    /// <summary>Total number of failed (HTTP5xx) responses observed.</summary>
    public long TotalFailures;

    /// <summary>Cumulative latency (milliseconds) across all observed requests.</summary>
    public long TotalLatencyMs;

    /// <summary>Gets the calculated application uptime.</summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - this.StartedAt;
}
