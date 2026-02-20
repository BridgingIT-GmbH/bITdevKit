// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a retry policy for a handler.
/// </summary>
/// <remarks>
/// When parameters are not specified, defaults from <see cref="RetryOptions"/> will be used.
/// If no options are configured, an exception will be thrown at runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerRetryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerRetryAttribute"/> class with specified values.
    /// </summary>
    /// <param name="count">The number of retry attempts.</param>
    /// <param name="delay">The delay between retries in milliseconds.</param>
    public HandlerRetryAttribute(int count, int delay)
    {
        this.Count = count;
        this.Delay = delay;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerRetryAttribute"/> class using defaults from options.
    /// </summary>
    /// <remarks>
    /// When using this constructor, ensure <see cref="RetryOptions"/> is configured with default values.
    /// </remarks>
    public HandlerRetryAttribute()
    {
    }

    /// <summary>
    /// Gets the number of retry attempts, or null to use the default from <see cref="RetryOptions"/>.
    /// </summary>
    public int? Count { get; }

    /// <summary>
    /// Gets the delay between retries in milliseconds, or null to use the default from <see cref="RetryOptions"/>.
    /// </summary>
    public int? Delay { get; }
}