// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Files.Dashboard;

using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps the File Storage dashboard plugin pages and operational file-management actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string FilesPath = "/storage/files";
    internal const string FilesContentPath = "/storage/files/content";
    internal const string FilesDownloadPath = "/storage/files/download";

    private const string ActionsPath = "/storage/files/actions";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            FilesPath,
            "_bdk.Dashboard.Storage.Files",
            "Dashboard Files",
            "Shows registered file storage locations with file and directory management actions.");

        group.MapDashboardPage<Pages.Content>(
            FilesContentPath,
            "_bdk.Dashboard.Storage.FilesContent",
            "Dashboard Files Content",
            "Shows the refreshable file storage dashboard content fragment.");

        group.MapGet(FilesDownloadPath, async (
            HttpContext context,
            [FromQuery] string provider,
            [FromQuery] string path,
            CancellationToken cancellationToken) =>
            await DownloadFileAsync(context, provider, path, cancellationToken))
            .WithName("_bdk.Dashboard.Storage.Files.Download")
            .WithSummary("Download file storage file")
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/create-directory", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
            {
                var path = CombinePath(GetFormValue(form, "path"), GetFormValue(form, "name"));
                return string.IsNullOrWhiteSpace(path)
                    ? Result.Failure().WithError(new ValidationError("Directory name is required."))
                    : await provider.CreateDirectoryAsync(path, cancellationToken);
            }))
            .WithName("_bdk.Dashboard.Storage.Files.CreateDirectory")
            .WithSummary("Create file storage directory")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/create-text-file", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
            {
                var path = CombinePath(GetFormValue(form, "path"), GetFormValue(form, "name"));
                if (string.IsNullOrWhiteSpace(path))
                {
                    return Result.Failure().WithError(new ValidationError("File name is required."));
                }

                await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(GetFormText(form, "content") ?? string.Empty));
                return await provider.WriteFileAsync(path, stream, cancellationToken: cancellationToken);
            }))
            .WithName("_bdk.Dashboard.Storage.Files.CreateTextFile")
            .WithSummary("Create file storage text file")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/upload", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
            {
                var file = form.Files.GetFile("file");
                if (file is null || file.Length == 0)
                {
                    return Result.Failure().WithError(new ValidationError("Upload file is required."));
                }

                var path = CombinePath(GetFormValue(form, "path"), file.FileName);
                await using var stream = file.OpenReadStream();
                return await provider.WriteFileAsync(path, stream, cancellationToken: cancellationToken);
            }))
            .WithName("_bdk.Dashboard.Storage.Files.Upload")
            .WithSummary("Upload file storage file")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/delete-file", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
                await provider.DeleteFileAsync(GetFormValue(form, "path"), cancellationToken: cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Files.DeleteFile")
            .WithSummary("Delete file storage file")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/delete-directory", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
                await provider.DeleteDirectoryAsync(GetFormValue(form, "path"), recursive: true, cancellationToken: cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Files.DeleteDirectory")
            .WithSummary("Delete file storage directory")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/rename-file", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
                await provider.RenameFileAsync(GetFormValue(form, "path"), GetFormValue(form, "destinationPath"), cancellationToken: cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Files.RenameFile")
            .WithSummary("Rename file storage file")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/rename-directory", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
                await provider.RenameDirectoryAsync(GetFormValue(form, "path"), GetFormValue(form, "destinationPath"), cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Files.RenameDirectory")
            .WithSummary("Rename file storage directory")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/move-file", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
                await provider.MoveFileAsync(GetFormValue(form, "path"), GetFormValue(form, "destinationPath"), cancellationToken: cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Files.MoveFile")
            .WithSummary("Move file storage file")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/copy-file", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (provider, form) =>
                await provider.CopyFileAsync(GetFormValue(form, "path"), GetFormValue(form, "destinationPath"), cancellationToken: cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Files.CopyFile")
            .WithSummary("Copy file storage file")
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }

    internal static string BuildFilesPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, FilesPath);

    internal static string BuildFilesContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, FilesContentPath);

    internal static string BuildFilesDownloadPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, FilesDownloadPath);

    internal static string BuildFilesActionBase(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, ActionsPath);

    private static async Task<HttpResult> DownloadFileAsync(
        HttpContext context,
        string providerName,
        string path,
        CancellationToken cancellationToken)
    {
        if (!TryCreateProvider(context, providerName, out var provider, out var failure))
        {
            return failure;
        }

        var normalizedPath = NormalizePath(path);
        var result = await provider.ReadFileAsync(normalizedPath, cancellationToken: cancellationToken);
        if (result.IsFailure)
        {
            return MapFailure(result, normalizedPath);
        }

        var contentType = ContentTypeExtensions.FromFileName(normalizedPath, ContentType.DEFAULT).MimeType();
        var fileName = GetName(normalizedPath);

        return Results.File(
            result.Value,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            string.IsNullOrWhiteSpace(fileName) ? "download" : fileName);
    }

    private static async Task<HttpResult> ExecuteFormActionAsync(
        HttpContext context,
        Func<IFileStorageProvider, IFormCollection, Task<Result>> action)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var providerName = GetFormValue(form, "provider");
        if (!TryCreateProvider(context, providerName, out var provider, out var failure))
        {
            return failure;
        }

        try
        {
            var result = await action(provider, form);
            return result.IsSuccess
                ? Results.Ok(new { message = result.Messages?.LastOrDefault() ?? "Storage action completed." })
                : MapFailure(result, GetFormValue(form, "path"));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    private static bool TryCreateProvider(
        HttpContext context,
        string providerName,
        out IFileStorageProvider provider,
        out HttpResult failure)
    {
        provider = null;
        failure = null;

        if (string.IsNullOrWhiteSpace(providerName))
        {
            failure = Results.Problem("Storage provider is required.", statusCode: (int)HttpStatusCode.BadRequest);
            return false;
        }

        var factory = context.RequestServices.GetService<IFileStorageProviderFactory>();
        if (factory is null)
        {
            failure = Results.Problem("AddFileStorage() is not registered.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
            return false;
        }

        try
        {
            provider = factory.CreateProvider(providerName);
            return true;
        }
        catch (KeyNotFoundException)
        {
            failure = Results.NotFound($"No file storage provider registered with name '{providerName}'.");
            return false;
        }
    }

    private static HttpResult MapFailure(Result result, string path)
    {
        var error = result.Errors?.FirstOrDefault();
        var message = result.Messages?.LastOrDefault()
            ?? error?.Message
            ?? $"The storage request for '{path}' failed.";

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

    private static string GetFormValue(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) ? NormalizePath(value.ToString()) : string.Empty;

    private static string GetFormText(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;

    private static string CombinePath(string parentPath, string name)
    {
        var normalizedName = NormalizePath(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return string.Empty;
        }

        var normalizedParent = NormalizePath(parentPath);
        return string.IsNullOrWhiteSpace(normalizedParent)
            ? normalizedName
            : $"{normalizedParent}/{normalizedName}";
    }

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim('/');

    private static string GetName(string path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }
}
