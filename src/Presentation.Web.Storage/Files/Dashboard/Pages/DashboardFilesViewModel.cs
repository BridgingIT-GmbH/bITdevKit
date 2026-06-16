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
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string SelectedProviderName { get; set; }

    public string CurrentPath { get; set; } = string.Empty;

    public string ActionBase { get; set; } = "/_bdk/dashboard/storage/files/actions";

    public string DownloadPath { get; set; } = "/_bdk/dashboard/storage/files/download";

    public IReadOnlyList<FileStorageProviderInfoModel> Providers { get; set; } = [];

    public IReadOnlyList<DashboardDirectoryNode> DirectoryTree { get; set; } = [];

    public IReadOnlyList<DashboardDirectoryRow> Directories { get; set; } = [];

    public IReadOnlyList<DashboardFileRow> Files { get; set; } = [];

    public List<string> Errors { get; } = [];

    public bool IsAvailable { get; set; } = true;
}

public sealed record DashboardDirectoryNode(
    string Path,
    string Name,
    IReadOnlyList<DashboardDirectoryNode> Children);

public sealed record DashboardDirectoryRow(string Path, string Name);

public sealed record DashboardFileRow(string Path, string Name, long? Length, DateTime? LastModified);
