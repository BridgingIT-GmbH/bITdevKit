// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes one hosted retention sweep across registered blob-store clients.
/// </summary>
/// <example>
/// <code>
/// var summary = await service.SweepOnceAsync();
/// </code>
/// </example>
public sealed class BlobRetentionSweepSummary
{
    /// <summary>
    /// Gets or initializes the UTC sweep start timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// var started = summary.StartedAt;
    /// </code>
    /// </example>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets or initializes the UTC sweep completion timestamp.
    /// </summary>
    /// <example>
    /// <code>
    /// var completed = summary.CompletedAt;
    /// </code>
    /// </example>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Gets or initializes the number of configured clients inspected by the hosted service.
    /// </summary>
    /// <example>
    /// <code>
    /// var clients = summary.ClientCount;
    /// </code>
    /// </example>
    public int ClientCount { get; init; }

    /// <summary>
    /// Gets or initializes the number of clients that exposed provider-native retention sweeping.
    /// </summary>
    /// <example>
    /// <code>
    /// var supported = summary.SupportedClientCount;
    /// </code>
    /// </example>
    public int SupportedClientCount { get; init; }

    /// <summary>
    /// Gets or initializes the total number of expired blobs deleted.
    /// </summary>
    /// <example>
    /// <code>
    /// var deleted = summary.DeletedCount;
    /// </code>
    /// </example>
    public int DeletedCount { get; init; }

    /// <summary>
    /// Gets or initializes the provider-side sweep results.
    /// </summary>
    /// <example>
    /// <code>
    /// var stores = summary.Results.Select(result => result.StoreName);
    /// </code>
    /// </example>
    public IReadOnlyCollection<BlobRetentionSweepResult> Results { get; init; } = [];

    /// <summary>
    /// Gets or initializes the client names that failed during the sweep.
    /// </summary>
    /// <example>
    /// <code>
    /// var failed = summary.FailedClientNames;
    /// </code>
    /// </example>
    public IReadOnlyCollection<string> FailedClientNames { get; init; } = [];
}
