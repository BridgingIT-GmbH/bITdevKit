# FileStorage Feature Documentation

[TOC]

## Overview

Managing file storage presents challenges due to inconsistent APIs across storage systems, complex requirements for secure file handling, the need for progress reporting during long operations, robust error handling, thread safety concerns, and the demand for extensibility to support new providers or custom functionality.

The BridgingIT DevKit’s `FileStorage` feature addresses these through an `IFileStorageProvider` interface for abstracting file operations, a fluent DI setup with `AddFileStorage`, and the `Result` pattern for error handling and messaging. It supports progress reporting via `IProgress<FileProgress>`.

### Use Cases
- Read, transform, and write files, handling errors and reporting status.
- Traverse providers to index files with metadata, processing files while reporting progress.
- Encrypt files before cloud uploads, ensuring decryption compatibility.

## Usage

### Setting Up a Provider with Dependency Injection (DI)
Configure `FileStorage` using `Microsoft.Extensions.DependencyInjection` with the `AddFileStorage` method, supporting a fluent API for named provider registrations, behaviors, and lifetimes, resolved via `IFileStorageFactory`. Example:

```csharp
services.AddFileStorage(c => c
    .RegisterProvider("inMemory", builder =>
    {
        builder.UseInMemory("TestInMemory")
               .WithLogging()
               .WithLifetime(ServiceLifetime.Transient);
    })
    .RegisterProvider("local", builder =>
    {
        builder.UseLocal(Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString()), "TestLocal")
               .WithLogging()
               .WithLifetime(ServiceLifetime.Singleton);
    })
    .RegisterProvider("network", builder =>
    {
        builder.UseWindowsNetwork(@"\\server\docs", "NetworkStorage", "username", "password", "domain")
               .WithLogging()
               .WithRetry(new RetryOptions { MaxRetries = 3 })
               .WithLifetime(ServiceLifetime.Singleton);
    })
    .RegisterProvider("azureBlob", builder =>
    {
        builder.UseAzureBlob("connection-string", "container-name", "AzureBlobStorage")
               .WithCaching(new CachingOptions { CacheDuration = TimeSpan.FromMinutes(10) })
               .WithLifetime(ServiceLifetime.Scoped);
    }));

// Use the factory to resolve providers
public class FileService
{
    private readonly IFileStorageFactory factory;

    public FileService(IFileStorageFactory factory)
    {
        this.factory = factory;
    }

    public async Task<Result> ProcessFileAsync(string path)
    {
        var provider = factory.CreateProvider("local");
        return await provider.WriteFileAsync(path, new MemoryStream(Encoding.UTF8.GetBytes("Test content")), null, CancellationToken.None);
    }
}
```

This registers "inMemory", "local", "network", and "azureBlob" providers with behaviors and lifetimes, resolved by `IFileStorageFactory`.

### Using Providers
The `IFileStorageProvider` interface defines core file operations, returning `Result` or `Result<T>` for error handling and messaging. Use the factory-resolved provider to perform operations:

#### Core Methods
- **ExistsAsync(string path, CancellationToken token)**: Checks if a file exists at `path`. Returns `Task<Result>` indicating success or failure with errors (e.g., `FileSystemError` for missing files).
  ```csharp
  var existsResult = await factory.CreateProvider("local").ExistsAsync("data.txt", CancellationToken.None);
  existsResult.ShouldBeSuccess("File should exist");
  ```

- **ReadFileAsync(string path, CancellationToken token)**: Reads a file as a `Stream`. Returns `Task<Result<Stream>>` with the stream or errors (e.g., `PermissionError`).
  ```csharp
  var readResult = await factory.CreateProvider("local").ReadFileAsync("data.txt", CancellationToken.None);
  readResult.ShouldBeSuccess("Read should succeed");
  await using var stream = readResult.Value;
  new StreamReader(stream).ReadToEnd().ShouldBe("Test content");
  ```

- **WriteFileAsync(string path, Stream content, CancellationToken token)**: Writes `content` to `path`. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).
  ```csharp
  var writeResult = await factory.CreateProvider("local").WriteFileAsync("data.txt", new MemoryStream(Encoding.UTF8.GetBytes("Test content")), CancellationToken.None);
  writeResult.ShouldBeSuccess("Write should succeed");
  ```

- **DeleteFileAsync(string path, CancellationToken token)**: Deletes a file at `path`. Returns `Task<Result>` with success or errors (e.g., `PermissionError`).
  ```csharp
  var deleteResult = await factory.CreateProvider("local").DeleteFileAsync("data.txt", CancellationToken.None);
  deleteResult.ShouldBeSuccess("Delete should succeed");
  ```

- **GetChecksumAsync(string path, CancellationToken token)**: Computes a checksum for a file. Returns `Task<Result<string>>` with the checksum or errors (e.g., `FileSystemError`).
  ```csharp
  var checksumResult = await factory.CreateProvider("local").GetChecksumAsync("data.txt", CancellationToken.None);
  checksumResult.ShouldBeSuccess("Checksum should succeed");
  ```

- **GetFileInfoAsync(string path, CancellationToken token)**: Retrieves metadata for a file. Returns `Task<Result<FileMetadata>>` with `FileMetadata` or errors (e.g., `NotFoundError`).
  ```csharp
  var infoResult = await factory.CreateProvider("local").GetFileInfoAsync("data.txt", CancellationToken.None);
  infoResult.ShouldBeSuccess("Metadata retrieval should succeed");
  var metadata = infoResult.Value;
  metadata.Path.ShouldBe("data.txt");
  ```

- **ListFilesAsync(string path, string searchPattern, bool recursive, string continuationToken, CancellationToken token)**: Lists files matching `searchPattern` under `path`. Returns `Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>>` with files and pagination token or errors (e.g., `PermissionError`).
  ```csharp
  var listResult = await factory.CreateProvider("local").ListFilesAsync("/", "*.txt", true, null, CancellationToken.None);
  listResult.ShouldBeSuccess("Listing should succeed");
  var files = listResult.Value.Files;
  files.ShouldContain("data.txt");
  ```

- **IsDirectoryAsync(string path, CancellationToken token)**: Checks if `path` is a directory. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).
  ```csharp
  var isDirResult = await factory.CreateProvider("local").IsDirectoryAsync("/", CancellationToken.None);
  isDirResult.ShouldBeSuccess("Should identify root as directory");
  ```

- **CreateDirectoryAsync(string path, CancellationToken token)**: Creates a directory at `path`. Returns `Task<Result>` with success or errors (e.g., `PermissionError`).
  ```csharp
  var createDirResult = await factory.CreateProvider("local").CreateDirectoryAsync("new_dir", CancellationToken.None);
  createDirResult.ShouldBeSuccess("Directory creation should succeed");
  ```

- **DeleteDirectoryAsync(string path, bool recursive, CancellationToken token)**: Deletes a directory at `path`. Returns `Task<Result>` with success or errors (e.g., `PermissionError`).
  ```csharp
  var deleteDirResult = await factory.CreateProvider("local").DeleteDirectoryAsync("new_dir", true, CancellationToken.None);
  deleteDirResult.ShouldBeSuccess("Directory deletion should succeed");
  ```

- **ListDirectoriesAsync(string path, string searchPattern, bool recursive, CancellationToken token)**: Lists directories matching `searchPattern` under `path`. Returns `Task<Result<IEnumerable<string>>>` with directories or errors (e.g., `PermissionError`).
  ```csharp
  var dirsResult = await factory.CreateProvider("local").ListDirectoriesAsync("/", null, true, CancellationToken.None);
  dirsResult.ShouldBeSuccess("Directory listing should succeed");
  var directories = dirsResult.Value;
  directories.ShouldContain("new_dir");
  ```

- **CheckHealthAsync(CancellationToken token)**: Verifies storage provider health. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).
  ```csharp
  var healthResult = await factory.CreateProvider("local").CheckHealthAsync(CancellationToken.None);
  healthResult.ShouldBeSuccess("Health check should succeed");
  ```

For custom storage, implement `IFileStorageProvider` to adapt to cloud or network storage, ensuring compatibility.

### Using Extensions
`FileStorageProviderExtensions` enhance `IFileStorageProvider` with advanced functionality, returning `Result` or `Result<T>`. Use these for compression, encryption, and traversal, supporting progress reporting.

#### Compressing and Decompressing Files
Compress a file or directory into a ZIP archive, optionally with a password:

```csharp
var provider = factory.CreateProvider("local");
var content = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
var compressResult = await provider.WriteCompressedFileAsync("archive.zip", content, "", null, CancellationToken.None);
compressResult.ShouldBeSuccess("Compression should succeed");

var readResult = await provider.ReadCompressedFileAsync("archive.zip", "", null, CancellationToken.None);
readResult.ShouldBeSuccess("Decompression should succeed");
await using var decompressedStream = readResult.Value;
new StreamReader(decompressedStream).ReadToEnd().ShouldBe("Test content");
```

#### Encrypting and Decrypting Files
Encrypt sensitive files:

```csharp
var content = new MemoryStream(Encoding.UTF8.GetBytes("Sensitive data"));
const string encryptionKey = "testKey123456789012345678901234567890";
const string initializationVector = "testIV1234567890123";

var encryptResult = await provider.WriteEncryptedFileAsync("encrypted.bin", content, encryptionKey, initializationVector, null, CancellationToken.None);
encryptResult.ShouldBeSuccess("Encryption should succeed");

var decryptResult = await provider.ReadEncryptedFileAsync("encrypted.bin", encryptionKey, initializationVector, null, CancellationToken.None);
decryptResult.ShouldBeSuccess("Decryption should succeed");
await using var decryptedStream = decryptResult.Value;
new StreamReader(decryptedStream).ReadToEnd().ShouldBe("Sensitive data");
```

#### Traversing Files
Explore and process files:

```csharp
var progress = new Progress<FileProgress>(p => Console.WriteLine($"Progress: Bytes={p.BytesProcessed}, Files={p.FilesProcessed}/{p.TotalFiles}"));

var traverseResult = await provider.TraverseFilesAsync("/", null, progress, CancellationToken.None);
traverseResult.ShouldBeSuccess("File traversal should succeed");
var fileMetadatas = traverseResult.Value;

var processedFiles = new List<string>();
var processFile = async (string filePath, Stream stream, CancellationToken ct) =>
{
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync(ct);
    processedFiles.Add(filePath);
};
var actionResult = await provider.TraverseFilesAsync("/", processFile, progress, CancellationToken.None);
actionResult.ShouldBeSuccess("File traversal with action should succeed");
processedFiles.Count.ShouldBe(fileMetadatas.Count);
```

#### Handling Errors and Progress
Use `Result` for errors:

```csharp
var errorResult = await provider.WriteCompressedFileAsync("non_existent.zip", "non_existent_directory", "", null, CancellationToken.None);
errorResult.ShouldBeFailure("Should fail with non-existent directory");
errorResult.Messages.ShouldContain("Directory does not exist");
```

### Best Practices
- Configure via DI: Register providers with `AddFileStorage` for loose coupling.
- Leverage Extensions: Use `FileStorageProviderExtensions` for advanced operations.
- Handle Results: Check `Result.IsSuccess` and inspect `Messages` or `Errors`.
- Report Progress: Use `IProgress<FileProgress>` for feedback.
- Test Across Providers: Verify with `InMemoryFileStorageProvider`, `LocalFileStorageProvider`, `NetworkFileStorageProvider`, `AzureBlobFileStorageProvider`, and custom providers.