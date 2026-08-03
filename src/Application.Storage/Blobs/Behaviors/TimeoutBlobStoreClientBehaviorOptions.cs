// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures blob-store client operation timeouts.
/// </summary>
/// <example>
/// <code>
/// var options = new TimeoutBlobStoreClientBehaviorOptions
/// {
///     Timeout = TimeSpan.FromSeconds(30)
/// };
/// </code>
/// </example>
public sealed class TimeoutBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the maximum operation duration.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Timeout = TimeSpan.FromSeconds(5);
    /// </code>
    /// </example>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
