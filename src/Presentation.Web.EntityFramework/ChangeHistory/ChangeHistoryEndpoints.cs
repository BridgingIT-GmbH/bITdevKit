// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;

using System.ComponentModel;
using System.Net;
using System.Reflection;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps ChangeHistory query and restore endpoints for one entity and EF Core context.
/// </summary>
/// <typeparam name="TEntity">The entity type whose ChangeHistory is exposed.</typeparam>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddRequester();
/// services.AddChangeHistoryEndpoints&lt;Customer, AppDbContext&gt;();
/// app.MapEndpoints();
/// </code>
/// </example>
public sealed class ChangeHistoryEndpoints<TEntity, TContext>(
    ChangeHistoryEndpointsOptions options = null,
    ILoggerFactory loggerFactory = null) : EndpointsBase
    where TEntity : class, IEntity
    where TContext : DbContext
{
    private readonly ChangeHistoryEndpointsOptions options = options ?? new ChangeHistoryEndpointsOptions();
    private readonly ILogger<ChangeHistoryEndpoints<TEntity, TContext>> logger = loggerFactory?.CreateLogger<ChangeHistoryEndpoints<TEntity, TContext>>() ?? NullLogger<ChangeHistoryEndpoints<TEntity, TContext>>.Instance;

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        this.RequireReadPolicy(group.MapGet(string.Empty, this.FindAll)
                .Produces<ChangeHistoryFindAllResult>()
                .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
                .Produces<ProblemDetails>((int)HttpStatusCode.Forbidden)
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .WithName(BuildRouteName(this.options, "FindAll"))
                .WithSummary("Get ChangeHistory rows")
                .WithDescription("Retrieves a paged list of ChangeHistory rows for the configured entity type."));

        this.RequireReadPolicy(group.MapGet("change-sets", this.FindAllChangeSets)
                .Produces<ChangeHistoryFindAllChangeSetsResult>()
                .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
                .Produces<ProblemDetails>((int)HttpStatusCode.Forbidden)
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .WithName(BuildRouteName(this.options, "FindAllChangeSets"))
                .WithSummary("Get ChangeHistory change sets")
                .WithDescription("Retrieves a paged list of grouped ChangeHistory change sets for the configured entity type."));

        this.RequireReadPolicy(group.MapGet("change-sets/{changeSetId:guid}", this.FindOneChangeSet)
                .Produces<ChangeHistoryChangeSetRecord>()
                .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
                .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
                .Produces<ProblemDetails>((int)HttpStatusCode.Forbidden)
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .WithName(BuildRouteName(this.options, "FindOneChangeSet"))
                .WithSummary("Get a ChangeHistory change set")
                .WithDescription("Retrieves one grouped ChangeHistory change set."));

        this.RequireReadPolicy(group.MapGet("{entityId}", this.FindAllByEntityId)
                .Produces<ChangeHistoryFindAllResult>()
                .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
                .Produces<ProblemDetails>((int)HttpStatusCode.Forbidden)
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .WithName(BuildRouteName(this.options, "FindAllByEntityId"))
                .WithSummary("Get entity ChangeHistory rows")
                .WithDescription("Retrieves a paged list of ChangeHistory rows for one entity id."));

        this.RequireRestorePolicy(group.MapPost("{entityId}/change-sets/{changeSetId:guid}/restore", this.Restore)
                .Produces<ChangeHistoryRestoreResponseModel>()
                .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
                .Produces<ProblemDetails>((int)HttpStatusCode.NotFound)
                .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
                .Produces<ProblemDetails>((int)HttpStatusCode.Forbidden)
                .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
                .WithName(BuildRouteName(this.options, "Restore"))
                .WithSummary("Restore a ChangeHistory change set")
                .WithDescription("Restores the configured entity to values captured before the specified ChangeHistory change set."));

        this.IsRegistered = true;
    }

    private Task<IResult> FindAll(
        [FromServices] IChangeHistoryService<TEntity, TContext> changeHistory,
        [FromQuery] string entityId,
        [FromQuery] string propertyName,
        [FromQuery] Guid? changeSetId,
        [FromQuery] Guid? bulkOperationId,
        [FromQuery] string changedByUserId,
        [FromQuery] DateTimeOffset? changedDateFrom,
        [FromQuery] DateTimeOffset? changedDateTo,
        [FromQuery] string operation,
        [FromQuery] string captureSource,
        [FromQuery] string captureStrategy,
        [FromQuery] string captureStatus,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool? orderAscending,
        CancellationToken cancellationToken)
    {
        return this.QueryAsync(
            changeHistory,
            entityId,
            propertyName,
            changeSetId,
            bulkOperationId,
            changedByUserId,
            changedDateFrom,
            changedDateTo,
            operation,
            captureSource,
            captureStrategy,
            captureStatus,
            page,
            pageSize,
            orderAscending,
            cancellationToken);
    }

    private Task<IResult> FindAllByEntityId(
        [FromServices] IChangeHistoryService<TEntity, TContext> changeHistory,
        string entityId,
        [FromQuery] string propertyName,
        [FromQuery] Guid? changeSetId,
        [FromQuery] Guid? bulkOperationId,
        [FromQuery] string changedByUserId,
        [FromQuery] DateTimeOffset? changedDateFrom,
        [FromQuery] DateTimeOffset? changedDateTo,
        [FromQuery] string operation,
        [FromQuery] string captureSource,
        [FromQuery] string captureStrategy,
        [FromQuery] string captureStatus,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool? orderAscending,
        CancellationToken cancellationToken)
    {
        return this.QueryAsync(
            changeHistory,
            entityId,
            propertyName,
            changeSetId,
            bulkOperationId,
            changedByUserId,
            changedDateFrom,
            changedDateTo,
            operation,
            captureSource,
            captureStrategy,
            captureStatus,
            page,
            pageSize,
            orderAscending,
            cancellationToken);
    }

    private async Task<IResult> FindAllChangeSets(
        [FromServices] IChangeHistoryService<TEntity, TContext> changeHistory,
        [FromQuery] string entityId,
        [FromQuery] string propertyName,
        [FromQuery] Guid? changeSetId,
        [FromQuery] Guid? bulkOperationId,
        [FromQuery] string changedByUserId,
        [FromQuery] DateTimeOffset? changedDateFrom,
        [FromQuery] DateTimeOffset? changedDateTo,
        [FromQuery] string operation,
        [FromQuery] string captureSource,
        [FromQuery] string captureStrategy,
        [FromQuery] string captureStatus,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool? orderAscending,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await changeHistory.FindAllChangeSetsAsync(new ChangeHistoryFindAllQuery
            {
                EntityType = typeof(TEntity).Name,
                EntityId = entityId,
                PropertyName = propertyName,
                ChangeSetId = changeSetId,
                BulkOperationId = bulkOperationId,
                ChangedByUserId = changedByUserId,
                ChangedDateFrom = changedDateFrom,
                ChangedDateTo = changedDateTo,
                Operation = operation,
                CaptureSource = captureSource,
                CaptureStrategy = captureStrategy,
                CaptureStatus = captureStatus,
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                OrderAscending = orderAscending ?? false,
                IncludeValues = this.options.IncludeValues
            }, cancellationToken).AnyContext();

            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[ChangeHistory] grouped query endpoint failed (entityType={EntityType}, entityId={EntityId})", typeof(TEntity).Name, entityId);

            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Query Failed",
                Detail = "An error occurred while querying ChangeHistory change sets."
            });
        }
    }

    private async Task<IResult> FindOneChangeSet(
        [FromServices] IChangeHistoryService<TEntity, TContext> changeHistory,
        Guid changeSetId,
        [FromQuery] string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await changeHistory.FindOneChangeSetAsync(new ChangeHistoryFindOneChangeSetQuery
            {
                ChangeSetId = changeSetId,
                EntityType = typeof(TEntity).Name,
                EntityId = entityId,
                IncludeValues = this.options.IncludeValues
            }, cancellationToken).AnyContext();

            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[ChangeHistory] change-set query endpoint failed (entityType={EntityType}, changeSetId={ChangeSetId})", typeof(TEntity).Name, changeSetId);

            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Query Failed",
                Detail = "An error occurred while querying the ChangeHistory change set."
            });
        }
    }

    private async Task<IResult> Restore(
        [FromServices] IChangeHistoryService<TEntity, TContext> changeHistory,
        string entityId,
        Guid changeSetId,
        [FromBody] ChangeHistoryRestoreRequestModel request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parsedEntityId = ConvertEntityId(entityId);
            var result = await changeHistory.RestoreAsync(new ChangeHistoryRestoreCommand<TEntity>(
                parsedEntityId,
                changeSetId,
                request?.Reason,
                request?.ExpectedConcurrencyVersion,
                request?.RestoreMode ?? ChangeHistoryRestoreMode.ChangeSet), cancellationToken).AnyContext();

            return result.IsSuccess
                ? Results.Ok(new ChangeHistoryRestoreResponseModel(result.Value.RestoredChangeSetId, result.Value.RestoredPropertyCount))
                : ToProblem(result);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidCastException)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Entity Id",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[ChangeHistory] restore endpoint failed (entityType={EntityType}, entityId={EntityId}, changeSetId={ChangeSetId})", typeof(TEntity).Name, entityId, changeSetId);

            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Restore Failed",
                Detail = "An error occurred while restoring ChangeHistory."
            });
        }
    }

    private async Task<IResult> QueryAsync(
        IChangeHistoryService<TEntity, TContext> changeHistory,
        string entityId,
        string propertyName,
        Guid? changeSetId,
        Guid? bulkOperationId,
        string changedByUserId,
        DateTimeOffset? changedDateFrom,
        DateTimeOffset? changedDateTo,
        string operation,
        string captureSource,
        string captureStrategy,
        string captureStatus,
        int? page,
        int? pageSize,
        bool? orderAscending,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await changeHistory.FindAllAsync(new ChangeHistoryFindAllQuery
            {
                EntityType = typeof(TEntity).Name,
                EntityId = entityId,
                PropertyName = propertyName,
                ChangeSetId = changeSetId,
                BulkOperationId = bulkOperationId,
                ChangedByUserId = changedByUserId,
                ChangedDateFrom = changedDateFrom,
                ChangedDateTo = changedDateTo,
                Operation = operation,
                CaptureSource = captureSource,
                CaptureStrategy = captureStrategy,
                CaptureStatus = captureStatus,
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                OrderAscending = orderAscending ?? false,
                IncludeValues = this.options.IncludeValues
            }, cancellationToken).AnyContext();

            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[ChangeHistory] query endpoint failed (entityType={EntityType}, entityId={EntityId})", typeof(TEntity).Name, entityId);

            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Query Failed",
                Detail = "An error occurred while querying ChangeHistory."
            });
        }
    }

    private void RequireReadPolicy(RouteHandlerBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(this.options.ReadPolicy))
        {
            builder.RequireAuthorization(this.options.ReadPolicy);
        }
    }

    private void RequireRestorePolicy(RouteHandlerBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(this.options.RestorePolicy))
        {
            builder.RequireAuthorization(this.options.RestorePolicy);
        }
    }

    private static IResult ToProblem<TValue>(Result<TValue> result)
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

        return Results.Problem(new ProblemDetails
        {
            Status = (int)status,
            Title = status == HttpStatusCode.InternalServerError ? "ChangeHistory Request Failed" : "ChangeHistory Request Invalid",
            Detail = result.Messages.FirstOrDefault() ?? result.Errors.FirstOrDefault()?.Message ?? "The ChangeHistory request failed."
        });
    }

    private static object ConvertEntityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Entity id is required.", nameof(value));
        }

        var idType = typeof(TEntity).GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault() ?? typeof(string);

        if (idType == typeof(string))
        {
            return value;
        }

        if (idType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        var createStringMethod = idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (createStringMethod is not null)
        {
            return createStringMethod.Invoke(null, [value]);
        }

        var createGuidMethod = idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [typeof(Guid)]);
        if (createGuidMethod is not null)
        {
            return createGuidMethod.Invoke(null, [Guid.Parse(value)]);
        }

        var parseMethod = idType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (parseMethod is not null)
        {
            return parseMethod.Invoke(null, [value]);
        }

        var converter = TypeDescriptor.GetConverter(idType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(value);
        }

        return Convert.ChangeType(value, idType);
    }
}
