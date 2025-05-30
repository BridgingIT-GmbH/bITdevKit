// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.Threading;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[IntegrationTest("Storage")]
[Collection(nameof(TestEnvironmentCollection))]
public class FileStorageProviderTreeTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    private IFileStorageProvider CreateInMemoryProvider()
    {
        return new LoggingFileStorageBehavior(
            new InMemoryFileStorageProvider("InMemory"),
            this.fixture.ServiceProvider.GetRequiredService<ILoggerFactory>());
    }

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_FullStructure_Success()
    {
        // Arrange
        var provider = this.CreateInMemoryProvider();
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();

        // Act
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        // Assert
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

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_SubPath_Success()
    {
        // Arrange
        var provider = this.CreateInMemoryProvider();
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();

        // Act
        var result = await provider.RenderDirectoryAsync(renderer, "root/dir1", skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        // Assert
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

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_EmptyStructure_Success()
    {
        // Arrange
        var provider = this.CreateInMemoryProvider();
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        var renderer = new TextFileStorageTreeRenderer();

        // Act
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain("Rendered storage provider structure starting at ''");

        var treeText = renderer.ToString();
        treeText.ShouldContain("└── root (0 files, 0 B)");
        treeText.ShouldContain("├── dir1 (0 files, 0 B)");
        treeText.ShouldContain("│   └── dir2 (0 files, 0 B)");
        treeText.ShouldContain("└── dir3 (0 files, 0 B)");
        treeText.ShouldContain("Total: 0 files, 0 B");
    }

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_SkipFiles_Success()
    {
        // Arrange
        var provider = this.CreateInMemoryProvider();
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();

        // Act
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: true, progress: null, cancellationToken: CancellationToken.None);

        // Assert
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

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_ProgressReporting()
    {
        // Arrange
        var provider = this.CreateInMemoryProvider();
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");
        await provider.WriteTextFileAsync("root/dir1/dir2/file3.txt", "File 3 content");
        await provider.WriteTextFileAsync("root/dir3/file4.txt", "File 4 content");

        var renderer = new TextFileStorageTreeRenderer();
        var progressItems = new List<FileProgress>();

        // Act
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: new Progress<FileProgress>(p => progressItems.Add(p)), cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"RenderDirectoryAsync failed: {string.Join(", ", result.Messages)}");
        progressItems.ShouldNotBeEmpty();
        var lastProgress = progressItems.Last();
        lastProgress.FilesProcessed.ShouldBe(4, "Should process exactly 4 files");
        lastProgress.BytesProcessed.ShouldBe(56, "Total bytes should match the sum of file contents");
    }

    [Fact]
    public async Task RenderDirectoryAsync_InMemoryProvider_HtmlRenderer_Success()
    {
        // Arrange
        var provider = this.CreateInMemoryProvider();
        await provider.CreateDirectoryAsync("root/dir1/dir2");
        await provider.CreateDirectoryAsync("root/dir3");
        await provider.WriteTextFileAsync("root/file1.txt", "File 1 content");
        await provider.WriteTextFileAsync("root/dir1/file2.txt", "File 2 content");

        var renderer = new HtmlFileStorageTreeRenderer();

        // Act
        var result = await provider.RenderDirectoryAsync(renderer, null, skipFiles: false, progress: null, cancellationToken: CancellationToken.None);

        // Assert
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