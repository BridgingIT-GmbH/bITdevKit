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
/// Dispatches a ChangeHistory restore through the requester pipeline.
/// </summary>
/// <typeparam name="TEntity">The entity type to restore.</typeparam>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// var request = new ChangeHistoryRestoreRequest&lt;Customer, AppDbContext&gt;(customerId, changeSetId, "Undo edit");
/// var result = await requester.SendAsync&lt;ChangeHistoryRestoreResult&gt;(request);
/// </code>
/// </example>
public sealed class ChangeHistoryRestoreRequest<TEntity, TContext> : RequestBase<ChangeHistoryRestoreResult>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryRestoreRequest{TEntity,TContext}" /> class.
    /// </summary>
    /// <param name="entityId">The entity id to restore.</param>
    /// <param name="changeSetId">The source ChangeHistory change set id.</param>
    /// <param name="reason">The optional restore reason.</param>
    /// <param name="expectedConcurrencyVersion">The optional expected concurrency version.</param>
    /// <param name="restoreMode">The restore selection mode.</param>
    public ChangeHistoryRestoreRequest(
        object entityId,
        Guid changeSetId,
        string reason = null,
        Guid? expectedConcurrencyVersion = null,
        ChangeHistoryRestoreMode restoreMode = ChangeHistoryRestoreMode.ChangeSet)
    {
        this.EntityId = entityId;
        this.ChangeSetId = changeSetId;
        this.Reason = reason;
        this.ExpectedConcurrencyVersion = expectedConcurrencyVersion;
        this.RestoreMode = restoreMode;
    }

    /// <summary>
    /// Gets the entity id to restore.
    /// </summary>
    public object EntityId { get; }

    /// <summary>
    /// Gets the source ChangeHistory change set id.
    /// </summary>
    public Guid ChangeSetId { get; }

    /// <summary>
    /// Gets the optional restore reason.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the optional expected concurrency version.
    /// </summary>
    public Guid? ExpectedConcurrencyVersion { get; }

    /// <summary>
    /// Gets the restore selection mode.
    /// </summary>
    public ChangeHistoryRestoreMode RestoreMode { get; }
}

/// <summary>
/// Handles requester-based ChangeHistory restore operations for one entity and EF Core context.
/// </summary>
/// <typeparam name="TEntity">The entity type to restore.</typeparam>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddChangeHistoryRequesterHandlers&lt;Customer, AppDbContext&gt;();
/// </code>
/// </example>
public sealed class ChangeHistoryRestoreRequestHandler<TEntity, TContext>(
    ChangeHistoryRestoreCommandHandler<TEntity, TContext> handler,
    ChangeHistoryOptions changeHistoryOptions = null,
    IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext> authorizer = null)
    : RequestHandlerBase<ChangeHistoryRestoreRequest<TEntity, TContext>, ChangeHistoryRestoreResult>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    /// <inheritdoc />
    protected override async Task<Result<ChangeHistoryRestoreResult>> HandleAsync(
        ChangeHistoryRestoreRequest<TEntity, TContext> request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError("A ChangeHistory restore request is required."));
        }

        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeRestoreAsync(
            changeHistoryOptions,
            authorizer,
            request,
            cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryRestoreResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        return await handler.HandleAsync(new ChangeHistoryRestoreCommand<TEntity>(
            request.EntityId,
            request.ChangeSetId,
            request.Reason,
            request.ExpectedConcurrencyVersion,
            request.RestoreMode), cancellationToken).AnyContext();
    }
}
