// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;

public static class FileStorageTreeTestScenarios
{
    public static async Task RenderDirectoryAsync_FullStructure_Success(IFileStorageProvider provider)
    {
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain("Rendered storage provider structure starting at ''");

        var treeText = renderer.ToString();
        treeText.ShouldContain("└── root (4 files, 56 B)");
        treeText.ShouldContain("├── file1.txt (14 B, ");
        treeText.ShouldContain("├── dir1 (2 files, 28 B)");
        treeText.ShouldContain("│   ├── file2.txt (14 B, ");
        treeText.ShouldContain("│   └── dir2 (1 files, 14 B)");
        treeText.ShouldContain("│       └── file3.txt (14 B, ");
        treeText.ShouldContain("└── dir3 (1 files, 14 B)");
        treeText.ShouldContain("    └── file4.txt (14 B, ");
        treeText.ShouldContain("Total: 4 files, 56 B");
    }

    public static async Task RenderDirectoryAsync_SubPath_Success(IFileStorageProvider provider)
    {
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();
        var result = await provider.RenderDirectoryAsync(renderer, "root/dir1", skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain("Rendered storage provider structure starting at 'root/dir1'");

        var treeText = renderer.ToString();
        treeText.ShouldContain("├── dir1 (2 files, 28 B)");
        treeText.ShouldContain("├── file2.txt (14 B, ");
        treeText.ShouldContain("└── dir2 (1 files, 14 B)");
        treeText.ShouldContain("    └── file3.txt (14 B, ");
        treeText.ShouldContain("Total: 2 files, 28 B");
        treeText.ShouldNotContain("file1.txt");
        treeText.ShouldNotContain("dir3");
    }

    public static async Task RenderDirectoryAsync_EmptyStructure_Success(IFileStorageProvider provider)
    {
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");

        var renderer = new TextFileStorageTreeRenderer();
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain("Rendered storage provider structure starting at ''");

        var treeText = renderer.ToString();
        treeText.ShouldContain("└── root (0 files, 0 B)");
        treeText.ShouldContain("├── dir1 (0 files, 0 B)");
        treeText.ShouldContain("│   └── dir2 (0 files, 0 B)");
        treeText.ShouldContain("└── dir3 (0 files, 0 B)");
        treeText.ShouldContain("Total: 0 files, 0 B");
    }

    public static async Task RenderDirectoryAsync_SkipFiles_Success(IFileStorageProvider provider)
    {
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: true, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain("Rendered storage provider structure starting at ''");

        var treeText = renderer.ToString();
        treeText.ShouldContain("└── root");
        treeText.ShouldContain("├── dir1");
        treeText.ShouldContain("│   └── dir2");
        treeText.ShouldContain("└── dir3");
        treeText.ShouldNotContain("file1.txt");
        treeText.ShouldNotContain("file2.txt");
        treeText.ShouldNotContain("file3.txt");
        treeText.ShouldNotContain("file4.txt");
        treeText.ShouldNotContain("Total:");
    }

    public static async Task RenderDirectoryAsync_ProgressReporting(IFileStorageProvider provider)
    {
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();
        var progress = new RecordingProgress<FileProgress>();
        var result = await provider.RenderDirectoryAsync(
            renderer,
            null,
            skipFiles: false,
            progress,
            cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        var progressItems = progress.Items;
        progressItems.ShouldNotBeEmpty();

        var lastProgress = progressItems.Last();
        lastProgress.FilesProcessed.ShouldBe(4, "Should process exactly 4 files");
        lastProgress.BytesProcessed.ShouldBe(56, "Total bytes should match the sum of file contents");
    }

    public static async Task RenderDirectoryAsync_HtmlRenderer_Success(IFileStorageProvider provider)
    {
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");

        var renderer = new HtmlFileStorageTreeRenderer();
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain("Rendered storage provider structure starting at ''");

        var htmlContent = renderer.ToString();
        htmlContent.ShouldContain("<ul>");
        htmlContent.ShouldContain("<li class=\"directory\">root (2 files, 28 B)");
        htmlContent.ShouldContain("<li class=\"file\">file1.txt (14 B, ");
        htmlContent.ShouldContain("<li class=\"directory\">dir1 (1 files, 14 B)");
        htmlContent.ShouldContain("<li class=\"file\">file2.txt (14 B, ");
        htmlContent.ShouldContain("<li class=\"directory\">dir2 (0 files, 0 B)");
        htmlContent.ShouldContain("<li class=\"directory\">dir3 (0 files, 0 B)");
        htmlContent.ShouldContain("<li class=\"totals\">Total: 2 files, 28 B</li>");
        htmlContent.ShouldContain("</ul>");
        htmlContent.ShouldNotContain("└──");
        htmlContent.ShouldNotContain("├──");
        htmlContent.ShouldNotContain("│");
    }
}
