// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Defines operations for i retry message handler.
/// </summary>
public interface IRetryMessageHandler
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    RetryMessageHandlerOptions Options { get; }
}

/// <summary>
/// Configures retry message handler.
/// </summary>
public class RetryMessageHandlerOptions
{
    /// <summary>
    /// Gets or sets the attempts.
    /// </summary>
    public int Attempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the backoff.
    /// </summary>
    public TimeSpan Backoff { get; set; } = new(0, 0, 0, 0, 200);

    /// <summary>
    /// Gets or sets the backoff exponential.
    /// </summary>
    public bool BackoffExponential { get; set; }
}
