// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Defines operations for i timeout query.
/// </summary>
public interface ITimeoutQuery
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    TimeoutQueryOptions Options { get; }
}

/// <summary>
/// Configures timeout query.
/// </summary>
public class TimeoutQueryOptions
{
    /// <summary>
    /// Gets or sets the timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = new(0, 0, 0, 30);
}
