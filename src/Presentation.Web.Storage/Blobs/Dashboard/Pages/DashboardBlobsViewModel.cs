// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.Pages;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// View model for the server-rendered blob storage dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardBlobsViewModel();
/// </code>
/// </example>
public sealed class DashboardBlobsViewModel
{
    /// <summary>
    /// Gets or sets the captured at utc.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the selected store name.
    /// </summary>
    public string SelectedStoreName { get; set; }

    /// <summary>
    /// Gets or sets the container.
    /// </summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prefix.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the take.
    /// </summary>
    public int Take { get; set; } = 100;

    /// <summary>
    /// Gets or sets the allow full scan.
    /// </summary>
    public bool AllowFullScan { get; set; }

    /// <summary>
    /// Gets or sets the continuation token.
    /// </summary>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the action base.
    /// </summary>
    public string ActionBase { get; set; } = "/_bdk/dashboard/storage/blobs/actions";

    /// <summary>
    /// Gets or sets the download path.
    /// </summary>
    public string DownloadPath { get; set; } = "/_bdk/dashboard/storage/blobs/download";

    /// <summary>
    /// Gets or sets the stores.
    /// </summary>
    public IReadOnlyList<BlobStorageClientInfoModel> Stores { get; set; } = [];

    /// <summary>
    /// Gets or sets the containers.
    /// </summary>
    public IReadOnlyList<string> Containers { get; set; } = [];

    /// <summary>
    /// Gets or sets the blobs.
    /// </summary>
    public IReadOnlyList<DashboardBlobRow> Blobs { get; set; } = [];

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the selected blob client supports Storage Permalinks.
    /// </summary>
    /// <example>
    /// <code>
    /// if (model.PermalinksEnabled) { }
    /// </code>
    /// </example>
    public bool PermalinksEnabled { get; set; }

    /// <summary>
    /// Gets or sets the has more.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets the next continuation token.
    /// </summary>
    public string NextContinuationToken { get; set; }
}

/// <summary>
/// Represents dashboard blob row.
/// </summary>
/// <param name="Container">The container used by the operation.</param>
/// <param name="Name">The name of the value.</param>
/// <param name="Length">The length used by the operation.</param>
/// <param name="ContentType">The content type used by the operation.</param>
/// <param name="ContentHash">The content hash used by the operation.</param>
/// <param name="ETag">The e tag used by the operation.</param>
/// <param name="CreatedAt">The created at used by the operation.</param>
/// <param name="LastModifiedAt">The last modified at used by the operation.</param>
/// <param name="ExpiresAt">The expires at used by the operation.</param>
/// <param name="Properties">The properties used by the operation.</param>
public sealed record DashboardBlobRow(
    string Container,
    string Name,
    long Length,
    string ContentType,
    string ContentHash,
    string ETag,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastModifiedAt,
    DateTimeOffset? ExpiresAt,
    PropertyBag Properties);
