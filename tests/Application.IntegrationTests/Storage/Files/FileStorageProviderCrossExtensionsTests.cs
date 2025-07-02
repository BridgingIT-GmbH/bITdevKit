// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using System;
using System.IO;
using System.Threading;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class FileStorageProviderCrossTests(ITestOutputHelper output, TestEnvironmentFixture fixture)
{
    private readonly TestEnvironmentFixture fixture = fixture.WithOutput(output);
    private readonly ITestOutputHelper output = output;

    private IFileStorageProvider CreateInMemoryProvider()
    {
        return new LoggingFileStorageBehavior(
            new InMemoryFileStorageProvider("InMemory"),
            this.fixture.ServiceProvider.GetRequiredService<ILoggerFactory>());
    }

    private IFileStorageProvider CreateLocalProvider()
    {
        return new LoggingFileStorageBehavior(
            new LocalFileStorageProvider("Local", Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString())),
            this.fixture.ServiceProvider.GetRequiredService<ILoggerFactory>());
    }

    [Fact]
    public async Task CopyFileAsync_CrossProvider_SourceNotFound_Fails()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        const string sourcePath = "nonexistent.txt";
        const string destPath = "copied_file.txt";

        // Act
        var result = await sourceProvider.CopyFileAsync(destProvider, sourcePath, destPath, null, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
        result.Messages.ShouldContain($"Failed to read file at '{sourcePath}'");
    }

    [Fact]
    public async Task CopyFilesAsync_CrossProvider_PartialFailure()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        var filePairs = new[]
        {
            ("source1.txt", "dest1.txt"),
            ("nonexistent.txt", "dest2.txt")
        };
        await sourceProvider.WriteTextFileAsync(filePairs[0].Item1, "Content1");

        // Act
        var result = await sourceProvider.CopyFilesAsync(destProvider, filePairs, null, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Messages.ShouldContain("Failed to write some files: 1/2 succeeded");
        result.ShouldContainError<FileSystemError>("File not found");

        var destExists1 = await destProvider.FileExistsAsync(filePairs[0].Item2);
        destExists1.IsSuccess.ShouldBeTrue($"Destination file {filePairs[0].Item2} should exist: {string.Join(", ", destExists1.Messages)}");

        var destExists2 = await destProvider.FileExistsAsync(filePairs[1].Item2);
        destExists2.IsSuccess.ShouldBeFalse($"Destination file {filePairs[1].Item2} should not exist: {string.Join(", ", destExists2.Messages)}");
    }

    [Fact]
    public async Task CopyFileAsync_CrossProvider_Success()
    {
        // Arrange
        var sourceProvider = this.CreateLocalProvider();
        var destProvider = this.CreateInMemoryProvider();
        const string sourcePath = "file.txt";
        const string destPath = "copied_file.txt";
        var content = "Hello, World!"u8.ToArray();
        await sourceProvider.WriteFileAsync(sourcePath, new MemoryStream(content), null, CancellationToken.None);

        // Act
        var result = await sourceProvider.CopyFileAsync(destProvider, sourcePath, destPath, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"CopyFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Wrote file from '{sourcePath}' (source provider) to '{destPath}' (destination provider)");

        var destExists = await destProvider.FileExistsAsync(destPath);
        destExists.IsSuccess.ShouldBeTrue($"Destination file should exist: {string.Join(", ", destExists.Messages)}");

        var destContentResult = await destProvider.ReadBytesAsync(destPath);
        destContentResult.IsSuccess.ShouldBeTrue($"Reading destination file failed: {string.Join(", ", destContentResult.Messages)}");
        destContentResult.Value.ShouldBeEquivalentTo(content);
    }

    [Fact]
    public async Task CopyFilesAsync_CrossProvider_Success()
    {
        // Arrange
        var sourceProvider = this.CreateLocalProvider();
        var destProvider = this.CreateInMemoryProvider();
        var filePairs = new[]
        {
            ("source1.txt", "dest1.txt"),
            ("source2.txt", "dest2.txt")
        };
        await sourceProvider.WriteTextFileAsync(filePairs[0].Item1, "Content1");
        await sourceProvider.WriteTextFileAsync(filePairs[1].Item1, "Content2");

        // Act
        var result = await sourceProvider.CopyFilesAsync(destProvider, filePairs, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"CopyFilesAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Wrote all {filePairs.Length} files from source provider to destination provider");

        foreach (var (_, dest) in filePairs)
        {
            var destExists = await destProvider.FileExistsAsync(dest);
            destExists.IsSuccess.ShouldBeTrue($"Destination file {dest} should exist: {string.Join(", ", destExists.Messages)}");
        }
    }

    [Fact]
    public async Task MoveFileAsync_CrossProvider_Success()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        const string sourcePath = "file.txt";
        const string destPath = "moved_file.txt";
        var content = "Hello, World!"u8.ToArray();
        await sourceProvider.WriteFileAsync(sourcePath, new MemoryStream(content), null, CancellationToken.None);

        // Act
        var result = await sourceProvider.MoveFileAsync(destProvider, sourcePath, destPath, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"MoveFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Moved file from '{sourcePath}' (source provider) to '{destPath}' (destination provider)");

        var destExists = await destProvider.FileExistsAsync(destPath);
        destExists.IsSuccess.ShouldBeTrue($"Destination file should exist: {string.Join(", ", destExists.Messages)}");

        var sourceExists = await sourceProvider.FileExistsAsync(sourcePath);
        sourceExists.IsSuccess.ShouldBeFalse($"Source file should not exist: {string.Join(", ", sourceExists.Messages)}");

        var destContentResult = await destProvider.ReadBytesAsync(destPath);
        destContentResult.IsSuccess.ShouldBeTrue($"Reading destination file failed: {string.Join(", ", destContentResult.Messages)}");
        destContentResult.Value.ShouldBeEquivalentTo(content);
    }

    [Fact]
    public async Task MoveFilesAsync_CrossProvider_Success()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        var filePairs = new[]
        {
            ("source1.txt", "dest1.txt"),
            ("source2.txt", "dest2.txt")
        };
        await sourceProvider.WriteTextFileAsync(filePairs[0].Item1, "Content1");
        await sourceProvider.WriteTextFileAsync(filePairs[1].Item1, "Content2");

        // Act
        var result = await sourceProvider.MoveFilesAsync(destProvider, filePairs, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"MoveFilesAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Moved all {filePairs.Length} files from source provider to destination provider");

        foreach (var (source, dest) in filePairs)
        {
            var destExists = await destProvider.FileExistsAsync(dest);
            destExists.IsSuccess.ShouldBeTrue($"Destination file {dest} should exist: {string.Join(", ", destExists.Messages)}");

            var sourceExists = await sourceProvider.FileExistsAsync(source);
            sourceExists.IsSuccess.ShouldBeFalse($"Source file {source} should not exist: {string.Join(", ", sourceExists.Messages)}");
        }
    }

    [Fact]
    public async Task DeepCopyAsync_CrossProvider_DirectoryStructureWithFiles_Success()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        const string sourcePath = "source";
        const string destPath = "dest";

        // Create directory structure: source/dir1/dir2, source/dir3
        await sourceProvider.CreateDirectoryAsync("source/dir1/dir2");
        await sourceProvider.CreateDirectoryAsync("source/dir3");

        // Create files: source/file1.txt, source/dir1/file2.txt, source/dir1/dir2/file3.txt, source/dir3/file4.txt
        await sourceProvider.WriteTextFileAsync("source/file1.txt", "File 1 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/file2.txt", "File 2 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/dir2/file3.txt", "File 3 content");
        await sourceProvider.WriteTextFileAsync("source/dir3/file4.txt", "File 4 content");

        var expectedDirs = new List<string> { "dest/dir1", "dest/dir1/dir2", "dest/dir3" };
        var expectedFiles = new List<string> { "dest/file1.txt", "dest/dir1/file2.txt", "dest/dir1/dir2/file3.txt", "dest/dir3/file4.txt" };

        // Act
        var result = await sourceProvider.DeepCopyAsync(destProvider, sourcePath, destPath, skipFiles: false, searchPattern: null, progress: null, cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"DeepCopyAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Deep copied structure from '{sourcePath}' (source provider) to '{destPath}' (destination provider)");

        // Verify directories
        var destDirsResult = await destProvider.ListDirectoriesAsync(destPath, "*.*", true);
        destDirsResult.IsSuccess.ShouldBeTrue($"Listing directories failed: {string.Join(", ", destDirsResult.Messages)}");
        destDirsResult.Value.ShouldContain(expectedDirs[0]);
        destDirsResult.Value.ShouldContain(expectedDirs[1]);
        destDirsResult.Value.ShouldContain(expectedDirs[2]);

        // Verify files
        var destFilesResult = await destProvider.ListFilesAsync(destPath, "*.*", true);
        destFilesResult.IsSuccess.ShouldBeTrue($"Listing files failed: {string.Join(", ", destFilesResult.Messages)}");
        destFilesResult.Value.Files.ShouldContain(expectedFiles[0]);
        destFilesResult.Value.Files.ShouldContain(expectedFiles[1]);
        destFilesResult.Value.Files.ShouldContain(expectedFiles[2]);

        // Verify file contents
        foreach (var file in expectedFiles)
        {
            var sourceFile = file.Replace("dest", "source");
            var sourceContent = await sourceProvider.ReadTextFileAsync(sourceFile);
            var destContent = await destProvider.ReadTextFileAsync(file);
            destContent.IsSuccess.ShouldBeTrue($"Reading file {file} failed: {string.Join(", ", destContent.Messages)}");
            //destContent.Value.ShouldBeSameAs(sourceContent.Value);
        }
    }

    [Fact]
    public async Task DeepCopyAsync_CrossProvider_SkipFiles_CopiesOnlyDirectoryStructure()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        const string sourcePath = "source";
        const string destPath = "dest";

        // Create directory structure: source/dir1/dir2, source/dir3
        await sourceProvider.CreateDirectoryAsync("source/dir1/dir2");
        await sourceProvider.CreateDirectoryAsync("source/dir3");

        // Create files: source/file1.txt, source/dir1/file2.txt, source/dir1/dir2/file3.txt
        await sourceProvider.WriteTextFileAsync("source/file1.txt", "File 1 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/file2.txt", "File 2 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/dir2/file3.txt", "File 3 content");

        var expectedDirs = new List<string> { "dest/dir1", "dest/dir1/dir2", "dest/dir3" };

        // Act
        var result = await sourceProvider.DeepCopyAsync(destProvider, sourcePath, destPath, skipFiles: true, searchPattern: null, progress: null, cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"DeepCopyAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Deep copied structure from '{sourcePath}' (source provider) to '{destPath}' (destination provider) (files skipped)");

        // Verify directories
        var destDirsResult = await destProvider.ListDirectoriesAsync(destPath, null, true);
        destDirsResult.IsSuccess.ShouldBeTrue($"Listing directories failed: {string.Join(", ", destDirsResult.Messages)}");
        destDirsResult.Value.ShouldContain(expectedDirs[0]);
        destDirsResult.Value.ShouldContain(expectedDirs[1]);
        destDirsResult.Value.ShouldContain(expectedDirs[2]);

        // Verify no files were copied
        var destFilesResult = await destProvider.ListFilesAsync(destPath, "*.*", true);
        destFilesResult.IsSuccess.ShouldBeTrue($"Listing files failed: {string.Join(", ", destFilesResult.Messages)}");
        destFilesResult.Value.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeepCopyAsync_CrossProvider_FilterFilesWithSearchPattern_Success()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        const string sourcePath = "source";
        const string destPath = "dest";

        // Create directory structure: source/dir1/dir2, source/dir3
        await sourceProvider.CreateDirectoryAsync("source/dir1/dir2");
        await sourceProvider.CreateDirectoryAsync("source/dir3");

        // Create files: source/file1.txt, source/dir1/file2.txt, source/dir1/dir2/file3.jpg, source/dir3/file4.txt
        await sourceProvider.WriteTextFileAsync("source/file1.txt", "File 1 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/file2.txt", "File 2 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/dir2/file3.jpg", "File 3 content"); // exluded by wildcard
        await sourceProvider.WriteTextFileAsync("source/dir3/file4.txt", "File 4 content");

        var expectedDirs = new List<string> { "dest/dir1", /*"dest/dir1/dir2",*/ "dest/dir3" };
        var expectedFiles = new List<string> { "dest/file1.txt", "dest/dir1/file2.txt", "dest/dir3/file4.txt" };

        // Act
        var result = await sourceProvider.DeepCopyAsync(destProvider, sourcePath, destPath, skipFiles: false, searchPattern: "*.txt", progress: null, cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"DeepCopyAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Deep copied structure from '{sourcePath}' (source provider) to '{destPath}' (destination provider) (filtered by '*.txt')");

        // Verify directories
        var destDirsResult = await destProvider.ListDirectoriesAsync(destPath, null, true);
        destDirsResult.IsSuccess.ShouldBeTrue($"Listing directories failed: {string.Join(", ", destDirsResult.Messages)}");
        destDirsResult.Value.ShouldContain(expectedDirs[0]);
        destDirsResult.Value.ShouldContain(expectedDirs[1]);

        // Verify files (only .txt files should be copied)
        var destFilesResult = await destProvider.ListFilesAsync(destPath, "*.*", true);
        destFilesResult.IsSuccess.ShouldBeTrue($"Listing files failed: {string.Join(", ", destFilesResult.Messages)}");
        destFilesResult.Value.Files.ShouldContain(expectedFiles[0]);
        destFilesResult.Value.Files.ShouldContain(expectedFiles[1]);
        destFilesResult.Value.Files.ShouldContain(expectedFiles[2]);
    }

    [Fact]
    public async Task DeepCopyAsync_CrossProvider_Cancellation_Fails()
    {
        // Arrange
        var sourceProvider = this.CreateInMemoryProvider();
        var destProvider = this.CreateLocalProvider();
        const string sourcePath = "source";
        const string destPath = "dest";
        var cts = new CancellationTokenSource();

        // Create directory structure: source/dir1/dir2, source/dir3
        await sourceProvider.CreateDirectoryAsync("source/dir1/dir2");
        await sourceProvider.CreateDirectoryAsync("source/dir3");

        // Create files: source/file1.txt, source/dir1/file2.txt
        await sourceProvider.WriteTextFileAsync("source/file1.txt", "File 1 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/file2.txt", "File 2 content");

        // Act
        cts.Cancel(); // Cancel immediately
        var result = await sourceProvider.DeepCopyAsync(destProvider, sourcePath, destPath, skipFiles: false, searchPattern: null, progress: null, cancellationToken: cts.Token);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<OperationCancelledError>("Operation cancelled");
        result.Messages.ShouldContain($"Cancelled deep copying from '{sourcePath}' to '{destPath}'");
    }
}