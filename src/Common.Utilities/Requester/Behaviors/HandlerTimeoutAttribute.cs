// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a timeout policy for a handler.
/// </summary>
/// <remarks>
/// When duration is not specified, defaults from <see cref="TimeoutOptions"/> will be used.
/// If no options are configured, an exception will be thrown at runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerTimeoutAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerTimeoutAttribute"/> class with a specified timeout.
    /// </summary>
    /// <param name="timeout">The timeout duration in milliseconds.</param>
    public HandlerTimeoutAttribute(int timeout)
    {
        this.Duration = timeout;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerTimeoutAttribute"/> class using defaults from options.
    /// </summary>
    /// <remarks>
    /// When using this constructor, ensure <see cref="TimeoutOptions"/> is configured with default values.
    /// </remarks>
    public HandlerTimeoutAttribute()
    {
    }

    /// <summary>
    /// Gets the timeout duration in milliseconds, or null to use the default from <see cref="TimeoutOptions"/>.
    /// </summary>
    public int? Duration { get; }
}