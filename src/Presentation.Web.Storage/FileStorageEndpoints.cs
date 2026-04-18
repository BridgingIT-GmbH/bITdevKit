// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Exposes provider-backed REST endpoints for registered file storage providers.
/// </summary>
/// <example>
/// <code>
/// services.AddFileStorage(factory => factory
///     .RegisterProvider("documents", builder => builder
///         .UseLocal("Documents", rootPath)
///         .WithLifetime(ServiceLifetime.Singleton)))
///     .AddEndpoints(options => options
///         .GroupPath("/api/_system")
///         .RequireAuthorization());
/// </code>
/// </example>
public class FileStorageEndpoints(
    ILoggerFactory loggerFactory,
    IFileStorageProviderFactory factory,
    FileStorageEndpointsOptions options = null) : EndpointsBase
{
    private const string RouteNamePrefix = "_System.Storage";
    private readonly ILogger<FileStorageEndpoints> logger = loggerFactory?.CreateLogger<FileStorageEndpoints>() ?? NullLogger<FileStorageEndpoints>.Instance;
    private readonly IFileStorageProviderFactory factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly FileStorageEndpointsOptions options = options ?? new FileStorageEndpointsOptions();

    /// <summary>
    /// Maps the file storage REST endpoints into the current endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options)
            .DisableAntiforgery();

        group.MapGet("locations", (CancellationToken cancellationToken) =>
                this.ListProvidersAsync(cancellationToken))
            .Produces<List<FileStorageProviderInfoModel>>()
            .WithName($"{RouteNamePrefix}.ListProviders")
            .WithSummary("List registered storage locations")
            .WithDescription("Retrieves the registered file storage providers and their exposed location metadata.");

        group.MapGet("{providerName}/provider", (string providerName, CancellationToken cancellationToken) =>
                this.GetProviderInfoAsync(providerName, cancellationToken))
            .Produces<FileStorageProviderInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .WithName($"{RouteNamePrefix}.GetProviderInfo")
            .WithSummary("Get provider information")
            .WithDescription("Retrieves information about the configured file storage provider.");

        group.MapGet("{providerName}/health", (string providerName, CancellationToken cancellationToken) =>
                this.CheckHealthAsync(providerName, cancellationToken))
            .Produces<FileStorageHealthResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.CheckHealth")
            .WithSummary("Check provider health")
            .WithDescription("Runs the configured provider health check.");

        group.MapGet("{providerName}/files/exists", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.FileExistsAsync(providerName, path, cancellationToken))
            .Produces<FileStorageExistsResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.FileExists")
            .WithSummary("Check whether a file exists")
            .WithDescription("Checks whether the specified file path exists in the configured provider.");

        group.MapGet("{providerName}/files/content", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.ReadFileAsync(providerName, path, cancellationToken))
            .Produces((int)HttpStatusCode.OK)
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ReadFile")
            .WithSummary("Read file content")
            .WithDescription("Streams the file content for the specified provider-backed path.");

        group.MapPut("{providerName}/files/content", (string providerName, [FromQuery] string path, HttpRequest request, CancellationToken cancellationToken) =>
                this.WriteFileAsync(providerName, path, request, cancellationToken))
            .Accepts<IFormFile>("application/octet-stream")
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.WriteFile")
            .WithSummary("Write file content")
            .WithDescription("Writes the raw request body to the specified provider-backed file path.");

        group.MapDelete("{providerName}/files", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.DeleteFileAsync(providerName, path, cancellationToken))
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.DeleteFile")
            .WithSummary("Delete a file")
            .WithDescription("Deletes the specified provider-backed file path.");

        group.MapGet("{providerName}/files/metadata", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.GetFileMetadataAsync(providerName, path, cancellationToken))
            .Produces<FileMetadata>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.GetFileMetadata")
            .WithSummary("Get file metadata")
            .WithDescription("Retrieves metadata for the specified provider-backed file path.");

        group.MapGet("{providerName}/files/checksum", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.GetChecksumAsync(providerName, path, cancellationToken))
            .Produces<FileStorageChecksumResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.GetChecksum")
            .WithSummary("Get file checksum")
            .WithDescription("Computes a checksum for the specified provider-backed file path.");

        group.MapGet("{providerName}/files", (string providerName, [FromQuery] string path, [FromQuery] string searchPattern, [FromQuery] bool recursive, [FromQuery] string continuationToken, CancellationToken cancellationToken) =>
                this.ListFilesAsync(providerName, path, searchPattern, recursive, continuationToken, cancellationToken))
            .Produces<FileStorageFilesResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ListFiles")
            .WithSummary("List files")
            .WithDescription("Lists files from the configured provider with optional recursion and continuation support.");

        group.MapGet("{providerName}/directories/exists", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.DirectoryExistsAsync(providerName, path, cancellationToken))
            .Produces<FileStorageExistsResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.DirectoryExists")
            .WithSummary("Check whether a directory exists")
            .WithDescription("Checks whether the specified provider-backed directory path exists.");

        group.MapGet("{providerName}/directories", (string providerName, [FromQuery] string path, [FromQuery] string searchPattern, [FromQuery] bool recursive, CancellationToken cancellationToken) =>
                this.ListDirectoriesAsync(providerName, path, searchPattern, recursive, cancellationToken))
            .Produces<IEnumerable<string>>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ListDirectories")
            .WithSummary("List directories")
            .WithDescription("Lists directories from the configured provider with optional recursion.");

        group.MapPost("{providerName}/directories", (string providerName, [FromQuery] string path, CancellationToken cancellationToken) =>
                this.CreateDirectoryAsync(providerName, path, cancellationToken))
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.CreateDirectory")
            .WithSummary("Create a directory")
            .WithDescription("Creates the specified provider-backed directory path.");

        group.MapDelete("{providerName}/directories", (string providerName, [FromQuery] string path, [FromQuery] bool recursive, CancellationToken cancellationToken) =>
                this.DeleteDirectoryAsync(providerName, path, recursive, cancellationToken))
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.DeleteDirectory")
            .WithSummary("Delete a directory")
            .WithDescription("Deletes the specified provider-backed directory path.");

        group.MapGet("{providerName}/events", (HttpContext httpContext, string providerName, [FromQuery] string path, [FromQuery] string eventType, [FromQuery] DateTimeOffset? fromDate, [FromQuery] DateTimeOffset? tillDate, [FromQuery] int? take, CancellationToken cancellationToken) =>
                this.ListFileEventsAsync(httpContext.RequestServices, providerName, path, eventType, fromDate, tillDate, take, cancellationToken))
            .Produces<FileStorageFileEventsResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ListFileEvents")
            .WithSummary("List file events")
            .WithDescription("Lists stored monitoring events for the provider-backed location, optionally filtered by path, event type, date range, and result size.");

        group.MapPost("{providerName}/events/scan", (HttpContext httpContext, string providerName, [FromQuery] bool waitForProcessing = true, [FromQuery] string searchPattern = null, [FromQuery] int? maxFilesToScan = null, [FromQuery] bool skipChecksum = false, CancellationToken cancellationToken = default) =>
                this.ScanFileEventsAsync(httpContext.RequestServices, providerName, waitForProcessing, searchPattern, maxFilesToScan, skipChecksum, cancellationToken))
            .Produces<FileStorageFileEventScanResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.ServiceUnavailable)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ScanFileEvents")
            .WithSummary("Scan a monitored location")
            .WithDescription("Triggers an on-demand file monitoring scan for the provider-backed location and returns the detected events.");

        this.IsRegistered = true;
    }

    private Task<HttpResult> GetProviderInfoAsync(string providerName, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return Task.FromResult(failure);
        }

        return Task.FromResult<HttpResult>(Results.Ok(new FileStorageProviderInfoModel
        {
            ProviderName = providerName,
            LocationName = provider.LocationName,
            Description = provider.Description,
            SupportsNotifications = provider.SupportsNotifications
        }));
    }

    private Task<List<FileStorageProviderInfoModel>> ListProvidersAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var providers = this.factory.GetProviderNames()
            .Select(providerName =>
            {
                var provider = this.factory.CreateProvider(providerName);
                return new FileStorageProviderInfoModel
                {
                    ProviderName = providerName,
                    LocationName = provider.LocationName,
                    Description = provider.Description,
                    SupportsNotifications = provider.SupportsNotifications
                };
            })
            .OrderBy(model => model.LocationName ?? model.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(providers);
    }

    private async Task<HttpResult> CheckHealthAsync(string providerName, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        this.logger.LogInformation("Checking health for file storage provider {ProviderName}", providerName);
        var result = await provider.CheckHealthAsync(cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new FileStorageHealthResponseModel
            {
                ProviderName = providerName,
                IsHealthy = true,
                Message = result.Messages?.LastOrDefault() ?? "Healthy"
            })
            : MapFailure(result, providerName);
    }

    private async Task<HttpResult> FileExistsAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.FileExistsAsync(path, cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(new FileStorageExistsResponseModel { Path = path, Exists = true });
        }

        return IsNotFound(result)
            ? Results.Ok(new FileStorageExistsResponseModel { Path = path, Exists = false })
            : MapFailure(result, path);
    }

    private async Task<HttpResult> ReadFileAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.ReadFileAsync(path, cancellationToken: cancellationToken);
        if (result.IsFailure)
        {
            return MapFailure(result, path);
        }

        var contentType = ContentTypeExtensions.FromFileName(path, ContentType.DEFAULT).MimeType();
        return Results.Stream(result.Value, contentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
    }

    private async Task<HttpResult> WriteFileAsync(string providerName, string path, HttpRequest request, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        await using var bufferedContent = new MemoryStream();
        await request.Body.CopyToAsync(bufferedContent, cancellationToken);
        bufferedContent.Position = 0;

        var result = await provider.WriteFileAsync(path, bufferedContent, cancellationToken: cancellationToken);

        return result.IsSuccess
            ? Results.Ok($"File '{path}' was written successfully using provider '{providerName}'.")
            : MapFailure(result, path);
    }

    private async Task<HttpResult> DeleteFileAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.DeleteFileAsync(path, cancellationToken: cancellationToken);

        return result.IsSuccess
            ? Results.Ok($"File '{path}' was deleted successfully using provider '{providerName}'.")
            : MapFailure(result, path);
    }

    private async Task<HttpResult> GetFileMetadataAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.GetFileMetadataAsync(path, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : MapFailure(result, path);
    }

    private async Task<HttpResult> GetChecksumAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.GetChecksumAsync(path, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new FileStorageChecksumResponseModel { Path = path, Checksum = result.Value })
            : MapFailure(result, path);
    }

    private async Task<HttpResult> ListFilesAsync(
        string providerName,
        string path,
        string searchPattern,
        bool recursive,
        string continuationToken,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.ListFilesAsync(path ?? string.Empty, searchPattern, recursive, continuationToken, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new FileStorageFilesResponseModel
            {
                Files = result.Value.Files.ToArray(),
                NextContinuationToken = result.Value.NextContinuationToken
            })
            : MapFailure(result, path);
    }

    private async Task<HttpResult> DirectoryExistsAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.DirectoryExistsAsync(path, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Ok(new FileStorageExistsResponseModel { Path = path, Exists = true });
        }

        return IsNotFound(result)
            ? Results.Ok(new FileStorageExistsResponseModel { Path = path, Exists = false })
            : MapFailure(result, path);
    }

    private async Task<HttpResult> ListDirectoriesAsync(
        string providerName,
        string path,
        string searchPattern,
        bool recursive,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.ListDirectoriesAsync(path ?? string.Empty, searchPattern, recursive, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value.ToArray())
            : MapFailure(result, path);
    }

    private async Task<HttpResult> CreateDirectoryAsync(string providerName, string path, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.CreateDirectoryAsync(path, cancellationToken);

        return result.IsSuccess
            ? Results.Ok($"Directory '{path}' was created successfully using provider '{providerName}'.")
            : MapFailure(result, path);
    }

    private async Task<HttpResult> DeleteDirectoryAsync(string providerName, string path, bool recursive, CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        var result = await provider.DeleteDirectoryAsync(path, recursive, cancellationToken);

        return result.IsSuccess
            ? Results.Ok($"Directory '{path}' was deleted successfully using provider '{providerName}'.")
            : MapFailure(result, path);
    }

    private async Task<HttpResult> ListFileEventsAsync(
        IServiceProvider requestServices,
        string providerName,
        string path,
        string eventType,
        DateTimeOffset? fromDate,
        DateTimeOffset? tillDate,
        int? take,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        if (!TryResolveFileEventStore(requestServices, out var eventStore, out failure))
        {
            return failure;
        }

        FileEventType parsedEventType = default;
        if (!string.IsNullOrWhiteSpace(eventType) &&
            !Enum.TryParse<FileEventType>(eventType, ignoreCase: true, out parsedEventType))
        {
            return Results.Problem(
                $"The file event type '{eventType}' is invalid. Expected one of: {string.Join(", ", Enum.GetNames<FileEventType>())}.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        var normalizedTake = Math.Clamp(take ?? 200, 1, 500);
        var normalizedPath = NormalizePathFilter(path);

        var events = await eventStore.GetFileEventsForLocationAsync(providerName, fromDate, tillDate, cancellationToken);
        var filteredEvents = events
            .WhereIf(fileEvent => MatchesPathFilter(fileEvent.FilePath, normalizedPath), !string.IsNullOrWhiteSpace(normalizedPath))
            .WhereIf(fileEvent => fileEvent.EventType == parsedEventType, !string.IsNullOrWhiteSpace(eventType))
            .Take(normalizedTake)
            .Select(MapEventModel)
            .ToArray();

        return Results.Ok(new FileStorageFileEventsResponseModel
        {
            ProviderName = providerName,
            LocationName = provider.LocationName,
            Count = filteredEvents.Length,
            Events = filteredEvents
        });
    }

    private async Task<HttpResult> ScanFileEventsAsync(
        IServiceProvider requestServices,
        string providerName,
        bool waitForProcessing,
        string searchPattern,
        int? maxFilesToScan,
        bool skipChecksum,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateProvider(providerName, out var provider, out var failure))
        {
            return failure;
        }

        if (!TryResolveFileMonitoringService(requestServices, out var monitoringService, out failure))
        {
            return failure;
        }

        try
        {
            var context = await monitoringService.ScanLocationAsync(
                providerName,
                new FileScanOptions
                {
                    WaitForProcessing = waitForProcessing,
                    Timeout = TimeSpan.FromMinutes(2),
                    FileFilter = searchPattern,
                    MaxFilesToScan = maxFilesToScan,
                    SkipChecksum = skipChecksum
                },
                token: cancellationToken);

            var events = context.Events.Select(MapEventModel).ToArray();

            return Results.Ok(new FileStorageFileEventScanResponseModel
            {
                ProviderName = providerName,
                LocationName = context.LocationName ?? provider.LocationName,
                ScanId = context.ScanId,
                StartTime = context.StartTime,
                EndTime = context.EndTime,
                EventCount = events.Length,
                Events = events
            });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
    }

    private bool TryCreateProvider(string providerName, out IFileStorageProvider provider, out HttpResult failure)
    {
        try
        {
            provider = this.factory.CreateProvider(providerName);
            failure = null;
            return true;
        }
        catch (KeyNotFoundException)
        {
            provider = null;
            failure = Results.NotFound($"No file storage provider registered with name '{providerName}'.");
            return false;
        }
    }

    private static bool TryResolveFileEventStore(IServiceProvider requestServices, out IFileEventStore store, out HttpResult failure)
    {
        store = requestServices.GetService<IFileEventStore>();
        if (store is not null)
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            "File monitoring is not configured. Register AddFileMonitoring(...) before using file event endpoints.",
            statusCode: (int)HttpStatusCode.ServiceUnavailable);
        return false;
    }

    private static bool TryResolveFileMonitoringService(IServiceProvider requestServices, out IFileMonitoringService monitoringService, out HttpResult failure)
    {
        monitoringService = requestServices.GetService<IFileMonitoringService>();
        if (monitoringService is not null)
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            "File monitoring is not configured. Register AddFileMonitoring(...) before using file event endpoints.",
            statusCode: (int)HttpStatusCode.ServiceUnavailable);
        return false;
    }

    private static HttpResult MapFailure(Result result, string path)
    {
        var error = result.Errors?.FirstOrDefault();
        var message = result.Messages?.LastOrDefault()
            ?? error?.Message
            ?? $"The request for '{path}' failed.";

        if (IsNotFound(result))
        {
            return Results.NotFound(message);
        }

        return error switch
        {
            ValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            ArgumentError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            ConflictError => Results.Problem(message, statusCode: (int)HttpStatusCode.Conflict),
            ResourceUnavailableError => Results.Problem(message, statusCode: (int)HttpStatusCode.Locked),
            AccessDeniedError => Results.Problem(message, statusCode: (int)HttpStatusCode.Forbidden),
            UnauthorizedError => Results.Problem(message, statusCode: (int)HttpStatusCode.Unauthorized),
            OperationCancelledError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestTimeout),
            FileSystemError fileSystemError when IsBadRequest(fileSystemError.Message) => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            _ => Results.Problem(message, statusCode: (int)HttpStatusCode.InternalServerError)
        };
    }

    private static bool IsNotFound(Result result)
        => result.Errors?.Any(error =>
            error is NotFoundError ||
            error is FileSystemError fileSystemError &&
            fileSystemError.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)) == true;

    private static bool IsBadRequest(string message)
        => !string.IsNullOrWhiteSpace(message) &&
           (message.Contains("cannot be null", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cannot be empty", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("invalid", StringComparison.OrdinalIgnoreCase));

    private static string NormalizePathFilter(string path)
        => path?.Replace('\\', '/').Trim().Trim('/');

    private static bool MatchesPathFilter(string filePath, string pathFilter)
        => string.Equals(filePath, pathFilter, StringComparison.OrdinalIgnoreCase) ||
           filePath.StartsWith(pathFilter + "/", StringComparison.OrdinalIgnoreCase);

    private static FileStorageFileEventModel MapEventModel(FileEvent fileEvent)
        => new()
        {
            Id = fileEvent.Id,
            ScanId = fileEvent.ScanId,
            LocationName = fileEvent.LocationName,
            FilePath = fileEvent.FilePath,
            EventType = fileEvent.EventType.ToString(),
            DetectedDate = fileEvent.DetectedDate,
            FileSize = fileEvent.FileSize,
            LastModifiedDate = fileEvent.LastModifiedDate,
            Checksum = fileEvent.Checksum
        };
}
