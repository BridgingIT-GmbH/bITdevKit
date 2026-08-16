// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Defines operations for i retry query.
/// </summary>
public interface IRetryQuery
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    RetryQueryOptions Options { get; }
}

/// <summary>
/// Configures retry query.
/// </summary>
public class RetryQueryOptions
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
