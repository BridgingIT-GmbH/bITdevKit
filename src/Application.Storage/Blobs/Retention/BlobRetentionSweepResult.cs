// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes the outcome of one provider-side expired blob sweep.
/// </summary>
/// <example>
/// <code>
/// var deleted = result.DeletedCount;
/// </code>
/// </example>
public sealed class BlobRetentionSweepResult
{
    /// <summary>
    /// Gets or initializes the configured blob-store name.
    /// </summary>
    /// <example>
    /// <code>
    /// var store = result.StoreName;
    /// </code>
    /// </example>
    public string StoreName { get; init; }

    /// <summary>
    /// Gets or initializes the provider name.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = result.ProviderName;
    /// </code>
    /// </example>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets or initializes the number of batches processed.
    /// </summary>
    /// <example>
    /// <code>
    /// var batches = result.BatchCount;
    /// </code>
    /// </example>
    public int BatchCount { get; init; }

    /// <summary>
    /// Gets or initializes the number of expired blobs deleted.
    /// </summary>
    /// <example>
    /// <code>
    /// var deleted = result.DeletedCount;
    /// </code>
    /// </example>
    public int DeletedCount { get; init; }

    /// <summary>
    /// Gets the exact blob keys successfully deleted by this sweep.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var key in result.DeletedKeys) { Console.WriteLine(key.Name); }
    /// </code>
    /// </example>
    public IReadOnlyList<BlobKey> DeletedKeys { get; init; } = [];

    /// <summary>
    /// Gets or initializes the number of expired blobs skipped because they could not be claimed.
    /// </summary>
    /// <example>
    /// <code>
    /// var skipped = result.SkippedCount;
    /// </code>
    /// </example>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets or initializes the UTC sweep completion timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// var completed = result.CompletedAt;
    /// </code>
    /// </example>
    public DateTimeOffset CompletedAt { get; init; }
}
