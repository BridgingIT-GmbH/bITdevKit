// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Files.Dashboard.Pages;

using BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>
/// View model for the server-rendered file storage dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardFilesViewModel();
/// </code>
/// </example>
public sealed class DashboardFilesViewModel
{
    /// <summary>
    /// Gets or sets the captured at utc.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the selected provider name.
    /// </summary>
    public string SelectedProviderName { get; set; }

    /// <summary>
    /// Gets or sets the current path.
    /// </summary>
    public string CurrentPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action base.
    /// </summary>
    public string ActionBase { get; set; } = "/_bdk/dashboard/storage/files/actions";

    /// <summary>
    /// Gets or sets the download path.
    /// </summary>
    public string DownloadPath { get; set; } = "/_bdk/dashboard/storage/files/download";

    /// <summary>
    /// Gets or sets the providers.
    /// </summary>
    public IReadOnlyList<FileStorageProviderInfoModel> Providers { get; set; } = [];

    /// <summary>
    /// Gets or sets the directory tree.
    /// </summary>
    public IReadOnlyList<DashboardDirectoryNode> DirectoryTree { get; set; } = [];

    /// <summary>
    /// Gets or sets the directories.
    /// </summary>
    public IReadOnlyList<DashboardDirectoryRow> Directories { get; set; } = [];

    /// <summary>
    /// Gets or sets the files.
    /// </summary>
    public IReadOnlyList<DashboardFileRow> Files { get; set; } = [];

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the selected file provider supports Storage Permalinks.
    /// </summary>
    /// <example>
    /// <code>
    /// if (model.PermalinksEnabled) { }
    /// </code>
    /// </example>
    public bool PermalinksEnabled { get; set; }
}

/// <summary>
/// Represents dashboard directory node.
/// </summary>
/// <param name="Path">The path used by the operation.</param>
/// <param name="Name">The name of the value.</param>
/// <param name="Children">The children used by the operation.</param>
public sealed record DashboardDirectoryNode(
    string Path,
    string Name,
    IReadOnlyList<DashboardDirectoryNode> Children);

/// <summary>
/// Represents dashboard directory row.
/// </summary>
/// <param name="Path">The path used by the operation.</param>
/// <param name="Name">The name of the value.</param>
public sealed record DashboardDirectoryRow(string Path, string Name);

/// <summary>
/// Represents dashboard file row.
/// </summary>
/// <param name="Path">The path used by the operation.</param>
/// <param name="Name">The name of the value.</param>
/// <param name="Length">The length used by the operation.</param>
/// <param name="LastModified">The last modified used by the operation.</param>
public sealed record DashboardFileRow(string Path, string Name, long? Length, DateTime? LastModified);
