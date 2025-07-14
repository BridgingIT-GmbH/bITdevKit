// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a retry policy for a handler.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HandlerRetryAttribute"/> class.
/// </remarks>
/// <param name="count">The number of retry attempts.</param>
/// <param name="delay">The delay between retries in milliseconds.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerRetryAttribute(int count, int delay) : Attribute
{
    /// <summary>
    /// Gets the number of retry attempts.
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// Gets the delay between retries in milliseconds.
    /// </summary>
    public int Delay { get; } = delay;
}
