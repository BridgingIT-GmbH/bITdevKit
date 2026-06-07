// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Stores runtime availability for the built-in queueing alive probe.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueing().AliveEnabled(false);
/// </code>
/// </example>
public sealed class QueueingAliveOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the queueing alive probe is available.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

