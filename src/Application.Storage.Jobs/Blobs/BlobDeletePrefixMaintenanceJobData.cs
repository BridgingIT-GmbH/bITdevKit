// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines the typed payload for <see cref="BlobDeletePrefixMaintenanceJob"/>.
/// </summary>
/// <example>
/// <code>
/// var data = new BlobDeletePrefixMaintenanceJobData
/// {
///     StoreName = "reports",
///     Container = "reports",
///     Prefix = "tmp/"
/// };
/// </code>
/// </example>
public sealed class BlobDeletePrefixMaintenanceJobData
{
    /// <summary>
    /// Gets or sets the configured blob-store client name.
    /// </summary>
    public string StoreName { get; set; }

    /// <summary>
    /// Gets or sets the blob container to scan.
    /// </summary>
    public string Container { get; set; }

    /// <summary>
    /// Gets or sets the blob name prefix to delete.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the job should only report candidates.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an empty prefix/full scan is explicitly approved.
    /// </summary>
    public bool AllowFullScan { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether delete failures should be collected while processing continues.
    /// </summary>
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Gets or sets the optional page size.
    /// </summary>
    public int? Take { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum number of candidate blobs to process.
    /// </summary>
    public int? MaxItems { get; set; }
}
