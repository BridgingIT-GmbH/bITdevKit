// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Identifies a startup task that supplies retry options.
/// </summary>
public interface IRetryStartupTask
{
    /// <summary>Gets the retry options for the task.</summary>
    RetryStartupTaskOptions Options { get; }
}

/// <summary>
///     Configures retry attempts and backoff for a startup task.
/// </summary>
public class RetryStartupTaskOptions
{
    /// <summary>Gets or sets the number of retry attempts.</summary>
    public int Attempts { get; set; } = 3;

    /// <summary>Gets or sets the delay between retry attempts.</summary>
    public TimeSpan Backoff { get; set; } = new(0, 0, 0, 0, 200);

    /// <summary>Gets or sets whether retry backoff grows exponentially.</summary>
    public bool BackoffExponential { get; set; }
}
