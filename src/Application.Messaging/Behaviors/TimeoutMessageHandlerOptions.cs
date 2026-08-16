// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Defines operations for i timeout message handler.
/// </summary>
public interface ITimeoutMessageHandler
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    TimeoutMessageHandlerOptions Options { get; }
}

/// <summary>
/// Configures timeout message handler.
/// </summary>
public class TimeoutMessageHandlerOptions
{
    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}
