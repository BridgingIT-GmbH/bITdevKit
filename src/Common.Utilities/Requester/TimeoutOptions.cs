// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Options for configuring default timeout behavior.
/// </summary>
public class TimeoutOptions
{
    /// <summary>
    /// Gets or sets the default timeout duration in milliseconds.
    /// Used when the <see cref="HandlerTimeoutAttribute"/> doesn't specify a duration.
    /// </summary>
    public int? DefaultDuration { get; set; }
}