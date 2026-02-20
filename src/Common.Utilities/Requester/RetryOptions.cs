// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Options for configuring default retry behavior.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the default number of retry attempts.
    /// Used when the <see cref="HandlerRetryAttribute"/> doesn't specify a count.
    /// </summary>
    public int? DefaultCount { get; set; }

    /// <summary>
    /// Gets or sets the default delay between retries in milliseconds.
    /// Used when the <see cref="HandlerRetryAttribute"/> doesn't specify a delay.
    /// </summary>
    public int? DefaultDelay { get; set; }
}