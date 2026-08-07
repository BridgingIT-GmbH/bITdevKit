// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Dispatches a ChangeHistory query through the requester pipeline for one EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// var request = new ChangeHistoryFindAllRequest&lt;AppDbContext&gt; { EntityType = "Customer", PageSize = 50 };
/// var result = await requester.SendAsync&lt;ChangeHistoryFindAllResult&gt;(request);
/// </code>
/// </example>
public class ChangeHistoryFindAllRequest<TContext> : RequestBase<ChangeHistoryFindAllResult>
    where TContext : DbContext
{
    /// <summary>
    /// Gets or sets the entity type filter.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity id filter.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the property name/path filter.
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the change set id filter.
    /// </summary>
    public Guid? ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the bulk operation id filter.
    /// </summary>
    public Guid? BulkOperationId { get; set; }

    /// <summary>
    /// Gets or sets the user id filter.
    /// </summary>
    public string ChangedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the inclusive changed-date lower bound.
    /// </summary>
    public DateTimeOffset? ChangedDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive changed-date upper bound.
    /// </summary>
    public DateTimeOffset? ChangedDateTo { get; set; }

    /// <summary>
    /// Gets or sets the operation filter.
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the capture source filter.
    /// </summary>
    public string CaptureSource { get; set; }

    /// <summary>
    /// Gets or sets the capture strategy filter.
    /// </summary>
    public string CaptureStrategy { get; set; }

    /// <summary>
    /// Gets or sets the capture status filter.
    /// </summary>
    public string CaptureStatus { get; set; }

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether results should be ordered oldest first.
    /// </summary>
    public bool OrderAscending { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether serialized old/new values should be included in query DTOs.
    /// </summary>
    public bool IncludeValues { get; set; } = true;

    internal ChangeHistoryFindAllQuery ToQuery() => new()
    {
        EntityType = this.EntityType,
        EntityId = this.EntityId,
        PropertyName = this.PropertyName,
        ChangeSetId = this.ChangeSetId,
        BulkOperationId = this.BulkOperationId,
        ChangedByUserId = this.ChangedByUserId,
        ChangedDateFrom = this.ChangedDateFrom,
        ChangedDateTo = this.ChangedDateTo,
        Operation = this.Operation,
        CaptureSource = this.CaptureSource,
        CaptureStrategy = this.CaptureStrategy,
        CaptureStatus = this.CaptureStatus,
        Page = this.Page,
        PageSize = this.PageSize,
        OrderAscending = this.OrderAscending,
        IncludeValues = this.IncludeValues
    };
}

/// <summary>
/// Dispatches a grouped ChangeHistory query through the requester pipeline for one EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// var request = new ChangeHistoryFindAllChangeSetsRequest&lt;AppDbContext&gt; { EntityId = id };
/// var result = await requester.SendAsync&lt;ChangeHistoryFindAllChangeSetsResult&gt;(request);
/// </code>
/// </example>
public sealed class ChangeHistoryFindAllChangeSetsRequest<TContext> : ChangeHistoryFindAllRequest<TContext>, IRequest<ChangeHistoryFindAllChangeSetsResult>
    where TContext : DbContext;

/// <summary>
/// Dispatches a single ChangeHistory change-set query through the requester pipeline.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// var request = new ChangeHistoryFindOneChangeSetRequest&lt;AppDbContext&gt; { ChangeSetId = changeSetId };
/// var result = await requester.SendAsync&lt;ChangeHistoryChangeSetRecord&gt;(request);
/// </code>
/// </example>
public sealed class ChangeHistoryFindOneChangeSetRequest<TContext> : RequestBase<ChangeHistoryChangeSetRecord>
    where TContext : DbContext
{
    /// <summary>
    /// Gets or sets the change set id.
    /// </summary>
    public Guid ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the entity type filter.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity id filter.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether serialized old/new values should be included in query DTOs.
    /// </summary>
    public bool IncludeValues { get; set; } = true;
}

/// <summary>
/// Handles requester-based ChangeHistory queries for one EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddChangeHistoryRequesterHandlers&lt;AppDbContext&gt;();
/// </code>
/// </example>
public sealed class ChangeHistoryFindAllRequestHandler<TContext>(
    ChangeHistoryQueryService<TContext> queryService,
    ChangeHistoryOptions changeHistoryOptions = null,
    IChangeHistoryReadAuthorizer<TContext> authorizer = null)
    : RequestHandlerBase<ChangeHistoryFindAllRequest<TContext>, ChangeHistoryFindAllResult>
    where TContext : DbContext
{
    /// <inheritdoc />
    protected override async Task<Result<ChangeHistoryFindAllResult>> HandleAsync(
        ChangeHistoryFindAllRequest<TContext> request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<ChangeHistoryFindAllResult>.Failure(new ValidationError("A ChangeHistory query request is required."));
        }

        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeReadAsync(changeHistoryOptions, authorizer, request, request.ChangeSetId, cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryFindAllResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        var result = await queryService.FindAllAsync(request.ToQuery(), cancellationToken).AnyContext();
        if (result.IsFailure)
        {
            return Result<ChangeHistoryFindAllResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        return Result<ChangeHistoryFindAllResult>.Success(new ChangeHistoryFindAllResult(
            result.Value,
            result.TotalCount,
            result.CurrentPage,
            result.PageSize,
            result.TotalPages,
            result.HasPreviousPage,
            result.HasNextPage));
    }
}

/// <summary>
/// Handles requester-based grouped ChangeHistory queries for one EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddChangeHistoryRequesterHandlers&lt;AppDbContext&gt;();
/// </code>
/// </example>
public sealed class ChangeHistoryFindAllChangeSetsRequestHandler<TContext>(
    ChangeHistoryQueryService<TContext> queryService,
    ChangeHistoryOptions changeHistoryOptions = null,
    IChangeHistoryReadAuthorizer<TContext> authorizer = null)
    : RequestHandlerBase<ChangeHistoryFindAllChangeSetsRequest<TContext>, ChangeHistoryFindAllChangeSetsResult>
    where TContext : DbContext
{
    /// <inheritdoc />
    protected override async Task<Result<ChangeHistoryFindAllChangeSetsResult>> HandleAsync(
        ChangeHistoryFindAllChangeSetsRequest<TContext> request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<ChangeHistoryFindAllChangeSetsResult>.Failure(new ValidationError("A ChangeHistory query request is required."));
        }

        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeReadAsync(changeHistoryOptions, authorizer, request, request.ChangeSetId, cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryFindAllChangeSetsResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        var result = await queryService.FindAllChangeSetsAsync(request.ToQuery(), cancellationToken).AnyContext();
        if (result.IsFailure)
        {
            return Result<ChangeHistoryFindAllChangeSetsResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        return Result<ChangeHistoryFindAllChangeSetsResult>.Success(new ChangeHistoryFindAllChangeSetsResult(
            result.Value,
            result.TotalCount,
            result.CurrentPage,
            result.PageSize,
            result.TotalPages,
            result.HasPreviousPage,
            result.HasNextPage));
    }
}

/// <summary>
/// Handles requester-based single ChangeHistory change-set queries for one EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddChangeHistoryRequesterHandlers&lt;AppDbContext&gt;();
/// </code>
/// </example>
public sealed class ChangeHistoryFindOneChangeSetRequestHandler<TContext>(
    ChangeHistoryQueryService<TContext> queryService,
    ChangeHistoryOptions changeHistoryOptions = null,
    IChangeHistoryReadAuthorizer<TContext> authorizer = null)
    : RequestHandlerBase<ChangeHistoryFindOneChangeSetRequest<TContext>, ChangeHistoryChangeSetRecord>
    where TContext : DbContext
{
    /// <inheritdoc />
    protected override async Task<Result<ChangeHistoryChangeSetRecord>> HandleAsync(
        ChangeHistoryFindOneChangeSetRequest<TContext> request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<ChangeHistoryChangeSetRecord>.Failure(new ValidationError("A ChangeHistory change-set query request is required."));
        }

        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeReadAsync(
            changeHistoryOptions,
            authorizer,
            request.EntityType,
            request.EntityId,
            request.ChangeSetId,
            request.IncludeValues,
            cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryChangeSetRecord>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        return await queryService.FindOneChangeSetAsync(new ChangeHistoryFindOneChangeSetQuery
        {
            ChangeSetId = request.ChangeSetId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            IncludeValues = request.IncludeValues
        }, cancellationToken).AnyContext();
    }

}
