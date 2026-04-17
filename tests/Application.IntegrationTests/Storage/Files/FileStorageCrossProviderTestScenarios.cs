// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;

public static class FileStorageCrossProviderTestScenarios
{
    public static async Task CopyFileAsync_SourceNotFound_Fails(IFileStorageProvider sourceProvider, IFileStorageProvider destinationProvider)
    {
        const string sourcePath = "nonexistent.txt";
        const string destinationPath = "copied_file.txt";

        var result = await sourceProvider.CopyFileAsync(destinationProvider, sourcePath, destinationPath, null, CancellationToken.None);

        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
        result.Messages.ShouldContain($"Failed to read file at '{sourcePath}'");
    }

    public static async Task CopyFileAsync_Success(IFileStorageProvider sourceProvider, IFileStorageProvider destinationProvider)
    {
        const string sourcePath = "file.txt";
        const string destinationPath = "copied_file.txt";
        byte[] content = "Hello, World!"u8.ToArray();
        await sourceProvider.WriteFileAsync(sourcePath, new MemoryStream(content), null, CancellationToken.None);

        var result = await sourceProvider.CopyFileAsync(destinationProvider, sourcePath, destinationPath, null, CancellationToken.None);

        result.ShouldBeSuccess($"CopyFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Wrote file from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider)");

        var destinationExists = await destinationProvider.FileExistsAsync(destinationPath);
        destinationExists.IsSuccess.ShouldBeTrue($"Destination file should exist: {string.Join(", ", destinationExists.Messages)}");

        var destinationContentResult = await destinationProvider.ReadBytesAsync(destinationPath);
        destinationContentResult.IsSuccess.ShouldBeTrue($"Reading destination file failed: {string.Join(", ", destinationContentResult.Messages)}");
        destinationContentResult.Value.ShouldBeEquivalentTo(content);
    }

    public static async Task MoveFileAsync_Success(IFileStorageProvider sourceProvider, IFileStorageProvider destinationProvider)
    {
        const string sourcePath = "file.txt";
        const string destinationPath = "moved_file.txt";
        byte[] content = "Hello, World!"u8.ToArray();
        await sourceProvider.WriteFileAsync(sourcePath, new MemoryStream(content), null, CancellationToken.None);

        var result = await sourceProvider.MoveFileAsync(destinationProvider, sourcePath, destinationPath, null, CancellationToken.None);

        result.ShouldBeSuccess($"MoveFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Moved file from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider)");

        var destinationExists = await destinationProvider.FileExistsAsync(destinationPath);
        destinationExists.IsSuccess.ShouldBeTrue($"Destination file should exist: {string.Join(", ", destinationExists.Messages)}");

        var sourceExists = await sourceProvider.FileExistsAsync(sourcePath);
        sourceExists.IsSuccess.ShouldBeFalse($"Source file should not exist: {string.Join(", ", sourceExists.Messages)}");

        var destinationContentResult = await destinationProvider.ReadBytesAsync(destinationPath);
        destinationContentResult.IsSuccess.ShouldBeTrue($"Reading destination file failed: {string.Join(", ", destinationContentResult.Messages)}");
        destinationContentResult.Value.ShouldBeEquivalentTo(content);
    }

    public static async Task DeepCopyAsync_DirectoryStructureWithFiles_Success(IFileStorageProvider sourceProvider, IFileStorageProvider destinationProvider)
    {
        const string sourcePath = "source";
        const string destinationPath = "dest";

        await sourceProvider.CreateDirectoryAsync("source/dir1/dir2");
        await sourceProvider.CreateDirectoryAsync("source/dir3");
        await sourceProvider.WriteTextFileAsync("source/file1.txt", "File 1 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/file2.txt", "File 2 content");
        await sourceProvider.WriteTextFileAsync("source/dir1/dir2/file3.txt", "File 3 content");
        await sourceProvider.WriteTextFileAsync("source/dir3/file4.txt", "File 4 content");

        var expectedDirectories = new List<string> { "dest/dir1", "dest/dir1/dir2", "dest/dir3" };
        var expectedFiles = new List<string> { "dest/file1.txt", "dest/dir1/file2.txt", "dest/dir1/dir2/file3.txt", "dest/dir3/file4.txt" };

        var result = await sourceProvider.DeepCopyAsync(destinationProvider, sourcePath, destinationPath, skipFiles: false, searchPattern: null, progress: null, cancellationToken: CancellationToken.None);

        result.ShouldBeSuccess($"DeepCopyAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Deep copied structure from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider)");

        foreach (var directory in expectedDirectories)
        {
            var destinationDirectoryExistsResult = await destinationProvider.DirectoryExistsAsync(directory, CancellationToken.None);
            destinationDirectoryExistsResult.IsSuccess.ShouldBeTrue($"Directory {directory} should exist: {string.Join(", ", destinationDirectoryExistsResult.Messages)}");
        }

        var destinationFilesResult = await destinationProvider.ListFilesAsync(destinationPath, "*.*", true);
        destinationFilesResult.IsSuccess.ShouldBeTrue($"Listing files failed: {string.Join(", ", destinationFilesResult.Messages)}");
        destinationFilesResult.Value.Files.ShouldContain(expectedFiles[0]);
        destinationFilesResult.Value.Files.ShouldContain(expectedFiles[1]);
        destinationFilesResult.Value.Files.ShouldContain(expectedFiles[2]);
        destinationFilesResult.Value.Files.ShouldContain(expectedFiles[3]);

        foreach (var file in expectedFiles)
        {
            var sourceFile = file.Replace("dest", "source");
            var sourceContent = await sourceProvider.ReadTextFileAsync(sourceFile);
            var destinationContent = await destinationProvider.ReadTextFileAsync(file);

            sourceContent.ShouldBeSuccess();
            destinationContent.IsSuccess.ShouldBeTrue($"Reading file {file} failed: {string.Join(", ", destinationContent.Messages)}");
            destinationContent.Value.ShouldBe(sourceContent.Value);
        }
    }
}
