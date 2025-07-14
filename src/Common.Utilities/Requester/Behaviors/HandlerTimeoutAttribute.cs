// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a timeout policy for a handler.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HandlerTimeoutAttribute"/> class.
/// </remarks>
/// <param name="timeout">The timeout duration in milliseconds.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerTimeoutAttribute(int timeout) : Attribute
{
    /// <summary>
    /// Gets the timeout duration in milliseconds.
    /// </summary>
    public int Duration { get; } = timeout;
}
