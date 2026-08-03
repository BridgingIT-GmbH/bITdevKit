// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures prefix delete helpers.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobDeletePrefixOptions
/// {
///     DryRun = true,
///     MaxItems = 100
/// };
/// </code>
/// </example>
public sealed class BlobDeletePrefixOptions
{
    /// <summary>
    /// Gets the optional page size for listing candidates.
    /// </summary>
    /// <example>
    /// <code>
    /// var take = options.Take;
    /// </code>
    /// </example>
    public int? Take { get; init; }

    /// <summary>
    /// Gets the maximum number of candidate blobs to process when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxItems = options.MaxItems;
    /// </code>
    /// </example>
    public int? MaxItems { get; init; }

    /// <summary>
    /// Gets a value indicating whether full container scans are explicitly approved.
    /// </summary>
    /// <example>
    /// <code>
    /// var allow = options.AllowFullScan;
    /// </code>
    /// </example>
    public bool AllowFullScan { get; init; }

    /// <summary>
    /// Gets a value indicating whether candidates are only reported and not deleted.
    /// </summary>
    /// <example>
    /// <code>
    /// var dryRun = options.DryRun;
    /// </code>
    /// </example>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether delete failures should be collected and processing should continue.
    /// </summary>
    /// <example>
    /// <code>
    /// var continueOnError = options.ContinueOnError;
    /// </code>
    /// </example>
    public bool ContinueOnError { get; init; }
}
