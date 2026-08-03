// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes one provider-side expired blob sweep request.
/// </summary>
/// <example>
/// <code>
/// var request = new BlobRetentionSweepRequest
/// {
///     StoreName = "reports",
///     ExpiresOnOrBefore = DateTimeOffset.UtcNow,
///     BatchSize = 1000
/// };
/// </code>
/// </example>
public sealed class BlobRetentionSweepRequest
{
    /// <summary>
    /// Gets or initializes the configured blob-store name.
    /// </summary>
    /// <example>
    /// <code>
    /// var store = request.StoreName;
    /// </code>
    /// </example>
    public string StoreName { get; init; }

    /// <summary>
    /// Gets or initializes the provider name.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = request.ProviderName;
    /// </code>
    /// </example>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets or initializes the UTC sweep timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// var started = request.StartedAt;
    /// </code>
    /// </example>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets or initializes the inclusive expiration cutoff.
    /// </summary>
    /// <example>
    /// <code>
    /// var cutoff = request.ExpiresOnOrBefore;
    /// </code>
    /// </example>
    public DateTimeOffset ExpiresOnOrBefore { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of expired blobs to delete in one batch.
    /// </summary>
    /// <example>
    /// <code>
    /// var batchSize = request.BatchSize;
    /// </code>
    /// </example>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    /// Gets or initializes the maximum number of batches to process for the store.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxBatches = request.MaxBatches;
    /// </code>
    /// </example>
    public int MaxBatches { get; init; } = 10;

    /// <summary>
    /// Gets or initializes the delay between provider-side batches.
    /// </summary>
    /// <example>
    /// <code>
    /// var delay = request.BatchDelay;
    /// </code>
    /// </example>
    public TimeSpan BatchDelay { get; init; }

    /// <summary>
    /// Gets or initializes the logical worker identifier used for diagnostics.
    /// </summary>
    /// <example>
    /// <code>
    /// var worker = request.WorkerId;
    /// </code>
    /// </example>
    public string WorkerId { get; init; }
}
