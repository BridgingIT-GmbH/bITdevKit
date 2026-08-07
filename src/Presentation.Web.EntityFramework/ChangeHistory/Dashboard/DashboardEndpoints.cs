// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;

using System.Net;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps the ChangeHistory dashboard plugin pages and restore action endpoint.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string ChangeHistoryPath = "/change-history";
    internal const string ChangeHistoryContentPath = "/change-history/content";

    private const string RestoreActionPath = "/change-history/actions/restore";

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
            ChangeHistoryPath,
            "_bdk.Dashboard.ChangeHistory",
            "Dashboard ChangeHistory",
            "Shows tracked entity ChangeHistory rows, grouped change sets, and restore actions.");

        group.MapDashboardPage<Pages.Content>(
            ChangeHistoryContentPath,
            "_bdk.Dashboard.ChangeHistoryContent",
            "Dashboard ChangeHistory Content",
            "Shows the refreshable ChangeHistory dashboard content fragment.");

        group.MapPost(RestoreActionPath, async (HttpContext context, CancellationToken cancellationToken) =>
            await RestoreAsync(context, cancellationToken))
            .WithName("_bdk.Dashboard.ChangeHistory.Restore")
            .WithSummary("Restore a ChangeHistory change set from the dashboard")
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }

    internal static string BuildChangeHistoryPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, ChangeHistoryPath);

    internal static string BuildChangeHistoryContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, ChangeHistoryContentPath);

    internal static string BuildRestoreActionPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, RestoreActionPath);

    private static async Task<HttpResult> RestoreAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var descriptor = FindDescriptor(context, GetFormValue(form, "registration"));
        if (descriptor is null)
        {
            return Problem(HttpStatusCode.BadRequest, "Unknown ChangeHistory Registration", "Select a ChangeHistory registration before restoring.");
        }

        if (!Guid.TryParse(GetFormValue(form, "changeSetId"), out var changeSetId))
        {
            return Problem(HttpStatusCode.BadRequest, "Invalid Change Set", "A valid change set id is required.");
        }

        var entityId = GetFormValue(form, "entityId");
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return Problem(HttpStatusCode.BadRequest, "Invalid Entity", "An entity id is required.");
        }

        Guid? expectedConcurrencyVersion = null;
        var expectedConcurrencyVersionText = GetFormValue(form, "expectedConcurrencyVersion");
        if (!string.IsNullOrWhiteSpace(expectedConcurrencyVersionText))
        {
            if (!Guid.TryParse(expectedConcurrencyVersionText, out var parsedExpectedConcurrencyVersion))
            {
                return Problem(HttpStatusCode.BadRequest, "Invalid Concurrency Version", "Expected concurrency version must be a valid GUID.");
            }

            expectedConcurrencyVersion = parsedExpectedConcurrencyVersion;
        }

        var restoreMode = Enum.TryParse<ChangeHistoryRestoreMode>(GetFormValue(form, "restoreMode"), ignoreCase: true, out var parsedRestoreMode)
            ? parsedRestoreMode
            : ChangeHistoryRestoreMode.ChangeSet;

        try
        {
            var result = await ChangeHistoryDashboardInvoker.RestoreAsync(
                context.RequestServices,
                descriptor,
                entityId,
                changeSetId,
                GetFormValue(form, "reason"),
                expectedConcurrencyVersion,
                restoreMode,
                cancellationToken);

            return result.IsSuccess
                ? Results.Ok(new ChangeHistoryRestoreResponseModel(result.Value.RestoredChangeSetId, result.Value.RestoredPropertyCount))
                : MapFailure(result);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidCastException)
        {
            return Problem(HttpStatusCode.BadRequest, "Invalid Entity Id", ex.Message);
        }
    }

    private static ChangeHistoryDashboardDescriptor FindDescriptor(HttpContext context, string key)
        => context.RequestServices.GetServices<ChangeHistoryDashboardDescriptor>()
            .FirstOrDefault(descriptor => descriptor.Key == key);

    private static string GetFormValue(IFormCollection form, string key)
        => form.TryGetValue(key, out var value) ? value.ToString().Trim() : null;

    private static HttpResult MapFailure<TValue>(Result<TValue> result)
    {
        var status = result.HasError<ValidationError>() || result.HasError<InvalidInputError>()
            ? HttpStatusCode.BadRequest
            : result.HasError<NotFoundError>() || result.HasError<EntityNotFoundError>()
                ? HttpStatusCode.NotFound
                : result.HasError<ConcurrencyError>() || result.HasError<ConflictError>()
                    ? HttpStatusCode.Conflict
                    : result.HasError<ForbiddenError>() || result.HasError<UnauthorizedError>() || result.HasError<InsufficientPermissionsError>()
                        ? HttpStatusCode.Forbidden
                        : HttpStatusCode.InternalServerError;

        return Problem(
            status,
            status == HttpStatusCode.InternalServerError ? "ChangeHistory Restore Failed" : "ChangeHistory Restore Invalid",
            result.Messages.FirstOrDefault() ?? result.Errors.FirstOrDefault()?.Message ?? "The ChangeHistory restore request failed.");
    }

    private static HttpResult Problem(HttpStatusCode status, string title, string detail)
        => Results.Problem(new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail
        });
}
