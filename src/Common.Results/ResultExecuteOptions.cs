// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Configuration options for async operations.
/// </summary>
public class ResultExecuteOptions
{
    /// <summary>
    ///     Gets or sets the timeout duration for async operations.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    ///     Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; }

    /// <summary>
    ///     Gets or sets a function to determine if an exception is retryable.
    /// </summary>
    public Func<Exception, bool> RetryableException { get; set; }

    /// <summary>
    ///     Creates default options with reasonable values.
    /// </summary>
    public static ResultExecuteOptions Default => new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxRetries = 3,
        RetryDelay = TimeSpan.FromSeconds(1),
        RetryableException = ex => ex is TimeoutException or HttpRequestException
    };
}