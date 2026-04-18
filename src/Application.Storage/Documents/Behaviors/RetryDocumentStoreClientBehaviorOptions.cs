// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures retry behavior for <see cref="RetryDocumentStoreClientBehavior{T}" />.
/// </summary>
public class RetryDocumentStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the number of attempts to execute before failing.
    /// </summary>
    public int Attempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base backoff delay between retries.
    /// </summary>
    public TimeSpan Backoff { get; set; } = new(0, 0, 0, 0, 200);

    /// <summary>
    /// Gets or sets a value indicating whether retry delays should use exponential backoff.
    /// </summary>
    public bool BackoffExponential { get; set; }
}
