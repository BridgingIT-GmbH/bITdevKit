// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures Result-native retry behavior for blob-store client operations.
/// </summary>
/// <example>
/// <code>
/// var options = new RetryBlobStoreClientBehaviorOptions
/// {
///     Attempts = 3,
///     Backoff = TimeSpan.FromMilliseconds(100)
/// };
/// </code>
/// </example>
public sealed class RetryBlobStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the total number of attempts including the first execution.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Attempts = 3;
    /// </code>
    /// </example>
    public int Attempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay before retry attempts.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Backoff = TimeSpan.FromMilliseconds(50);
    /// </code>
    /// </example>
    public TimeSpan Backoff { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets a value indicating whether exponential backoff is used.
    /// </summary>
    /// <example>
    /// <code>
    /// options.BackoffExponential = true;
    /// </code>
    /// </example>
    public bool BackoffExponential { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether non-seekable upload streams may be retried.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AllowNonSeekableUploadRetries = false;
    /// </code>
    /// </example>
    public bool AllowNonSeekableUploadRetries { get; set; }
}
