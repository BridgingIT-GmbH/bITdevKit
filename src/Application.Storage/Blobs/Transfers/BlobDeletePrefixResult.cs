// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes the result of a prefix delete operation.
/// </summary>
/// <example>
/// <code>
/// var deleted = result.Value.DeletedCount;
/// </code>
/// </example>
public sealed class BlobDeletePrefixResult
{
    /// <summary>
    /// Gets the container that was scanned.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = result.Container;
    /// </code>
    /// </example>
    public string Container { get; init; }

    /// <summary>
    /// Gets the prefix that was scanned.
    /// </summary>
    /// <example>
    /// <code>
    /// var prefix = result.Prefix;
    /// </code>
    /// </example>
    public string Prefix { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation was a dry run.
    /// </summary>
    /// <example>
    /// <code>
    /// var dryRun = result.DryRun;
    /// </code>
    /// </example>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets the number of candidate blobs observed.
    /// </summary>
    /// <example>
    /// <code>
    /// var candidates = result.CandidateCount;
    /// </code>
    /// </example>
    public int CandidateCount { get; init; }

    /// <summary>
    /// Gets the number of blobs deleted.
    /// </summary>
    /// <example>
    /// <code>
    /// var deleted = result.DeletedCount;
    /// </code>
    /// </example>
    public int DeletedCount { get; init; }

    /// <summary>
    /// Gets the names of candidate blobs.
    /// </summary>
    /// <example>
    /// <code>
    /// var names = result.CandidateNames;
    /// </code>
    /// </example>
    public IReadOnlyList<string> CandidateNames { get; init; } = [];

    /// <summary>
    /// Gets delete failure descriptions when processing continued after errors.
    /// </summary>
    /// <example>
    /// <code>
    /// var failures = result.Failures;
    /// </code>
    /// </example>
    public IReadOnlyList<string> Failures { get; init; } = [];
}
