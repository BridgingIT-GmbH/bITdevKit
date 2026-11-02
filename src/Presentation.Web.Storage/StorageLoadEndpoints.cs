// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal static class StorageLoadEndpoints
{
    internal static StorageLoadTest Map(RouteGroupBuilder group, string path, string feature, IServiceProvider services)
    {
        var runner = new StorageLoadTest(feature, services.GetRequiredService<IServiceScopeFactory>());
        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() => runner.Stop());
        lifetime.ApplicationStopped.Register(runner.Dispose);
        var prefix = path + "/diagnostics/load";
        group.MapGet(prefix + "/targets", (HttpContext context) =>
        {
            var scoped = context.RequestServices;
            var targets = feature switch
            {
                "blobs" => scoped.GetServices<BlobStoreClientRegistration>().Select(x => new { value = x.Name, label = x.Name + " · " + x.ProviderName }),
                "documents" => scoped.GetServices<DocumentStoreClientDescriptor>().Select(x => new { value = x.ClientId, label = x.DocumentTypeName + " · " + x.Name + " · " + x.ProviderName }),
                _ => scoped.GetService<IFileStorageProviderFactory>()?.GetProviderNames().Select(name => new { value = name, label = name }) ?? []
            };

            return Results.Json(targets.ToArray());
        }).ExcludeFromDescription();
        group.MapGet(prefix + "/containers", async (HttpContext context, string target, CancellationToken cancellationToken) =>
        {
            var registration = context.RequestServices.GetServices<BlobStoreClientRegistration>().SingleOrDefault(x => x.Name == target);

            if (feature != "blobs" || registration is null)
            {
                return Results.BadRequest("Select a registered blob client.");
            }

            var provider = context.RequestServices.GetRequiredKeyedService<IBlobStoreProvider>(registration.Name);

            if (provider is not IBlobStoreContainerCatalog catalog)
            {
                return Results.BadRequest("This provider cannot list existing containers.");
            }

            var result = await catalog.ListContainersAsync(cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? Results.Json(result.Value) : Results.Problem("Containers could not be listed.");
        }).ExcludeFromDescription();
        group.MapPost(prefix + "/run", async (HttpContext context) =>
        {
            if (!SameOrigin(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (context.Request.ContentLength is> 4096)
            {
                return Results.BadRequest("Load-test settings are too large.");
            }

            try
            {
                var request = await context.Request.ReadFromJsonAsync<StorageLoadRequest>(context.RequestAborted).ConfigureAwait(false);

                if (request is null)
                {
                    return Results.BadRequest("Load-test settings are required.");
                }

                return Results.Json(await runner.ExecuteAsync(request, context.RequestAborted).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is StorageLoadValidationException or System.Text.Json.JsonException)
            {
                return Results.Problem(exception is StorageLoadValidationException ? exception.Message : "Invalid load-test settings.", statusCode: 400);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(exception.Message, statusCode: 409);
            }
        }).ExcludeFromDescription();
        group.MapPost(prefix + "/stop", async (HttpContext context) =>
        {
            if (!SameOrigin(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var request = await context.Request.ReadFromJsonAsync<StorageLoadRequest>(context.RequestAborted).ConfigureAwait(false);

            if (!Guid.TryParse(request?.Id, out var id))
            {
                return Results.BadRequest("The active run identifier is required.");
            }

            runner.Stop(id.ToString("N"));

            return Results.Accepted();
        }).ExcludeFromDescription();
        group.MapPost(prefix + "/cleanup", async (HttpContext context) =>
        {
            if (!SameOrigin(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                return Results.Json(await runner.RetryCleanupAsync().ConfigureAwait(false));
            }
            catch (InvalidOperationException exception)
            {
                return Results.Problem(exception.Message, statusCode: 409);
            }
        }).ExcludeFromDescription();

        return runner;
    }

    private static bool SameOrigin(HttpRequest request)
    {
        if (request.Headers["X-BDK-Storage-Load"] != "1")
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();

        return string.IsNullOrEmpty(origin) || string.Equals(origin, request.Scheme + "://" + request.Host, StringComparison.OrdinalIgnoreCase);
    }
}