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
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string SelectedStoreName { get; set; }

    public string Container { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int Take { get; set; } = 100;

    public bool AllowFullScan { get; set; }

    public string ContinuationToken { get; set; }

    public string ActionBase { get; set; } = "/_bdk/dashboard/storage/blobs/actions";

    public string DownloadPath { get; set; } = "/_bdk/dashboard/storage/blobs/download";

    public IReadOnlyList<BlobStorageClientInfoModel> Stores { get; set; } = [];

    public IReadOnlyList<string> Containers { get; set; } = [];

    public IReadOnlyList<DashboardBlobRow> Blobs { get; set; } = [];

    public List<string> Errors { get; } = [];

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

    public bool HasMore { get; set; }

    public string NextContinuationToken { get; set; }
}

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
