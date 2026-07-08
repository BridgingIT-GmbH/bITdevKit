// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard.Pages;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// View model for the server-rendered document storage dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardDocumentsViewModel();
/// </code>
/// </example>
public sealed class DashboardDocumentsViewModel
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the dashboard model was captured.
    /// </summary>
    /// <example>
    /// <code>
    /// var capturedAt = model.CapturedAtUtc;
    /// </code>
    /// </example>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether document storage dashboard data is available.
    /// </summary>
    /// <example>
    /// <code>
    /// if (model.IsAvailable) { }
    /// </code>
    /// </example>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Gets or sets the registered document clients available for selection.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var client in model.Clients) { }
    /// </code>
    /// </example>
    public IReadOnlyList<DocumentStoreClientDescriptor> Clients { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected document client id.
    /// </summary>
    /// <example>
    /// <code>
    /// var clientId = model.SelectedClientId;
    /// </code>
    /// </example>
    public string SelectedClientId { get; set; }

    /// <summary>
    /// Gets or sets the selected document client descriptor.
    /// </summary>
    /// <example>
    /// <code>
    /// var selected = model.SelectedClient;
    /// </code>
    /// </example>
    public DocumentStoreClientDescriptor SelectedClient { get; set; }

    /// <summary>
    /// Gets or sets the partition-key filter.
    /// </summary>
    /// <example>
    /// <code>
    /// model.PartitionKey = "archive/openmeteo/weather";
    /// </code>
    /// </example>
    public string PartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the row-key filter.
    /// </summary>
    /// <example>
    /// <code>
    /// model.RowKey = "2026/";
    /// </code>
    /// </example>
    public string RowKey { get; set; }

    /// <summary>
    /// Gets or sets the row-key filter mode.
    /// </summary>
    /// <example>
    /// <code>
    /// model.RowKeyMode = "prefix";
    /// </code>
    /// </example>
    public string RowKeyMode { get; set; } = "prefix";

    /// <summary>
    /// Gets or sets the requested page size.
    /// </summary>
    /// <example>
    /// <code>
    /// model.PageSize = 100;
    /// </code>
    /// </example>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the current continuation token.
    /// </summary>
    /// <example>
    /// <code>
    /// model.ContinuationToken = token;
    /// </code>
    /// </example>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the next continuation token.
    /// </summary>
    /// <example>
    /// <code>
    /// var next = model.NextContinuationToken;
    /// </code>
    /// </example>
    public string NextContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the document keys in the current page.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var key in model.Keys) { }
    /// </code>
    /// </example>
    public IReadOnlyList<DocumentKey> Keys { get; set; } = [];

    /// <summary>
    /// Gets or sets the total matching document count when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var count = model.TotalCount;
    /// </code>
    /// </example>
    public long? TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the selected document partition key.
    /// </summary>
    /// <example>
    /// <code>
    /// var partition = model.SelectedPartitionKey;
    /// </code>
    /// </example>
    public string SelectedPartitionKey { get; set; }

    /// <summary>
    /// Gets or sets the selected document row key.
    /// </summary>
    /// <example>
    /// <code>
    /// var row = model.SelectedRowKey;
    /// </code>
    /// </example>
    public string SelectedRowKey { get; set; }

    /// <summary>
    /// Gets or sets the selected document content.
    /// </summary>
    /// <example>
    /// <code>
    /// model.SelectedContent = "{ }";
    /// </code>
    /// </example>
    public string SelectedContent { get; set; }

    /// <summary>
    /// Gets the dashboard errors captured while creating the model.
    /// </summary>
    /// <example>
    /// <code>
    /// model.Errors.Add("Unable to list documents.");
    /// </code>
    /// </example>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the dashboard-local action base path.
    /// </summary>
    /// <example>
    /// <code>
    /// model.ActionBase = "/_bdk/dashboard/storage/documents/actions";
    /// </code>
    /// </example>
    public string ActionBase { get; set; } = "/_bdk/dashboard/storage/documents/actions";

    /// <summary>
    /// Gets or sets the dashboard-local document download path.
    /// </summary>
    /// <example>
    /// <code>
    /// model.DownloadPath = "/_bdk/dashboard/storage/documents/download";
    /// </code>
    /// </example>
    public string DownloadPath { get; set; } = "/_bdk/dashboard/storage/documents/download";
}
