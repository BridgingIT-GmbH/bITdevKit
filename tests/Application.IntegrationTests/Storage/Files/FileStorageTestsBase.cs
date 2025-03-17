// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using System.Security.Cryptography;

public abstract class FileStorageTestsBase
{
    protected abstract IFileStorageProvider CreateProvider();
    //protected abstract FileStorageFactory CreateFactory(IServiceProvider serviceProvider = null);

    public virtual async Task ExistsAsync_ExistingFile_FileFound()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(path, stream, null, CancellationToken.None);

        // Act
        var result = await provider.ExistsAsync(path, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Checked existence of file at '{path}'");
    }

    public virtual async Task ExistsAsync_NonExistingFile_FileNotFound()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";

        // Act
        var result = await provider.ExistsAsync(path, null, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<NotFoundError>("File not found");
    }

    public virtual async Task ReadFileAsync_ExistingFile_ReturnsStream()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        var content = Encoding.UTF8.GetBytes("Test content");
        await using var writeStream = new MemoryStream(content);
        await provider.WriteFileAsync(path, writeStream, null, CancellationToken.None);

        // Act
        var result = await provider.ReadFileAsync(path, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
        await using var readStream = result.Value;
        var readBytes = new byte[content.Length];
        await readStream.ReadExactlyAsync(readBytes, 0, content.Length, CancellationToken.None);
        readBytes.ShouldBe(content);
    }

    public virtual async Task ReadFileAsync_NonExistingFile_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";

        // Act
        var result = await provider.ReadFileAsync(path, null, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
    }

    public virtual async Task WriteFileAsync_ValidInput_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));

        // Act
        var result = await provider.WriteFileAsync(path, stream, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Wrote file at '{path}'");
        var existsResult = await provider.ExistsAsync(path, null, CancellationToken.None);
        existsResult.ShouldBeSuccess();
    }

    public virtual async Task DeleteFileAsync_ExistingFile_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(path, stream, null, CancellationToken.None);

        // Act
        var result = await provider.DeleteFileAsync(path, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Deleted file at '{path}'");
        var existsResult = await provider.ExistsAsync(path, null, CancellationToken.None);
        existsResult.ShouldBeFailure();
        existsResult.ShouldContainError<NotFoundError>("File not found");
    }

    public virtual async Task DeleteFileAsync_NonExistingFile_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";

        // Act
        var result = await provider.DeleteFileAsync(path, null, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
    }

    public virtual async Task GetChecksumAsync_ExistingFile_ReturnsChecksum()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        var content = Encoding.UTF8.GetBytes("Test content");
        await using var stream = new MemoryStream(content);
        await provider.WriteFileAsync(path, stream, null, CancellationToken.None);

        // Act
        var result = await provider.GetChecksumAsync(path, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNullOrEmpty();
        result.Value.ShouldBe(this.ComputeSha256Hash(content));
    }

    public virtual async Task GetChecksumAsync_NonExistingFile_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";

        // Act
        var result = await provider.GetChecksumAsync(path, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
    }

    public virtual async Task GetFileInfoAsync_ExistingFile_ReturnsMetadata()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        var content = Encoding.UTF8.GetBytes("Test content");
        await using var stream = new MemoryStream(content);
        await provider.WriteFileAsync(path, stream, null, CancellationToken.None);

        // Act
        var result = await provider.GetFileMetadataAsync(path, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
        result.Value.Path.ShouldBe(path);
    }

    public virtual async Task GetFileInfoAsync_NonExistingFile_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";

        // Act
        var result = await provider.GetFileMetadataAsync(path, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
    }

    public virtual async Task SetFileMetadataAsync_ExistingFile_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(path, stream, null, CancellationToken.None);
        var metadata = new FileMetadata { LastModified = DateTime.UtcNow.AddDays(1) };

        // Act
        var result = await provider.SetFileMetadataAsync(path, metadata, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Set metadata for file at '{path}'");
    }

    public virtual async Task SetFileMetadataAsync_NonExistingFile_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";
        var metadata = new FileMetadata { LastModified = DateTime.UtcNow };

        // Act
        var result = await provider.SetFileMetadataAsync(path, metadata, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
    }

    public virtual async Task UpdateFileMetadataAsync_ExistingFile_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/file.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(path, stream, null, CancellationToken.None);
        var update = (FileMetadata m) => new FileMetadata { Path = m.Path, Length = m.Length, LastModified = DateTime.UtcNow.AddDays(1) };

        // Act
        var result = await provider.UpdateFileMetadataAsync(path, update, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldNotBeNull();
    }

    public virtual async Task UpdateFileMetadataAsync_NonExistingFile_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "nonexistent/file.txt";
        var update = (FileMetadata m) => new FileMetadata { Path = m.Path, Length = m.Length, LastModified = DateTime.UtcNow };

        // Act
        var result = await provider.UpdateFileMetadataAsync(path, update, CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<FileSystemError>("File not found");
    }

    public virtual async Task ListFilesAsync_ValidPath_ReturnsFiles()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/";
        const string file1 = "test/file1.txt";
        const string file2 = "test/file2.txt";
        const string file3 = "test/file3.md";
        await using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content1"));
        await using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content2"));
        await using var stream3 = new MemoryStream(Encoding.UTF8.GetBytes("Content3"));
        await provider.WriteFileAsync(file1, stream1, null, CancellationToken.None);
        await provider.WriteFileAsync(file2, stream2, null, CancellationToken.None);
        await provider.WriteFileAsync(file3, stream3, null, CancellationToken.None);

        // Act
        var result = await provider.ListFilesAsync(path, "*.txt", true, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.Files.ShouldNotBeNull();
        result.Value.Files.ShouldNotBeEmpty();
        result.Value.Files.ShouldContain(f => f == "test/file1.txt" || f == "test/file2.txt");
        result.Value.Files.ShouldNotContain("test/file3.md");
    }

    public virtual async Task CopyFileAsync_ValidPaths_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string source = "test/source.txt";
        const string dest = "test/dest.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(source, stream, null, CancellationToken.None);

        // Act
        var result = await provider.CopyFileAsync(source, dest, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Copied file from '{source}' to '{dest}'");
        var destExists = await provider.ExistsAsync(dest, null, CancellationToken.None);
        destExists.ShouldBeSuccess();
    }

    public virtual async Task RenameFileAsync_ValidPaths_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string oldPath = "test/old.txt";
        const string newPath = "test/new.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(oldPath, stream, null, CancellationToken.None);

        // Act
        var result = await provider.RenameFileAsync(oldPath, newPath, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Renamed file from '{oldPath}' to '{newPath}'");
        var newExists = await provider.ExistsAsync(newPath, null, CancellationToken.None);
        newExists.ShouldBeSuccess();
        var oldExists = await provider.ExistsAsync(oldPath, null, CancellationToken.None);
        oldExists.ShouldBeFailure();
        oldExists.ShouldContainError<NotFoundError>("File not found");
    }

    public virtual async Task MoveFileAsync_ValidPaths_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string source = "test/source.txt";
        const string dest = "test/dest.txt";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        await provider.WriteFileAsync(source, stream, null, CancellationToken.None);

        // Act
        var result = await provider.MoveFileAsync(source, dest, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Moved file from '{source}' to '{dest}'");
        var destExists = await provider.ExistsAsync(dest, null, CancellationToken.None);
        destExists.ShouldBeSuccess();
        var sourceExists = await provider.ExistsAsync(source, null, CancellationToken.None);
        sourceExists.ShouldBeFailure();
        sourceExists.ShouldContainError<NotFoundError>("File not found");
    }

    public virtual async Task CopyFilesAsync_ValidPairs_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        var pairs = new[]
        {
            ("test/source1.txt", "test/dest1.txt"),
            ("test/source2.txt", "test/dest2.txt")
        };
        await using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content1"));
        await using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content2"));
        await provider.WriteFileAsync(pairs[0].Item1, stream1, null, CancellationToken.None);
        await provider.WriteFileAsync(pairs[1].Item1, stream2, null, CancellationToken.None);

        // Act
        var result = await provider.CopyFilesAsync(pairs, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage("Copied all 2 files");
        foreach (var (_, dest) in pairs)
        {
            var destExists = await provider.ExistsAsync(dest, null, CancellationToken.None);
            destExists.ShouldBeSuccess();
        }
    }

    public virtual async Task MoveFilesAsync_ValidPairs_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        var pairs = new[]
        {
            ("test/source1.txt", "test/dest1.txt"),
            ("test/source2.txt", "test/dest2.txt")
        };
        await using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content1"));
        await using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content2"));
        await provider.WriteFileAsync(pairs[0].Item1, stream1, null, CancellationToken.None);
        await provider.WriteFileAsync(pairs[1].Item1, stream2, null, CancellationToken.None);

        // Act
        var result = await provider.MoveFilesAsync(pairs, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage("Moved all 2 files");
        foreach (var (_, dest) in pairs)
        {
            var destExists = await provider.ExistsAsync(dest, null, CancellationToken.None);
            destExists.ShouldBeSuccess();
        }
        foreach (var (source, _) in pairs)
        {
            var sourceExists = await provider.ExistsAsync(source, null, CancellationToken.None);
            sourceExists.ShouldBeFailure();
            sourceExists.ShouldContainError<NotFoundError>("File not found");
        }
    }

    public virtual async Task DeleteFilesAsync_ExistingFiles_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        var paths = new[] { "test/file1.txt", "test/file2.txt" };
        await using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content1"));
        await using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content2"));
        await provider.WriteFileAsync(paths[0], stream1, null, CancellationToken.None);
        await provider.WriteFileAsync(paths[1], stream2, null, CancellationToken.None);

        // Act
        var result = await provider.DeleteFilesAsync(paths, null, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage("Deleted all 2 files");
        foreach (var path in paths)
        {
            var existsResult = await provider.ExistsAsync(path, null, CancellationToken.None);
            existsResult.ShouldBeFailure();
            existsResult.ShouldContainError<NotFoundError>("File not found");
        }
    }

    public virtual async Task IsDirectoryAsync_ExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/";
        await provider.CreateDirectoryAsync(path, CancellationToken.None);

        // Act
        var result = await provider.IsDirectoryAsync(path, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
    }

    public virtual async Task CreateDirectoryAsync_ValidPath_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/newdir/";

        // Act
        var result = await provider.CreateDirectoryAsync(path, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        //result.ShouldContainMessage($"Created directory at '{path}'");
        var isDirResult = await provider.IsDirectoryAsync(path, CancellationToken.None);
        isDirResult.ShouldBeSuccess();
    }

    public virtual async Task DeleteDirectoryAsync_ExistingDirectory_Succeeds()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/dir/";
        await provider.CreateDirectoryAsync(path, CancellationToken.None);

        // Act
        var result = await provider.DeleteDirectoryAsync(path, true, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.ShouldContainMessage($"Deleted directory at '{path}'");
        var isDirResult = await provider.IsDirectoryAsync(path, CancellationToken.None);
        isDirResult.ShouldBeFailure();
        isDirResult.ShouldContainError<NotFoundError>("Directory not found");
    }

    public virtual async Task ListDirectoriesAsync_ValidPath_ReturnsDirectories()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test/";
        await provider.CreateDirectoryAsync("test/dir1", CancellationToken.None);
        await provider.CreateDirectoryAsync("test/dir2", CancellationToken.None);

        // Act
        var result = await provider.ListDirectoriesAsync(path, "*dir*", true, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.ShouldContain(d => d == "test/dir1" || d == "test/dir2");
    }

    public virtual async Task WriteCompressedFileAsync_Stream_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test_compressed.zip";
        var content = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        const string password = ""; // Empty password as per your update

        // Act
        var result = await provider.WriteCompressedFileAsync(path, content, password, null, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"WriteCompressedFileAsync failed: {string.Join(", ", result.Messages)}");

        // Verify the file exists
        var existsResult = await provider.ExistsAsync(path);
        existsResult.IsSuccess.ShouldBeTrue($"File should exist: {string.Join(", ", existsResult.Messages)}");

        // Read and verify the content
        var readResult = await provider.ReadCompressedFileAsync(path, password, null, CancellationToken.None);
        readResult.IsSuccess.ShouldBeTrue($"ReadCompressedFileAsync failed: {string.Join(", ", readResult.Messages)}");
        await using var decompressedStream = readResult.Value;
        new StreamReader(decompressedStream).ReadToEnd().ShouldBe("Test content");
    }

    public virtual async Task WriteCompressedFileAsync_Directory_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string directoryPath = "test_directory";
        const string zipPath = "test_directory.zip";
        const string password = ""; // Empty password as per your update

        // Create test files in the directory
        await provider.CreateDirectoryAsync(directoryPath, CancellationToken.None);
        await provider.WriteFileAsync(Path.Combine(directoryPath, "file1.txt"), new MemoryStream(Encoding.UTF8.GetBytes("File 1 content")), null, CancellationToken.None);
        await provider.WriteFileAsync(Path.Combine(directoryPath, "subdir/file2.txt"), new MemoryStream(Encoding.UTF8.GetBytes("File 2 content")), null, CancellationToken.None);

        // Act
        var result = await provider.WriteCompressedFileAsync(zipPath, directoryPath, password, null, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"WriteCompressedFileAsync (directory) failed: {string.Join(", ", result.Messages)}");

        // Verify the ZIP file exists
        var existsResult = await provider.ExistsAsync(zipPath);
        existsResult.IsSuccess.ShouldBeTrue($"ZIP file should exist: {string.Join(", ", existsResult.Messages)}");

        // Uncompress and verify content
        var uncompressResult = await provider.UncompressFileAsync(zipPath, "uncompressed", password, null, CancellationToken.None);
        uncompressResult.IsSuccess.ShouldBeTrue($"UncompressFileAsync failed: {string.Join(", ", uncompressResult.Messages)}");

        // Verify uncompressed files
        var file1Result = await provider.ReadFileAsync(Path.Combine("uncompressed", "file1.txt"), null, CancellationToken.None);
        file1Result.IsSuccess.ShouldBeTrue($"File1 should exist and be readable: {string.Join(", ", file1Result.Messages)}");
        await using var file1Stream = file1Result.Value;
        new StreamReader(file1Stream).ReadToEnd().ShouldBe("File 1 content");

        var file2Result = await provider.ReadFileAsync(Path.Combine("uncompressed", "subdir/file2.txt"), null, CancellationToken.None);
        file2Result.IsSuccess.ShouldBeTrue($"File2 should exist and be readable: {string.Join(", ", file2Result.Messages)}");
        await using var file2Stream = file2Result.Value;
        new StreamReader(file2Stream).ReadToEnd().ShouldBe("File 2 content");
    }

    public virtual async Task ReadCompressedFileAsync_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test_compressed.zip";
        var content = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
        const string password = "testPassword123";

        await provider.WriteCompressedFileAsync(path, content, password, null, CancellationToken.None);

        // Act
        var result = await provider.ReadCompressedFileAsync(path, password, null, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"ReadCompressedFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Password-protected decompressed and read file at '{path}'");
        await using var decompressedStream = result.Value;
        new StreamReader(decompressedStream).ReadToEnd().ShouldBe("Test content");
    }

    public virtual async Task UncompressFileAsync_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string zipPath = "test_directory.zip";
        const string directoryPath = "test_directory";
        const string destinationPath = "uncompressed";
        const string password = "testPassword123";

        // Create test files in the directory
        await provider.CreateDirectoryAsync(directoryPath, CancellationToken.None);
        await provider.WriteFileAsync(Path.Combine(directoryPath, "file1.txt"), new MemoryStream(Encoding.UTF8.GetBytes("File 1 content")), null, CancellationToken.None);
        await provider.WriteFileAsync(Path.Combine(directoryPath, "subdir/file2.txt"), new MemoryStream(Encoding.UTF8.GetBytes("File 2 content")), null, CancellationToken.None);

        // Compress the directory
        await provider.WriteCompressedFileAsync(zipPath, directoryPath, password, null, CancellationToken.None);

        // Act
        var result = await provider.UncompressFileAsync(zipPath, destinationPath, password, null, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"UncompressFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Password-protected uncompressed file at '{zipPath}' to directory '{destinationPath}'");

        // Verify uncompressed files
        var file1Result = await provider.ReadFileAsync(Path.Combine(destinationPath, "file1.txt"), null, CancellationToken.None);
        file1Result.IsSuccess.ShouldBeTrue($"File1 should exist and be readable: {string.Join(", ", file1Result.Messages)}");
        await using var file1Stream = file1Result.Value;
        new StreamReader(file1Stream).ReadToEnd().ShouldBe("File 1 content");

        var file2Result = await provider.ReadFileAsync(Path.Combine(destinationPath, "subdir/file2.txt"), null, CancellationToken.None);
        file2Result.IsSuccess.ShouldBeTrue($"File2 should exist and be readable: {string.Join(", ", file2Result.Messages)}");
        await using var file2Stream = file2Result.Value;
        new StreamReader(file2Stream).ReadToEnd().ShouldBe("File 2 content");
    }

    public virtual async Task WriteEncryptedFileAsync_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test_encrypted.bin";
        var content = new MemoryStream(Encoding.UTF8.GetBytes("Encrypted content"));
        const string encryptionKey = "testKey123456789012345678901234567890";
        const string initializationVector = "testIV1234567890123";
        var progress = new Progress<FileProgress>();

        // Act
        var result = await provider.WriteEncryptedFileAsync(path, content, encryptionKey, initializationVector, progress, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"WriteEncryptedFileAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Encrypted and wrote file at '{path}'");

        // Verify the file exists
        var existsResult = await provider.ExistsAsync(path);
        existsResult.IsSuccess.ShouldBeTrue($"File should exist: {string.Join(", ", existsResult.Messages)}");

        // Read and verify the content
        var readResult = await provider.ReadEncryptedFileAsync(path, encryptionKey, initializationVector, progress, CancellationToken.None);
        readResult.IsSuccess.ShouldBeTrue($"ReadEncryptedFileAsync failed: {string.Join(", ", readResult.Messages)}");
        await using var decryptedStream = readResult.Value;
        new StreamReader(decryptedStream).ReadToEnd().ShouldBe("Encrypted content");
    }

    public virtual async Task WriteBytesAsync_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test_bytes.bin";
        var bytes = Encoding.UTF8.GetBytes("Byte content");
        var progress = new Progress<FileProgress>();

        // Act
        var result = await provider.WriteBytesAsync(path, bytes, progress, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"WriteBytesAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Wrote bytes to file at '{path}'");

        // Verify the file exists
        var existsResult = await provider.ExistsAsync(path);
        existsResult.IsSuccess.ShouldBeTrue($"File should exist: {string.Join(", ", existsResult.Messages)}");

        // Read and verify the content
        var readResult = await provider.ReadBytesAsync(path, progress, CancellationToken.None);
        readResult.IsSuccess.ShouldBeTrue($"ReadBytesAsync failed: {string.Join(", ", readResult.Messages)}");
        readResult.Value.ShouldBe(bytes);
    }

    public virtual async Task WriteReadObjectAsync_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test_object.json";
        var person = new PersonStub { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe" };
        var progress = new Progress<FileProgress>();

        // Act
        var result = await provider.WriteFileAsync(path, person, null, progress, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue($"WriteObjectAsync failed: {string.Join(", ", result.Messages)}");
        result.Messages.ShouldContain($"Wrote object to file at '{path}'");

        // Verify the file exists
        var existsResult = await provider.ExistsAsync(path);
        existsResult.IsSuccess.ShouldBeTrue($"File should exist: {string.Join(", ", existsResult.Messages)}");

        // Read and verify the content
        var readResult = await provider.ReadFileAsync<PersonStub>(path, null, progress, CancellationToken.None);
        readResult.IsSuccess.ShouldBeTrue($"ReadObjectAsync failed: {string.Join(", ", readResult.Messages)}");
        var personRead = readResult.Value;
        personRead.Id.ShouldBe(person.Id);
        personRead.FirstName.ShouldBe(person.FirstName);
        personRead.LastName.ShouldBe(person.LastName);
        personRead.Age.ShouldBe(person.Age);
    }

    public virtual async Task WriteCompressedFileAsync_Directory_NonExistentDirectory_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string zipPath = "test_directory.zip";
        const string directoryPath = "non_existent_directory";
        var progress = new Progress<FileProgress>();

        // Act
        var result = await provider.WriteCompressedFileAsync(zipPath, directoryPath, "testPassword123", progress, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse("WriteCompressedFileAsync should fail with non-existent directory");
        result.Messages.ShouldContain("Directory does not exist"); // Adjust based on your IFileStorageProvider implementation
    }

    public virtual async Task UncompressFileAsync_NonExistentZip_Fails()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string zipPath = "non_existent.zip";
        const string destinationPath = "uncompressed";
        var progress = new Progress<FileProgress>();

        // Act
        var result = await provider.UncompressFileAsync(zipPath, destinationPath, "testPassword123", progress, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse("UncompressFileAsync should fail with non-existent ZIP");
        result.Messages.ShouldContain("File does not exist"); // Adjust based on your IFileStorageProvider implementation
    }

    public virtual async Task TraverseFilesAsync_Success()
    {
        // Arrange
        var provider = this.CreateProvider();
        const string path = "test_directory";
        var progress = new Progress<FileProgress>();

        // Create test files in the directory
        await provider.CreateDirectoryAsync(path, CancellationToken.None);
        await provider.WriteFileAsync(Path.Combine(path, "file1.txt"), new MemoryStream(Encoding.UTF8.GetBytes("File 1 content")), null, CancellationToken.None);
        await provider.WriteFileAsync(Path.Combine(path, "subdir/file2.txt"), new MemoryStream(Encoding.UTF8.GetBytes("File 2 content")), null, CancellationToken.None);

        long expectedBytes = 0;
        var file1Info = await provider.GetFileMetadataAsync(Path.Combine(path, "file1.txt"), CancellationToken.None);
        var file2Info = await provider.GetFileMetadataAsync(Path.Combine(path, "subdir/file2.txt"), CancellationToken.None);
        expectedBytes += file1Info.Value.Length + file2Info.Value.Length;

        // Act
        var result = await provider.TraverseFilesAsync(path, null, progress, CancellationToken.None);

        // Assert
        result.ShouldBeSuccess($"TraverseFilesAsync failed: {string.Join(", ", result.Messages)}");
        result.ShouldContainMessage($"Traversed '{path}' and found 2 files");

        result.Value.Count.ShouldBe(2, "Should find exactly 2 files");

        // Verify file metadata
        var file1Metadata = result.Value.FirstOrDefault(m => m.Path == $"{path}/file1.txt");
        var file2Metadata = result.Value.FirstOrDefault(m => m.Path == $"{path}/subdir/file2.txt");
        file1Metadata.ShouldNotBeNull("File1 metadata should exist");
        file2Metadata.ShouldNotBeNull("File2 metadata should exist");
        file1Metadata.Length.ShouldBe(file1Info.Value.Length, "File1 size should match");
        file2Metadata.Length.ShouldBe(file2Info.Value.Length, "File2 size should match");

        // Test with file action
        var processedFiles = new List<string>();
        var processFile = async (string filePath, Stream stream, CancellationToken ct) =>
        {
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(ct);
            processedFiles.Add(filePath);
            //this.output.WriteLine($"Processed {filePath}: {content}");
        };
        var resultWithAction = await provider.TraverseFilesAsync(path, processFile, progress, CancellationToken.None);

        resultWithAction.ShouldBeSuccess($"TraverseFilesAsync with action failed: {string.Join(", ", resultWithAction.Messages)}");
        processedFiles.Count.ShouldBe(2, "Should process exactly 2 files");
        processedFiles.ShouldContain($"{path}/file1.txt", "Should process file1.txt");
        processedFiles.ShouldContain($"{path}/subdir/file2.txt", "Should process file2.txt");
    }

    private string ComputeSha256Hash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToBase64String(hash);
    }
}