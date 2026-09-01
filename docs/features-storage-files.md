
# File Storage

> Read, write, move, and monitor files through extensible storage providers and behaviors.

[TOC]

## Overview

Storage systems expose different APIs and failure behavior. Applications also need secure file handling, progress reporting for long operations, predictable error handling, thread safety, and support for custom providers.

The `FileStorage` feature defines file operations through `IFileStorageProvider`, registers providers through `AddFileStorage`, and returns failures through `Result`. `IProgress<FileProgress>` reports progress, while `FileMetadata` carries file metadata. Applications can add providers and behaviors. Providers with `SupportsNotifications` can notify `FileMonitoring` about changes. Callers can push content with `WriteFileAsync` or stream bytes into a destination returned by `OpenWriteFileAsync`.

Available providers included:

- Local Files (e.g., `C:\data\file.txt` or `/var/data/file.txt`)
- Network Shares (e.g., Windows UNC paths)
- Azure Files
- Azure Blob Storage
- Entity Framework backed storage

## Challenges

File systems, shares, databases, and cloud stores expose different APIs and failure modes. Applications still need one way to stream content, manage directories and metadata, report progress, handle expected failures, and move files across providers.

## Solution

`IFileStorageProvider` defines Result-native file and directory operations. `AddFileStorage(...)` registers named providers and optional behaviors, while `IFileStorageProviderFactory` resolves the configured provider at runtime. Extensions add compression, serialization, and cross-provider transfers without expanding provider implementations.

## Key Features

- named local, in-memory, network, Azure, and Entity Framework providers
- stream-based read, write, and open-write operations
- file and directory metadata, checksums, paging, and health checks
- logging, retry, caching, metrics, and custom behaviors
- progress reporting and cross-provider copy or move helpers
- optional REST endpoints, monitoring, and scheduled scans

## Architecture

Application code resolves an `IFileStorageProvider` from the factory. Behaviors decorate the provider contract, and provider implementations translate paths and operations to their native store. File Monitoring composes providers with location handlers, event stores, and processors; the scheduled scan job calls the same monitoring service.

## Use Cases

- read and write files without binding application code to a storage SDK
- move files between local, database-backed, and cloud providers
- expose a controlled provider surface over HTTP
- monitor a location in real time or through on-demand scans
- persist a virtual file system in an application database

## Basic Usage

This example registers one singleton in-memory provider, writes UTF-8 content, checks both results, disposes the returned stream, and returns the stored text.

```csharp
using System.Text;
using BridgingIT.DevKit.Application.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFileStorage(factory => factory
    .RegisterProvider("memory", storage => storage
        .UseInMemory("MemoryFiles")
        .WithLifetime(ServiceLifetime.Singleton)));

var app = builder.Build();

app.MapPut("/files/{name}", async (
    string name,
    IFileStorageProviderFactory factory,
    CancellationToken cancellationToken) =>
{
    var storage = factory.CreateProvider("memory");
    await using var source = new MemoryStream(
        Encoding.UTF8.GetBytes($"File: {name}"),
        writable: false);

    var written = await storage.WriteFileAsync(
        name,
        source,
        cancellationToken: cancellationToken);

    if (written.IsFailure)
    {
        return Results.Problem(string.Join(
            "; ",
            written.Errors.Select(error => error.Message)));
    }

    var read = await storage.ReadFileAsync(
        name,
        cancellationToken: cancellationToken);

    if (read.IsFailure)
    {
        return Results.Problem(string.Join(
            "; ",
            read.Errors.Select(error => error.Message)));
    }

    await using var content = read.Value;
    using var reader = new StreamReader(
        content,
        Encoding.UTF8,
        leaveOpen: true);
    var text = await reader.ReadToEndAsync(cancellationToken);

    return Results.Ok(new { Path = name, Content = text });
});

app.Run();
```

`PUT /files/example.txt` returns `File: example.txt`. The in-memory provider retains files only while its singleton instance remains alive.

## Architecture details

The `FileStorage` subsystem is built around the `IFileStorageProvider` interface, which defines core file operations. Providers like `LocalFileStorageProvider`, `InMemoryFileStorageProvider`, `EntityFrameworkFileStorageProvider<TContext>`, and others implement this interface. The `IFileStorageProviderFactory` resolves providers by name, and extensions like `FileStorageProviderCompressionExtensions` and `FileStorageProviderCrossExtensions` add advanced functionality such as compression and cross-provider operations. Behaviors can be applied to providers to add cross-cutting concerns like logging or retry logic.

Below is a high-level architecture diagram:

```mermaid
classDiagram
    class IFileStorageProvider {
        +FileExistsAsync(path, progress, token) Task~Result~
        +ReadFileAsync(path, progress, token) Task~Result~Stream~~
        +WriteFileAsync(path, content, progress, token) Task~Result~
        +OpenWriteFileAsync(path, useTemporaryWrite, progress, token) Task~Result~Stream~~
        +DeleteFileAsync(path, progress, token) Task~Result~
        +GetChecksumAsync(path, token) Task~Result~string~~
        +GetFileMetadataAsync(path, token) Task~Result~FileMetadata~~
        +SetFileMetadataAsync(path, metadata, token) Task~Result~
        +ListFilesAsync(path, pattern, recursive, token) Task~Result~IEnumerable~string~~~~
        +CheckHealthAsync(token) Task~Result~
    }

    class IFileStorageProviderFactory {
        +CreateProvider(name) IFileStorageProvider
    }

    class BaseFileStorageProvider {
        +LocationName : string
        +Description : string
        +SupportsNotifications : bool
    }

    class LocalFileStorageProvider {
        +RootPath : string
    }

    class InMemoryFileStorageProvider {
        +Files : ConcurrentDictionary
    }

    class EntityFrameworkFileStorageProvider~TContext~ {
        +LocationName : string
        +SupportsNotifications : bool
    }

    class IFileStorageContext {
        +StorageFiles : DbSet
        +StorageFileContents : DbSet
        +StorageDirectories : DbSet
    }

    class FileStorageProviderCompressionExtensions {
        +WriteCompressedFileAsync(provider, path, content, progress, options, token) Task~Result~
        +ReadCompressedFile(provider, path, password, progress, options, token) Task~Result~Stream~~
    }

    class FileStorageProviderCrossExtensions {
        +CopyFileAsync(sourceProvider, sourcePath, destProvider, destPath, progress, token) Task~Result~
        +MoveFileAsync(sourceProvider, sourcePath, destProvider, destPath, progress, token) Task~Result~
    }

    IFileStorageProvider <|.. BaseFileStorageProvider
    BaseFileStorageProvider <|-- LocalFileStorageProvider
    BaseFileStorageProvider <|-- InMemoryFileStorageProvider
    BaseFileStorageProvider <|-- EntityFrameworkFileStorageProvider~TContext~
    EntityFrameworkFileStorageProvider~TContext~ --> IFileStorageContext : requires
    IFileStorageProvider --> FileStorageProviderCompressionExtensions : Extends
    IFileStorageProvider --> FileStorageProviderCrossExtensions : Extends
    IFileStorageProviderFactory --> IFileStorageProvider : Resolves
```

## Use case details

- **Basic File Operations**: Read, write, delete, and check file existence across different storage systems.
- **Metadata Management**: Retrieve and update file metadata for indexing or auditing purposes.
- **Bulk Operations**: Copy, move, or delete multiple files within or across providers with progress reporting.
- **Compression**: Compress files or directories into archives (e.g., ZIP) and decompress them, supporting password protection for decompression.
- **Cross-Provider Transfers**: Copy or move files between different storage providers (e.g., from local to cloud storage).
- **Database-backed Virtual Filesystems**: Persist files and directories in the application database through Entity Framework when a separate blob store or file share is unnecessary.
- **Health Monitoring**: Check the health of storage providers to ensure availability.

## Detailed usage

### Setting up a provider with dependency injection

Configure `FileStorage` using `Microsoft.Extensions.DependencyInjection` with the `AddFileStorage` method, which supports a fluent API for registering named providers, applying behaviors, and setting lifetimes. Providers are resolved via `IFileStorageProviderFactory`.

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
        builder.UseLocal("TestLocal", Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString()))
               .WithLogging()
               .WithLifetime(ServiceLifetime.Singleton);
    })
    .RegisterProvider("network", builder =>
    {
        builder.UseWindowsNetwork("NetworkStorage", @"\\server\docs", "username", "password", "domain")
               .WithLogging()
               .WithRetry(new RetryOptions { MaxRetries = 3 })
               .WithLifetime(ServiceLifetime.Singleton);
    })
    .RegisterProvider("azureBlob", builder =>
    {
        builder.UseAzureBlob("AzureBlobStorage", "connection-string", "container-name")
               .WithCaching(new CachingOptions { CacheDuration = TimeSpan.FromMinutes(10) })
               .WithLifetime(ServiceLifetime.Scoped);
    }));

// Use the factory to resolve providers
public class FileService
{
    private readonly IFileStorageProviderFactory factory;

    public FileService(IFileStorageProviderFactory factory)
    {
        this.factory = factory;
    }

    public async Task<Result> ProcessFileAsync(string path)
    {
        var provider = this.factory.CreateProvider("local");
        return await provider.WriteFileAsync(path, new MemoryStream(Encoding.UTF8.GetBytes("Test content")), null, CancellationToken.None);
    }
}
```

This registers "inMemory", "local", "network", and "azureBlob" providers with behaviors and lifetimes, resolved by `IFileStorageProviderFactory`.

`AddFileStorage(...)` also registers a single standard ASP.NET Core health check for all configured providers:

```text
FileStorage
```

The check resolves every provider name from `IFileStorageProviderFactory`, calls each provider's `CheckHealthAsync(...)`, and reports failures with the failed provider names and error details in the health-check data. The health check is tagged with `ready`, `storage`, and `files`.

### Setting up the Entity Framework provider

Use the Entity Framework provider when files should live in the same relational database as the rest of your application state.

1. Make your `DbContext` implement `IFileStorageContext`.
2. Add the three required `DbSet<>` properties.
3. Register the provider with `UseEntityFramework<TContext>(...)`.
4. Create and apply your own application migration for the storage tables.

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IFileStorageContext
{
    public DbSet<FileStorageFileEntity> StorageFiles { get; set; }

    public DbSet<FileStorageFileContentEntity> StorageFileContents { get; set; }

    public DbSet<FileStorageDirectoryEntity> StorageDirectories { get; set; }
}

services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddFileStorage(factory => factory
    .RegisterProvider("db", builder => builder
        .UseEntityFramework<AppDbContext>(
            "DatabaseFiles",
            "Entity Framework file storage",
            options => options
                .LeaseDuration(TimeSpan.FromSeconds(30))
                .RetryCount(3)
                .RetryBackoff(TimeSpan.FromMilliseconds(250))
                .PageSize(100)
                .MaximumBufferedContentSize(ByteSize.Megabytes(4)))
        .WithLogging()
        .WithLifetime(ServiceLifetime.Singleton)));
```

#### Entity Framework provider behavior

- Uses three tables: `__Storage_Files`, `__Storage_FileContents`, and `__Storage_Directories`
- Resolves a fresh scoped `DbContext` per operation, so singleton provider lifetime is safe
- Stores directory rows explicitly, including empty directories
- Uses `ListFilesAsync(..., continuationToken)` with an opaque seek-based continuation token
- Returns `SupportsNotifications = false`
- Buffers `WriteFileAsync` and `OpenWriteFileAsync` content in memory before the final database commit
- Preserves exact bytes for text payloads when possible and falls back to binary payload storage when a text-looking file cannot be round-tripped losslessly

#### Entity Framework provider options

| Option | Default | Meaning |
| --- | --- | --- |
| `LeaseDuration` | `00:00:30` | Lease window for row-level mutation coordination |
| `RetryCount` | `3` | Total replay-safe attempts for transient database contention |
| `RetryBackoff` | `00:00:00.250` | Base backoff used for retry delay calculation |
| `PageSize` | `100` | Default page size for `ListFilesAsync` |
| `MaximumBufferedContentSize` | `null` | Optional write-size limit before buffered writes are rejected |

#### Entity Framework provider notes

- The feature does **not** ship library-owned migrations. Your application owns schema evolution after implementing `IFileStorageContext`.
- The provider is Entity Framework-level and works across supported relational providers. This repository includes integration coverage for SQLite, SQL Server, and PostgreSQL.
- Because writes are buffered before commit, set `MaximumBufferedContentSize(...)` when you want predictable limits for large uploads.
- The provider supports cross-provider copy/move and the existing compression, traversal, text, and object extension methods through the normal `IFileStorageProvider` contract.

### Exposing providers through REST endpoints

Use `Presentation.Web.Storage` when you want registered providers to be reachable through HTTP. The endpoint package registers **one global storage endpoint surface** and resolves the target provider by route segment through `IFileStorageProviderFactory.CreateProvider(...)`.

```csharp
services.AddFileStorage(factory => factory
    .RegisterProvider("documents", builder => builder
        .UseEntityFramework<AppDbContext>(
            "DatabaseFiles",
            "Entity Framework file storage")
        .WithLifetime(ServiceLifetime.Singleton)))
    .AddEndpoints(options => options
        .RequireAuthorization()
        .GroupPath("/_bdk/api")
        .GroupTag("_bdk.Storage"));
```

#### REST endpoint behavior

- Works with **any** `IFileStorageProvider`, not only the Entity Framework provider
- Uses `/_bdk/api/storage/files/{providerName}` by default when `GroupPath` is not specified
- Publishes one endpoint set for all registered providers, with the provider selected from the sanitized route segment
- Supports provider info, health checks, file/directory existence checks, file listing, directory listing, metadata, checksum, create/delete directory, delete file, file download, raw file upload, and file-event query/scan routes when storage monitoring is configured for that provider
- Raw uploads write the request body directly to the provider-backed path; send them as `application/octet-stream`

#### Common routes

| Route | Purpose |
| --- | --- |
| `GET /_bdk/api/{provider}/provider` | Provider information |
| `GET /_bdk/api/{provider}/health` | Provider health |
| `GET /_bdk/api/{provider}/directories?path=...&recursive=true` | List directories |
| `POST /_bdk/api/{provider}/directories?path=...` | Create directory |
| `DELETE /_bdk/api/{provider}/directories?path=...&recursive=true` | Delete directory |
| `GET /_bdk/api/{provider}/files?path=...&recursive=true` | List files |
| `GET /_bdk/api/{provider}/files/content?path=...` | Download file content |
| `PUT /_bdk/api/{provider}/files/content?path=...` | Upload or overwrite file content |
| `GET /_bdk/api/{provider}/files/metadata?path=...` | Read file metadata |
| `GET /_bdk/api/{provider}/files/checksum?path=...` | Read checksum |
| `DELETE /_bdk/api/{provider}/files?path=...` | Delete file |
| `GET /_bdk/api/{provider}/events?path=...&eventType=...&fromDate=...&tillDate=...&take=...` | Query stored file events for the provider-backed monitoring location |
| `POST /_bdk/api/{provider}/events/scan?waitForProcessing=true&searchPattern=...&maxFilesToScan=...&skipChecksum=false` | Trigger an on-demand monitoring scan and return detected events |

#### Endpoint notes

- Use `RequireAuthorization`, `RequireRoles`, or `RequirePolicy` on `FileStorageEndpointsOptions` when the storage surface should not be public.
- Prefer `services.AddFileStorage(...).AddEndpoints(...)` when wiring providers and HTTP access together; `AddFileStorageEndpoints(...)` remains available when endpoint registration needs to happen separately.
- The endpoint layer resolves the named provider through `factory.CreateProvider(...)`, so HTTP callers always go through the same behaviors, lifetime, retries, and provider composition as in-process callers.
- File-event routes require `AddFileMonitoring(...)` plus `UseProvider(...)` for the same provider name. Without monitoring registration, the event routes return `503`.
- The endpoint group disables antiforgery so generated clients and operational dashboards can call the unsafe file and scan routes with bearer tokens.
- The provider info route intentionally lives at `/provider` so it does not collide with other fixed `_bdk` endpoints such as `/_bdk/api/info`.
- The DoFiesta example uses this endpoint package to back both the Operations > Files and Operations > File Events dashboards against the `"documents"` Entity Framework provider, and the WASM client consumes the generated Kiota client directly via `BackendApiClient.Api._bdk_["documents"]`.

### Using providers

The `IFileStorageProvider` interface defines core file operations, returning `Result` or `Result<T>` for error handling and messaging. Use the factory-resolved provider to perform operations.

#### Core methods

- `FileExistsAsync(string path, IProgress<FileProgress> progress, CancellationToken token)`: Checks if a file exists at `path`. Returns `Task<Result>` indicating success or failure with errors (e.g., `FileSystemError` for missing files).

  ```csharp
  var provider = factory.CreateProvider("local");
  var existsResult = await provider.FileExistsAsync("data.txt", null, CancellationToken.None);
  existsResult.ShouldBeSuccess("File should exist");
  ```

- `ReadFileAsync(string path, IProgress<FileProgress> progress, CancellationToken token)`: Reads a file as a `Stream`. Returns `Task<Result<Stream>>` with the stream or errors (e.g., `PermissionError`).

  ```csharp
  var readResult = await provider.ReadFileAsync("data.txt", null, CancellationToken.None);
  readResult.ShouldBeSuccess("Read should succeed");
  await using var stream = readResult.Value;
  new StreamReader(stream).ReadToEnd().ShouldBe("Test content");
  ```

- `WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress, CancellationToken token)`: Writes `content` to `path`. Use this when the caller already has a source stream. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).

  ```csharp
  var writeResult = await provider.WriteFileAsync("data.txt", new MemoryStream(Encoding.UTF8.GetBytes("Test content")), null, CancellationToken.None);
  writeResult.ShouldBeSuccess("Write should succeed");
  ```

- `OpenWriteFileAsync(string path, bool useTemporaryWrite, IProgress<FileProgress> progress, CancellationToken token)`: Opens a writable stream for `path`. Use this when the caller wants to write directly into the provider without first materializing a full source stream. Open failures are returned in the `Result`; write, flush, or dispose failures surface from the returned stream. `useTemporaryWrite: false` writes directly to the final path and may expose partial content, while `useTemporaryWrite: true` requests staged publish semantics when the provider supports it.

  ```csharp
  var openResult = await provider.OpenWriteFileAsync("feeds/data.csv", useTemporaryWrite: false, progress: null, CancellationToken.None);
  openResult.ShouldBeSuccess("Open write should succeed");

  await using var output = openResult.Value;
  await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: false);
  await writer.WriteLineAsync("id,name");
  await writer.WriteLineAsync("1,Sample");
  ```

- `DeleteFileAsync(string path, IProgress<FileProgress> progress, CancellationToken token)`: Deletes a file at `path`. Returns `Task<Result>` with success or errors (e.g., `PermissionError`).

  ```csharp
  var deleteResult = await provider.DeleteFileAsync("data.txt", null, CancellationToken.None);
  deleteResult.ShouldBeSuccess("Delete should succeed");
  ```

- **GetChecksumAsync(string path, CancellationToken token)**: Computes a checksum for a file. Returns `Task<Result<string>>` with the checksum or errors (e.g., `FileSystemError`).

  ```csharp
  var checksumResult = await provider.GetChecksumAsync("data.txt", CancellationToken.None);
  checksumResult.ShouldBeSuccess("Checksum should succeed");
  ```

- **GetFileMetadataAsync(string path, CancellationToken token)**: Retrieves metadata for a file. Returns `Task<Result<FileMetadata>>` with `FileMetadata` or errors (e.g., `FileSystemError`).

  ```csharp
  var metadataResult = await provider.GetFileMetadataAsync("data.txt", CancellationToken.None);
  metadataResult.ShouldBeSuccess("Metadata retrieval should succeed");
  var metadata = metadataResult.Value;
  metadata.Path.ShouldBe("data.txt");
  metadata.Length.ShouldBeGreaterThan(0);
  ```

- **SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken token)**: Sets metadata for a file. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).

  ```csharp
  var metadata = new FileMetadata { Path = "data.txt", Length = 100, LastModified = DateTime.UtcNow };
  var setResult = await provider.SetFileMetadataAsync("data.txt", metadata, CancellationToken.None);
  setResult.ShouldBeSuccess("Metadata set should succeed");
  ```

- **UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken token)**: Updates metadata for a file using a transformation function. Returns `Task<Result<FileMetadata>>` with updated metadata or errors.

  ```csharp
  var updateResult = await provider.UpdateFileMetadataAsync("data.txt", m => { m.Length = 200; return m; }, CancellationToken.None);
  updateResult.ShouldBeSuccess("Metadata update should succeed");
  updateResult.Value.Length.ShouldBe(200);
  ```

- **ListFilesAsync(string path, string searchPattern, bool recursive, string continuationToken, CancellationToken token)**: Lists files matching `searchPattern` under `path`. Returns `Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>>` with files and pagination token or errors (e.g., `PermissionError`).

  ```csharp
  var listResult = await provider.ListFilesAsync("/", "*.txt", true, null, CancellationToken.None);
  listResult.ShouldBeSuccess("Listing should succeed");
  var files = listResult.Value.Files;
  files.ShouldContain("data.txt");
  ```

- `CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress, CancellationToken token)`: Copies a file from `sourcePath` to `destinationPath` within the same provider. Returns `Task<Result>` with success or errors.

  ```csharp
  var copyResult = await provider.CopyFileAsync("data.txt", "data_copy.txt", null, CancellationToken.None);
  copyResult.ShouldBeSuccess("Copy should succeed");
  ```

- `RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress, CancellationToken token)`: Renames a file from `path` to `destinationPath` within the same provider. Returns `Task<Result>` with success or errors.

  ```csharp
  var renameResult = await provider.RenameFileAsync("data.txt", "renamed.txt", null, CancellationToken.None);
  renameResult.ShouldBeSuccess("Rename should succeed");
  ```

- `MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress, CancellationToken token)`: Moves a file from `path` to `destinationPath` within the same provider. Returns `Task<Result>` with success or errors.

  ```csharp
  var moveResult = await provider.MoveFileAsync("data.txt", "moved.txt", null, CancellationToken.None);
  moveResult.ShouldBeSuccess("Move should succeed");
  ```

- `CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress, CancellationToken token)`: Copies multiple files within the same provider. Returns `Task<Result>` with success or partial failure (e.g., `PartialOperationError`).

  ```csharp
  var filePairs = new[] { ("data1.txt", "copy1.txt"), ("data2.txt", "copy2.txt") };
  var copyFilesResult = await provider.CopyFilesAsync(filePairs, null, CancellationToken.None);
  copyFilesResult.ShouldBeSuccess("Bulk copy should succeed");
  ```

- `DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress, CancellationToken token)`: Deletes multiple files. Returns `Task<Result>` with success or partial failure.

  ```csharp
  var paths = new[] { "data1.txt", "data2.txt" };
  var deleteFilesResult = await provider.DeleteFilesAsync(paths, null, CancellationToken.None);
  deleteFilesResult.ShouldBeSuccess("Bulk delete should succeed");
  ```

- `MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress, CancellationToken token)`: Moves multiple files within the same provider. Returns `Task<Result>` with success or partial failure.

  ```csharp
  var moveFilesResult = await provider.MoveFilesAsync(filePairs, null, CancellationToken.None);
  moveFilesResult.ShouldBeSuccess("Bulk move should succeed");
  ```

- **DirectoryExistsAsync(string path, CancellationToken token)**: Checks if `path` is a directory. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).

  ```csharp
  var dirExistsResult = await provider.DirectoryExistsAsync("new_dir", CancellationToken.None);
  dirExistsResult.ShouldBeSuccess("Directory should exist");
  ```

- **CreateDirectoryAsync(string path, CancellationToken token)**: Creates a directory at `path`. Returns `Task<Result>` with success or errors (e.g., `PermissionError`).

  ```csharp
  var createDirResult = await provider.CreateDirectoryAsync("new_dir", CancellationToken.None);
  createDirResult.ShouldBeSuccess("Directory creation should succeed");
  ```

- **DeleteDirectoryAsync(string path, bool recursive, CancellationToken token)**: Deletes a directory at `path`. Returns `Task<Result>` with success or errors (e.g., `PermissionError`).

  ```csharp
  var deleteDirResult = await provider.DeleteDirectoryAsync("new_dir", true, CancellationToken.None);
  deleteDirResult.ShouldBeSuccess("Directory deletion should succeed");
  ```

- **ListDirectoriesAsync(string path, string searchPattern, bool recursive, CancellationToken token)**: Lists directories matching `searchPattern` under `path`. Returns `Task<Result<IEnumerable<string>>>` with directories or errors (e.g., `PermissionError`).

  ```csharp
  var dirsResult = await provider.ListDirectoriesAsync("/", null, true, CancellationToken.None);
  dirsResult.ShouldBeSuccess("Directory listing should succeed");
  var directories = dirsResult.Value;
  directories.ShouldContain("new_dir");
  ```

- **CheckHealthAsync(CancellationToken token)**: Verifies storage provider health. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`).

  ```csharp
  var healthResult = await provider.CheckHealthAsync(CancellationToken.None);
  healthResult.ShouldBeSuccess("Health check should succeed");
  ```

#### Implementing a custom provider

For custom storage systems (e.g., a proprietary cloud storage API), implement the `IFileStorageProvider` interface by inheriting from `BaseFileStorageProvider` and overriding the necessary methods. Below is a minimal example:

```csharp
public class CustomFileStorageProvider : BaseFileStorageProvider
{
    public CustomFileStorageProvider(string locationName) : base(locationName) { }

    public override async Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken token = default)
    {
        try
        {
            // Custom logic to check file existence (e.g., API call)
            bool exists = await Task.FromResult(true); // Simulate existence check
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to check existence of file at '{path}'");
        }
    }

    public override async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken token = default)
    {
        try
        {
            // Custom logic to read file (e.g., API call)
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Custom content"));
            return Result<Stream>.Success(stream);
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to read file at '{path}'");
        }
    }

    // Implement other required methods...
}
```

Register the custom provider using `AddFileStorage`:

```csharp
services.AddFileStorage(c => c
    .RegisterProvider("custom", builder =>
    {
        builder.UseCustom<CustomFileStorageProvider>("CustomStorage")
               .WithLifetime(ServiceLifetime.Singleton);
    }));
```

### Using extensions

The `FileStorage` subsystem extends `IFileStorageProvider` with compression, cross-provider operations, and progress reporting. The extension methods return `Result` or `Result<T>` for expected failures.

#### Compressing and decompressing files

Compress a file or directory into an archive (e.g., ZIP, GZip, Tar), optionally configuring compression options:

```csharp
var provider = factory.CreateProvider("local");
var content = new MemoryStream(Encoding.UTF8.GetBytes("Test content"));
var options = new FileCompressionOptions { ArchiveType = FileCompressionArchiveType.Zip };
var compressResult = await provider.WriteCompressedFileAsync("archive.zip", content, null, options, CancellationToken.None);
compressResult.ShouldBeSuccess("Compression should succeed");

var readResult = await provider.ReadCompressedFile("archive.zip", null, null, options, CancellationToken.None);
readResult.ShouldBeSuccess("Decompression should succeed");
await using var decompressedStream = readResult.Value;
new StreamReader(decompressedStream).ReadToEnd().ShouldBe("Test content");
```

Compress a directory:

```csharp
var compressDirResult = await provider.CompressAsync("archive.zip", "input_dir", null, options, CancellationToken.None);
compressDirResult.ShouldBeSuccess("Directory compression should succeed");

var uncompressResult = await provider.UncompressAsync("archive.zip", "output_dir", null, null, options, CancellationToken.None);
uncompressResult.ShouldBeSuccess("Directory decompression should succeed");
```

#### Cross-provider operations

The `FileStorageProviderCrossExtensions` class provides methods to perform operations across different `IFileStorageProvider` instances, such as copying or moving files between providers (e.g., from a local file system to an in-memory provider). These methods support progress reporting and handle errors using the `Result` pattern.

- `CopyFileAsync(IFileStorageProvider sourceProvider, string sourcePath, IFileStorageProvider destinationProvider, string destinationPath, IProgress<FileProgress> progress, CancellationToken token)`: Copies a file from the source provider to the destination provider. Returns `Task<Result>` with success or errors (e.g., `FileSystemError`, `PermissionError`).

  ```csharp
  var sourceProvider = factory.CreateProvider("local");
  var destProvider = factory.CreateProvider("inMemory");

  // Write a file to the source provider
  await sourceProvider.WriteFileAsync("source.txt", new MemoryStream(Encoding.UTF8.GetBytes("Cross-provider content")), null, CancellationToken.None);

  var copyResult = await sourceProvider.CopyFileAsync("source.txt", destProvider, "dest.txt", null, CancellationToken.None);
  copyResult.ShouldBeSuccess("Cross-provider copy should succeed");

  // Verify the file exists in the destination provider
  var existsResult = await destProvider.FileExistsAsync("dest.txt", null, CancellationToken.None);
  existsResult.ShouldBeSuccess("File should exist in destination provider");
  ```

- `CopyFilesAsync(IFileStorageProvider sourceProvider, IFileStorageProvider destinationProvider, IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress, CancellationToken token)`: Copies multiple files between providers in a batch. Returns `Task<Result>` with success or partial failure (e.g., `PartialOperationError`).

  ```csharp
  var filePairs = new[]
  {
      ("source1.txt", "dest1.txt"),
      ("source2.txt", "dest2.txt")
  };

  // Write files to the source provider
  foreach (var (sourcePath, _) in filePairs)
  {
      await sourceProvider.WriteFileAsync(sourcePath, new MemoryStream(Encoding.UTF8.GetBytes($"Content for {sourcePath}")), null, CancellationToken.None);
  }

  var progress = new Progress<FileProgress>(p => Console.WriteLine($"Copied {p.FilesProcessed}/{p.TotalFiles} files"));
  var copyFilesResult = await sourceProvider.CopyFilesAsync(destProvider, filePairs, progress, CancellationToken.None);
  copyFilesResult.ShouldBeSuccess("Cross-provider bulk copy should succeed");

  // Verify files in the destination provider
  foreach (var (_, destPath) in filePairs)
  {
      var exists = await destProvider.FileExistsAsync(destPath, null, CancellationToken.None);
      exists.ShouldBeSuccess($"File {destPath} should exist in destination provider");
  }
  ```

- `MoveFileAsync(IFileStorageProvider sourceProvider, string sourcePath, IFileStorageProvider destinationProvider, string destinationPath, IProgress<FileProgress> progress, CancellationToken token)`: Moves a file by copying it to the destination provider and deleting it from the source provider. Returns `Task<Result>` with success or errors.

  ```csharp
  await sourceProvider.WriteFileAsync("move.txt", new MemoryStream(Encoding.UTF8.GetBytes("Move content")), null, CancellationToken.None);

  var moveResult = await sourceProvider.MoveFileAsync("move.txt", destProvider, "moved.txt", null, CancellationToken.None);
  moveResult.ShouldBeSuccess("Cross-provider move should succeed");

  // Verify the file exists in the destination and not in the source
  var destExists = await destProvider.FileExistsAsync("moved.txt", null, CancellationToken.None);
  destExists.ShouldBeSuccess("File should exist in destination provider");
  var sourceExists = await sourceProvider.FileExistsAsync("move.txt", null, CancellationToken.None);
  sourceExists.ShouldBeFailure("File should not exist in source provider");
  ```

- `MoveFilesAsync(IFileStorageProvider sourceProvider, IFileStorageProvider destinationProvider, IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress, CancellationToken token)`: Moves multiple files between providers in a batch. Returns `Task<Result>` with success or partial failure.

  ```csharp
  var moveFilesResult = await sourceProvider.MoveFilesAsync(destProvider, filePairs, progress, CancellationToken.None);
  moveFilesResult.ShouldBeSuccess("Cross-provider bulk move should succeed");

  // Verify files in the destination provider and not in the source
  foreach (var (sourcePath, destPath) in filePairs)
  {
      var destExists = await destProvider.FileExistsAsync(destPath, null, CancellationToken.None);
      destExists.ShouldBeSuccess($"File {destPath} should exist in destination provider");
      var sourceExists = await sourceProvider.FileExistsAsync(sourcePath, null, CancellationToken.None);
      sourceExists.ShouldBeFailure($"File {sourcePath} should not exist in source provider");
  }
  ```

#### Handling errors and progress

Use `Result` for error handling and `IProgress<FileProgress>` for progress reporting in both compression and cross-provider operations:

```csharp
// Compression error handling
var progress = new Progress<FileProgress>(p => Console.WriteLine($"Processed {p.FilesProcessed}/{p.TotalFiles} files"));
var errorResult = await provider.CompressAsync("archive.zip", "non_existent_dir", progress, null, CancellationToken.None);
errorResult.ShouldBeFailure("Should fail with non-existent directory");
errorResult.Messages.ShouldContain(m => m.Contains("Directory path (content) cannot be null or empty"));

// Cross-provider error handling
var invalidFilePairs = new[] { ("non_existent.txt", "dest.txt") };
var copyErrorResult = await sourceProvider.CopyFilesAsync(destProvider, invalidFilePairs, progress, CancellationToken.None);
copyErrorResult.ShouldBeFailure("Should fail with non-existent source file");
copyErrorResult.Messages.ShouldContain(m => m.Contains("Copied 0/1 files, 1 failed"));
```

### Best practices

- **Configure via DI**: Register providers with `AddFileStorage` for loose coupling and easy provider switching.
- **Own EF Migrations in the App**: When using `UseEntityFramework<TContext>`, add the storage tables through your consuming application's normal migration workflow.
- **Leverage Extensions**: Use `FileStorageProviderCompressionExtensions` for compression and `FileStorageProviderCrossExtensions` for cross-provider operations like copying or moving files between storage systems.
- **Handle Results**: Always check `Result.IsSuccess` and inspect `Messages` or `Errors` for detailed feedback.
- **Report Progress**: Use `IProgress<FileProgress>` to provide feedback during long-running operations, such as bulk cross-provider copies or compression tasks.
- **Tune Buffered Writes**: For the Entity Framework provider, configure `MaximumBufferedContentSize(...)` if uploads may become large enough to pressure memory.
- **Do Not Expect Notifications from EF Storage**: The Entity Framework provider is fully usable for file operations, but it reports `SupportsNotifications = false`.
- **Test Across Providers**: Verify functionality with `InMemoryFileStorageProvider`, `LocalFileStorageProvider`, and custom providers to ensure compatibility, especially for cross-provider operations.

## Appendix A: FileMonitoring

### Overview

The `FileMonitoring` feature builds on `FileStorage` to provide real-time and on-demand monitoring of file changes in specified locations. It uses `IFileStorageProvider` to access files and detect changes, generating `FileEvent` instances (e.g., Added, Changed, Deleted) that are processed by a chain of `IFileEventProcessor` implementations. The `IFileMonitoringService` orchestrates monitoring across multiple locations, each managed by an `ILocationHandler`. Event processing rates can be controlled using a `RateLimiter`, configured via `LocationOptions.RateLimit`.

#### `FileEvent` structure

The `FileEvent` class represents a detected file change, with the following key properties:

- **EventType**: The type of change (e.g., `Added`, `Changed`, `Deleted`, `Unchanged`).
- **FilePath**: The relative path of the file.
- **Checksum**: The file's checksum for change detection.
- **DetectedDate**: The timestamp of the event detection.
- **LocationName**: The name of the monitored location.
- **FileSize**: The size of the file in bytes.
- **LastModifiedDate**: The last modification date of the file.

### Architecture

The `FileMonitoring` subsystem integrates with `FileStorage` providers to monitor file changes. The `IFileMonitoringService` manages multiple `ILocationHandler` instances, each responsible for a specific location. Handlers like `LocalLocationHandler` use `FileSystemWatcher` for real-time monitoring, while `InMemoryLocationHandler` supports in-memory providers. Events are stored in an `IFileEventStore` and processed by a chain of `IFileEventProcessor` instances.

```mermaid
classDiagram
    class IFileMonitoringService {
        +StartAsync(token) Task
        +StopAsync(token) Task
        +ScanLocationAsync(locationName, options, progress, token) Task~FileScanContext~
    }

    class ILocationHandler {
        +Provider : IFileStorageProvider
        +Options : LocationOptions
        +ScanAsync(options, progress, token) Task~FileScanContext~
        +StartAsync(token) Task
        +StopAsync(token) Task
    }

    class LocalLocationHandler {
        +FileSystemWatcher : FileSystemWatcher
    }

    class InMemoryLocationHandler {
        +InMemoryProvider : InMemoryFileStorageProvider
    }

    class IFileEventStore {
        +StoreEventAsync(fileEvent, token) Task
        +GetFileEventsAsync(filePath, fromDate, tillDate, token) Task~IEnumerable~FileEvent~~~~
    }

    class IFileEventProcessor {
        +ProcessAsync(context, token) Task
    }

    IFileMonitoringService --> ILocationHandler : Manages
    ILocationHandler <|.. LocalLocationHandler
    ILocationHandler <|.. InMemoryLocationHandler
    ILocationHandler --> IFileStorageProvider : Uses
    ILocationHandler --> IFileEventStore : Stores Events
    ILocationHandler --> IFileEventProcessor : Processes Events
```

### Usage

#### Setting up FileMonitoring

Configure `FileMonitoring` using `AddFileMonitoring` with a fluent API to specify locations, providers, and processors:

```csharp
services.AddFileMonitoring(monitoring =>
{
    monitoring
        .UseLocal("Docs", Path.Combine(Path.GetTempPath(), "Docs"), options =>
        {
            options.FileFilter = "*.txt";
            options.FileBlackListFilter = ["*.tmp"];
            options.RateLimit = RateLimitOptions.HighSpeed; // Configure event processing rate
            options.UseProcessor<FileLoggerProcessor>();
            options.UseProcessor<FileMoverProcessor>(config =>
                config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = Path.Combine(Path.GetTempPath(), "MovedDocs")));
        });
});

var monitoringService = serviceProvider.GetRequiredService<IFileMonitoringService>();
await monitoringService.StartAsync(CancellationToken.None);
```

#### On-demand scanning

Perform an on-demand scan to detect changes:

```csharp
var progress = new Progress<FileScanProgress>(report =>
    Console.WriteLine($"Scanned {report.FilesScanned}/{report.TotalFiles} files ({report.PercentageComplete:F2}%)"));
var scanOptions = FileScanOptionsBuilder.Create()
    .WithEventFilter(FileEventType.Added)
    .WithFileFilter(".txt")
    .WithFileBlackListFilter(["*.tmp"])
    .WithProgressIntervalPercentage(5)
    .Build();
var scanContext = await monitoringService.ScanLocationAsync("Docs", scanOptions, progress, CancellationToken.None);
Console.WriteLine($"Detected {scanContext.Events.Count} events");
```

#### Real-time monitoring

Real-time monitoring is enabled by default (unless `UseOnDemandOnly` is set). The `LocalLocationHandler` uses `FileSystemWatcher` to detect changes:

```csharp
// File changes are automatically detected and processed
File.WriteAllText(Path.Combine(Path.GetTempPath(), "Docs", "test.txt"), "Test content");
await Task.Delay(500); // Allow processing
var store = serviceProvider.GetRequiredService<IFileEventStore>();
var events = await store.GetFileEventsAsync("test.txt");
events.ShouldNotBeEmpty();
```

#### Pausing and resuming monitoring

Control real-time monitoring by pausing and resuming the `ILocationHandler`:

```csharp
await monitoringService.PauseLocationAsync("Docs");
File.WriteAllText(Path.Combine(Path.GetTempPath(), "Docs", "test.txt"), "Test content");
await Task.Delay(500); // No events during pause
var eventsDuringPause = await store.GetFileEventsAsync("test.txt");
eventsDuringPause.ShouldBeEmpty();

await monitoringService.ResumeLocationAsync("Docs");
File.WriteAllText(Path.Combine(Path.GetTempPath(), "Docs", "test.txt"), "Updated content");
await Task.Delay(500); // Event detected after resume
var eventsAfterResume = await store.GetFileEventsAsync("test.txt");
eventsAfterResume.ShouldNotBeEmpty();
eventsAfterResume.First().EventType.ShouldBe(FileEventType.Changed);
```

### Best practices

- **Use Appropriate Providers**: Ensure the `IFileStorageProvider` supports notifications for real-time monitoring (e.g., `LocalFileStorageProvider` with `SupportsNotifications = true`).
- **Configure Processors**: Chain multiple `IFileEventProcessor` instances to handle events (e.g., logging, moving files).
- **Control Event Processing**: Use `LocationOptions.RateLimit` to manage the rate of event processing, preventing overload in high-frequency scenarios.
- **Handle Progress**: Use `IProgress<FileScanProgress>` to monitor scan progress.
- **Test with InMemory**: Use `InMemoryFileStorageProvider` and `InMemoryLocationHandler` for unit testing.

## Appendix B: FileMonitoringLocationScanJob

### Overview

The `FileMonitoringLocationScanJob` is a scheduled job that triggers on-demand scans for a specified location using the `IFileMonitoringService`. It integrates with the `Application.Jobs` feature to run scans at defined intervals, supporting retry logic and configurable scan options. Configure job-level concurrency when overlapping scans must be prevented.

### Usage

#### Registering the job

Register the `FileMonitoringLocationScanJob` using `AddJobScheduler`, specifying the location name and scan options via the typed `FileMonitoringLocationScanJobData` payload:

```csharp
services.AddJobScheduler()
    .WithJob<FileMonitoringLocationScanJob>("scan_inbound", job => job
        .Description("Scans the inbound location.")
        .WithConcurrency(1)
        .WithRetry(retry => retry.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
        .AddTrigger("schedule", trigger => trigger
            .Cron(CronExpressions.EveryMinute)
            .Data(new FileMonitoringLocationScanJobData
            {
                LocationName = "inbound",
                DelayPerFile = TimeSpan.FromSeconds(1),
                WaitForProcessing = true,
                BatchSize = 10,
                ProgressIntervalPercentage = 5,
                FileFilter = ".txt",
                FileBlackListFilter = [".tmp", "*.log"],
                MaxFilesToScan = 100,
                Timeout = TimeSpan.FromMinutes(1)
            })));
```

- **Cron Schedule**: `CronExpressions.EveryMinute` runs the job every minute.
- **Job Data**: `FileMonitoringLocationScanJobData` defines the location name and scan options (e.g., `DelayPerFile`, `BatchSize`).
- **Retry Logic**: Configure retry through the Jobs registration with `WithRetry(...)`.

#### How it works

1. The job retrieves the location name from `FileMonitoringLocationScanJobData.LocationName`.
2. It constructs a `FileScanOptions` object based on the typed data, setting properties like `DelayPerFile`, `BatchSize`, and `Timeout`.
3. It calls `IFileMonitoringService.ScanLocationAsync` to perform the scan, logging progress and events.
4. Events are logged using structured logging (`TypedLogger`), capturing details like files scanned, events detected, and elapsed time.

#### Example log output

```bash
STR job: scan started (location=inbound)
STR job: progress (location=inbound, filesScanned=10, totalFiles=100, percentageComplete=10.00) -> took 1000.0000 ms
STR job: scan completed (location=inbound, eventCount=5)
STR job: event processed (location=inbound, eventType=Added, filePath=file1.txt, size=1024, detected=2025-03-28T12:00:00Z)
```

#### Retry handling

Configure retries on the Jobs registration. For example, `WithRetry(retry => retry.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))` retries transient scan failures up to 3 times:

```bash
STR job: scan started (location=inbound)
STR job: scan failed (location=inbound, attempt=1)
STR job: scan started (location=inbound)
STR job: scan completed (location=inbound, eventCount=5)
```

### Best practices

- **Set Appropriate Options**: Configure `DelayPerFile` and `BatchSize` to manage load during scans.
- **Monitor Logs**: Use the structured logs to track scan progress and events.
- **Handle Retries**: Leverage the built-in retry mechanism for transient failures, monitoring retry logs for issues.
- **Test Scheduling**: Verify the cron schedule and job data in a test environment to ensure expected behavior.
